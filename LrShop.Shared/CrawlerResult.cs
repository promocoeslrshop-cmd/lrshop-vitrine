namespace LrShop.Shared;

public class CrawlerResult
{
    public string ItemId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string Titulo { get; set; } = "";
    public decimal Preco { get; set; }
    public decimal? PrecoOriginal { get; set; }
    public string Link { get; set; } = "";
    public string ImagemUrl { get; set; } = "";
    public string VideoUrl { get; set; } = "";
    public int Score { get; set; }
    public string Fonte { get; set; } = "";
    public string Categoria { get; set; } = "";

    public int Vendas { get; set; }
    public decimal Rating { get; set; }
    public decimal Desconto { get; set; }
    public decimal ComissaoValor { get; set; }
    public decimal ComissaoTaxa { get; set; }
    public string ShopName { get; set; } = "";
}