using System.Globalization;
using System.Text.RegularExpressions;
using LrShop.Shared;
using Microsoft.Playwright;

namespace LrShop.Infrastructure;

public class ShopeeService
{
    public async Task<Produto?> ImportarAsync(string url)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                Locale = "pt-BR"
            });

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 60000
            });

            await page.WaitForTimeoutAsync(4000);

            var html = await page.ContentAsync();

            var titulo = await ExtrairTituloRenderizado(page);
            if (string.IsNullOrWhiteSpace(titulo))
                titulo = ExtrairTituloFallback(html);

            var preco = await ExtrairPrecoRenderizado(page);
            if (preco <= 0)
                preco = ExtrairPrecoFallback(html);

            return new Produto
            {
                Titulo = string.IsNullOrWhiteSpace(titulo) ? "Produto Shopee" : titulo,
                Preco = preco,
                Link = url,
                Ativo = true,
                Score = 80,
                CriadoEm = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ShopeeService: " + ex.Message);
            return null;
        }
    }

    private async Task<string> ExtrairTituloRenderizado(IPage page)
    {
        string[] seletores =
        {
            "h1",
            "[data-sqe='name']",
            ".VCNVHn",
            ".qaNIZv",
            ".VU-ZEz",
            "meta[property='og:title']"
        };

        foreach (var seletor in seletores)
        {
            try
            {
                if (seletor.StartsWith("meta"))
                {
                    var content = await page.GetAttributeAsync(seletor, "content");
                    if (!string.IsNullOrWhiteSpace(content))
                        return LimparTitulo(content);
                }
                else
                {
                    var locator = page.Locator(seletor).First;
                    if (await locator.CountAsync() > 0)
                    {
                        var texto = await locator.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(texto) && texto.Length > 3)
                            return LimparTitulo(texto);
                    }
                }
            }
            catch
            {
            }
        }

        try
        {
            var title = await page.TitleAsync();
            if (!string.IsNullOrWhiteSpace(title))
                return LimparTitulo(title);
        }
        catch
        {
        }

        return "";
    }

    private async Task<decimal> ExtrairPrecoRenderizado(IPage page)
    {
        string[] seletores =
        {
            "[data-sqe='price']",
            ".pqTWkA",
            ".IZPeQz",
            ".Ybrg9j",
            ".vioxXd .pqTWkA",
            ".vioxXd",
            "meta[property='product:price:amount']"
        };

        foreach (var seletor in seletores)
        {
            try
            {
                if (seletor.StartsWith("meta"))
                {
                    var content = await page.GetAttributeAsync(seletor, "content");
                    var precoMeta = ParsePreco(content);
                    if (precoMeta > 0)
                        return precoMeta;
                }
                else
                {
                    var locator = page.Locator(seletor).First;
                    if (await locator.CountAsync() > 0)
                    {
                        var texto = await locator.InnerTextAsync();
                        var preco = ParsePreco(texto);
                        if (preco > 0)
                            return preco;
                    }
                }
            }
            catch
            {
            }
        }

        try
        {
            var body = await page.InnerTextAsync("body");
            var precoBody = ParsePreco(body);
            if (precoBody > 0)
                return precoBody;
        }
        catch
        {
        }

        return 0m;
    }

    private string ExtrairTituloFallback(string html)
    {
        string[] patterns =
        {
            "<meta property=\"og:title\" content=\"(.*?)\"",
            "<meta name=\"twitter:title\" content=\"(.*?)\"",
            "<title>(.*?)</title>",
            "\"name\":\"(.*?)\""
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                var titulo = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
                titulo = LimparTitulo(titulo);
                if (!string.IsNullOrWhiteSpace(titulo))
                    return titulo;
            }
        }

        return "";
    }

    private decimal ExtrairPrecoFallback(string html)
    {
        string[] patterns =
        {
            @"""price"":\s*""?([0-9\.,]+)""?",
            @"""price_min"":\s*([0-9\.,]+)",
            @"""price_max"":\s*([0-9\.,]+)",
            @"R\$\s*([0-9\.,]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var preco = ParsePreco(match.Groups[1].Value);
                if (preco > 0)
                    return preco;
            }
        }

        return 0m;
    }

    private string LimparTitulo(string titulo)
    {
        return titulo
            .Replace(" | Shopee Brasil", "")
            .Replace(" | Shopee", "")
            .Replace("&quot;", "\"")
            .Trim();
    }

    private decimal ParsePreco(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return 0m;

        var match = Regex.Match(texto, @"([0-9]{1,3}(?:\.[0-9]{3})*(?:,[0-9]{2})|[0-9]+(?:\.[0-9]{2})?)");
        if (!match.Success)
            return 0m;

        var valor = match.Groups[1].Value.Trim();

        if (valor.Contains(","))
            valor = valor.Replace(".", "").Replace(",", ".");
        else if (valor.Count(c => c == '.') > 1)
            valor = valor.Replace(".", "");

        if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var preco))
            return preco;

        return 0m;
    }
}