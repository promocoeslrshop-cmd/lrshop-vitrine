using LrShop.Shared;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace LrShop.Infrastructure;

public class ImagemService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AppSettings> _settings;
    private readonly string _webRootPath;

    public ImagemService(HttpClient httpClient, IOptions<AppSettings> settings, string webRootPath)
    {
        _httpClient = httpClient;
        _settings = settings;
        _webRootPath = webRootPath;
    }

    public async Task<string?> BaixarEConverterParaJpgAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        using var response = await _httpClient.GetAsync(imageUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var image = await SixLabors.ImageSharp.Image.LoadAsync(stream, cancellationToken);

        var uploadsDir = Path.Combine(_webRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid():N}.jpg";
        var filePath = Path.Combine(uploadsDir, fileName);

        await image.SaveAsJpegAsync(
            filePath,
            new JpegEncoder { Quality = 90 },
            cancellationToken
        );

        var baseUrl = _settings.Value.App.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/uploads/{fileName}";
    }
}