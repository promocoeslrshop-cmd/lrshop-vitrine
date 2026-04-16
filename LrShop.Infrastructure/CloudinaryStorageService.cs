using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using LrShop.Shared;
using HttpMethod = System.Net.Http.HttpMethod;

namespace LrShop.Infrastructure;

public class CloudinaryStorageService
{
    private readonly HttpClient _httpClient;
    private readonly Cloudinary _cloudinary;

    public CloudinaryStorageService(HttpClient httpClient, IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;

        var account = new Account(
            settings.Value.Cloudinary.CloudName,
            settings.Value.Cloudinary.ApiKey,
            settings.Value.Cloudinary.ApiSecret
        );

        _cloudinary = new Cloudinary(account);
    }

    public async Task<string?> ProcessarImagemAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
        request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
        request.Headers.Referrer = new Uri("https://shopee.com.br/");

        using var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync();

        Image image;
        try
        {
            image = await Image.LoadAsync(stream);
        }
        catch
        {
            return null;
        }

        await using var ms = new MemoryStream();
        using (image)
        {
            await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 });
        }

        ms.Position = 0;

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription($"{Guid.NewGuid():N}.jpg", ms),
            Folder = "lrshop"
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            return null;

        return result.SecureUrl?.ToString();
    }

    public async Task<string?> ProcessarVideoAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");

        using var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync();

        var uploadParams = new VideoUploadParams
        {
            File = new FileDescription($"{Guid.NewGuid():N}.mp4", stream),
            Folder = "lrshop/videos",

            Transformation = new Transformation()
                .Width(1080)
                .Height(1920)
                .Crop("limit")
                .VideoCodec("h264")
                .AudioCodec("aac")
                .FetchFormat("mp4")
                .Quality("auto")
                .BitRate("2000k"),

            EagerAsync = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            return null;

        return result.SecureUrl?.ToString();
    }
    public async Task<string?> UploadVideoFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        var uploadParams = new VideoUploadParams
        {
            File = new FileDescription(filePath),
            Folder = "lrshop/videos",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = true
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            return null;

        return result.SecureUrl?.ToString();
    }
}