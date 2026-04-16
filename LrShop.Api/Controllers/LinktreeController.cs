using Microsoft.AspNetCore.Mvc;
using LrShop.Infrastructure;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/linktree")]
public class LinktreeController : ControllerBase
{
    private readonly LinktreePanelService _service;
    private readonly LinktreeAutomationService _automation;

    public LinktreeController(
        LinktreePanelService service,
        LinktreeAutomationService automation)
    {
        _service = service;
        _automation = automation;
    }

    [HttpGet("pendentes")]
    public IActionResult Pendentes()
    {
        try
        {
            var itens = _service.ListarPendentes();
            return Ok(new { success = true, items = itens });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id}/copiar")]
    public IActionResult Copiar(int id)
    {
        try
        {
            var texto = _service.CopiarTextoLinktree(id);
            return Ok(new { success = true, texto });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id}/enviado")]
    public IActionResult MarcarEnviado(int id)
    {
        try
        {
            _service.MarcarComoEnviado(id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id}/pendente")]
    public IActionResult MarcarPendente(int id)
    {
        try
        {
            _service.MarcarComoPendente(id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("login-manual")]
    public async Task<IActionResult> LoginManual()
    {
        try
        {
            await _automation.AbrirLoginManualAsync();
            return Ok(new
            {
                success = true,
                message = "Janela aberta para login manual no Linktree."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("sessao")]
    public async Task<IActionResult> Sessao()
    {
        try
        {
            var logado = await _automation.VerificarSessaoAsync();
            return Ok(new { success = true, logado });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id}/auto")]
    public async Task<IActionResult> PostarAuto(int id)
    {
        try
        {
            await _service.PostarAutomatico(id);
            return Ok(new
            {
                success = true,
                message = "Produto enviado automaticamente ao Linktree."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}