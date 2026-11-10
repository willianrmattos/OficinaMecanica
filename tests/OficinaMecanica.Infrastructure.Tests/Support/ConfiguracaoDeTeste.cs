using Microsoft.Extensions.Configuration;

namespace OficinaMecanica.Infrastructure.Tests.Support;

public static class ConfiguracaoDeTeste
{
    // Connection string que nunca abre conexao: serve aos testes que so montam o container de DI.
    public const string ConnectionStringSemBanco = "Server=tcp:localhost,1;Database=NaoUsado;Trusted_Connection=True;Encrypt=False;Connection Timeout=1;";

    public static IConfiguration Criar(params (string Chave, string? Valor)[] valores)
    {
        var dados = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionStringSemBanco,
            ["JwtSettings:Issuer"] = "OficinaMecanica.Seguranca",
            ["JwtSettings:Audience"] = "OficinaMecanica.Client",
            ["JwtSettings:JwksUri"] = "https://localhost/.well-known/jwks.json",
            ["Mensageria:Transporte"] = "EmMemoria",
            ["Mensageria:Persistencia"] = "Nenhuma"
        };

        foreach (var (chave, valor) in valores)
            dados[chave] = valor;

        return new ConfigurationBuilder().AddInMemoryCollection(dados).Build();
    }
}
