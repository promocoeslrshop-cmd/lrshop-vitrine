using Microsoft.Playwright;

namespace LrShop.Infrastructure;

public class ShopeeAffiliateAutomationService
{
    private readonly string _userDataDir;

    public ShopeeAffiliateAutomationService()
    {
        _userDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LrShop",
            "playwright-shopee-profile");

        Directory.CreateDirectory(_userDataDir);
    }

    private static string GetChromePath()
    {
        var localAppChrome = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ms-playwright",
            "chromium-1208",
            "chrome-win",
            "chrome.exe");

        if (File.Exists(localAppChrome))
            return localAppChrome;

        var pfChrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        if (File.Exists(pfChrome))
            return pfChrome;

        var pf86Chrome = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        if (File.Exists(pf86Chrome))
            return pf86Chrome;

        throw new Exception("Chrome/Chromium não encontrado. Rode o install do Playwright.");
    }

    private static string ObterChromePath()
    {
        var localAppChrome = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ms-playwright",
            "chromium-1208",
            "chrome-win",
            "chrome.exe");

        if (File.Exists(localAppChrome))
            return localAppChrome;

        var pfChrome = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        if (File.Exists(pfChrome))
            return pfChrome;

        var pf86Chrome = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        if (File.Exists(pf86Chrome))
            return pf86Chrome;

        throw new Exception("Chrome/Chromium não encontrado. Rode o install do Playwright.");
    }

    private async Task<IBrowserContext> CreateContextAsync(bool headless)
    {
        var chromePath = GetChromePath();
        var playwright = await Playwright.CreateAsync();

        return await playwright.Chromium.LaunchPersistentContextAsync(
            _userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = headless,
                ExecutablePath = chromePath,
                ViewportSize = new ViewportSize
                {
                    Width = 1400,
                    Height = 900
                },
                Args = new[]
                {
                    "--start-maximized",
                    "--disable-blink-features=AutomationControlled",
                    "--no-sandbox",
                    "--disable-dev-shm-usage"
                },
                Locale = "pt-BR",
                TimezoneId = "America/Sao_Paulo"
            });
    }

    public async Task AbrirLoginManualAsync()
    {
        var userDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LrShop",
            "playwright-shopee-profile");

        Directory.CreateDirectory(userDataDir);

        var chromePath = ObterChromePath();
        using var playwright = await Playwright.CreateAsync();

        await using var context = await playwright.Chromium.LaunchPersistentContextAsync(
            userDataDir,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                ExecutablePath = chromePath,
                ViewportSize = new ViewportSize { Width = 1400, Height = 900 },
                Locale = "pt-BR",
                TimezoneId = "America/Sao_Paulo"
            });

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

        await page.GotoAsync("https://affiliate.shopee.com.br/", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 120000
        });

        Console.WriteLine("Faça login manualmente na Shopee Afiliados e depois pressione ENTER aqui.");
        Console.ReadLine();
    }

    public async Task<bool> VerificarSessaoAsync()
    {
        try
        {
            await using var context = await CreateContextAsync(headless: true);
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            await page.GotoAsync("https://affiliate.shopee.com.br/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            await page.WaitForTimeoutAsync(3000);

            var content = await page.ContentAsync();

            if (content.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("entrar", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GerarLinkAfiliadoAsync(string urlProduto)
    {
        if (string.IsNullOrWhiteSpace(urlProduto))
            return null;

        await using var context = await CreateContextAsync(headless: true);
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

        try
        {
            await page.GotoAsync("https://affiliate.shopee.com.br/", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            await page.WaitForTimeoutAsync(3000);

            var contentInicial = await page.ContentAsync();
            if (contentInicial.Contains("login", StringComparison.OrdinalIgnoreCase) ||
                contentInicial.Contains("entrar", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Tenta abrir a área de link personalizado.
            // Ajuste o caminho se a Shopee mudar a rota.
            await page.GotoAsync("https://affiliate.shopee.com.br/home/customlink", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            await page.WaitForTimeoutAsync(3000);

            // Possíveis seletores para textarea/input de URL
            var selectorsEntrada = new[]
            {
                "textarea",
                "input[type='url']",
                "input[placeholder*='http']",
                "input"
            };

            ILocator? campoUrl = null;

            foreach (var selector in selectorsEntrada)
            {
                var loc = page.Locator(selector).First;
                if (await loc.CountAsync() > 0)
                {
                    campoUrl = loc;
                    break;
                }
            }

            if (campoUrl == null)
                return null;

            await campoUrl.FillAsync(string.Empty);
            await campoUrl.FillAsync(urlProduto);

            // Botões mais prováveis
            var botoes = new[]
            {
                "button:has-text('Gerar link')",
                "button:has-text('Gerar')",
                "button:has-text('Generate')",
                "button:has-text('Criar link')"
            };

            bool clicou = false;

            foreach (var botao in botoes)
            {
                var locator = page.Locator(botao).First;
                if (await locator.CountAsync() > 0)
                {
                    await locator.ClickAsync();
                    clicou = true;
                    break;
                }
            }

            if (!clicou)
                return null;

            await page.WaitForTimeoutAsync(4000);

            // Tenta achar campo/input com o link gerado
            var inputs = page.Locator("input");
            var totalInputs = await inputs.CountAsync();

            for (var i = 0; i < totalInputs; i++)
            {
                var input = inputs.Nth(i);
                var value = await input.InputValueAsync();

                if (!string.IsNullOrWhiteSpace(value) &&
                    (value.Contains("shopee", StringComparison.OrdinalIgnoreCase) ||
                     value.Contains("shope.ee", StringComparison.OrdinalIgnoreCase)))
                {
                    return value.Trim();
                }
            }

            // Fallback: tenta capturar qualquer link visível no HTML
            var html = await page.ContentAsync();
            var match = System.Text.RegularExpressions.Regex.Match(
                html,
                @"https?:\/\/[^\s""'<>]+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Value.Trim();

            return null;
        }
        catch
        {
            return null;
        }
    }
}