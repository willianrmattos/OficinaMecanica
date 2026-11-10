using FluentAssertions;
using OficinaMecanica.Contracts;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// Um teste por transicao do caminho feliz do docs/diagramas/saga-maquina-de-estados.puml, cada um partindo do estado de verdade
// (alcancado pelos eventos anteriores) e afirmando o estado da saga, o status da OS, o estoque e os comandos enviados.
public class SagaCaminhoFelizTests
{
    [Fact]
    public async Task Inicio_OrdemAberta_DeveCriarASagaComOIdDaOsEPedirODiagnosticoComOSnapshot()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();

        await cenario.AbrirAsync();

        var saga = cenario.Saga();
        saga.CorrelationId.Should().Be(cenario.OrdemId);
        saga.CurrentState.Should().Be(cenario.Maquina.AguardandoDiagnostico.Name);
        saga.Numero.Should().Be((await cenario.OrdemAsync()).Numero);
        saga.CriadaEm.Should().Be(AmbienteDaSaga.Inicio);
        saga.AtualizadaEm.Should().Be(AmbienteDaSaga.Inicio);

        var comando = cenario.EnviadosDoTipo<SolicitarDiagnostico>().Should().ContainSingle().Subject;
        comando.OrdemDeServicoId.Should().Be(cenario.OrdemId);
        comando.Numero.Should().Be(saga.Numero);
        comando.Placa.Should().NotBeNullOrWhiteSpace();
        comando.Marca.Should().NotBeNullOrWhiteSpace();
        comando.Modelo.Should().NotBeNullOrWhiteSpace();
        comando.Observacoes.Should().Be("Barulho ao frear.");
        comando.OcorridoEm.Should().Be(AmbienteDaSaga.Inicio);
        comando.Servicos.Should().BeEquivalentTo(new[] { new ItemDeServicoMensagem(cenario.ServicoId, CenarioDaSaga.NomeDoServico, CenarioDaSaga.PrecoDoServico, 1) });
        comando.Pecas.Should().BeEquivalentTo(new[] { new ItemDePecaMensagem(cenario.PecaId, CenarioDaSaga.NomeDaPeca, CenarioDaSaga.PrecoDaPeca, CenarioDaSaga.QuantidadeDaPeca) });
        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.Recebida);
    }

    [Fact]
    public async Task AguardandoDiagnostico_DiagnosticoIniciado_DeveMoverAOsParaEmDiagnosticoSemEnviarComandos()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromMinutes(5));

        await cenario.IniciarDiagnosticoAsync();

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        cenario.Saga().AtualizadaEm.Should().Be(AmbienteDaSaga.Inicio.AddMinutes(5));
        cenario.TodosOsComandosEnviados().Should().ContainSingle().Which.Should().BeOfType<SolicitarDiagnostico>();
    }

    [Fact]
    public async Task EmDiagnostico_DiagnosticoConcluido_DeveEnviarGerarOrcamentoComOValorOsItensOLaudoEOPrazoDeAprovacao()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromHours(1));

        await cenario.PublicarAsync(new DiagnosticoConcluidoBuilder().DaOrdem(cenario.OrdemId).ComLaudo("Pastilhas gastas.").Build());
        await cenario.AguardarEstadoAsync(m => m.GerandoOrcamento);

        var comando = cenario.EnviadosDoTipo<GerarOrcamento>().Should().ContainSingle().Subject;
        comando.OrdemDeServicoId.Should().Be(cenario.OrdemId);
        comando.Numero.Should().Be(cenario.Saga().Numero);
        comando.ValorTotal.Should().Be(CenarioDaSaga.ValorTotalDaOs);
        comando.Laudo.Should().Be("Pastilhas gastas.");
        comando.OcorridoEm.Should().Be(AmbienteDaSaga.Inicio.AddHours(1));
        comando.ValidoAte.Should().Be(AmbienteDaSaga.Inicio.AddHours(1 + 72));
        comando.Servicos.Should().ContainSingle().Which.Nome.Should().Be(CenarioDaSaga.NomeDoServico);
        comando.Pecas.Should().ContainSingle().Which.Quantidade.Should().Be(CenarioDaSaga.QuantidadeDaPeca);
        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
    }

    [Fact]
    public async Task GerandoOrcamento_OrcamentoGerado_DeveMoverAOsParaAguardandoAprovacaoGuardarOOrcamentoECriarOPrazoDeAprovacao()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        var validoAte = AmbienteDaSaga.Inicio.AddDays(5).AddHours(7);

        await cenario.PublicarAsync(new OrcamentoGeradoBuilder().DaOrdem(cenario.OrdemId).ComOrcamento(cenario.OrcamentoId).ValidoAte(validoAte).Build());
        await cenario.AguardarEstadoAsync(m => m.AguardandoAprovacao);

        var saga = cenario.Saga();
        saga.OrcamentoId.Should().Be(cenario.OrcamentoId);
        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.AguardandoAprovacao);

        var prazo = (await cenario.PrazosAsync()).Should().ContainSingle().Subject;
        prazo.Tipo.Should().Be(TipoPrazo.Aprovacao);
        prazo.ExpiraEm.Should().Be(validoAte, "o prazo de aprovacao e o ValidoAte que o Billing confirmou, nao agora + o prazo configurado");
        prazo.Processado.Should().BeFalse();
        prazo.CorrelationId.Should().Be(cenario.OrdemId);
        saga.PrazoAprovacaoId.Should().Be(prazo.PrazoId);
        saga.PrazoPagamentoId.Should().BeNull();
    }

    [Fact]
    public async Task AguardandoAprovacao_ClienteAprovou_DeveReservarOEstoqueCriarOPrazoDePagamentoEPedirOPagamento()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromHours(2));

        await cenario.AprovarAsync();

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        ordem.EstoqueReservado.Should().BeTrue();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial - CenarioDaSaga.QuantidadeDaPeca);

        var prazoDePagamento = (await cenario.PrazosAsync()).Single(p => p.Tipo == TipoPrazo.Pagamento);
        prazoDePagamento.ExpiraEm.Should().Be(AmbienteDaSaga.Inicio.AddHours(2 + 48));
        prazoDePagamento.Processado.Should().BeFalse();

        var saga = cenario.Saga();
        saga.PrazoPagamentoId.Should().Be(prazoDePagamento.PrazoId);
        saga.PrazoAprovacaoId.Should().BeNull("o prazo de aprovacao deixou de ser o vigente");

        var comando = cenario.EnviadosDoTipo<SolicitarPagamento>().Should().ContainSingle().Subject;
        comando.OrcamentoId.Should().Be(cenario.OrcamentoId);
        comando.ExpiraEm.Should().Be(prazoDePagamento.ExpiraEm);
        comando.OrdemDeServicoId.Should().Be(cenario.OrdemId);
    }

    [Fact]
    public async Task AguardandoPagamento_PagamentoSolicitado_DeveGravarOLinkNaOsGuardarOPagamentoEPermanecerNoEstado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        var expiraEm = AmbienteDaSaga.Inicio.AddHours(48);

        await cenario.PublicarAsync(new PagamentoSolicitadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId)
            .ComLink("https://pagamento.exemplo.com/checkout/xyz").ExpiraEm(expiraEm).Build());

        cenario.Saga().CurrentState.Should().Be(cenario.Maquina.AguardandoPagamento.Name);
        cenario.Saga().PagamentoId.Should().Be(cenario.PagamentoId);
        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.AguardandoPagamento);
        ordem.PagamentoId.Should().Be(cenario.PagamentoId);
        ordem.LinkPagamento.Should().Be("https://pagamento.exemplo.com/checkout/xyz");
        ordem.PagamentoExpiraEm.Should().Be(expiraEm);
        cenario.Ambiente.Emails.Enviados.Should().Contain(e => e.Corpo.Contains("https://pagamento.exemplo.com/checkout/xyz"),
            "o link de pagamento vai ao cliente por e-mail");
    }

    [Fact]
    public async Task AguardandoPagamento_PagamentoAprovado_DeveIniciarAExecucaoComOSnapshotAprovado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        await cenario.SolicitarPagamentoAsync();
        cenario.Ambiente.Relogio.Advance(TimeSpan.FromHours(3));

        await cenario.PagarAsync();

        (await cenario.OrdemAsync()).Status.Should().Be(StatusOrdemDeServico.EmExecucao);
        var saga = cenario.Saga();
        saga.PrazoPagamentoId.Should().BeNull("o pagamento foi concluido: o prazo deixou de ser o vigente");

        var comando = cenario.EnviadosDoTipo<IniciarExecucao>().Should().ContainSingle().Subject;
        comando.OrdemDeServicoId.Should().Be(cenario.OrdemId);
        comando.Numero.Should().Be(saga.Numero);
        comando.OcorridoEm.Should().Be(AmbienteDaSaga.Inicio.AddHours(3));
        comando.Servicos.Should().ContainSingle().Which.ServicoId.Should().Be(cenario.ServicoId);
        comando.Pecas.Should().ContainSingle().Which.Should().Be(new ItemDePecaMensagem(cenario.PecaId, CenarioDaSaga.NomeDaPeca, CenarioDaSaga.PrecoDaPeca, CenarioDaSaga.QuantidadeDaPeca));
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial - CenarioDaSaga.QuantidadeDaPeca, "o estoque continua reservado durante a execucao");
    }

    [Fact]
    public async Task PagamentoAprovado_SemPagamentoSolicitadoAntes_DeveGuardarOPagamentoIdDoProprioEvento()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();

        await cenario.PagarAsync();

        cenario.Saga().PagamentoId.Should().Be(cenario.PagamentoId, "o Billing e a autoridade sobre o id do pagamento");
    }

    [Fact]
    public async Task EmExecucao_ExecucaoFinalizada_DeveFinalizarAOsEConcluirASaga()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();

        await cenario.PublicarAsync(new ExecucaoFinalizadaBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.Concluida);

        var ordem = await cenario.OrdemAsync();
        ordem.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        ordem.DataConclusao.Should().NotBeNull();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial - CenarioDaSaga.QuantidadeDaPeca, "a peca foi consumida no reparo");
    }

    [Fact]
    public async Task FluxoCompleto_DeveTerUmaSagaPorOsComTodosOsComandosNasFilasDosDonos()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();
        await cenario.PublicarAsync(new ExecucaoFinalizadaBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.Concluida);

        cenario.TodosOsComandosEnviados().Select(c => c.GetType().Name)
            .Should().Equal(nameof(SolicitarDiagnostico), nameof(GerarOrcamento), nameof(SolicitarPagamento), nameof(IniciarExecucao));
        cenario.EnderecosDosComandos().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [nameof(SolicitarDiagnostico)] = Filas.Execucao,
            [nameof(GerarOrcamento)] = Filas.Billing,
            [nameof(SolicitarPagamento)] = Filas.Billing,
            [nameof(IniciarExecucao)] = Filas.Execucao
        });
        cenario.QuantidadeDeSagas().Should().Be(1);
    }
}
