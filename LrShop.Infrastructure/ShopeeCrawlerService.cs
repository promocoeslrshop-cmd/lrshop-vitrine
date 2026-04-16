using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LrShop.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LrShop.Infrastructure;

public class ShopeeCrawlerService
{
    private readonly ProdutoRepository _produtoRepository;
    private readonly ILogger<ShopeeCrawlerService> _logger;
    private readonly IConfiguration _config;
    private readonly CloudinaryStorageService _cloudinary;
    private readonly ShopeeOfferApiService _offerApi;

    public ShopeeCrawlerService(
        ProdutoRepository produtoRepository,
        ILogger<ShopeeCrawlerService> logger,
        IConfiguration config,
        CloudinaryStorageService cloudinary,
        ShopeeOfferApiService offerApi)
    {
        _produtoRepository = produtoRepository;
        _logger = logger;
        _config = config;
        _cloudinary = cloudinary;
        _offerApi = offerApi;
    }

    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.GetValue<bool>("Crawler:Ativo"))
        {
            _logger.LogInformation("Crawler desativado.");
            return 0;
        }

        var keywords = _config.GetSection("Crawler:Keywords").Get<string[]>() ?? Array.Empty<string>();
        var precoMin = _config.GetValue<decimal>("Crawler:PrecoMin");
        var precoMax = _config.GetValue<decimal>("Crawler:PrecoMax");
        var scoreMinimo = _config.GetValue<int>("Crawler:ScoreMinimo", 60);
        var limite = _config.GetValue<int>("Crawler:LimitePorExecucao", 20);

        var paginas = new[] { 1, 2, 3 };
        var limitePorKeyword = 3;
        var limitePorTipo = 2;

        var tiposUsados = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int inseridos = 0;

        foreach (var keyword in keywords)
        {
            if (inseridos >= limite)
                break;

            int inseridosDaKeyword = 0;

            foreach (var pagina in paginas)
            {
                if (inseridos >= limite || inseridosDaKeyword >= limitePorKeyword)
                    break;

                _logger.LogInformation("🔎 Buscando produtos via API | keyword={Keyword} | página={Pagina}", keyword, pagina);

                var encontrados = await _offerApi.BuscarProdutosAsync(
                    keyword: keyword,
                    page: pagina,
                    limit: 10,
                    listType: 2,
                    sortType: 2,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("📦 API retornou {Total} produtos para keyword {Keyword} na página {Pagina}",
                    encontrados.Count, keyword, pagina);

                foreach (var item in encontrados)
                {
                    if (inseridos >= limite || inseridosDaKeyword >= limitePorKeyword)
                        break;

                    if (item.Preco < precoMin || item.Preco > precoMax)
                    {
                        _logger.LogInformation("⛔ Ignorado por preço: {Titulo} | {Preco}", item.Titulo, item.Preco);
                        continue;
                    }

                    var score = CalcularScoreIA(item);

                    if (score < scoreMinimo)
                    {
                        _logger.LogInformation("⛔ Ignorado por score: {Titulo} | {Score}", item.Titulo, score);
                        continue;
                    }

                    var hash = GerarHashForte(item);
                    if (_produtoRepository.ExistePorHash(hash))
                    {
                        _logger.LogInformation("⛔ Ignorado por hash duplicado: {Titulo}", item.Titulo);
                        continue;
                    }

                    if (_produtoRepository.ExisteTituloParecido(item.Titulo, item.Preco))
                    {
                        _logger.LogInformation("⛔ Ignorado por título parecido: {Titulo}", item.Titulo);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(item.Link) && _produtoRepository.ExistePorLink(item.Link))
                    {
                        _logger.LogInformation("⛔ Ignorado por link duplicado: {Titulo}", item.Titulo);
                        continue;
                    }

                    var tipo = ExtrairTipo(item.Titulo);
                    if (tiposUsados.TryGetValue(tipo, out var qtdTipo) && qtdTipo >= limitePorTipo)
                    {
                        _logger.LogInformation("⛔ Ignorado por excesso do tipo {Tipo}: {Titulo}", tipo, item.Titulo);
                        continue;
                    }

                    string imagemFinal = item.ImagemUrl ?? "";
                    string videoFinal = item.VideoUrl ?? "";

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(item.ImagemUrl))
                        {
                            var imagemCloudinary = await _cloudinary.ProcessarImagemAsync(item.ImagemUrl);
                            if (!string.IsNullOrWhiteSpace(imagemCloudinary))
                                imagemFinal = imagemCloudinary;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao processar imagem no Cloudinary para {Titulo}", item.Titulo);
                    }

                    var produto = new Produto
                    {
                        Titulo = item.Titulo,
                        Preco = item.Preco,
                        PrecoOriginal = item.PrecoOriginal,
                        Link = item.Link,
                        ImagemUrl = imagemFinal,
                        VideoUrl = videoFinal,
                        Score = score,
                        Ativo = true,
                        CriadoEm = DateTime.UtcNow,
                        Fonte = "CrawlerShopeeApi",
                        Categoria = string.IsNullOrWhiteSpace(item.Categoria) ? keyword : item.Categoria,
                        HashUnico = hash
                    };

                    _produtoRepository.Criar(produto);

                    tiposUsados[tipo] = tiposUsados.TryGetValue(tipo, out var atual)
                        ? atual + 1
                        : 1;

                    inseridos++;
                    inseridosDaKeyword++;

                    _logger.LogInformation(
                        "✅ IA inseriu: {Titulo} | Tipo={Tipo} | Score={Score} | Keyword={Keyword}",
                        produto.Titulo,
                        tipo,
                        produto.Score,
                        keyword
                    );

                    await Task.Delay(300, cancellationToken);
                }
            }
        }

        _logger.LogInformation("🏁 Crawler IA finalizado. Inseridos: {Total}", inseridos);
        return inseridos;
    }

    private int CalcularScoreIA(CrawlerResult item)
    {
        int score = 40;

        // =========================
        // PREÇO (faixa ideal venda)
        // =========================
        if (item.Preco > 0 && item.Preco <= 25) score += 10;
        else if (item.Preco <= 50) score += 9;
        else if (item.Preco <= 80) score += 8;
        else if (item.Preco <= 120) score += 6;
        else if (item.Preco <= 200) score += 3;
        else if (item.Preco > 300) score -= 6;

        // =========================
        // DESCONTO
        // =========================
        if (item.Desconto >= 70) score += 10;
        else if (item.Desconto >= 50) score += 8;
        else if (item.Desconto >= 30) score += 5;
        else if (item.Desconto >= 15) score += 2;

        // =========================
        // VENDAS (PROVA SOCIAL)
        // =========================
        if (item.Vendas >= 10000) score += 14;
        else if (item.Vendas >= 3000) score += 11;
        else if (item.Vendas >= 1000) score += 8;
        else if (item.Vendas >= 200) score += 5;
        else if (item.Vendas < 20) score -= 4;

        // =========================
        // RATING
        // =========================
        if (item.Rating >= 4.9m) score += 12;
        else if (item.Rating >= 4.8m) score += 10;
        else if (item.Rating >= 4.6m) score += 7;
        else if (item.Rating >= 4.3m) score += 4;
        else if (item.Rating > 0 && item.Rating < 4.0m) score -= 8;

        // =========================
        // COMISSÃO
        // =========================
        if (item.ComissaoTaxa >= 0.12m) score += 8;
        else if (item.ComissaoTaxa >= 0.08m) score += 6;
        else if (item.ComissaoTaxa >= 0.05m) score += 4;

        if (item.ComissaoValor >= 10) score += 5;
        else if (item.ComissaoValor >= 6) score += 3;
        else if (item.ComissaoValor >= 3) score += 1;

        // =========================
        // IA AVANÇADA (SEU DIFERENCIAL)
        // =========================

        // 🔥 Produto caro COM comissão = bom
        if (item.Preco >= 150 && item.ComissaoValor >= 10)
            score += 6;

        // ❌ Produto caro SEM venda = ruim
        if (item.Preco > 250 && item.Vendas < 50)
            score -= 10;

        // 🚀 Produto barato viral = ouro
        if (item.Vendas >= 5000 && item.Preco <= 80)
            score += 6;

        // =========================
        // TEXTO (gatilho de venda)
        // =========================
        var t = (item.Titulo ?? "").ToLowerInvariant();
        var c = (item.Categoria ?? "").ToLowerInvariant();

        if (Regex.IsMatch(t, @"\bkit\b")) score += 4;
        if (Regex.IsMatch(t, @"\bled\b")) score += 3;
        if (Regex.IsMatch(t, @"\bbluetooth\b")) score += 2;
        if (Regex.IsMatch(t, @"\bsem fio\b")) score += 2;
        if (Regex.IsMatch(t, @"\bwireless\b")) score += 2;
        if (Regex.IsMatch(t, @"\borganizador\b")) score += 5;
        if (Regex.IsMatch(t, @"\bpet\b")) score += 4;
        if (Regex.IsMatch(t, @"\binfantil\b")) score += 4;
        if (Regex.IsMatch(t, @"\bcozinha\b")) score += 3;
        if (Regex.IsMatch(t, @"\bportátil\b")) score += 2;
        if (Regex.IsMatch(t, @"\brecarregável\b")) score += 3;

        // penalizações
        if (Regex.IsMatch(t, @"\bpeça\b")) score -= 6;
        if (Regex.IsMatch(t, @"\breposi")) score -= 6;
        if (Regex.IsMatch(t, @"\bsensor\b")) score -= 5;
        if (Regex.IsMatch(t, @"\bmódulo\b")) score -= 5;
        if (Regex.IsMatch(t, @"\bplaca\b")) score -= 4;

        // =========================
        // CATEGORIA
        // =========================
        if (c.Contains("casa")) score += 2;
        if (c.Contains("cozinha")) score += 2;
        if (c.Contains("beleza")) score += 2;
        if (c.Contains("pet")) score += 2;
        if (c.Contains("infantil")) score += 2;

        // =========================
        // MÍDIA
        // =========================
        if (!string.IsNullOrWhiteSpace(item.ImagemUrl)) score += 4;
        if (!string.IsNullOrWhiteSpace(item.VideoUrl)) score += 6;

        // loja confiável leve boost
        if (!string.IsNullOrWhiteSpace(item.ShopName)) score += 1;

        // =========================
        // FILTRO DE SPAM (título ruim)
        // =========================
        var qtdNumeros = Regex.Matches(t, @"\d").Count;
        if (qtdNumeros >= 10) score -= 4;

        // =========================
        // NORMALIZAÇÃO FINAL
        // =========================
        return Math.Max(35, Math.Min(95, score));
    }

    private string GerarHashForte(CrawlerResult item)
    {
        var baseTexto = !string.IsNullOrWhiteSpace(item.ItemId) || !string.IsNullOrWhiteSpace(item.ShopId)
            ? $"{item.ShopId}:{item.ItemId}"
            : $"{Normalizar(item.Titulo)}|{Normalizar(item.Categoria)}|{item.Preco:F2}";

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(baseTexto);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private string ExtrairTipo(string titulo)
    {
        var t = (titulo ?? "").ToLowerInvariant();

        if (Regex.IsMatch(t, @"\bcaixa\b") || Regex.IsMatch(t, @"\bspeaker\b"))
            return "caixa";

        if (Regex.IsMatch(t, @"\borganizador\b"))
            return "organizador";

        if (Regex.IsMatch(t, @"\bpet\b"))
            return "pet";

        if (Regex.IsMatch(t, @"\bcozinha\b"))
            return "cozinha";

        if (Regex.IsMatch(t, @"\binfantil\b"))
            return "infantil";

        if (Regex.IsMatch(t, @"\bled\b"))
            return "led";

        if (Regex.IsMatch(t, @"\bheadphone\b") || Regex.IsMatch(t, @"\bfone\b") || Regex.IsMatch(t, @"\bfones\b"))
            return "fone";

        if (Regex.IsMatch(t, @"\bbluetooth\b"))
            return "bluetooth";

        return t.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "outros";
    }

    private string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        texto = texto.ToLowerInvariant();
        texto = Regex.Replace(texto, @"[^\p{L}\p{Nd}\s]", " ");
        texto = Regex.Replace(texto, @"\s+", " ");
        return texto.Trim();
    }
}