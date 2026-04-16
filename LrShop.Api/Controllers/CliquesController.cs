using Microsoft.AspNetCore.Mvc;
using LrShop.Infrastructure;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/cliques")]
public class CliquesController : ControllerBase
{
    private readonly CliqueRepository _cliques;

    public CliquesController(CliqueRepository cliques)
    {
        _cliques = cliques;
    }

    [HttpGet("{produtoId}")]
    public IActionResult ContarPorProduto(int produtoId)
    {
        return Ok(new { total = _cliques.ContarPorProduto(produtoId) });
    }

    [HttpGet]
    public IActionResult ContarTodos()
    {
        return Ok(_cliques.ContarTodos());
    }
}
