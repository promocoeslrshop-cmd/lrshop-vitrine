using Microsoft.AspNetCore.Mvc;
using LrShop.Infrastructure;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly LogRepository _logs;
    public LogsController(LogRepository logs) => _logs = logs;

    [HttpGet]
    public IActionResult ListarRecentes() => Ok(_logs.ListarRecentes());
}
