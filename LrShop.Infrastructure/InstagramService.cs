using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class InstagramService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AppSettings> _settings;
    private readonly MetaTokenService _metaTokens;

    public InstagramService(HttpClient httpClient, IOptions<AppSettings> settings, MetaTokenService metaTokens)
    {
        _httpClient = httpClient;
        _settings = settings;
        _metaTokens = metaTokens;
    }

    public async Task<(bool ok, string? postId, string? permalink, string? erro)> EnviarAsync(
        string imageUrl,
        string caption,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var BusinessId = _settings.Value.Instagram.BusinessId;
            var accessToken = _settings.Value.Instagram.AccessToken;

            if (string.IsNullOrWhiteSpace(BusinessId))
                return (false, null, null, "Instagram.BusinessId não configurado.");

            if (string.IsNullOrWhiteSpace(accessToken))
                return (false, null, null, "Instagram.AccessToken não configurado.");

            var createUrl =
                $"https://graph.facebook.com/v25.0/{BusinessId}/media" +
                $"?image_url={HttpUtility.UrlEncode(imageUrl)}" +
                $"&caption={HttpUtility.UrlEncode(caption)}" +
                $"&access_token={HttpUtility.UrlEncode(accessToken)}";

            using var createResp = await _httpClient.PostAsync(createUrl, null, cancellationToken);
            var createBody = await createResp.Content.ReadAsStringAsync(cancellationToken);
            if (!createResp.IsSuccessStatusCode)
                return (false, null, null, $"Erro ao criar mídia: {createBody}");

            using var createJson = JsonDocument.Parse(createBody);
            var creationId = createJson.RootElement.GetProperty("id").GetString();

            if (string.IsNullOrWhiteSpace(creationId))
                return (false, null, null, "creation_id não retornado.");

            await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);

            var publishUrl =
                $"https://graph.facebook.com/v25.0/{BusinessId}/media_publish" +
                $"?creation_id={HttpUtility.UrlEncode(creationId)}" +
                $"&access_token={HttpUtility.UrlEncode(accessToken)}";

            using var publishResp = await _httpClient.PostAsync(publishUrl, null, cancellationToken);
            var publishBody = await publishResp.Content.ReadAsStringAsync(cancellationToken);
            if (!publishResp.IsSuccessStatusCode)
                return (false, null, null, $"Erro ao publicar mídia: {publishBody}");

            using var publishJson = JsonDocument.Parse(publishBody);
            var postId = publishJson.RootElement.GetProperty("id").GetString();

            string? permalink = null;
            if (!string.IsNullOrWhiteSpace(postId))
            {
                var infoUrl =
                    $"https://graph.facebook.com/v25.0/{postId}" +
                    $"?fields=id,permalink" +
                    $"&access_token={HttpUtility.UrlEncode(accessToken)}";

                using var infoResp = await _httpClient.GetAsync(infoUrl, cancellationToken);
                var infoBody = await infoResp.Content.ReadAsStringAsync(cancellationToken);

                if (infoResp.IsSuccessStatusCode)
                {
                    using var infoJson = JsonDocument.Parse(infoBody);
                    if (infoJson.RootElement.TryGetProperty("permalink", out var permalinkEl))
                        permalink = permalinkEl.GetString();
                }
            }

            return (true, postId, permalink, null);
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }
    public async Task<(bool ok, string? id, string? erro)> EnviarStoryVideoAsync(
     string videoUrl,
     CancellationToken cancellationToken = default)
    {
        try
        {
            var businessId = _settings.Value.Instagram.BusinessId;
            var accessToken = _settings.Value.Instagram.AccessToken;

            if (string.IsNullOrWhiteSpace(businessId))
                return (false, null, "Instagram.BusinessId não configurado.");

            if (string.IsNullOrWhiteSpace(accessToken))
                return (false, null, "Instagram.AccessToken não configurado.");

            if (string.IsNullOrWhiteSpace(videoUrl))
                return (false, null, "VideoUrl não informado.");

            var createUrl =
                $"https://graph.facebook.com/v25.0/{businessId}/media" +
                $"?media_type=STORIES" +
                $"&video_url={HttpUtility.UrlEncode(videoUrl)}" +
                $"&access_token={HttpUtility.UrlEncode(accessToken)}";

            using var createResp = await _httpClient.PostAsync(createUrl, null, cancellationToken);
            var createBody = await createResp.Content.ReadAsStringAsync(cancellationToken);

            if (!createResp.IsSuccessStatusCode)
                return (false, null, $"Erro ao criar story: {createBody}");

            using var createJson = JsonDocument.Parse(createBody);
            var creationId = createJson.RootElement.GetProperty("id").GetString();

            if (string.IsNullOrWhiteSpace(creationId))
                return (false, null, "CreationId do story não retornado.");

            var status = await AguardarContainerProntoAsync(creationId, accessToken, cancellationToken);
            if (!status.pronto)
                return (false, null, status.erro);

            var publishUrl =
                $"https://graph.facebook.com/v25.0/{businessId}/media_publish" +
                $"?creation_id={HttpUtility.UrlEncode(creationId)}" +
                $"&access_token={HttpUtility.UrlEncode(accessToken)}";

            using var publishResp = await _httpClient.PostAsync(publishUrl, null, cancellationToken);
            var publishBody = await publishResp.Content.ReadAsStringAsync(cancellationToken);

            if (!publishResp.IsSuccessStatusCode)
                return (false, null, $"Erro ao publicar story: {publishBody}");

            using var publishJson = JsonDocument.Parse(publishBody);
            var id = publishJson.RootElement.GetProperty("id").GetString();

            return (true, id, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
    private async Task<(bool pronto, string? erro)> AguardarContainerProntoAsync(
    string creationId,
    string accessToken,
    CancellationToken cancellationToken = default)
    {
        for (int tentativa = 1; tentativa <= 20; tentativa++)
        {
            var statusUrl =
                $"https://graph.facebook.com/v25.0/{creationId}" +
                $"?fields=status_code,status" +
                $"&access_token={HttpUtility.UrlEncode(accessToken)}";

            using var statusResp = await _httpClient.GetAsync(statusUrl, cancellationToken);
            var statusBody = await statusResp.Content.ReadAsStringAsync(cancellationToken);

            if (!statusResp.IsSuccessStatusCode)
                return (false, $"Erro ao consultar status da mídia: {statusBody}");

            using var statusJson = JsonDocument.Parse(statusBody);

            string? statusCode = null;

            if (statusJson.RootElement.TryGetProperty("status_code", out var statusCodeEl))
                statusCode = statusCodeEl.GetString();
            else if (statusJson.RootElement.TryGetProperty("status", out var statusEl))
                statusCode = statusEl.GetString();

            if (string.Equals(statusCode, "FINISHED", StringComparison.OrdinalIgnoreCase))
                return (true, null);

            if (string.Equals(statusCode, "ERROR", StringComparison.OrdinalIgnoreCase))
                return (false, $"Container retornou ERROR: {statusBody}");

            if (string.Equals(statusCode, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                return (false, $"Container expirou: {statusBody}");

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }

        return (false, "Tempo de processamento excedido. A mídia não ficou pronta para publicar.");
    }
}