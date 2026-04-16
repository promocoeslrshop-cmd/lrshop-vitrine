namespace LrShop.Shared;

public class CrawlerSettings
{
    public bool Ativo { get; set; } = true;
    public int IntervaloMinutos { get; set; } = 20;
    public int LimitePorExecucao { get; set; } = 20;
    public decimal PrecoMin { get; set; } = 9.90m;
    public decimal PrecoMax { get; set; } = 199.90m;
    public int ScoreMinimo { get; set; } = 40;
    public List<string> Keywords { get; set; } = new();
}