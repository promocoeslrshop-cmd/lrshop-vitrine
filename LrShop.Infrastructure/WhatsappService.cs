using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace LrShop.Infrastructure;

public class WhatsappService
{
    private ChromeDriver? _driver;
    private readonly string _profilePath = @"C:\chrome-whatsapp-bot";

    public void Iniciar()
    {
        if (_driver != null)
            return;

        Directory.CreateDirectory(_profilePath);

        var options = new ChromeOptions();
        options.BinaryLocation = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        options.AddArgument($"--user-data-dir={_profilePath}");
        options.AddArgument("--profile-directory=Default");
        options.AddArgument("--start-maximized");
        options.AddArgument("--no-first-run");
        options.AddArgument("--no-default-browser-check");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-gpu");

        _driver = new ChromeDriver(options);
        _driver.Navigate().GoToUrl("https://web.whatsapp.com/");

        Thread.Sleep(15000);
    }

    public bool EnviarMensagemGrupo(string nomeGrupo, string mensagem)
    {
        if (_driver == null)
            throw new InvalidOperationException("WhatsApp não iniciado.");

        AbrirGrupo(nomeGrupo);
        return EnviarMensagemInterna(mensagem);
    }

    private void AbrirGrupo(string nomeGrupo)
    {
        if (_driver == null)
            throw new InvalidOperationException("WhatsApp não iniciado.");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60));

        wait.Until(d => d.FindElements(By.XPath("//span[@title]")).Count > 0);

        var grupo = _driver.FindElements(By.XPath($"//span[@title=\"{nomeGrupo}\"]"))
                           .FirstOrDefault();

        if (grupo != null)
        {
            grupo.Click();
            Thread.Sleep(3000);
            return;
        }

        var search = wait.Until(d =>
            d.FindElements(By.XPath("//div[@contenteditable='true']")).FirstOrDefault());

        if (search == null)
            throw new Exception("Campo de busca não encontrado.");

        search.Click();
        Thread.Sleep(500);

        search.SendKeys(OpenQA.Selenium.Keys.Control + "a");
        search.SendKeys(OpenQA.Selenium.Keys.Backspace);
        Thread.Sleep(300);

        search.SendKeys(nomeGrupo);
        Thread.Sleep(3000);

        grupo = wait.Until(d =>
            d.FindElements(By.XPath($"//span[@title=\"{nomeGrupo}\"]")).FirstOrDefault());

        if (grupo == null)
            throw new Exception($"Grupo '{nomeGrupo}' não encontrado.");

        grupo.Click();
        Thread.Sleep(3000);
    }

    private bool EnviarMensagemInterna(string mensagem)
    {
        if (_driver == null)
            throw new InvalidOperationException("Driver não iniciado.");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));

        var caixa = wait.Until(d =>
            d.FindElements(By.XPath("//footer//div[@contenteditable='true']")).LastOrDefault());

        if (caixa == null)
            throw new Exception("Caixa de mensagem não encontrada.");

        try
        {
            caixa.Click();
        }
        catch
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", caixa);
        }

        Thread.Sleep(300);

        var texto = LimparTexto(mensagem);

        caixa.SendKeys(OpenQA.Selenium.Keys.Control + "a");
        Thread.Sleep(200);
        caixa.SendKeys(OpenQA.Selenium.Keys.Backspace);
        Thread.Sleep(300);

        caixa.SendKeys(texto);
        Thread.Sleep(500);

        // se for link, espera o card montar antes de enviar
        if (texto.Contains("shopee.com", StringComparison.OrdinalIgnoreCase))
        {
            bool previewApareceu = false;

            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(2000);

                var preview = _driver.FindElements(By.XPath("//a[contains(@href,'shopee')]"));

                if (preview.Count > 0)
                {
                    previewApareceu = true;
                    break;
                }
            }

            if (!previewApareceu)
                throw new Exception("Preview não apareceu.");

            // tempo dinâmico + seguro
            Thread.Sleep(5000);
        }

        var botaoEnviar = _driver.FindElements(
                By.XPath("//div[@role='button' and @aria-label='Enviar'] | //span[@data-icon='send']"))
            .LastOrDefault();

        if (botaoEnviar != null)
        {
            try
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click();", botaoEnviar);
            }
            catch
            {
                caixa.SendKeys(OpenQA.Selenium.Keys.Enter);
            }
        }
        else
        {
            caixa.SendKeys(OpenQA.Selenium.Keys.Enter);
        }

        return true;
    }

    private static string LimparTexto(string texto)
    {
        var sb = new StringBuilder();

        foreach (var c in texto)
        {
            if (c <= '\uFFFF' && !char.IsSurrogate(c))
                sb.Append(c);
        }

        return sb.ToString();
    }
}