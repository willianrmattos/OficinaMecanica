using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace OficinaMecanica.Integration.Tests;

public static class TestTokenFactory
{
    // Recebe a factory explicitamente (em vez de ler uma chave static compartilhada) pra assinar
    // sempre com a mesma chave RSA de instancia que o resolver daquela factory vai usar pra validar -
    // ver comentario em CustomWebApplicationFactory.TestSigningKey.
    public static string GerarToken(CustomWebApplicationFactory factory, string usuario = "admin", string perfil = "Admin")
    {
        var credenciais = new SigningCredentials(
            new RsaSecurityKey(factory.TestSigningKey) { KeyId = CustomWebApplicationFactory.TestKid },
            SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, usuario),
            new Claim(ClaimTypes.Role, perfil)
        };

        var token = new JwtSecurityToken(
            issuer: "OficinaMecanica.Seguranca",
            audience: "OficinaMecanica.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
