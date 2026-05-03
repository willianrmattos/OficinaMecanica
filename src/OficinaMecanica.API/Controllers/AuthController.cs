using OficinaMecanica.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace OficinaMecanica.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthController(ITokenService tokenService, IConfiguration configuration)
    {
        _tokenService = tokenService;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var adminUser = _configuration["AdminCredentials:Usuario"];
        var adminPassword = _configuration["AdminCredentials:Senha"];

        if (request.Usuario != adminUser || request.Senha != adminPassword)
            return Unauthorized(new { Mensagem = "Usuário ou senha inválidos." });

        var token = _tokenService.GerarToken(request.Usuario, "Admin");

        return Ok(new LoginResponse(token, "Bearer", 60));
    }
}

public record LoginRequest(string Usuario, string Senha);
public record LoginResponse(string Token, string Tipo, int ExpiracaoMinutos);
