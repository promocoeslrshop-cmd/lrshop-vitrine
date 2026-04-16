using System.Text.Json;
using Microsoft.Extensions.Options;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class MetaTokenService
{
    private readonly HttpClient _httpClient;
    private readonly AppSettings _settings;
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public MetaTokenService(HttpClient httpClient, IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _statePath = Path.Combine(AppContext.BaseDirectory, _settings.MetaAuth.TokenStatePath);
    }

    public async Task EnsureFreshTokensAsync(CancellationToken ct = default)
    {
        var state = await LoadStateAsync(ct);

        if (_settings.Instagram.Ativo)
        {
            state = await EnsureInstagramAsync(state, ct);
        }

        if (_settings.Facebook.Ativo)
        {
            state = await EnsureFacebookAsync(state, ct);
        }

        await SaveStateAsync(state, ct);
    }

    public async Task<string?> GetInstagramTokenAsync(CancellationToken ct = default)
    {
        var state = await LoadStateAsync(ct);
        return state.InstagramLongLivedToken;
    }

    public async Task<string?> GetFacebookPageTokenAsync(CancellationToken ct = default)
    {
        var state = await LoadStateAsync(ct);
        return state.FacebookPageToken;
    }

    public async Task<TokenSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        return await LoadStateAsync(ct);
    }

    private async Task<TokenSnapshot> EnsureInstagramAsync(TokenSnapshot state, CancellationToken ct)
    {
        var lead = _settings.MetaAuth.RefreshLeadDays;

        if (string.IsNullOrWhiteSpace(state.InstagramLongLivedToken))
        {
            if (string.IsNullOrWhiteSpace(_settings.Instagram.AccessToken))
                return state;

            var exchanged = await ExchangeInstagramShortForLongAsync(_settings.Instagram.AccessToken, ct);
            if (!string.IsNullOrWhiteSpace(exchanged.AccessToken))
            {
                state.InstagramLongLivedToken = exchanged.AccessToken;
                state.InstagramExpiresAtUtc = DateTime.UtcNow.AddSeconds(exchanged.ExpiresIn);
            }

            return state;
        }

        if (state.InstagramExpiresAtUtc <= DateTime.UtcNow.AddDays(lead))
        {
            var refreshed = await RefreshInstagramLongAsync(state.InstagramLongLivedToken, ct);
            if (!string.IsNullOrWhiteSpace(refreshed.AccessToken))
            {
                state.InstagramLongLivedToken = refreshed.AccessToken;
                state.InstagramExpiresAtUtc = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);
            }
        }

        return state;
    }

    private async Task<TokenSnapshot> EnsureFacebookAsync(TokenSnapshot state, CancellationToken ct)
    {
        var lead = _settings.MetaAuth.RefreshLeadDays;

        if (string.IsNullOrWhiteSpace(state.FacebookUserLongLivedToken))
        {
            if (string.IsNullOrWhiteSpace(_settings.Facebook.UserShortLivedToken))
                return state;

            var exchanged = await ExchangeFacebookShortForLongAsync(_settings.Facebook.UserShortLivedToken, ct);
            if (!string.IsNullOrWhiteSpace(exchanged.AccessToken))
            {
                state.FacebookUserLongLivedToken = exchanged.AccessToken;
                state.FacebookUserExpiresAtUtc = DateTime.UtcNow.AddSeconds(exchanged.ExpiresIn);

                var pageToken = await GetPageTokenFromUserTokenAsync(
                    state.FacebookUserLongLivedToken,
                    _settings.Facebook.PageId,
                    ct);

                if (!string.IsNullOrWhiteSpace(pageToken))
                    state.FacebookPageToken = pageToken;
            }

            return state;
        }

        var valid = await DebugTokenIsValidAsync(state.FacebookUserLongLivedToken, ct);

        // Se ainda válido, sempre rederiva o page token.
        if (valid)
        {
            var pageToken = await GetPageTokenFromUserTokenAsync(
                state.FacebookUserLongLivedToken,
                _settings.Facebook.PageId,
                ct);

            if (!string.IsNullOrWhiteSpace(pageToken))
                state.FacebookPageToken = pageToken;
        }

        // Se estiver perto do vencimento, não há refresh documentado igual ao Instagram.
        // Mantemos aviso via data estimada. Se expirar, você renova o user short token uma vez.
        return state;
    }

    private async Task<(string? AccessToken, int ExpiresIn)> ExchangeInstagramShortForLongAsync(string shortToken, CancellationToken ct)
    {
        var url =
            $"https://graph.instagram.com/access_token" +
            $"?grant_type=ig_exchange_token" +
            $"&client_secret={Uri.EscapeDataString(_settings.MetaAuth.AppSecret)}" +
            $"&access_token={Uri.EscapeDataString(shortToken)}";

        using var resp = await _httpClient.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return (null, 0);

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.GetProperty("access_token").GetString();
        var expires = json.RootElement.GetProperty("expires_in").GetInt32();
        return (token, expires);
    }

    private async Task<(string? AccessToken, int ExpiresIn)> RefreshInstagramLongAsync(string longToken, CancellationToken ct)
    {
        var url =
            $"https://graph.instagram.com/refresh_access_token" +
            $"?grant_type=ig_refresh_token" +
            $"&access_token={Uri.EscapeDataString(longToken)}";

        using var resp = await _httpClient.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return (null, 0);

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.TryGetProperty("access_token", out var tokenEl)
            ? tokenEl.GetString()
            : longToken;
        var expires = json.RootElement.GetProperty("expires_in").GetInt32();
        return (token, expires);
    }

    private async Task<(string? AccessToken, int ExpiresIn)> ExchangeFacebookShortForLongAsync(string shortToken, CancellationToken ct)
    {
        var url =
            $"https://graph.facebook.com/v25.0/oauth/access_token" +
            $"?grant_type=fb_exchange_token" +
            $"&client_id={Uri.EscapeDataString(_settings.MetaAuth.AppId)}" +
            $"&client_secret={Uri.EscapeDataString(_settings.MetaAuth.AppSecret)}" +
            $"&fb_exchange_token={Uri.EscapeDataString(shortToken)}";

        using var resp = await _httpClient.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return (null, 0);

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.GetProperty("access_token").GetString();
        var expires = json.RootElement.TryGetProperty("expires_in", out var expiresEl)
            ? expiresEl.GetInt32()
            : 60 * 24 * 60 * 60;
        return (token, expires);
    }

    private async Task<string?> GetPageTokenFromUserTokenAsync(string userToken, string pageId, CancellationToken ct)
    {
        var url =
            $"https://graph.facebook.com/v25.0/me/accounts" +
            $"?fields=id,name,access_token" +
            $"&access_token={Uri.EscapeDataString(userToken)}";

        using var resp = await _httpClient.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var item in dataEl.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            if (id == pageId)
                return item.GetProperty("access_token").GetString();
        }

        return null;
    }

    private async Task<bool> DebugTokenIsValidAsync(string token, CancellationToken ct)
    {
        var appAccessToken = $"{_settings.MetaAuth.AppId}|{_settings.MetaAuth.AppSecret}";
        var url =
            $"https://graph.facebook.com/v25.0/debug_token" +
            $"?input_token={Uri.EscapeDataString(token)}" +
            $"&access_token={Uri.EscapeDataString(appAccessToken)}";

        using var resp = await _httpClient.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return false;

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("data", out var dataEl)) return false;
        return dataEl.TryGetProperty("is_valid", out var validEl) && validEl.GetBoolean();
    }

    private async Task<TokenSnapshot> LoadStateAsync(CancellationToken ct)
    {
        if (!File.Exists(_statePath))
            return new TokenSnapshot();

        var json = await File.ReadAllTextAsync(_statePath, ct);
        return JsonSerializer.Deserialize<TokenSnapshot>(json, _jsonOptions) ?? new TokenSnapshot();
    }

    private async Task SaveStateAsync(TokenSnapshot state, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(_statePath, json, ct);
    }
}

public class TokenSnapshot
{
    public string? InstagramLongLivedToken { get; set; }
    public DateTime InstagramExpiresAtUtc { get; set; }

    public string? FacebookUserLongLivedToken { get; set; }
    public DateTime FacebookUserExpiresAtUtc { get; set; }

    public string? FacebookPageToken { get; set; }
}