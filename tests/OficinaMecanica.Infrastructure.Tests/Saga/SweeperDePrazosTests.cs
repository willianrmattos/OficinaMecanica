using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// Sweeper sem SQL: o repositorio de prazos e um fake em memoria e o relogio e controlado. A prova do claim com UPDLOCK e
// READPAST (duas instancias nunca recebem o mesmo prazo) esta nos testes Category=Infra, contra o SQL Server real.
public class SweeperDePrazosTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly PrazoRepositoryEmMemoria _prazos = new();
    private readonly PublicadorDeTeste _publicador = new();
    private readonly FakeTimeProvider _relogio = new(Agora);

    private SweeperDePrazos CriarSweeper(int lote = 50) =>
        new(_prazos, _publicador, _relogio, Options.Create(new SagaOptions { SweeperLote = lote }));

    private async Task<SagaPrazo> AgendarAsync(DateTimeOffset expiraEm, TipoPrazo tipo = TipoPrazo.Aprovacao)
    {
        var prazo = new SagaPrazo(Guid.NewGuid(), Guid.NewGuid(), tipo, expiraEm);
        await _prazos.AdicionarAsync(prazo);
        return prazo;
    }

    [Fact]
    public async Task ProcessarLote_PrazoVencido_DevePublicarPrazoExpiradoComIdDeterministicoEMarcarProcessado()
    {
        var prazo = await AgendarAsync(Agora.AddMinutes(-1), TipoPrazo.Pagamento);

        var processados = await CriarSweeper().ProcessarLoteAsync();

        processados.Should().Be(1);
        var evento = _publicador.Publicadas.Should().ContainSingle().Which.Should().BeOfType<PrazoExpirado>().Subject;
        evento.OrdemDeServicoId.Should().Be(prazo.CorrelationId);
        evento.PrazoId.Should().Be(prazo.PrazoId);
        evento.Tipo.Should().Be(TipoPrazo.Pagamento);
        evento.OcorridoEm.Should().Be(Agora);
        evento.MessageId.Should().Be(IdentificadorDeMensagem.Para(prazo.CorrelationId, nameof(PrazoExpirado), prazo.PrazoId));
        prazo.Processado.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessarLote_PrazoQueAindaNaoVenceu_NaoDevePublicarNada()
    {
        var prazo = await AgendarAsync(Agora.AddSeconds(1));

        var processados = await CriarSweeper().ProcessarLoteAsync();

        processados.Should().Be(0);
        _publicador.Publicadas.Should().BeEmpty();
        prazo.Processado.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessarLote_PrazoQueVenceExatamenteAgora_DevePublicar()
    {
        await AgendarAsync(Agora);

        (await CriarSweeper().ProcessarLoteAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ProcessarLote_DeveUsarOInstanteDoRelogioInjetado()
    {
        var prazo = await AgendarAsync(Agora.AddHours(48));
        var sweeper = CriarSweeper();

        (await sweeper.ProcessarLoteAsync()).Should().Be(0);
        _relogio.Advance(TimeSpan.FromHours(48));
        (await sweeper.ProcessarLoteAsync()).Should().Be(1);

        _prazos.Chamadas.Select(c => c.Agora).Should().Equal(Agora, Agora.AddHours(48));
        prazo.Processado.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessarLote_PrazoJaProcessado_NaoDevePublicarDeNovo()
    {
        await AgendarAsync(Agora.AddMinutes(-5));
        var sweeper = CriarSweeper();

        await sweeper.ProcessarLoteAsync();
        (await sweeper.ProcessarLoteAsync()).Should().Be(0);

        _publicador.Publicadas.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessarLote_MaisVencidosQueOLote_DeveRespeitarOLoteEComecarPelosMaisAntigos()
    {
        var maisNovo = await AgendarAsync(Agora.AddMinutes(-1));
        var maisAntigo = await AgendarAsync(Agora.AddMinutes(-30));
        var intermediario = await AgendarAsync(Agora.AddMinutes(-10));
        var sweeper = CriarSweeper(lote: 2);

        (await sweeper.ProcessarLoteAsync()).Should().Be(2);

        _publicador.Publicadas.OfType<PrazoExpirado>().Select(e => e.PrazoId).Should().Equal(maisAntigo.PrazoId, intermediario.PrazoId);
        maisNovo.Processado.Should().BeFalse();
        _prazos.Chamadas.Single().Lote.Should().Be(2);

        (await sweeper.ProcessarLoteAsync()).Should().Be(1);
        maisNovo.Processado.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessarLote_FalhaAoPublicar_DeveDesfazerAMarcaEPropagarParaOProximoCicloTentarDeNovo()
    {
        var prazo = await AgendarAsync(Agora.AddMinutes(-1));
        var sweeper = CriarSweeper();
        _publicador.FalharAoPublicar = new InvalidOperationException("outbox indisponivel");

        var falha = async () => await sweeper.ProcessarLoteAsync();

        await falha.Should().ThrowAsync<InvalidOperationException>();
        _prazos.Prazos.Single().Processado.Should().BeFalse("publicar e marcar comitam juntos: sem aviso, sem marca");

        _publicador.FalharAoPublicar = null;
        (await sweeper.ProcessarLoteAsync()).Should().Be(1);
        _publicador.Publicadas.OfType<PrazoExpirado>().Single().PrazoId.Should().Be(prazo.PrazoId);
    }

    [Fact]
    public async Task ProcessarLote_DuasInstanciasEmParalelo_DeveTratarCadaPrazoUmaSoVez()
    {
        for (var i = 0; i < 40; i++)
            await AgendarAsync(Agora.AddMinutes(-i - 1));

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => CriarSweeper(lote: 7).ProcessarLoteAsync())));

        resultados.Sum().Should().Be(28);
        _publicador.Publicadas.OfType<PrazoExpirado>().Select(e => e.PrazoId).Should().OnlyHaveUniqueItems();
    }
}
