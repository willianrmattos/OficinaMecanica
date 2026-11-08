using FluentAssertions;
using Microsoft.IdentityModel.Protocols;
using OficinaMecanica.Infrastructure.Services;

namespace OficinaMecanica.Integration.Tests.Services;

public class JsonWebKeySetRetrieverTests
{
    [Fact]
    public async Task GetConfigurationAsync_ComDocumentoValido_DeveRetornarJsonWebKeySet()
    {
        const string jwksJson = """
        {
            "keys": [
                {
                    "kty": "RSA",
                    "use": "sig",
                    "kid": "chave-teste",
                    "n": "sXchi2Rar",
                    "e": "AQAB"
                }
            ]
        }
        """;

        var retriever = new JsonWebKeySetRetriever();

        var jwks = await retriever.GetConfigurationAsync(
            "https://exemplo.com/jwks.json", new DocumentRetrieverFake(jwksJson), CancellationToken.None);

        jwks.Keys.Should().ContainSingle();
        jwks.Keys[0].Kid.Should().Be("chave-teste");
        jwks.Keys[0].Kty.Should().Be("RSA");
    }

    private class DocumentRetrieverFake(string documento) : IDocumentRetriever
    {
        public Task<string> GetDocumentAsync(string address, CancellationToken cancel) => Task.FromResult(documento);
    }
}
