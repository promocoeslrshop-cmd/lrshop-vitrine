using Microsoft.AspNetCore.Mvc;
using LrShop.Infrastructure;
using LrShop.Shared;

namespace LrShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UsuarioRepository _usuarios;
    private readonly TokenService _tokens;

    public AuthController(UsuarioRepository usuarios, TokenService tokens)
    {
        _usuarios = usuarios;
        _tokens = tokens;
    }

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        var usuario = _usuarios.Autenticar(request.Email, request.Senha);
        if (usuario is null)
            return Unauthorized(new LoginResponse
            {
                Success = false,
                Message = "Login inválido ou expirado."
            });

        return Ok(new LoginResponse
        {
            Success = true,
            Token = _tokens.GerarToken(usuario.Email),
            Message = "Login efetuado com sucesso."
        });
    }
}