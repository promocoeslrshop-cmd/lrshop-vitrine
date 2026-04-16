using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class FacebookService
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private readonly MetaTokenService _metaTokens;

    public FacebookService(HttpClient httpClient, IOptions<AppSettings> settings, MetaTokenService metaTokens)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _metaTokens = metaTokens;
    }

    public async Task<(bool ok, string? postId, string? erro)> EnviarAsync(string mensagem, string? imagemUrl)
    {
        try
        {
            if (!_settings.Facebook.Ativo)
                return (false, null, "Facebook desativado.");

            var pageId = _settings.Facebook.PageId;
            var token = await _metaTokens.GetFacebookPageTokenAsync();

            if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(token))
                return (false, null, "PageId/token do Facebook indisponível.");

            string url;

            if (!string.IsNullOrWhiteSpace(imagemUrl))
            {
                url = $"https://graph.facebook.com/v25.0/{pageId}/photos" +
                      $"?url={HttpUtility.UrlEncode(imagemUrl)}" +
                      $"&caption={HttpUtility.UrlEncode(mensagem)}" +
                      $"&access_token={HttpUtility.UrlEncode(token)}";
            }
            else
            {
                url = $"https://graph.facebook.com/v25.0/{pageId}/feed" +
                      $"?message={HttpUtility.UrlEncode(mensagem)}" +
                      $"&access_token={HttpUtility.UrlEncode(token)}";
            }

            var response = await _httpClient.PostAsync(url, null);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, null, content);

            using var json = JsonDocument.Parse(content);
            var id = json.RootElement.GetProperty("id").GetString();

            return (true, id, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}