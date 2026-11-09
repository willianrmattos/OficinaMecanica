using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Saga;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

// O broker entrega "pelo menos uma vez" e sem ordem entre servicos: evento duplicado, fora de ordem ou tardio e rotina. A
// regra do puml e que evento sem transicao no estado atual e IGNORADO (nao vai para _error, nao muda estado, nao envia nada).
public class SagaEventosIgnoradosTests
{
    private static readonly string[] TodosOsEventos =
    {
        nameof(OrdemDeServicoAberta), nameof(DiagnosticoIniciado), nameof(DiagnosticoConcluido), nameof(OrcamentoGerado),
        nameof(OrcamentoNaoGerado), nameof(ClienteAprovouOrcamento), nameof(ClienteRecusouOrcamento), nameof(PagamentoSolicitado),
        nameof(PagamentoAprovado), nameof(PagamentoCancelado), nameof(PagamentoNaoSolicitado), nameof(PagamentoEstornado),
        nameof(EstornoFalhou), nameof(ExecucaoFinalizada), nameof(ExecucaoInviavel), nameof(CancelamentoSolicitado), nameof(PrazoExpirado)
    };

    // Eventos com transicao (ou tratamento proprio) em cada estado, conforme docs/diagramas/saga-maquina-de-estados.puml. Todo o resto
    // tem que ser ignorado. PrazoExpirado aparece onde ha prazo a decidir: la o que e ignorado e o prazo que nao e o vigente.
    private static readonly Dictionary<string, string[]> EventosTratados = new()
    {
        ["AguardandoDiagnostico"] = new[] { nameof(DiagnosticoIniciado), nameof(DiagnosticoConcluido), nameof(CancelamentoSolicitado) },
        ["EmDiagnostico"] = new[] { nameof(DiagnosticoConcluido), nameof(CancelamentoSolicitado) },
        ["GerandoOrcamento"] = new[] { nameof(OrcamentoGerado), nameof(OrcamentoNaoGerado), nameof(CancelamentoSolicitado) },
        ["AguardandoAprovacao"] = new[] { nameof(ClienteAprovouOrcamento), nameof(ClienteRecusouOrcamento), nameof(CancelamentoSolicitado) },
        ["AguardandoPagamento"] = new[] { nameof(PagamentoSolicitado), nameof(PagamentoAprovado), nameof(PagamentoNaoSolicitado), nameof(CancelamentoSolicitado) },
        ["CancelandoPagamento"] = new[] { nameof(PagamentoCancelado), nameof(PagamentoNaoSolicitado), nameof(PagamentoAprovado), nameof(CancelamentoSolicitado) },
        ["EmExecucao"] = new[] { nameof(ExecucaoFinalizada), nameof(ExecucaoInviavel) },
        ["Estornando"] = new[] { nameof(PagamentoEstornado), nameof(EstornoFalhou) },
        ["EstornoPendenteManual"] = new[] { nameof(PagamentoEstornado) },
        ["Concluida"] = Array.Empty<string>(),
        ["Cancelada"] = Array.Empty<string>(),
        ["OrcamentoRecusado"] = Array.Empty<string>()
    };

    private static readonly Dictionary<string, Func<CenarioDaSaga, Task>> Chegar = new()
    {
        ["AguardandoDiagnostico"] = c => c.AbrirAsync(),
        ["EmDiagnostico"] = async c => { await c.AbrirAsync(); await c.IniciarDiagnosticoAsync(); },
        ["GerandoOrcamento"] = async c => { await c.AbrirAsync(); await c.IniciarDiagnosticoAsync(); await c.ConcluirDiagnosticoAsync(); },
        ["AguardandoAprovacao"] = async c => { await c.AbrirAsync(); await c.IniciarDiagnosticoAsync(); await c.ConcluirDiagnosticoAsync(); await c.GerarOrcamentoAsync(); },
        ["AguardandoPagamento"] = c => c.ChegarEmAguardandoPagamentoAsync(),
        ["CancelandoPagamento"] = c => c.ChegarEmCancelandoPagamentoAsync(),
        ["EmExecucao"] = c => c.ChegarEmExecucaoAsync(),
        ["Estornando"] = c => c.ChegarEmEstornandoAsync(),
        ["EstornoPendenteManual"] = c => c.ChegarEmEstornoPendenteManualAsync(),
        ["Concluida"] = c => c.ChegarEmConcluidaAsync(),
        ["Cancelada"] = c => c.ChegarEmCanceladaAsync(),
        ["OrcamentoRecusado"] = c => c.ChegarEmOrcamentoRecusadoAsync()
    };

