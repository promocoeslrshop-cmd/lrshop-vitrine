using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LrShop.Infrastructure;

public class ShopeeApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ShopeeApiService> _logger;
    private readonly string _appId;
    private readonly string _secret;
    private readonly string _endpoint;

    public ShopeeApiService(
        HttpClient http,
        IConfiguration config,
        ILogger<ShopeeApiService> logger)
    {
        _http = http;
        _logger = logger;
        _appId = config["Shopee:AppId"] ?? "";
        _secret = config["Shopee:Secret"] ?? "";
        _endpoint = config["Shopee:GraphQlUrl"] ?? "https://open-api.affiliate.shopee.com.br/graphql";
    }

    public async Task<string?> GerarLinkAfiliadoAsync(string originUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originUrl))
            return null;

        if (string.IsNullOrWhiteSpace(_appId) || string.IsNullOrWhiteSpace(_secret))
        {
            _logger.LogWarning("Shopee API sem AppId/Secret configurados.");
            return null;
        }

        var payloadObj = new
        {
            query = @"
mutation GenerateShortLink($originUrl: String!, $subIds: [String!]) {
  generateShortLink(input: { originUrl: $originUrl, subIds: $subIds }) {
    shortLink
  }
}",
            operationName = "GenerateShortLink",
            variables = new
            {
                originUrl,
                subIds = new[] { "lrshop", "robo", "api" }
            }
        };

        var payload = JsonSerializer.Serialize(payloadObj);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var rawSignature = $"{_appId}{timestamp}{payload}{_secret}";
        var signature = GerarSha256(rawSignature);

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"SHA256 Credential={_appId}, Timestamp={timestamp}, Signature={signature}");

        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Shopee API HTTP {Status}: {Content}", (int)response.StatusCode, content);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                _logger.LogWarning("Shopee API retornou errors: {Errors}", errors.ToString());
                return null;
            }

            var shortLink = doc.RootElement
                .GetProperty("data")
                .GetProperty("generateShortLink")
                .GetProperty("shortLink")
                .GetString();

            return string.IsNullOrWhiteSpace(shortLink) ? null : shortLink.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler resposta da Shopee: {Content}", content);
            return null;
        }
    }

    private static string GerarSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}