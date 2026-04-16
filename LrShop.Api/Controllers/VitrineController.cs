using LrShop.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VitrineController : ControllerBase
{
    private readonly JsonExportService _jsonExportService;
    private readonly ILogger<VitrineController> _logger;

    public VitrineController(
        JsonExportService jsonExportService,
        ILogger<VitrineController> logger)
    {
        _jsonExportService = jsonExportService;
        _logger = logger;
    }

    [HttpPost("exportar-json")]
    public async Task<IActionResult> ExportarJson(CancellationToken cancellationToken)
    {
        var total = await _jsonExportService.GerarJsonAsync(cancellationToken);
        _logger.LogInformation("JSON exportado manualmente com {Total} produtos", total);

        return Ok(new
        {
            ok = true,
            total,
            arquivo = "/produtos.json"
        });
    }
}