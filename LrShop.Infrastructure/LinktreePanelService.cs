using LrShop.Shared;

namespace LrShop.Infrastructure;

public class LinktreePanelService
{
    private readonly ProdutoLinktreeRepository _repository;
    private readonly LinktreeSyncService _syncService;

    public LinktreePanelService(
        ProdutoLinktreeRepository repository,
        LinktreeSyncService syncService)
    {
        _repository = repository;
        _syncService = syncService;
    }

    public List<ProdutoLinktreeItem> ListarPendentes()
    {
        return _syncService.ObterPendentes();
    }

    public ProdutoLinktreeItem? ObterPorId(int produtoId)
    {
        return _repository.ObterPorId(produtoId);
    }

    public string CopiarTextoLinktree(int produtoId)
    {
        var produto = _repository.ObterPorId(produtoId);
        if (produto == null)
            throw new Exception("Produto não encontrado.");

        return _syncService.GerarTextoCopia(produto);
    }

    public void MarcarComoEnviado(int produtoId)
    {
        _syncService.ConfirmarEnvioManual(produtoId);
    }

    public void MarcarComoPendente(int produtoId)
    {
        _syncService.VoltarParaPendente(produtoId);
    }

    public async Task PostarAutomatico(int produtoId)
    {
        await _syncService.PostarAutomatico(produtoId);
    }
}