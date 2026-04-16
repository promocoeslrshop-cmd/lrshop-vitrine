using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class StoryVideoComposerService
{
    private readonly HttpClient _httpClient;
    private readonly CloudinaryStorageService _cloudinary;
    private readonly string _ffmpegPath;
    private readonly string _workDir;
    private readonly ILogger<StoryVideoComposerService> _logger;

    public StoryVideoComposerService(
        HttpClient httpClient,
        CloudinaryStorageService cloudinary,
        IOptions<AppSettings> settings,
        ILogger<StoryVideoComposerService> logger)
    {
        _httpClient = httpClient;
        _cloudinary = cloudinary;
        _ffmpegPath = settings.Value.Ffmpeg.ExecutablePath;
        _workDir = Path.Combine(Path.GetTempPath(), "lrshop_story");
        _logger = logger;

        Directory.CreateDirectory(_workDir);
    }

    public async Task<string?> GerarStoryComTextoAsync(Produto produto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(produto.VideoUrl))
        {
            _logger.LogWarning("Story composer: produto sem VideoUrl.");
            return null;
        }

        var inputPath = Path.Combine(_workDir, $"{Guid.NewGuid():N}_in.mp4");
        var outputPath = Path.Combine(_workDir, $"{Guid.NewGuid():N}_out.mp4");

        try
        {
            _logger.LogInformation("Story composer: baixando vídeo original: {Url}", produto.VideoUrl);

            using (var response = await _httpClient.GetAsync(produto.VideoUrl, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Story composer: falha ao baixar vídeo. Status={StatusCode}", response.StatusCode);
                    return null;
                }

                await using var inputStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(inputPath);
                await inputStream.CopyToAsync(fileStream, cancellationToken);
            }

            if (!File.Exists(inputPath))
            {
                _logger.LogWarning("Story composer: arquivo de entrada não foi criado.");
                return null;
            }

            _logger.LogInformation("Story composer: vídeo salvo em {InputPath}", inputPath);
            var precoTexto = $"R$ {produto.Preco:N2}"
            .Replace("\\", "\\\\")
            .Replace(":", "\\:")
            .Replace("'", "\\'")
            .Replace(",", "\\,");

            var args =
                $"-y -stream_loop -1 -t 8 -i \"{inputPath}\" " +
                "-vf \"scale=1080:1920:force_original_aspect_ratio=decrease," +
                "pad=1080:1920:(1080-iw)/2:(1920-ih)/2:black," +
                "setsar=1," +

                // topo protegido
                "drawbox=x=0:y=0:w=1080:h=150:color=black@0.35:t=fill," +

                // faixa central leve para o sticker
                "drawbox=x=120:y=860:w=840:h=180:color=white@0.08:t=fill," +
                "drawbox=x=120:y=860:w=840:h=22:color=white@0.08:t=fill," +

                // base inferior
                "drawbox=x=0:y=1480:w=1080:h=440:color=black@0.78:t=fill," +

                // título
                "drawtext=text='OFERTA DO DIA':" +
                "fontcolor=white:fontsize=38:" +
                "x=(w-text_w)/2:" +
                "y='if(lt(t,1.0), 240-(30*t), 190)':" +
                "shadowcolor=black@0.88:shadowx=3:shadowy=3:" +
                "enable='gte(t,0)'," +

                // preço
                $"drawtext=text='{precoTexto}':" +
                "fontcolor=#22c55e:fontsize=92:" +
                "x=(w-text_w)/2:" +
                "y='if(lt(t,1.8), 1600, 1495)':" +
                "shadowcolor=black@0.94:shadowx=4:shadowy=4:" +
                "enable='gte(t,0.8)'," +

                // CTA
                "drawtext=text='TOQUE NO STICKER':" +
                "fontcolor=white:fontsize=42:" +
                "x=(w-text_w)/2:" +
                "y='if(lt(t,2.4), 1750, 1652)':" +
                "shadowcolor=black@0.86:shadowx=2:shadowy=2:" +
                "enable='gte(t,1.4)'," +

                // canal
                "drawtext=text='@lrshop_promocoes':" +
                "fontcolor=#bfdbfe:fontsize=30:" +
                "x=(w-text_w)/2:" +
                "y=1788:" +
                "shadowcolor=black@0.84:shadowx=2:shadowy=2:" +
                "enable='gte(t,1.8)'\" " +

                "-an " +
                "-c:v libx264 -preset medium -crf 24 -pix_fmt yuv420p " +
                "-movflags +faststart " +
                $"\"{outputPath}\"";

            _logger.LogInformation("Story composer: executando ffmpeg em {FfmpegPath}", _ffmpegPath);
            _logger.LogInformation("Story composer: args ffmpeg = {Args}", args);

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            // IMPORTANTE: ler as saídas em paralelo para evitar deadlock do ffmpeg
            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(90), cancellationToken);

            var completed = await Task.WhenAny(waitTask, timeoutTask);

            if (completed == timeoutTask)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch { }

                _logger.LogWarning("Story composer: ffmpeg excedeu o tempo limite.");
                return null;
            }

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            _logger.LogInformation("Story composer: ffmpeg finalizou. ExitCode={ExitCode}", process.ExitCode);
            _logger.LogInformation("Story composer: ffmpeg stdout: {StdOut}", stdOut);
            _logger.LogInformation("Story composer: ffmpeg stderr: {StdErr}", stdErr);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Story composer: ffmpeg falhou.");
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogWarning("Story composer: arquivo de saída não foi criado.");
                return null;
            }

            _logger.LogInformation("Story composer: vídeo final criado em {OutputPath}", outputPath);

            var finalUrl = await _cloudinary.UploadVideoFileAsync(outputPath);

            _logger.LogInformation("Story composer: upload final Cloudinary = {FinalUrl}", finalUrl ?? "(null)");

            return finalUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Story composer: erro ao gerar story com texto.");
            return null;
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}