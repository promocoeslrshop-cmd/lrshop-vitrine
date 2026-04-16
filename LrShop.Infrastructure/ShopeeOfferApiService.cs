using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class ShopeeOfferApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ShopeeOfferApiService> _logger;
    private readonly string _appId;
    private readonly string _secret;
    private readonly string _endpoint;

    public ShopeeOfferApiService(
        HttpClient http,
        IConfiguration config,
        ILogger<ShopeeOfferApiService> logger)
    {
        _http = http;
        _logger = logger;
        _appId = config["Shopee:AppId"] ?? "";
        _secret = config["Shopee:Secret"] ?? "";
        _endpoint = config["Shopee:GraphQlUrl"] ?? "https://open-api.affiliate.shopee.com.br/graphql";
    }

    public async Task<List<CrawlerResult>> BuscarProdutosAsync(
        string keyword,
        int page = 1,
        int limit = 20,
        int listType = 2,
        int sortType = 2,
        CancellationToken cancellationToken = default)
    {
        var resultados = new List<CrawlerResult>();

        if (string.IsNullOrWhiteSpace(keyword))
            return resultados;

        if (string.IsNullOrWhiteSpace(_appId) || string.IsNullOrWhiteSpace(_secret))
        {
            _logger.LogWarning("Shopee Offer API sem AppId/Secret configurados.");
            return resultados;
        }

        var payloadObj = new
        {
            query = @"
query ProductOffer($keyword: String!, $page: Int!, $limit: Int!, $listType: Int!, $sortType: Int!) {
  productOfferV2(
    keyword: $keyword,
    page: $page,
    limit: $limit,
    listType: $listType,
    sortType: $sortType
  ) {
    nodes {
      itemId
      productName
      productLink
      offerLink
      imageUrl
      priceMin
      priceMax
      priceDiscountRate
      sales
      ratingStar
      commissionRate
      commission
      shopId
      shopName
    }
    pageInfo {
      page
      limit
      hasNextPage
    }
  }
}",
            operationName = "ProductOffer",
            variables = new
            {
                keyword,
                page,
                limit,
                listType,
                sortType
            }
        };

        var payload = JsonSerializer.Serialize(payloadObj);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = GerarAssinatura(_appId, timestamp, payload, _secret);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"SHA256 Credential={_appId}, Timestamp={timestamp}, Signature={signature}");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Shopee productOfferV2 HTTP {Status}: {Content}", (int)response.StatusCode, content);
            return resultados;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                _logger.LogWarning("Shopee productOfferV2 retornou errors: {Errors}", errors.ToString());
                return resultados;
            }

            var nodes = doc.RootElement
                .GetProperty("data")
                .GetProperty("productOfferV2")
                .GetProperty("nodes");

            foreach (var node in nodes.EnumerateArray())
            {
                var itemId = GetString(node, "itemId");
                var shopId = GetString(node, "shopId");
                var titulo = GetString(node, "productName");
                var offerLink = GetString(node, "offerLink");
                var productLink = GetString(node, "productLink");
                var imageUrl = GetString(node, "imageUrl");
                var shopName = GetString(node, "shopName");

                var preco = ParseDecimal(node, "priceMin");
                var precoMax = ParseDecimal(node, "priceMax");
                var desconto = ParseDecimal(node, "priceDiscountRate");
                var vendas = ParseInt(node, "sales");
                var rating = ParseDecimal(node, "ratingStar");
                var comissaoValor = ParseDecimal(node, "commission");
                var comissaoTaxa = ParseDecimal(node, "commissionRate");

                var precoFinal = preco > 0 ? preco : precoMax;
                var linkFinal = !string.IsNullOrWhiteSpace(offerLink) ? offerLink : productLink;

                decimal? precoOriginal = null;
                if (desconto > 0 && desconto < 100 && precoFinal > 0)
                {
                    var divisor = 1m - (desconto / 100m);
                    if (divisor > 0)
                        precoOriginal = Math.Round(precoFinal / divisor, 2);
                }

                resultados.Add(new CrawlerResult
                {
                    ItemId = itemId,
                    ShopId = shopId,
                    Titulo = titulo,
                    Preco = precoFinal,
                    PrecoOriginal = precoOriginal,
                    Link = linkFinal,
                    ImagemUrl = imageUrl,
                    VideoUrl = "",
                    Fonte = "ShopeeApi",
                    Categoria = keyword,
                    Vendas = vendas,
                    Rating = rating,
                    Desconto = desconto,
                    ComissaoValor = comissaoValor,
                    ComissaoTaxa = comissaoTaxa,
                    ShopName = shopName,
                    Score = 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao interpretar response de productOfferV2: {Content}", content);
        }

        return resultados;
    }

    private static string GerarAssinatura(string appId, string timestamp, string payload, string secret)
    {
        var raw = $"{appId}{timestamp}{payload}{secret}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetString(JsonElement node, string prop)
    {
        return node.TryGetProperty(prop, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString() ?? ""
            : "";
    }

    private static decimal ParseDecimal(JsonElement node, string prop)
    {
        if (!node.TryGetProperty(prop, out var value) || value.ValueKind == JsonValueKind.Null)
            return 0;

        var raw = value.ToString()?.Trim() ?? "0";

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : 0;
    }

    private static int ParseInt(JsonElement node, string prop)
    {
        if (!node.TryGetProperty(prop, out var value) || value.ValueKind == JsonValueKind.Null)
            return 0;

        var raw = value.ToString()?.Trim() ?? "0";

        return int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var i)
            ? i
            : 0;
    }
}