using LrShop.Shared;

namespace LrShop.Infrastructure;

public class LinktreeSyncService
{
    private readonly ProdutoLinktreeRepository _repository;
    private readonly LinktreeAutomationService _automation;

    public LinktreeSyncService(ProdutoLinktreeRepository repository, LinktreeAutomationService automation)
    {
        _repository = repository;
        _automation = automation;
    }

    public string GerarTituloLinktree(ProdutoLinktreeItem produto)
    {
        if (produto == null || string.IsNullOrWhiteSpace(produto.Titulo))
            return "Oferta LR SHOP";

        var titulo = produto.Titulo.Trim();

        if (titulo.Length > 45)
            titulo = titulo[..45].Trim() + "...";

        return titulo;
    }

    public string GerarTextoCopia(ProdutoLinktreeItem produto)
    {
        var titulo = GerarTituloLinktree(produto);
        return $"{titulo}{Environment.NewLine}{produto.Link}";
    }

    public List<ProdutoLinktreeItem> ObterPendentes()
    {
        return _repository.ListarPendentes();
    }

    public void ConfirmarEnvioManual(int produtoId)
    {
        var produto = _repository.ObterPorId(produtoId);
        if (produto == null)
            throw new Exception("Produto não encontrado.");

        var titulo = GerarTituloLinktree(produto);
        _repository.MarcarComoEnviado(produtoId, titulo);
    }
    public async Task PostarAutomatico(int produtoId)
{
    var produto = _repository.ObterPorId(produtoId);

    if (produto == null)
        throw new Exception("Produto não encontrado");

    await _automation.PostarAsync(produto.Titulo, produto.Link);

    ConfirmarEnvioManual(produtoId);
}

    public void VoltarParaPendente(int produtoId)
    {
        _repository.MarcarComoPendente(produtoId);
    }
}