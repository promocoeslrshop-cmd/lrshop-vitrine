using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class TelegramService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AppSettings> _settings;

    public TelegramService(HttpClient httpClient, IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<bool> EnviarAsync(string mensagem, CancellationToken cancellationToken = default)
    {
        var botToken = _settings.Value.Telegram.BotToken;
        var chatId = _settings.Value.Telegram.ChatId;

        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            return false;

        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text = mensagem,
            disable_web_page_preview = false
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}