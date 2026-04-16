using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using LrShop.Infrastructure;
using LrShop.Shared;

namespace LrShop.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ProdutoRepository _produtos;
    private readonly AnuncioService _anuncios;
    private readonly TelegramService _telegram;
    private readonly InstagramService _instagram;
    private readonly FacebookService _facebook;
    private readonly MetaTokenService _metaTokens;
    private readonly LogRepository _logs;
    private readonly WhatsappService _whatsapp;
    private readonly ShopeeCrawlerService _crawler;
    private readonly IOptions<AppSettings> _settings;
    private readonly IConfiguration _config;
    private readonly CrawlerStateService _crawlerState;
    private readonly JsonExportService _jsonService;
    
    private DateTime _ultimoJson = DateTime.MinValue;
    private DateTime _ultimaChecagemTokens = DateTime.MinValue;
    private DateTime _ultimaExecucaoCrawler = DateTime.MinValue;
    private bool _whatsappIniciado = false;

    public Worker(
        ILogger<Worker> logger,
        ProdutoRepository produtos,
        AnuncioService anuncios,
        TelegramService telegram,
        InstagramService instagram,
        FacebookService facebook,
        MetaTokenService metaTokens,
        LogRepository logs,
        WhatsappService whatsapp,
        ShopeeCrawlerService crawler,
        IOptions<AppSettings> settings,
        CrawlerStateService crawlerState,
        IConfiguration config,
        JsonExportService jsonService)
    {
        _logger = logger;
        _produtos = produtos;
        _anuncios = anuncios;
        _telegram = telegram;
        _instagram = instagram;
        _facebook = facebook;
        _metaTokens = metaTokens;
        _logs = logs;
        _whatsapp = whatsapp;
        _crawler = crawler;
        _settings = settings;
        _config = config;
        _crawlerState = crawlerState;
        _jsonService = jsonService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = Math.Max(15, _settings.Value.Robo.IntervaloSegundos);
        var repeticaoMinimaHoras = Math.Max(0, _settings.Value.Robo.RepeticaoMinimaHoras);

        _logger.LogInformation("🤖 Worker iniciado com intervalo de {Intervalo}s", intervalo);


        // Inicia o WhatsApp uma vez só no startup, se estiver ativo
        if (_settings.Value.WhatsApp.Ativo)
        {
            try
            {
                _logger.LogInformation("🟢 Iniciando sessão do WhatsApp no startup...");
                _whatsapp.Iniciar();
                _whatsappIniciado = true;
                _logger.LogInformation("✅ Sessão do WhatsApp iniciada com sucesso.");
            }
            catch (Exception ex)
            {
                _whatsappIniciado = false;
                _logger.LogWarning(ex, "⚠️ Não foi possível iniciar o WhatsApp no startup. Vou tentar novamente quando precisar enviar.");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // SINCRONISMO DE PRODUTOS DA VITRINE
                if ((DateTime.Now - _ultimoJson).TotalHours >= 12)
                {
                    await _jsonService.GerarJsonAsync(stoppingToken);
                    _ultimoJson = DateTime.Now;
                    _logger.LogInformation("JSON da vitrine atualizado");
                }

                // CRAWLER AUTOMÁTICO
                var crawlerAtivoConfig = _config.GetValue<bool>("Crawler:Ativo");
                var crawlerAtivoDb = _crawlerState.EstaAtivo();

                var crawlerAtivo = crawlerAtivoConfig && crawlerAtivoDb;
                var intervaloCrawlerMin = _config.GetValue<int>("Crawler:IntervaloMinutos", 20);

                if (crawlerAtivo &&
                    DateTime.UtcNow >= _ultimaExecucaoCrawler.AddMinutes(intervaloCrawlerMin))
                {
                    try
                    {
                        _logger.LogInformation("🕷️ Executando crawler automático...");
                        var qtd = await _crawler.ExecutarAsync(stoppingToken);
                        _logger.LogInformation("✅ Crawler executado. Novos produtos: {Qtd}", qtd);
                    }
                    catch (Exception exCrawler)
                    {
                        _logger.LogError(exCrawler, "❌ Erro ao executar crawler");
                    }

                    _ultimaExecucaoCrawler = DateTime.UtcNow;
                }

                // TOKENS META
                if ((DateTime.UtcNow - _ultimaChecagemTokens) > TimeSpan.FromHours(12))
                {
                    try
                    {
                        _logger.LogInformation("🔐 Verificando/renovando tokens Meta...");
                        await _metaTokens.EnsureFreshTokensAsync(stoppingToken);

                        var snap = await _metaTokens.GetSnapshotAsync(stoppingToken);
                        _logger.LogInformation(
                            "🔐 Tokens | IG expira em {IgExpira} | FB user expira em {FbExpira}",
                            snap.InstagramExpiresAtUtc,
                            snap.FacebookUserExpiresAtUtc
                        );

                        _ultimaChecagemTokens = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Falha ao renovar tokens Meta. O robô vai continuar sem interromper os posts.");
                    }
                }

                var agora = DateTime.Now;

                if (agora.Hour < _settings.Value.Robo.HoraInicio ||
                    agora.Hour >= _settings.Value.Robo.HoraFim)
                {
                    _logger.LogInformation(
                        "⏰ Fora da janela. Agora: {Hora} | Permitido: {Inicio}h às {Fim}h",
                        agora.Hour,
                        _settings.Value.Robo.HoraInicio,
                        _settings.Value.Robo.HoraFim
                    );
                }
                else
                {
                    var produto = _produtos.ObterPrioritario(repeticaoMinimaHoras);

                    if (produto is null)
                    {
                        _logger.LogWarning("⚠️ Nenhum produto elegível.");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "🧠 Produto escolhido: ID={Id} | {Titulo} | R$ {Preco}",
                            produto.Id,
                            produto.Titulo,
                            produto.Preco
                        );

                        var mensagemTelegram = _anuncios.GerarTelegram(produto);
                        var mensagemInstagram = _anuncios.GerarInstagram(produto);
                        var mensagemFacebook = _anuncios.GerarFacebook(produto);
                        var mensagemWhatsapp = _anuncios.GerarWhatsappLink(produto);

                        var imageUrl = produto.ImagemUrl?.Trim() ?? "";
                        var videoUrl = produto.VideoUrl?.Trim() ?? "";

                        // TELEGRAM
                        try
                        {
                            var okTelegram = await _telegram.EnviarAsync(mensagemTelegram, stoppingToken);

                            _logs.Criar(new LogPostagem
                            {
                                ProdutoId = produto.Id,
                                Canal = "telegram",
                                Mensagem = mensagemTelegram,
                                CriadoEm = DateTime.UtcNow,
                                Sucesso = okTelegram
                            });

                            _logger.LogInformation("✅ Telegram OK? {Status}", okTelegram);
                        }
                        catch (Exception ex)
                        {
                            _logs.Criar(new LogPostagem
                            {
                                ProdutoId = produto.Id,
                                Canal = "telegram",
                                Mensagem = ex.Message,
                                CriadoEm = DateTime.UtcNow,
                                Sucesso = false
                            });

                            _logger.LogWarning(ex, "❌ Telegram falhou | Produto {Id}", produto.Id);
                        }

                        // INSTAGRAM FEED
                        if (_settings.Value.Instagram.Ativo)
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(imageUrl))
                                {
                                    _logger.LogWarning("⚠️ Produto {Id} sem imagem. Instagram feed ignorado.", produto.Id);

                                    _logs.Criar(new LogPostagem
                                    {
                                        ProdutoId = produto.Id,
                                        Canal = "instagram",
                                        Mensagem = mensagemInstagram,
                                        CriadoEm = DateTime.UtcNow,
                                        Sucesso = false
                                    });
                                }
                                else
                                {
                                    var resultadoInstagram = await _instagram.EnviarAsync(
                                        imageUrl,
                                        mensagemInstagram,
                                        stoppingToken
                                    );

                                    _logs.Criar(new LogPostagem
                                    {
                                        ProdutoId = produto.Id,
                                        Canal = "instagram",
                                        Mensagem = mensagemInstagram,
                                        CriadoEm = DateTime.UtcNow,
                                        Sucesso = resultadoInstagram.ok
                                    });

                                    if (resultadoInstagram.ok)
                                    {
                                        _logger.LogInformation(
                                            "✅ Instagram Feed OK | PostId={PostId} | Link={Permalink}",
                                            resultadoInstagram.postId,
                                            resultadoInstagram.permalink
                                        );
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            "❌ Instagram Feed falhou | Produto {Id} | Erro: {Erro}",
                                            produto.Id,
                                            resultadoInstagram.erro
                                        );
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logs.Criar(new LogPostagem
                                {
                                    ProdutoId = produto.Id,
                                    Canal = "instagram",
                                    Mensagem = ex.Message,
                                    CriadoEm = DateTime.UtcNow,
                                    Sucesso = false
                                });

                                _logger.LogWarning(ex, "❌ Instagram Feed quebrou | Produto {Id}", produto.Id);
                            }

                            // INSTAGRAM STORY
                            if (_settings.Value.Instagram.StoryAtivo)
                            {
                                try
                                {
                                    if (string.IsNullOrWhiteSpace(videoUrl))
                                    {
                                        _logger.LogWarning("⚠️ Produto {Id} sem vídeo. Instagram Story ignorado.", produto.Id);

                                        _logs.Criar(new LogPostagem
                                        {
                                            ProdutoId = produto.Id,
                                            Canal = "instagram_story",
                                            Mensagem = "Produto sem VideoUrl",
                                            CriadoEm = DateTime.UtcNow,
                                            Sucesso = false
                                        });
                                    }
                                    else
                                    {
                                        var resultadoStory = await _instagram.EnviarStoryVideoAsync(
                                            videoUrl,
                                            stoppingToken
                                        );

                                        _logs.Criar(new LogPostagem
                                        {
                                            ProdutoId = produto.Id,
                                            Canal = "instagram_story",
                                            Mensagem = videoUrl,
                                            CriadoEm = DateTime.UtcNow,
                                            Sucesso = resultadoStory.ok
                                        });

                                        if (resultadoStory.ok)
                                        {
                                            _logger.LogInformation(
                                                "🔥 Instagram Story OK | StoryId={StoryId} | Produto={ProdutoId}",
                                                resultadoStory.id,
                                                produto.Id
                                            );
                                        }
                                        else
                                        {
                                            _logger.LogWarning(
                                                "❌ Instagram Story falhou | Produto {Id} | Erro: {Erro}",
                                                produto.Id,
                                                resultadoStory.erro
                                            );
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logs.Criar(new LogPostagem
                                    {
                                        ProdutoId = produto.Id,
                                        Canal = "instagram_story",
                                        Mensagem = ex.Message,
                                        CriadoEm = DateTime.UtcNow,
                                        Sucesso = false
                                    });

                                    _logger.LogWarning(ex, "❌ Instagram Story quebrou | Produto {Id}", produto.Id);
                                }
                            }
                        }

                        // FACEBOOK
                        if (_settings.Value.Facebook.Ativo)
                        {
                            try
                            {
                                var resultadoFacebook = await _facebook.EnviarAsync(
                                    mensagemFacebook,
                                    imageUrl
                                );

                                _logs.Criar(new LogPostagem
                                {
                                    ProdutoId = produto.Id,
                                    Canal = "facebook",
                                    Mensagem = mensagemFacebook,
                                    CriadoEm = DateTime.UtcNow,
                                    Sucesso = resultadoFacebook.ok
                                });

                                if (resultadoFacebook.ok)
                                {
                                    _logger.LogInformation(
                                        "✅ Facebook OK | PostId={PostId}",
                                        resultadoFacebook.postId
                                    );
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "❌ Facebook falhou | Produto {Id} | Erro: {Erro}",
                                        produto.Id,
                                        resultadoFacebook.erro
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                _logs.Criar(new LogPostagem
                                {
                                    ProdutoId = produto.Id,
                                    Canal = "facebook",
                                    Mensagem = ex.Message,
                                    CriadoEm = DateTime.UtcNow,
                                    Sucesso = false
                                });

                                _logger.LogWarning(ex, "❌ Facebook quebrou | Produto {Id}", produto.Id);
                            }
                        }

                        // WHATSAPP
                        if (_settings.Value.WhatsApp.Ativo)
                        {
                            try
                            {
                                if (!_whatsappIniciado)
                                {
                                    _logger.LogInformation("🟢 Sessão do WhatsApp não está ativa. Tentando iniciar...");
                                    _whatsapp.Iniciar();
                                    _whatsappIniciado = true;
                                    _logger.LogInformation("✅ Sessão do WhatsApp iniciada.");
                                }

                                var grupo = _settings.Value.WhatsApp.Grupo;
                                var okWhatsapp = _whatsapp.EnviarMensagemGrupo(grupo, mensagemWhatsapp);

                                _logs.Criar(new LogPostagem
                                {
                                    ProdutoId = produto.Id,
                                    Canal = "whatsapp_grupo",
                                    Mensagem = mensagemWhatsapp,
                                    CriadoEm = DateTime.UtcNow,
                                    Sucesso = okWhatsapp
                                });

                                if (okWhatsapp)
                                {
                                    _logger.LogInformation(
                                        "✅ WhatsApp grupo OK | Produto={ProdutoId} | Grupo={Grupo}",
                                        produto.Id,
                                        grupo
                                    );
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "❌ WhatsApp grupo não confirmou envio | Produto={ProdutoId} | Grupo={Grupo}",
                                        produto.Id,
                                        grupo
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                _whatsappIniciado = false;

                                _logs.Criar(new LogPostagem
                                {
                                    ProdutoId = produto.Id,
                                    Canal = "whatsapp_grupo",
                                    Mensagem = ex.Message,
                                    CriadoEm = DateTime.UtcNow,
                                    Sucesso = false
                                });

                                _logger.LogWarning(
                                    ex,
                                    "❌ WhatsApp falhou | Produto {Id}",
                                    produto.Id
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Erro no robô");
            }

            _logger.LogInformation("⏳ Aguardando {Intervalo}s...", intervalo);
            await Task.Delay(TimeSpan.FromSeconds(intervalo), stoppingToken);
        }
    }

    private async Task<string?> BaixarImagemTemporariaAsync(string imageUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient();
            using var response = await http.GetAsync(imageUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var ext = ".jpg";
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

            if (contentType.Contains("png"))
                ext = ".png";
            else if (contentType.Contains("webp"))
                ext = ".webp";

            var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{ext}");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var file = File.Create(filePath);
            await stream.CopyToAsync(file, cancellationToken);

            return filePath;
        }
        catch
        {
            return null;
        }
    }
}