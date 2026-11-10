using FluentAssertions;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// O inbox do destino deduplica por MessageId: se a saga refizesse um envio com id novo, a reentrega executaria o comando duas
// vezes. Por isso todo comando sai com um id derivado da OS, do nome do comando e, quando faz sentido, de um discriminador, e o
// MessageId do contrato e o do envelope.
public class SagaIdentificadoresDosComandosTests
{
    private static void DeveTerIdDeterministico<T>(CenarioDaSaga cenario, Func<T, Guid> idDoContrato, string nome, Guid? discriminador = null) where T : class
    {
        var (comando, envelope) = cenario.EnviadosComEnvelope<T>().Should().ContainSingle().Subject;
        var esperado = IdentificadorDeMensagem.Para(cenario.OrdemId, nome, discriminador);

        idDoContrato(comando).Should().Be(esperado, $"{nome} deve sair com o id derivado da OS");
        envelope.Should().Be(esperado, "o MessageId do contrato vira o do envelope, que e o que o inbox do destino usa");
    }

    [Fact]
    public async Task CaminhoFeliz_DeveEnviarOsQuatroComandosComIdDerivadoDaOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        var prazoDePagamento = cenario.Saga().PrazoPagamentoId;
        await cenario.SolicitarPagamentoAsync();
        await cenario.PagarAsync();

        DeveTerIdDeterministico<SolicitarDiagnostico>(cenario, c => c.MessageId, nameof(SolicitarDiagnostico));
        DeveTerIdDeterministico<GerarOrcamento>(cenario, c => c.MessageId, nameof(GerarOrcamento));
        DeveTerIdDeterministico<SolicitarPagamento>(cenario, c => c.MessageId, nameof(SolicitarPagamento), prazoDePagamento);
        DeveTerIdDeterministico<IniciarExecucao>(cenario, c => c.MessageId, nameof(IniciarExecucao));
    }

    [Fact]
    public async Task CancelamentoDoStaffEmAguardandoPagamento_DeveEnviarCancelarPagamentoComOPrazoComoDiscriminadorECancelarProducao()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        var prazoDePagamento = cenario.Saga().PrazoPagamentoId;
        await cenario.CancelarPeloStaffAsync();
        await cenario.AguardarEstadoAsync(m => m.CancelandoPagamento);

        DeveTerIdDeterministico<CancelarPagamento>(cenario, c => c.MessageId, nameof(CancelarPagamento), prazoDePagamento);
        DeveTerIdDeterministico<CancelarProducao>(cenario, c => c.MessageId, nameof(CancelarProducao));
    }

    [Fact]
    public async Task CancelamentoPorPrazoDePagamento_DeveEnviarCancelarPagamentoComOPrazoComoDiscriminador()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        var prazoDePagamento = cenario.Saga().PrazoPagamentoId;
        await cenario.ExpirarPagamentoAsync();

        DeveTerIdDeterministico<CancelarPagamento>(cenario, c => c.MessageId, nameof(CancelarPagamento), prazoDePagamento);
    }

    [Fact]
    public async Task Recusa_DeveEnviarCancelarOrcamentoComIdDerivadoDaOs()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmOrcamentoRecusadoAsync();

        DeveTerIdDeterministico<CancelarOrcamento>(cenario, c => c.MessageId, nameof(CancelarOrcamento));
    }

    [Fact]
    public async Task ExecucaoInviavel_DeveEnviarEstornarPagamentoComOPagamentoComoDiscriminador()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmEstornandoAsync();

        DeveTerIdDeterministico<EstornarPagamento>(cenario, c => c.MessageId, nameof(EstornarPagamento), cenario.PagamentoId);
    }

    [Fact]
    public async Task OsDiferentes_DevemEnviarComandosComIdsDiferentes()
    {
        await using var primeira = await CenarioDaSaga.CriarAsync();
        await using var segunda = await CenarioDaSaga.CriarAsync();
        await primeira.AbrirAsync();
        await segunda.AbrirAsync();

        primeira.EnviadosDoTipo<SolicitarDiagnostico>().Single().MessageId
            .Should().NotBe(segunda.EnviadosDoTipo<SolicitarDiagnostico>().Single().MessageId);
    }

    [Fact]
    public async Task OcorridoEm_DeveVirDoRelogioInjetadoEnaoDoRelogioDoSistema()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromDays(400));
        await cenario.AbrirAsync();

        cenario.EnviadosDoTipo<SolicitarDiagnostico>().Single().OcorridoEm.Should().Be(AmbienteDaSaga.Inicio.AddDays(400));
    }
}
