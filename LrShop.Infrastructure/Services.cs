using LrShop.Shared;
using static System.Net.WebRequestMethods;

namespace LrShop.Infrastructure;

public class AnuncioService
{
    private readonly Random _random = new();

    private static readonly string[] Aberturas =
    [
        "🔥 OFERTA DO DIA",
        "✨ ACHADINHO DO MOMENTO",
        "🚨 PROMOÇÃO RELÂMPAGO",
        "💥 OFERTA IMPERDÍVEL",
        "🛍️ PRODUTO EM DESTAQUE",
        "💥 TESTADO E APROVADO"
    ];

    private static readonly string[] CtasInstagram =
    [
        "👉 Link na bio para comprar",
        "🛒 Acesse o link do perfil",
        "📲 Clique no link da bio",
        "🔥 Corre no link do perfil"
    ];

    private static readonly string[] CtasTelegram =
    [
        "🛒 Compre aqui:",
        "👉 Link do produto:",
        "📲 Acesse agora:",
        "🔥 Garanta o seu no link:"
    ];

    private static readonly string[] TelegramDivulgacaoInstagram =
    [
        "📲 Entre no nosso Telegram: @lrshop_promocoes",
        "🚀 Promoções diárias no Telegram: @lrshop_promocoes",
        "💣 Ofertas secretas no Telegram: @lrshop_promocoes",
        "📢 Procure no Telegram por: @lrshop_promocoes"
    ];

    private static readonly string[] TelegramDivulgacaoTelegram =
    [
        "📲 Entre no canal: https://t.me/lrshop_promocoes",
        "📲 Entre na loja: https://linktr.ee/lrshop_oficial",
        "💣 Ofertas secretas: https://t.me/lrshop_promocoes",
        "🚀 Promoções diárias: https://t.me/lrshop_promocoes",
        "📢 Canal VIP: https://t.me/lrshop_promocoes"
    ];

    private static readonly string[] Urgencias =
    [
        "⚠️ Promoções limitadas, aproveite agora",
        "⏳ Oferta por tempo limitado",
        "🔥 Corre antes que acabe",
        "🚨 Aproveite enquanto ainda está disponível"
    ];

    private static readonly string[] HashtagsInstagram =
    [
        "#compras #desconto #promoção #lrshop",
        "#oferta #achadinhos #desconto #lrshop",
        "#promoção #ofertas #compras #lrshop",
        "#achadinho #promo #oferta #lrshop"
    ];

    private static readonly string[] CtasStory =
    [
        "🛒 Compre pelo link da bio",
        "👉 Toque no link da bio",
        "📲 Acesse o link do perfil",
        "🔥 Corre no link da bio"
    ];

    private static readonly string[] ChamadasStory =
    [
        "🔥 OFERTA LR SHOP",
        "🚨 PROMOÇÃO ESPECIAL",
        "💥 ACHADINHO IMPERDÍVEL",
        "🛍️ OFERTA DO MOMENTO"
    ];

    public string GerarTelegram(Produto produto)
    {
        var abertura = Aberturas[_random.Next(Aberturas.Length)];
        var cta = CtasTelegram[_random.Next(CtasTelegram.Length)];
        var telegram = TelegramDivulgacaoTelegram[_random.Next(TelegramDivulgacaoTelegram.Length)];
        var urgencia = Urgencias[_random.Next(Urgencias.Length)];

        return
$"""
{abertura}

{produto.Titulo}
💰 Apenas R$ {produto.Preco:N2}

{cta}
{produto.Link}

{telegram}

{urgencia}
""";
    }

    public string GerarInstagram(Produto produto)
    {
        var abertura = Aberturas[_random.Next(Aberturas.Length)];
        var cta = CtasInstagram[_random.Next(CtasInstagram.Length)];
        var telegram = TelegramDivulgacaoInstagram[_random.Next(TelegramDivulgacaoInstagram.Length)];
        var urgencia = Urgencias[_random.Next(Urgencias.Length)];
        var hashtags = HashtagsInstagram[_random.Next(HashtagsInstagram.Length)];

        return
$"""
{abertura}

{produto.Titulo}
💰 Apenas R$ {produto.Preco:N2}

{cta}

{telegram}

{urgencia}

{hashtags}
""";
    }

    public string GerarTextoStory(Produto produto)
    {
        var chamada = ChamadasStory[_random.Next(ChamadasStory.Length)];
        var cta = CtasStory[_random.Next(CtasStory.Length)];

        return
$"""
{chamada}
💰 R$ {produto.Preco:N2}
{cta}
📲 @lrshop_promocoes
""";
    }

    public string GerarTextoStoryCurto(Produto produto)
    {
        var cta = CtasStory[_random.Next(CtasStory.Length)];

        return
$"""
🔥 OFERTA
💰 R$ {produto.Preco:N2}
{cta}
""";
    }

    public string GerarTextoStoryComTitulo(Produto produto)
    {
        var titulo = produto.Titulo ?? "";

        if (titulo.Length > 38)
            titulo = titulo.Substring(0, 38) + "...";

        var cta = CtasStory[_random.Next(CtasStory.Length)];

        return
$"""
🔥 {titulo}
💰 R$ {produto.Preco:N2}
{cta}
📲 @lrshop_promocoes
""";
    }

    public string GerarWhatsappLink(Produto produto)
    {
        return produto.Link ?? "";
    }

    public string Gerar(Produto produto)
    {
        return GerarTelegram(produto);
    }

    public string GerarFacebook(Produto produto)
    {
        var abertura = Aberturas[_random.Next(Aberturas.Length)];
        var urgencia = Urgencias[_random.Next(Urgencias.Length)];

        return
$"""
{abertura}

{produto.Titulo}
💰 Apenas R$ {produto.Preco:N2}

🛒 Compre aqui:
{produto.Link}

📲 Entre no Telegram:
https://t.me/lrshop_promocoes

{urgencia}
""";
    }
}