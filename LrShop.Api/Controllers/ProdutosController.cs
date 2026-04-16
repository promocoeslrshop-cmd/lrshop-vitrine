using LrShop.Infrastructure;
using LrShop.Shared;
using Microsoft.AspNetCore.Mvc;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/produtos")]
public class ProdutosController : ControllerBase
{
    private readonly ProdutoRepository _produtos;
    private readonly AnuncioService _anuncios;
    private readonly TelegramService _telegram;
    private readonly InstagramService _instagram;
    private readonly LogRepository _logs;
    private readonly ILogger<ProdutosController> _logger;
    private readonly CloudinaryStorageService _cloudinary;
    private readonly StoryVideoComposerService _storyComposer;

    public ProdutosController(
        ProdutoRepository produtos,
        AnuncioService anuncios,
        TelegramService telegram,
        InstagramService instagram,
        LogRepository logs,
        ILogger<ProdutosController> logger,
        CloudinaryStorageService cloudinary,
        StoryVideoComposerService storyComposer)
    {
        _produtos = produtos;
        _anuncios = anuncios;
        _telegram = telegram;
        _instagram = instagram;
        _logs = logs;
        _logger = logger;
        _cloudinary = cloudinary;
        _storyComposer = storyComposer;

    }

    [HttpGet]
    public IActionResult Listar()
    {
        return Ok(_produtos.ListarComStats());
    }

    [HttpGet("ativos/quantidade")]
    public IActionResult QuantidadeAtivos()
    {
        return Ok(new { total = _produtos.ContarAtivos() });
    }

    [HttpGet("{id}")]
    public IActionResult ObterPorId(int id)
    {
        var produto = _produtos.ObterPorId(id);

        if (produto == null)
            return NotFound(new { success = false, message = "Produto não encontrado." });

        return Ok(produto);
    }

