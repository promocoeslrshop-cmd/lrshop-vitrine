using Microsoft.AspNetCore.Mvc;
using LrShop.Infrastructure;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/shopee")]
public class ShopeeController : ControllerBase
{
    private readonly ShopeeApiService _shopeeApi;
    private readonly ShopeeAffiliateAutomationService _shopeeAutomation;

    public ShopeeController(
        ShopeeApiService shopeeApi,
        ShopeeAffiliateAutomationService shopeeAutomation)
    {
        _shopeeApi = shopeeApi;
        _shopeeAutomation = shopeeAutomation;
    }

    [HttpGet("teste-link")]
    public async Task<IActionResult> TesteLink(
        [FromQuery] string url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { ok = false, erro = "Informe a url." });

        var link = await _shopeeApi.GerarLinkAfiliadoAsync(url, cancellationToken);

        if (string.IsNullOrWhiteSpace(link))
            return BadRequest(new
            {
                ok = false,
                erro = "Não foi possível gerar link afiliado."
            });

        return Ok(new
        {
            ok = true,
            original = url,
            afiliado = link
        });
    }

    [HttpGet("login-manual")]
    public async Task<IActionResult> ShopeeLoginManual()
    {
        await _shopeeAutomation.AbrirLoginManualAsync();

        return Ok(new
        {
            success = true,
            message = "Login manual realizado. Sessão salva."
        });
    }

    [HttpGet("verificar-sessao")]
    public async Task<IActionResult> VerificarSessao()
    {
        var ativa = await _shopeeAutomation.VerificarSessaoAsync();

        return Ok(new
        {
            success = true,
            sessaoAtiva = ativa
        });
    }
}