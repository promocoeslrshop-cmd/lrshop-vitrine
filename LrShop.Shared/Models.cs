namespace LrShop.Shared;

public class Produto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public decimal Preco { get; set; }
    public string Link { get; set; } = "";
    public string ImagemUrl { get; set; } = ""; // 👈 ADICIONA AQUI
    public string VideoUrl { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public int Score { get; set; } = 50;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public decimal? PrecoOriginal { get; set; }
    public string Fonte { get; set; } = "Shopee";
    public string Categoria { get; set; } = "";
    public string HashUnico { get; set; } = "";

    public FacebookSettings Facebook { get; set; } = new();
}

public class Usuario
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
    public DateTime DataExpiracao { get; set; }
}

public class LogPostagem
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public string Canal { get; set; } = "telegram";
    public string Mensagem { get; set; } = "";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public bool Sucesso { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string Token { get; set; } = "";
    public string Message { get; set; } = "";
}

public class AppSettings
{
    public TelegramSettings Telegram { get; set; } = new();
    public RoboSettings Robo { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public FrontSettings App { get; set; } = new();
    public InstagramSettings Instagram { get; set; } = new();
    public CloudinarySettings Cloudinary { get; set; } = new();
    public FacebookSettings Facebook { get; set; } = new();
    public MetaAuthSettings MetaAuth { get; set; } = new();
    public FfmpegSettings Ffmpeg { get; set; } = new();
    public WhatsAppSettings WhatsApp { get; set; } = new();
}

public class TelegramSettings
{
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
}

public class RoboSettings
{
    public int IntervaloSegundos { get; set; } = 60;
    public int RepeticaoMinimaHoras { get; set; } = 6;
    public int HoraInicio { get; set; } = 8;
    public int HoraFim { get; set; } = 22;
}

public class CliqueProduto
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public DateTime CriadoEm { get; set; }
    public string Origem { get; set; } = "";
}

public class FrontSettings
{
    public string BaseUrl { get; set; } = "";
}

public class InstagramSettings
{
    public bool Ativo { get; set; }
    public bool StoryAtivo { get; set; } = true;
    public bool StorySomenteVideo { get; set; } = true;
    public string BusinessId { get; set; } = "";
    public string AccessToken { get; set; } = "";
}
public class CloudinarySettings
{
    public bool Ativo { get; set; } = true;
    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
}
public class FacebookSettings
{
    public bool Ativo { get; set; } = true;
    public string PageId { get; set; } = "";
    public string UserShortLivedToken { get; set; } = ""; // usado 1x para iniciar
}
public class MetaAuthSettings
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string TokenStatePath { get; set; } = "meta-tokens.json";
    public int RefreshLeadDays { get; set; } = 15;
}

public class WhatsAppSettings
{
    public bool Ativo { get; set; } = false;
    public string Grupo { get; set; } = "Promoções LR SHOP";
}

public class DatabaseSettings
{
    public string Path { get; set; } = "lrshop.db";
}

public class FfmpegSettings
{
    public string ExecutablePath { get; set; } = "ffmpeg";
}
