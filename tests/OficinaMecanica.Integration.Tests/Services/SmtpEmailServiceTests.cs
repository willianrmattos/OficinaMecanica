using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OficinaMecanica.Infrastructure.Services;

namespace OficinaMecanica.Integration.Tests.Services;

public class SmtpEmailServiceTests
{
    [Fact]
    public async Task EnviarAsync_SemHostConfigurado_NaoTentaConectarNemLancaExcecao()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = ""
            })
            .Build();
        var service = new SmtpEmailService(configuration, NullLogger<SmtpEmailService>.Instance);

        var act = () => service.EnviarAsync("cliente@teste.com", "Assunto", "Corpo");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnviarAsync_FalhaDeConexao_NaoLancaExcecao()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = "host-invalido.oficinamecanica.local",
                ["Smtp:Port"] = "25"
            })
            .Build();
        var service = new SmtpEmailService(configuration, NullLogger<SmtpEmailService>.Instance);

        var act = () => service.EnviarAsync("cliente@teste.com", "Assunto", "Corpo");

        await act.Should().NotThrowAsync();
    }
}
