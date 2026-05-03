using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ComCredenciaisValidas_DeveRetornarToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Usuario = "admin", Senha = "Admin@123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("token");
    }

    [Fact]
    public async Task Login_ComCredenciaisInvalidas_DeveRetornar401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Usuario = "admin", Senha = "errada" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
