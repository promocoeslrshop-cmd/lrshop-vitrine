namespace LrShop.Shared;

public class ProdutoLinktreeItem
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public decimal Preco { get; set; }
    public string Link { get; set; } = "";
    public string ImagemUrl { get; set; } = "";
    public bool Ativo { get; set; }
    public int Score { get; set; }
    public string CriadoEm { get; set; } = "";
    public string LinktreeStatus { get; set; } = "";
    public string LinktreeTitle { get; set; } = "";
    public string LinktreeSentAt { get; set; } = "";
}
