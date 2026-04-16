using Microsoft.Playwright;

namespace LrShop.Infrastructure;

public class LinktreeAutomationService
{
    private readonly string _userDataDir;

    public LinktreeAutomationService()
    {
        _userDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LrShop",
            "playwright-linktree-profile");

        Directory.CreateDirectory(_userDataDir);
    }

    public Task AbrirLoginManualAsync()
    {
        var chromePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ms-playwright",
            "chromium-1208",
            "chrome-win64",
            "chrome.exe");

        if (!File.Exists(chromePath))
            throw new Exception("Chromium do Playwright não encontrado. Rode o install do Playwright.");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = chromePath,
            Arguments = $"--user-data-dir=\"{_userDataDir}\" https://linktr.ee/admin/links",
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }

    public async Task<bool> VerificarSessaoAsync()
    {
        IPlaywright? playwright = null;
        IBrowserContext? context = null;

        try
        {
            playwright = await Playwright.CreateAsync();

            context = await playwright.Chromium.LaunchPersistentContextAsync(
                _userDataDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    Timeout = 60000
                });

            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            await page.GotoAsync("https://linktr.ee/admin/links", new PageGotoOptions
            {
                Timeout = 60000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await page.WaitForTimeoutAsync(5000);

            var url = page.Url.ToLowerInvariant();

            return !url.Contains("/login") &&
                   !url.Contains("accounts.google.com");
        }
        catch (Exception ex)
        {
            throw new Exception("Erro ao verificar sessão do Linktree: " + ex.Message);
        }
        finally
        {
            if (context != null)
                await context.CloseAsync();

            playwright?.Dispose();
        }
    }

    public async Task PostarAsync(string titulo, string link)
    {
        IPlaywright? playwright = null;
        IBrowserContext? context = null;

        try
        {
            Console.WriteLine("=== LINKTREE AUTO START ===");
            Console.WriteLine("Título original: " + titulo);
            Console.WriteLine("Link original: " + link);

            playwright = await Playwright.CreateAsync();

            context = await playwright.Chromium.LaunchPersistentContextAsync(
                _userDataDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    Timeout = 60000
                });

            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            Console.WriteLine("Abrindo painel do Linktree...");
            await page.GotoAsync("https://linktr.ee/admin/links", new PageGotoOptions
            {
                Timeout = 60000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            await page.WaitForTimeoutAsync(4000);

            Console.WriteLine("URL atual: " + page.Url);

            if (page.Url.Contains("login", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Sessão não está logada no Linktree.");

            link = (link ?? "").Trim();
            titulo = (titulo ?? "").Trim();

            if (string.IsNullOrWhiteSpace(link))
                throw new Exception("Link não informado.");

            if (!Uri.TryCreate(link, UriKind.Absolute, out _))
                throw new Exception("Link inválido.");

            var tituloFinal = titulo;

            const int limiteTotal = 70;
            const string sufixo = "...";

            if (tituloFinal.Length > limiteTotal)
            {
                var limiteBase = limiteTotal - sufixo.Length;
                var tituloBase = tituloFinal[..limiteBase].TrimEnd();

                var ultimoEspaco = tituloBase.LastIndexOf(' ');
                if (ultimoEspaco >= limiteBase * 0.7)
                    tituloBase = tituloBase[..ultimoEspaco].TrimEnd();

                tituloFinal = tituloBase + sufixo;
            }

            Console.WriteLine($"Título final: {tituloFinal} (len={tituloFinal.Length})");

            Console.WriteLine("Procurando coleção SHOPEE...");
            var collectionCard = await ObterCardDaColecaoAsync(page, "SHOPEE");
            Console.WriteLine("Coleção SHOPEE localizada.");

            var addLinkButton = await ObterBotaoAddExatoDaColecaoAsync(page, "SHOPEE");

            if (addLinkButton == null)
                throw new Exception("Botão AddLink exato da coleção SHOPEE não encontrado.");

            Console.WriteLine("Botão AddLink exato da coleção encontrado.");

            await addLinkButton.ClickAsync(new() { Force = true });
            await page.WaitForTimeoutAsync(1500);

            var modal = await ObterModalAtivoAsync(page);
            Console.WriteLine("Modal ativo localizado.");

            var searchInput = modal.Locator("input[name='search']:visible").Last;

            if (await searchInput.CountAsync() == 0)
            {
                var linkTile = modal.GetByText("Link", new() { Exact = true }).First;
                if (await linkTile.CountAsync() > 0)
                {
                    Console.WriteLine("Clicando tile Link...");
                    await linkTile.ClickAsync(new() { Force = true });
                    await page.WaitForTimeoutAsync(1500);
                }

                searchInput = modal.Locator("input[name='search']:visible").Last;
            }

            if (await searchInput.CountAsync() == 0)
                throw new Exception("Campo de search do modal não encontrado.");

            Console.WriteLine("Digitando link no campo search...");
            await searchInput.ClickAsync(new() { Force = true });
            await searchInput.FillAsync("");
            await searchInput.TypeAsync(link, new() { Delay = 20 });

            await page.WaitForTimeoutAsync(800);

            var valorSearch = await searchInput.InputValueAsync();
            Console.WriteLine("VALOR SEARCH: " + valorSearch);

            if (string.IsNullOrWhiteSpace(valorSearch) || !valorSearch.Contains("http", StringComparison.OrdinalIgnoreCase))
                throw new Exception("O campo search não recebeu o link corretamente.");

            Console.WriteLine("Confirmando com Enter...");
            await searchInput.PressAsync("Enter");

            Console.WriteLine("Aguardando processamento do Linktree...");
            await page.WaitForTimeoutAsync(2500);

            var tentativa = 0;
            string valorAntes = "";

            while (tentativa < 5)
            {
                var campoTemp = page.Locator("input[name='title']:visible").Last;

                if (await campoTemp.CountAsync() > 0)
                {
                    var valorAtual = await campoTemp.InputValueAsync();

                    if (valorAtual == valorAntes)
                        break;

                    valorAntes = valorAtual;
                }

                await page.WaitForTimeoutAsync(800);
                tentativa++;
            }

            try
            {
                await page.Keyboard.PressAsync("Escape");
                await page.WaitForTimeoutAsync(1000);
            }
            catch
            {
            }

            var dominioEsperado = ExtrairDominio(link);
            Console.WriteLine("Domínio esperado no card: " + dominioEsperado);

            var novoCard = await LocalizarCardRecemCriadoDentroDaColecaoAsync(
                page,
                collectionCard,
                link,
                dominioEsperado);

            Console.WriteLine("Card recém-criado localizado dentro da coleção.");

            var titleInput = novoCard.Locator("input[name='title']:visible").First;
            var urlInput = novoCard.Locator("input[name='url']:visible").First;

            if (await titleInput.CountAsync() == 0 || await urlInput.CountAsync() == 0)
            {
                Console.WriteLine("Card não está em modo edição. Tentando abrir edição do card...");

                var editButton = await ObterBotaoEditarDoCardAsync(novoCard);

                if (editButton != null)
                {
                    try
                    {
                        await editButton.ClickAsync(new() { Force = true });
                        await page.WaitForTimeoutAsync(1800);
                    }
                    catch
                    {
                    }
                }

                titleInput = page.Locator("input[name='title']:visible").Last;
                urlInput = page.Locator("input[name='url']:visible").Last;
            }

            if (await urlInput.CountAsync() == 0)
                throw new Exception("Campo URL do item recém-criado não encontrado.");

            Console.WriteLine("Ajustando URL final...");
            await urlInput.ClickAsync(new() { Force = true });
            await urlInput.FillAsync("");
            await urlInput.TypeAsync(link, new() { Delay = 15 });
            await page.WaitForTimeoutAsync(700);

            var valorUrl = await urlInput.InputValueAsync();
            Console.WriteLine("LINK ESPERADO: " + link);
            Console.WriteLine("LINK LIDO NO CAMPO: " + valorUrl);

            if (!string.Equals(valorUrl.Trim(), link.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("URL divergente, reaplicando...");
                await urlInput.FillAsync(link);
                await page.WaitForTimeoutAsync(700);

                valorUrl = await urlInput.InputValueAsync();
                Console.WriteLine("LINK LIDO APÓS REAPLICAR: " + valorUrl);
            }

            var sugestao = page.GetByText("Suggested Titles", new() { Exact = false });
            if (await sugestao.CountAsync() > 0)
            {
                Console.WriteLine("Fechando sugestão automática...");
                try
                {
                    await page.Keyboard.PressAsync("Escape");
                    await page.WaitForTimeoutAsync(500);
                }
                catch
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(tituloFinal) && await titleInput.CountAsync() > 0)
            {
                Console.WriteLine("Preenchendo título...");

                await titleInput.ClickAsync(new() { Force = true });
                await titleInput.FillAsync("");

                await titleInput.EvaluateAsync("(el, value) => { el.value = value; }", tituloFinal);
                await titleInput.DispatchEventAsync("input");
                await titleInput.DispatchEventAsync("change");
                await titleInput.DispatchEventAsync("blur");

                await page.WaitForTimeoutAsync(900);

                var valorTitulo = await titleInput.InputValueAsync();
                Console.WriteLine("Título lido no campo: " + valorTitulo);

                if (!string.Equals(valorTitulo.Trim(), tituloFinal.Trim(), StringComparison.Ordinal))
                {
                    Console.WriteLine("Título foi sobrescrito, reaplicando...");

                    await titleInput.ClickAsync(new() { Force = true });
                    await titleInput.FillAsync("");
                    await titleInput.EvaluateAsync("(el, value) => { el.value = value; }", tituloFinal);
                    await titleInput.DispatchEventAsync("input");
                    await titleInput.DispatchEventAsync("change");
                    await titleInput.DispatchEventAsync("blur");

                    await page.WaitForTimeoutAsync(700);

                    valorTitulo = await titleInput.InputValueAsync();
                    Console.WriteLine("Título após reaplicar: " + valorTitulo);
                }
            }
            else
            {
                Console.WriteLine("Campo de título não encontrado; mantendo automático.");
            }

            Console.WriteLine("Aguardando autosave...");
            await page.WaitForTimeoutAsync(4000);

            string finalTitle = "";
            string finalUrl = "";

            try
            {
                if (await titleInput.CountAsync() > 0)
                    finalTitle = await titleInput.InputValueAsync();
            }
            catch
            {
            }

            try
            {
                finalUrl = await urlInput.InputValueAsync();
            }
            catch
            {
            }

            Console.WriteLine("VAL FINAL TITLE: " + finalTitle);
            Console.WriteLine("VAL FINAL TITLE LEN: " + finalTitle.Length);
            Console.WriteLine("VAL FINAL URL: " + finalUrl);

            if (string.IsNullOrWhiteSpace(finalTitle))
                throw new Exception("Título final ficou vazio. O Linktree não aceitou o preenchimento do título.");

            if (!string.Equals(finalUrl.Trim(), link.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new Exception($"URL final divergente. Esperado: '{link}' / Atual: '{finalUrl}'");

            Console.WriteLine("=== LINKTREE AUTO END OK ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine("=== LINKTREE AUTO ERROR ===");
            Console.WriteLine(ex.ToString());
            throw new Exception("Erro ao postar no Linktree: " + ex.Message);
        }
        finally
        {
            if (context != null)
                await context.CloseAsync();

            playwright?.Dispose();
        }
    }

    private static async Task<ILocator> ObterCardDaColecaoAsync(IPage page, string nomeColecao)
    {
        var titulo = page.GetByText(nomeColecao, new() { Exact = true }).First;

        if (await titulo.CountAsync() == 0)
            throw new Exception($"Coleção {nomeColecao} não encontrada.");

        await titulo.WaitForAsync(new() { Timeout = 15000 });

        for (var nivel = 1; nivel <= 4; nivel++)
        {
            var ancestor = titulo.Locator($"xpath=ancestor::div[{nivel}]");
            try
            {
                if (await ancestor.CountAsync() > 0 && await ancestor.IsVisibleAsync())
                    return ancestor;
            }
            catch
            {
            }
        }

        return titulo;
    }

    private static async Task<ILocator?> ObterBotaoAddExatoDaColecaoAsync(IPage page, string nomeColecao)
    {
        var titulo = page.GetByText(nomeColecao, new() { Exact = true }).First;

        if (await titulo.CountAsync() == 0)
            return null;

        var candidatos = new[]
        {
            "xpath=following::button[@data-testid='LinkEditor_GroupFields_AddLinkButton'][1]",
            "xpath=following::button[normalize-space(text())='+'][1]",
            "xpath=following::button[contains(@aria-label,'Add')][1]",
            "xpath=following::button[contains(@aria-label,'add')][1]",
            "xpath=following::button[contains(@title,'Add')][1]",
            "xpath=following::button[contains(@title,'add')][1]",
            "xpath=following::button[1]"
        };

        foreach (var xpath in candidatos)
        {
            var btn = titulo.Locator(xpath).First;
            try
            {
                if (await btn.CountAsync() > 0 && await btn.IsVisibleAsync())
                    return btn;
            }
            catch
            {
            }
        }

        return null;
    }

    private static async Task<ILocator> ObterModalAtivoAsync(IPage page)
    {
        for (var i = 0; i < 20; i++)
        {
            var modals = page.Locator("div[role='dialog']");
            var count = await modals.CountAsync();

            if (count > 0)
            {
                for (var idx = count - 1; idx >= 0; idx--)
                {
                    var modal = modals.Nth(idx);
                    try
                    {
                        if (await modal.IsVisibleAsync())
                            return modal;
                    }
                    catch
                    {
                    }
                }
            }

            await page.WaitForTimeoutAsync(300);
        }

        throw new Exception("Nenhum modal ativo do Linktree foi encontrado.");
    }

    private static string ExtrairDominio(string url)
    {
        try
        {
            return new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return url;
        }
    }

    private static async Task<ILocator> LocalizarCardRecemCriadoDentroDaColecaoAsync(
        IPage page,
        ILocator collectionCard,
        string linkCompleto,
        string dominio)
    {
        for (var i = 0; i < 20; i++)
        {
            var porLink = collectionCard.GetByText(linkCompleto, new() { Exact = false }).Last;
            if (await porLink.CountAsync() > 0)
            {
                try
                {
                    if (await porLink.IsVisibleAsync())
                        return porLink.Locator("xpath=ancestor::div[1]");
                }
                catch
                {
                }
            }

            var porDominio = collectionCard.GetByText(dominio, new() { Exact = false }).Last;
            if (await porDominio.CountAsync() > 0)
            {
                try
                {
                    if (await porDominio.IsVisibleAsync())
                        return porDominio.Locator("xpath=ancestor::div[1]");
                }
                catch
                {
                }
            }

            await page.WaitForTimeoutAsync(700);
        }

        throw new Exception("Não foi possível localizar o card recém-criado dentro da coleção SHOPEE.");
    }

    private static async Task<ILocator?> ObterBotaoEditarDoCardAsync(ILocator card)
    {
        var buttons = card.Locator("button, [role='button']");
        var total = await buttons.CountAsync();

        for (var i = 0; i < total; i++)
        {
            var btn = buttons.Nth(i);

            try
            {
                if (!await btn.IsVisibleAsync())
                    continue;

                var aria = await btn.GetAttributeAsync("aria-label");
                var title = await btn.GetAttributeAsync("title");
                var dataTestId = await btn.GetAttributeAsync("data-testid");

                if (!string.IsNullOrWhiteSpace(dataTestId) &&
                    (dataTestId.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
                     dataTestId.Contains("Edit", StringComparison.OrdinalIgnoreCase)))
                    return btn;

                if (!string.IsNullOrWhiteSpace(aria) &&
                    (aria.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
                     aria.Contains("editar", StringComparison.OrdinalIgnoreCase)))
                    return btn;

                if (!string.IsNullOrWhiteSpace(title) &&
                    (title.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
                     title.Contains("editar", StringComparison.OrdinalIgnoreCase)))
                    return btn;
            }
            catch
            {
            }
        }

        return null;
    }
}