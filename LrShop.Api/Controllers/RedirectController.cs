using Microsoft.AspNetCore.Mvc;
using LrShop.Infrastructure;

namespace LrShop.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly ProdutoRepository _produtos;
    private readonly CliqueRepository _cliques;

    public RedirectController(ProdutoRepository produtos, CliqueRepository cliques)
    {
        _produtos = produtos;
        _cliques = cliques;
    }

    [HttpGet("r/{id}")]
    public IActionResult Redirecionar(int id, [FromQuery] string? origem = null)
    {
        var produto = _produtos.ObterPorId(id);

        if (produto == null || !produto.Ativo || string.IsNullOrWhiteSpace(produto.Link))
            return NotFound("Produto não encontrado.");

        _cliques.Criar(id, origem ?? "");

        return Redirect(produto.Link);
    }
}