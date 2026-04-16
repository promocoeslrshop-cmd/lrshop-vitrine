using Microsoft.AspNetCore.Mvc;
using LrShop.Infrastructure;
using LrShop.Shared;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/teste")]
public class TesteController : ControllerBase
{
    private readonly ProdutoRepository _produtos;
    private readonly AnuncioService _anuncios;
    private readonly TelegramService _telegram;
    private readonly LogRepository _logs;
    private readonly WhatsappService _whatsapp;
    private static bool _whatsappIniciado = false;

    public TesteController(
        ProdutoRepository produtos,
        AnuncioService anuncios,
        TelegramService telegram,
        LogRepository logs,
        WhatsappService whatsapp)
    {
        _produtos = produtos;
        _anuncios = anuncios;
        _telegram = telegram;
        _logs = logs;
        _whatsapp = whatsapp;
    }

    [HttpPost("telegram")]
    public async Task<IActionResult> PostarTeste(CancellationToken cancellationToken)
    {
        var produto = _produtos.ObterPrioritario(0);

        if (produto is null)
            return BadRequest(new { success = false, message = "Nenhum produto ativo cadastrado." });

        var mensagem = _anuncios.GerarTelegram(produto);
        var ok = await _telegram.EnviarAsync(mensagem, cancellationToken);

        _logs.Criar(new LogPostagem
        {
            ProdutoId = produto.Id,
            Mensagem = mensagem,
            Sucesso = ok,
            Canal = "telegram",
            CriadoEm = DateTime.UtcNow
        });

        return Ok(new { success = ok, mensagem });
    }

    [HttpGet("crawler/status")]
    public IActionResult CrawlerStatus([FromServices] CrawlerStateService crawlerState)
    {
        return Ok(new { success = true, ativo = crawlerState.EstaAtivo() });
    }

    [HttpPost("crawler/toggle")]
    public IActionResult ToggleCrawler([FromServices] CrawlerStateService crawlerState)
    {
        var ativo = crawlerState.Alternar();
        return Ok(new { success = true, ativo });
    }

    [HttpGet("whatsapp-grupo")]
    public IActionResult PostarTesteWhatsappGrupo()
    {
        var produto = _produtos.ObterPrioritario(0);

        if (produto is null)
            return BadRequest(new { success = false, message = "Nenhum produto ativo cadastrado." });

        var mensagemWhatsappLink = _anuncios.GerarWhatsappLink(produto);

        try
        {
            if (!_whatsappIniciado)
            {
                _whatsapp.Iniciar();
                _whatsappIniciado = true;
            }

            var ok = _whatsapp.EnviarMensagemGrupo("Promoções LR SHOP", mensagemWhatsappLink);

            _logs.Criar(new LogPostagem
            {
                ProdutoId = produto.Id,
                Mensagem = mensagemWhatsappLink,
                Sucesso = ok,
                Canal = "whatsapp_grupo",
                CriadoEm = DateTime.UtcNow
            });

            return Ok(new
            {
                success = ok,
                message = ok
                    ? "Mensagem enviada com sucesso para o grupo."
                    : "Fluxo iniciado, mas o envio não foi confirmado.",
                grupo = "Promoções LR SHOP",
                mensagem = mensagemWhatsappLink
            });
        }
        catch (Exception ex)
        {
            _whatsappIniciado = false;

            _logs.Criar(new LogPostagem
            {
                ProdutoId = produto.Id,
                Mensagem = ex.Message,
                Sucesso = false,
                Canal = "whatsapp_grupo",
                CriadoEm = DateTime.UtcNow
            });

            return BadRequest(new
            {
                success = false,
                message = "Erro ao enviar para o grupo do WhatsApp.",
                erro = ex.Message
            });
        }
    }
}