    [HttpPost]
    public async Task<ActionResult<object>> Criar([FromBody] Produto produto, CancellationToken cancellationToken)
    {
        produto.CriadoEm = DateTime.UtcNow;

        // Cadastro pelo painel é sempre manual
        produto.Fonte = "Manual";

        if (!string.IsNullOrWhiteSpace(produto.ImagemUrl) &&
            !produto.ImagemUrl.Contains("cloudinary", StringComparison.OrdinalIgnoreCase))
        {
            var novaImagem = await _cloudinary.ProcessarImagemAsync(produto.ImagemUrl);

            if (!string.IsNullOrWhiteSpace(novaImagem))
                produto.ImagemUrl = novaImagem;
        }

        if (!string.IsNullOrWhiteSpace(produto.VideoUrl) &&
            !produto.VideoUrl.Contains("cloudinary", StringComparison.OrdinalIgnoreCase))
        {
            var novoVideo = await _cloudinary.ProcessarVideoAsync(produto.VideoUrl);

            if (!string.IsNullOrWhiteSpace(novoVideo))
                produto.VideoUrl = novoVideo;
        }

        var id = _produtos.Criar(produto);

        return Ok(new
        {
            success = true,
            id,
            fonte = produto.Fonte,
            imagemUrl = produto.ImagemUrl,
            videoUrl = produto.VideoUrl
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] Produto produto, CancellationToken cancellationToken)
    {
        produto.Id = id;

        var atual = _produtos.ObterPorId(id);
        if (atual == null)
            return NotFound(new { success = false, message = "Produto não encontrado." });

        // Mantém sempre a origem original do produto
        produto.Fonte = atual.Fonte;

        if (!string.IsNullOrWhiteSpace(produto.ImagemUrl) &&
            !produto.ImagemUrl.Contains("cloudinary", StringComparison.OrdinalIgnoreCase))
        {
            var novaImagem = await _cloudinary.ProcessarImagemAsync(produto.ImagemUrl);

            if (!string.IsNullOrWhiteSpace(novaImagem))
                produto.ImagemUrl = novaImagem;
        }

        if (!string.IsNullOrWhiteSpace(produto.VideoUrl) &&
            !produto.VideoUrl.Contains("cloudinary", StringComparison.OrdinalIgnoreCase))
        {
            var novoVideo = await _cloudinary.ProcessarVideoAsync(produto.VideoUrl);

            if (!string.IsNullOrWhiteSpace(novoVideo))
                produto.VideoUrl = novoVideo;
        }

        var ok = _produtos.Atualizar(produto);

        return Ok(new
        {
            success = ok,
            fonte = produto.Fonte,
            imagemUrl = produto.ImagemUrl,
            videoUrl = produto.VideoUrl
        });
    }

    [HttpPatch("{id}/status")]
    public IActionResult AlterarStatus(int id, [FromBody] StatusRequest request)
    {
        var ok = _produtos.AlterarStatus(id, request.Ativo);
        return Ok(new { success = ok });
    }

    [HttpDelete("{id}")]
    public IActionResult Excluir(int id)
    {
        var ok = _produtos.Excluir(id);
        return Ok(new { success = ok });
    }

    [HttpPost("{id}/postar")]
    public async Task<IActionResult> PostarAgora(int id, CancellationToken cancellationToken)
    {
        var produto = _produtos.ObterPorId(id);

        if (produto == null)
            return NotFound(new { success = false, message = "Produto não encontrado." });

        if (!produto.Ativo)
            return BadRequest(new { success = false, message = "Produto está inativo." });

        var mensagemTelegram = _anuncios.GerarTelegram(produto);
        var mensagemInstagram = _anuncios.GerarInstagram(produto);

        var okTelegram = await _telegram.EnviarAsync(mensagemTelegram, cancellationToken);

        _logs.Criar(new LogPostagem
        {
            ProdutoId = produto.Id,
            Canal = "telegram",
            Mensagem = mensagemTelegram,
            CriadoEm = DateTime.UtcNow,
            Sucesso = okTelegram
        });

        bool okInstagram = false;
        string? instagramPermalink = null;
        string? instagramErro = null;

        if (string.IsNullOrWhiteSpace(produto.ImagemUrl))
        {
            instagramErro = "Produto sem ImagemUrl.";

            _logs.Criar(new LogPostagem
            {
                ProdutoId = produto.Id,
                Canal = "instagram",
                Mensagem = mensagemInstagram,
                CriadoEm = DateTime.UtcNow,
                Sucesso = false
            });
        }
        else
        {
            var resultadoInstagram = await _instagram.EnviarAsync(
                produto.ImagemUrl,
                mensagemInstagram,
                cancellationToken
            );

            okInstagram = resultadoInstagram.ok;
            instagramPermalink = resultadoInstagram.permalink;
            instagramErro = resultadoInstagram.erro;

            _logs.Criar(new LogPostagem
            {
                ProdutoId = produto.Id,
                Canal = "instagram",
                Mensagem = mensagemInstagram,
                CriadoEm = DateTime.UtcNow,
                Sucesso = okInstagram
            });
        }

        return Ok(new
        {
            success = okTelegram || okInstagram,
            telegram = okTelegram,
            instagram = okInstagram,
            instagramPermalink,
            instagramErro
        });
    }

    [HttpPost("{id}/postar-story")]
    public async Task<IActionResult> PostarStory(int id, CancellationToken cancellationToken)
    {
        var produto = _produtos.ObterPorId(id);

        if (produto == null)
            return NotFound(new { success = false, message = "Produto não encontrado." });

        if (!produto.Ativo)
            return BadRequest(new { success = false, message = "Produto está inativo." });

        if (string.IsNullOrWhiteSpace(produto.VideoUrl))
            return BadRequest(new { success = false, message = "Produto sem VideoUrl." });

        var videoFinal = await _storyComposer.GerarStoryComTextoAsync(produto, cancellationToken);

        if (string.IsNullOrWhiteSpace(videoFinal))
        {
            _logger.LogWarning("PostarStory: falha ao gerar vídeo final com texto para o produto {Id}", produto.Id);
            return BadRequest(new
            {
                success = false,
                message = "Falha ao gerar vídeo do Story com texto. Verifique os logs do StoryVideoComposerService."
            });
        }

        var resultado = await _instagram.EnviarStoryVideoAsync(videoFinal, cancellationToken);

        _logs.Criar(new LogPostagem
        {
            ProdutoId = produto.Id,
            Canal = "instagram_story",
            Mensagem = videoFinal,
            CriadoEm = DateTime.UtcNow,
            Sucesso = resultado.ok
        });

        if (!resultado.ok)
            return BadRequest(new { success = false, erro = resultado.erro });

        return Ok(new
        {
            success = true,
            id = resultado.id,
            videoFinal
        });
    }

    public class StatusRequest
    {
        public bool Ativo { get; set; }
    }
}