    public static IEnumerable<object[]> Estados() => Chegar.Keys.Select(e => new object[] { e });

    private static Task PublicarIgnorado(CenarioDaSaga c, string evento)
    {
        var os = c.OrdemId;
        return evento switch
        {
            nameof(OrdemDeServicoAberta) => c.PublicarIgnoradoAsync(new OrdemDeServicoAbertaBuilder().DaOrdem(os).Build()),
            nameof(DiagnosticoIniciado) => c.PublicarIgnoradoAsync(new DiagnosticoIniciadoBuilder().DaOrdem(os).Build()),
            nameof(DiagnosticoConcluido) => c.PublicarIgnoradoAsync(new DiagnosticoConcluidoBuilder().DaOrdem(os).Build()),
            nameof(OrcamentoGerado) => c.PublicarIgnoradoAsync(new OrcamentoGeradoBuilder().DaOrdem(os).Build()),
            nameof(OrcamentoNaoGerado) => c.PublicarIgnoradoAsync(new OrcamentoNaoGeradoBuilder().DaOrdem(os).Build()),
            nameof(ClienteAprovouOrcamento) => c.PublicarIgnoradoAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(os).Build()),
            nameof(ClienteRecusouOrcamento) => c.PublicarIgnoradoAsync(new ClienteRecusouOrcamentoBuilder().DaOrdem(os).Build()),
            nameof(PagamentoSolicitado) => c.PublicarIgnoradoAsync(new PagamentoSolicitadoBuilder().DaOrdem(os).Build()),
            nameof(PagamentoAprovado) => c.PublicarIgnoradoAsync(new PagamentoAprovadoBuilder().DaOrdem(os).ComPagamento(c.PagamentoId).Build()),
            nameof(PagamentoCancelado) => c.PublicarIgnoradoAsync(new PagamentoCanceladoBuilder().DaOrdem(os).Build()),
            nameof(PagamentoNaoSolicitado) => c.PublicarIgnoradoAsync(new PagamentoNaoSolicitadoBuilder().DaOrdem(os).Build()),
            nameof(PagamentoEstornado) => c.PublicarIgnoradoAsync(new PagamentoEstornadoBuilder().DaOrdem(os).ComPagamento(c.PagamentoId).Build()),
            nameof(EstornoFalhou) => c.PublicarIgnoradoAsync(new EstornoFalhouBuilder().DaOrdem(os).ComPagamento(c.PagamentoId).Build()),
            nameof(ExecucaoFinalizada) => c.PublicarIgnoradoAsync(new ExecucaoFinalizadaBuilder().DaOrdem(os).Build()),
            nameof(ExecucaoInviavel) => c.PublicarIgnoradoAsync(new ExecucaoInviavelBuilder().DaOrdem(os).Build()),
            nameof(CancelamentoSolicitado) => c.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(os).Build()),
            nameof(PrazoExpirado) => c.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(os).DoTipo(TipoPrazo.Pagamento).Build()),
            _ => throw new ArgumentOutOfRangeException(nameof(evento), evento, null)
        };
    }

    [Theory]
    [MemberData(nameof(Estados))]
    public async Task Estado_EventoSemTransicao_DeveSerIgnoradoSemMudarNadaNemIrParaErro(string estado)
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar[estado](cenario);
        cenario.Saga().CurrentState.Should().Be(estado);

        foreach (var evento in TodosOsEventos.Where(e => !EventosTratados[estado].Contains(e)))
            await PublicarIgnorado(cenario, evento);

        cenario.Saga().CurrentState.Should().Be(estado);
    }

    [Fact]
    public async Task Maquina_DeveDeclararTodoEventoEmTodoEstadoParaNuncaLancarEventoNaoTratado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        var maquina = cenario.Maquina;
        var estados = new[]
        {
            maquina.AguardandoDiagnostico, maquina.EmDiagnostico, maquina.GerandoOrcamento, maquina.AguardandoAprovacao, maquina.AguardandoPagamento,
            maquina.CancelandoPagamento, maquina.EmExecucao, maquina.Estornando, maquina.EstornoPendenteManual, maquina.Concluida, maquina.Cancelada,
            maquina.OrcamentoRecusado
        };
        var eventos = new Event[]
        {
            maquina.OrdemDeServicoAberta, maquina.DiagnosticoIniciado, maquina.DiagnosticoConcluido, maquina.OrcamentoGerado, maquina.OrcamentoNaoGerado,
            maquina.ClienteAprovouOrcamento, maquina.ClienteRecusouOrcamento, maquina.PagamentoSolicitado, maquina.PagamentoAprovado,
            maquina.PagamentoCancelado, maquina.PagamentoNaoSolicitado, maquina.PagamentoEstornado, maquina.EstornoFalhou, maquina.ExecucaoFinalizada,
            maquina.ExecucaoInviavel, maquina.CancelamentoSolicitado, maquina.PrazoExpirado,
            maquina.FalhaAoSolicitarDiagnostico, maquina.FalhaAoGerarOrcamento, maquina.FalhaAoSolicitarPagamento, maquina.FalhaAoIniciarExecucao,
            maquina.FalhaAoEstornarPagamento, maquina.FalhaAoCancelarPagamento, maquina.FalhaAoCancelarOrcamento, maquina.FalhaAoCancelarProducao
        };

        foreach (var estado in estados)
            maquina.NextEvents(estado).Should().Contain(eventos, $"o estado {estado.Name} precisa tratar ou ignorar todo evento da saga");

        maquina.NextEvents(maquina.Initial).Should().Contain(maquina.OrdemDeServicoAberta);
        maquina.NextEvents(maquina.Initial).Should().NotContain(maquina.DiagnosticoIniciado, "sem instancia, o evento falha para o retry em vez de ser tratado");
    }

    private static bool TemAvisoDeDecisaoIgnorada(CenarioDaSaga cenario, string decisao, string estado) =>
        cenario.Ambiente.Logs.Registros.Any(r =>
            r.Nivel == LogLevel.Warning
            && r.Mensagem.StartsWith($"Decisão {decisao} da ordem de serviço {cenario.OrdemId} ignorada: a saga está em {estado} e a decisão não tem mais efeito, mensagem "));

    // Aprovar, recusar e cancelar sao decisoes de uma pessoa que a API ja respondeu com 202: ignorar sem rastro deixaria o
    // atendimento sem como explicar o que aconteceu com o pedido. O aviso nao pode mudar nada na saga nem na OS.
    [Theory]
    [InlineData("EmExecucao")]
    [InlineData("Estornando")]
    [InlineData("EstornoPendenteManual")]
    [InlineData("Concluida")]
    [InlineData("Cancelada")]
    [InlineData("OrcamentoRecusado")]
    public async Task CancelamentoSolicitado_OndeNaoTemMaisEfeito_DeveSerIgnoradoComAvisoComOIdDaOs(string estado)
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar[estado](cenario);

        await cenario.PublicarIgnoradoAsync(new CancelamentoSolicitadoBuilder().DaOrdem(cenario.OrdemId).Build());

        TemAvisoDeDecisaoIgnorada(cenario, nameof(CancelamentoSolicitado), estado).Should().BeTrue();
        cenario.Saga().CurrentState.Should().Be(estado);
    }

    [Theory]
    [InlineData("AguardandoDiagnostico")]
    [InlineData("EmDiagnostico")]
    [InlineData("GerandoOrcamento")]
    [InlineData("AguardandoPagamento")]
    [InlineData("CancelandoPagamento")]
    [InlineData("EmExecucao")]
    [InlineData("Estornando")]
    [InlineData("EstornoPendenteManual")]
    [InlineData("Concluida")]
    [InlineData("Cancelada")]
    [InlineData("OrcamentoRecusado")]
    public async Task AprovacaoERecusaDoCliente_ForaDeAguardandoAprovacao_DevemSerIgnoradasComAviso(string estado)
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar[estado](cenario);

        await cenario.PublicarIgnoradoAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.PublicarIgnoradoAsync(new ClienteRecusouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());

        TemAvisoDeDecisaoIgnorada(cenario, nameof(ClienteAprovouOrcamento), estado).Should().BeTrue();
        TemAvisoDeDecisaoIgnorada(cenario, nameof(ClienteRecusouOrcamento), estado).Should().BeTrue();
    }

    [Fact]
    public async Task AprovacaoDoClienteDepoisDoPrazo_DeveSerIgnoradaComAvisoENaoReservarEstoqueNemPedirPagamento()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar["AguardandoAprovacao"](cenario);
        var prazoId = cenario.Saga().PrazoAprovacaoId!.Value;
        await cenario.PublicarAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(prazoId).Build());
        await cenario.AguardarEstadoAsync(m => m.OrcamentoRecusado);

        await cenario.PublicarIgnoradoAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());

        TemAvisoDeDecisaoIgnorada(cenario, nameof(ClienteAprovouOrcamento), "OrcamentoRecusado").Should().BeTrue();
        cenario.EnviadosDoTipo<SolicitarPagamento>().Should().BeEmpty();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial);
    }

    // Contraprova dos testes acima: onde a decisao TEM transicao ou tratamento proprio, o aviso de "ignorada" nao pode aparecer
    // junto da acao real. Cobre os seis estados onde o cancelamento do staff tem efeito; tirar qualquer um da lista de
    // DeclararDecisoesSemEfeito faria o aviso rodar ali e este teste reprovar.
    [Theory]
    [InlineData("AguardandoDiagnostico", "Cancelada")]
    [InlineData("EmDiagnostico", "Cancelada")]
    [InlineData("GerandoOrcamento", "Cancelada")]
    [InlineData("AguardandoAprovacao", "Cancelada")]
    [InlineData("AguardandoPagamento", "CancelandoPagamento")]
    [InlineData("CancelandoPagamento", "CancelandoPagamento")]
    public async Task CancelamentoSolicitado_OndeTemEfeito_DeveAplicarSemAvisarDeDecisaoIgnorada(string estado, string estadoDepois)
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar[estado](cenario);
        cenario.EnviadosDoTipo<CancelarProducao>().Should().BeEmpty("o cancelamento do staff ainda nao aconteceu");

        await cenario.CancelarPeloStaffAsync();
        await cenario.AguardarEstadoAsync(m => m.States.Single(s => s.Name == estadoDepois));

        cenario.Saga().CurrentState.Should().Be(estadoDepois);
        cenario.Saga().CancelamentoPorStaff.Should().Be(estadoDepois == "CancelandoPagamento", "so o cancelamento durante o pagamento marca a intencao para um pagamento tardio");
        cenario.EnviadosDoTipo<CancelarProducao>().Should().ContainSingle("a decisao foi aplicada: a producao e desfeita");
        cenario.Ambiente.Logs.Registros.Should().NotContain(r => r.Mensagem.Contains("ignorada"), "a decisao teve efeito");
    }

    [Fact]
    public async Task ClienteAprovouOrcamento_EmAguardandoAprovacao_DeveAplicarSemAvisarDeDecisaoIgnorada()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar["AguardandoAprovacao"](cenario);

        await cenario.PublicarAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.AguardandoPagamento);

        cenario.EnviadosDoTipo<SolicitarPagamento>().Should().ContainSingle("a aprovacao foi aplicada");
        cenario.Ambiente.Logs.Registros.Should().NotContain(r => r.Mensagem.Contains("ignorada"), "a decisao teve efeito");
    }

    [Fact]
    public async Task ClienteRecusouOrcamento_EmAguardandoAprovacao_DeveAplicarSemAvisarDeDecisaoIgnorada()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar["AguardandoAprovacao"](cenario);

        await cenario.PublicarAsync(new ClienteRecusouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.OrcamentoRecusado);

        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().ContainSingle("a recusa foi aplicada");
        cenario.Ambiente.Logs.Registros.Should().NotContain(r => r.Mensagem.Contains("ignorada"), "a decisao teve efeito");
    }

    [Fact]
    public async Task EventoQueNaoEDecisaoDeUmaPessoa_DeveSerIgnoradoEmSilencio()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar["Cancelada"](cenario);

        await cenario.PublicarIgnoradoAsync(new PagamentoAprovadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.PublicarIgnoradoAsync(new PagamentoSolicitadoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.PublicarIgnoradoAsync(new ExecucaoFinalizadaBuilder().DaOrdem(cenario.OrdemId).Build());

        cenario.Ambiente.Logs.Registros.Should().NotContain(r => r.Mensagem.Contains("ignorada"));
    }

    [Fact]
    public async Task OrdemDeServicoAberta_DuplicadaComOutroMessageIdEmQualquerEstado_NaoDeveCriarNemEnviarSolicitarDiagnosticoDeNovo()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmExecucaoAsync();

        await cenario.PublicarIgnoradoAsync(new OrdemDeServicoAbertaBuilder().DaOrdem(cenario.OrdemId).Build());

        cenario.EnviadosDoTipo<SolicitarDiagnostico>().Should().ContainSingle();
        cenario.QuantidadeDeSagas().Should().Be(1);
    }

    [Fact]
    public async Task OrcamentoGerado_ReentregueComOMesmoMessageId_DeveSerIdempotenteEManterUmSoPrazo()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        var evento = new OrcamentoGeradoBuilder().DaOrdem(cenario.OrdemId).ComOrcamento(cenario.OrcamentoId).Build();
        await cenario.PublicarAsync(evento);
        await cenario.AguardarEstadoAsync(m => m.AguardandoAprovacao);

        await cenario.PublicarIgnoradoAsync(evento with { MessageId = Guid.NewGuid() });
        await cenario.PublicarIgnoradoAsync(evento);

        (await cenario.PrazosAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task PagamentoSolicitado_ReentregueNoMesmoPagamento_DeveSerIdempotenteSemSegundoEmail()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        var evento = new PagamentoSolicitadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build();
        await cenario.PublicarAsync(evento);
        var emails = cenario.Ambiente.Emails.Enviados.Count;
        var ordemAntes = await cenario.OrdemAsync();

        await cenario.PublicarAsync(evento with { MessageId = Guid.NewGuid() });

        cenario.Saga().CurrentState.Should().Be(cenario.Maquina.AguardandoPagamento.Name);
        cenario.Ambiente.Emails.Enviados.Count.Should().Be(emails, "o link nao e enviado de novo ao cliente");
        var ordem = await cenario.OrdemAsync();
        ordem.PagamentoId.Should().Be(ordemAntes.PagamentoId);
        ordem.LinkPagamento.Should().Be(ordemAntes.LinkPagamento);
    }

    [Fact]
    public async Task ClienteAprovouOrcamento_DuasVezes_DeveReservarEPedirPagamentoUmaSoVez()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();

        await cenario.PublicarIgnoradoAsync(new ClienteAprovouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());

        cenario.EnviadosDoTipo<SolicitarPagamento>().Should().ContainSingle();
        (await cenario.PecaAsync()).QuantidadeEstoque.Should().Be(CenarioDaSaga.EstoqueInicial - CenarioDaSaga.QuantidadeDaPeca);
    }

    [Fact]
    public async Task ClienteRecusouOrcamento_DepoisDaAprovacao_DeveSerIgnorado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();

        await cenario.PublicarIgnoradoAsync(new ClienteRecusouOrcamentoBuilder().DaOrdem(cenario.OrdemId).Build());

        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().BeEmpty();
    }

    [Theory]
    [InlineData("Cancelada")]
    [InlineData("CancelandoPagamento")]
    [InlineData("AguardandoPagamento")]
    public async Task EstornoAutomaticoDoBilling_ForaDeEstornando_DeveSerIgnorado(string estado)
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar[estado](cenario);

        await cenario.PublicarIgnoradoAsync(new PagamentoEstornadoBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
        await cenario.PublicarIgnoradoAsync(new EstornoFalhouBuilder().DaOrdem(cenario.OrdemId).ComPagamento(cenario.PagamentoId).Build());
    }

    [Fact]
    public async Task AguardandoAprovacao_PrazoExpiradoQueNaoEOVigente_DeveSerIgnorado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await Chegar["AguardandoAprovacao"](cenario);
        var vigente = cenario.Saga().PrazoAprovacaoId!.Value;

        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(Guid.NewGuid()).Build());
        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Pagamento).ComPrazo(vigente).Build());

        cenario.EnviadosDoTipo<CancelarOrcamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task AguardandoPagamento_PrazoDeAprovacaoAtrasadoOuPrazoDePagamentoQueNaoEOVigente_DeveSerIgnorado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();
        await cenario.IniciarDiagnosticoAsync();
        await cenario.ConcluirDiagnosticoAsync();
        await cenario.GerarOrcamentoAsync();
        var prazoDeAprovacao = cenario.Saga().PrazoAprovacaoId!.Value;
        await cenario.AprovarAsync();
        var prazoDePagamento = cenario.Saga().PrazoPagamentoId!.Value;

        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(prazoDeAprovacao).Build());
        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Pagamento).ComPrazo(Guid.NewGuid()).Build());
        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Aprovacao).ComPrazo(prazoDePagamento).Build());

        cenario.EnviadosDoTipo<CancelarPagamento>().Should().BeEmpty();
    }

    [Fact]
    public async Task EmExecucao_PrazoDePagamentoJaConcluido_DeveSerIgnorado()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.ChegarEmAguardandoPagamentoAsync();
        var prazoDePagamento = cenario.Saga().PrazoPagamentoId!.Value;
        await cenario.PagarAsync();

        await cenario.PublicarIgnoradoAsync(new PrazoExpiradoBuilder().DaOrdem(cenario.OrdemId).DoTipo(TipoPrazo.Pagamento).ComPrazo(prazoDePagamento).Build());

        cenario.EnviadosDoTipo<CancelarPagamento>().Should().BeEmpty("o pagamento ja foi aprovado: o timer atrasado nao cancela nada");
    }

    [Fact]
    public async Task AguardandoDiagnostico_DiagnosticoConcluidoAntesDoIniciado_DeveAdiantarOInicioESeguirParaOOrcamento()
    {
        await using var cenario = await CenarioDaSaga.CriarAsync();
        await cenario.AbrirAsync();

        await cenario.PublicarAsync(new DiagnosticoConcluidoBuilder().DaOrdem(cenario.OrdemId).Build());
        await cenario.AguardarEstadoAsync(m => m.GerandoOrcamento);

        (await cenario.OrdemAsync()).Status.Should().Be(OficinaMecanica.Domain.Enums.StatusOrdemDeServico.EmDiagnostico);
        cenario.EnviadosDoTipo<GerarOrcamento>().Should().ContainSingle();

        await cenario.PublicarIgnoradoAsync(new DiagnosticoIniciadoBuilder().DaOrdem(cenario.OrdemId).Build());
    }

    [Fact]
    public async Task EventoQueChegaAntesDaSaga_DeveFalharEReprocessarAteAAberturaCriarAInstancia()
    {
        // OnMissingInstance(Fault): descartar em silencio deixaria a OS sem andar para sempre. Falhando, o retry do endpoint
        // tenta de novo e o evento acaba aplicado depois que OrdemDeServicoAberta cria a saga.
        await using var cenario = await CenarioDaSaga.CriarAsync(
            configuracao: new (string, string?)[]
            {
                ("Mensageria:RetryIntervalosMs:0", "400"), ("Mensageria:RetryIntervalosMs:1", "400"), ("Mensageria:RetryIntervalosMs:2", "400"),
                ("Mensageria:RetryIntervalosMs:3", "400"), ("Mensageria:RetryIntervalosMs:4", "400")
            });
        var ordem = await cenario.CriarOrdemSemAbrirASagaAsync();
        await cenario.Ambiente.Harness.Bus.Publish(new DiagnosticoIniciadoBuilder().DaOrdem(ordem).Build());

        // A primeira tentativa acontece em milissegundos e falha (nao ha saga); a abertura chega antes da segunda.
        await Task.Delay(150);
        cenario.QuantidadeDeSagas().Should().Be(0);
        await cenario.Ambiente.Harness.Bus.Publish(new OrdemDeServicoAbertaBuilder().DaOrdem(ordem).Build());

        await cenario.AguardarEstadoAsync(m => m.EmDiagnostico);
        (await cenario.OrdemAsync()).Status.Should().Be(OficinaMecanica.Domain.Enums.StatusOrdemDeServico.EmDiagnostico);
    }
}
