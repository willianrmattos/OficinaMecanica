using System.Linq.Expressions;
using MassTransit;
using OficinaMecanica.Contracts.Commands;
using OficinaMecanica.Contracts.Common;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Infrastructure.Saga.Activities;

namespace OficinaMecanica.Infrastructure.Saga;

// Orquestrador da OS: coordena Execucao (diagnostico e reparo) e Billing (orcamento e pagamento) por comandos e eventos, e
// compensa o que ja foi feito quando uma etapa falha. O desenho completo (cada transicao e cada compensacao) esta em
// docs/diagramas/saga-maquina-de-estados.puml e as decisoes em docs/adr/0003 e 0008; este arquivo e a traducao literal do diagrama.
//
// Regras que atravessam tudo:
// - cada passo com efeito colateral e uma activity (Saga/Activities) que chama UM command idempotente da Application no
//   mesmo escopo, DbContext e transacao do consumer: OS, estoque, saga e mensagens do outbox comitam juntos ou nao comitam;
// - evento sem transicao no estado atual (duplicado, fora de ordem ou tardio) e IGNORADO, nunca vai para _error nem muda
//   estado; evento que chega antes de a saga existir falha de proposito para o retry do endpoint tentar de novo;
// - falha de negocio chega como evento explicito; Fault<T> e so falha tecnica e tem compensacao conservadora por comando.
public sealed class OrdemDeServicoStateMachine : MassTransitStateMachine<OrdemDeServicoSagaState>
{
    public OrdemDeServicoStateMachine()
    {
        InstanceState(x => x.CurrentState);

        // As metricas de transicao saem de um observador que so le o estado antes e depois de cada evento: nenhum passo da
        // maquina muda por causa delas.
        ConnectEventObserver(new ObservadorDeMetricasDaSaga());

        DeclararEventos();
        DeclararFalhas();

        Initially(
            When(OrdemDeServicoAberta)
                .Activity(x => x.OfType<IniciarSagaActivity>())
                .TransitionTo(AguardandoDiagnostico));

        // Declarado ANTES dos estados para o log ser sempre o primeiro passo: em todo estado uma falha tecnica deixa o
        // sinal no log, e os estados que compensam acrescentam seus passos depois dele.
        DuringAny(
            When(FalhaAoSolicitarDiagnostico).Activity(x => x.OfType<LogarFalhaTecnicaActivity<SolicitarDiagnostico>>()),
            When(FalhaAoGerarOrcamento).Activity(x => x.OfType<LogarFalhaTecnicaActivity<GerarOrcamento>>()),
            When(FalhaAoSolicitarPagamento).Activity(x => x.OfType<LogarFalhaTecnicaActivity<SolicitarPagamento>>()),
            When(FalhaAoIniciarExecucao).Activity(x => x.OfType<LogarFalhaTecnicaActivity<IniciarExecucao>>()),
            When(FalhaAoEstornarPagamento).Activity(x => x.OfType<LogarFalhaTecnicaActivity<EstornarPagamento>>()),
            When(FalhaAoCancelarPagamento).Activity(x => x.OfType<LogarFalhaTecnicaActivity<CancelarPagamento>>()),
            When(FalhaAoCancelarOrcamento).Activity(x => x.OfType<LogarFalhaTecnicaActivity<CancelarOrcamento>>()),
            When(FalhaAoCancelarProducao).Activity(x => x.OfType<LogarFalhaTecnicaActivity<CancelarProducao>>()));

        DeclararDiagnostico();
        DeclararOrcamento();
        DeclararPagamento();
        DeclararExecucao();
        DeclararEstorno();
        DeclararDecisoesSemEfeito();
        IgnorarOQueNaoTemTransicao();
    }

    // Estados. Concluida, Cancelada e OrcamentoRecusado sao finais so no desenho: a linha da saga permanece (ver o state).
    public State AguardandoDiagnostico { get; private set; } = null!;
    public State EmDiagnostico { get; private set; } = null!;
    public State GerandoOrcamento { get; private set; } = null!;
    public State AguardandoAprovacao { get; private set; } = null!;
    public State AguardandoPagamento { get; private set; } = null!;
    public State CancelandoPagamento { get; private set; } = null!;
    public State EmExecucao { get; private set; } = null!;
    public State Estornando { get; private set; } = null!;
    public State EstornoPendenteManual { get; private set; } = null!;
    public State Concluida { get; private set; } = null!;
    public State Cancelada { get; private set; } = null!;
    public State OrcamentoRecusado { get; private set; } = null!;

    public Event<OrdemDeServicoAberta> OrdemDeServicoAberta { get; private set; } = null!;
    public Event<DiagnosticoIniciado> DiagnosticoIniciado { get; private set; } = null!;
    public Event<DiagnosticoConcluido> DiagnosticoConcluido { get; private set; } = null!;
    public Event<OrcamentoGerado> OrcamentoGerado { get; private set; } = null!;
    public Event<OrcamentoNaoGerado> OrcamentoNaoGerado { get; private set; } = null!;
    public Event<ClienteAprovouOrcamento> ClienteAprovouOrcamento { get; private set; } = null!;
    public Event<ClienteRecusouOrcamento> ClienteRecusouOrcamento { get; private set; } = null!;
    public Event<PagamentoSolicitado> PagamentoSolicitado { get; private set; } = null!;
    public Event<PagamentoAprovado> PagamentoAprovado { get; private set; } = null!;
    public Event<PagamentoCancelado> PagamentoCancelado { get; private set; } = null!;
    public Event<PagamentoNaoSolicitado> PagamentoNaoSolicitado { get; private set; } = null!;
    public Event<PagamentoEstornado> PagamentoEstornado { get; private set; } = null!;
    public Event<EstornoFalhou> EstornoFalhou { get; private set; } = null!;
    public Event<ExecucaoFinalizada> ExecucaoFinalizada { get; private set; } = null!;
    public Event<ExecucaoInviavel> ExecucaoInviavel { get; private set; } = null!;
    public Event<CancelamentoSolicitado> CancelamentoSolicitado { get; private set; } = null!;
    public Event<PrazoExpirado> PrazoExpirado { get; private set; } = null!;

    public Event<Fault<SolicitarDiagnostico>> FalhaAoSolicitarDiagnostico { get; private set; } = null!;
    public Event<Fault<GerarOrcamento>> FalhaAoGerarOrcamento { get; private set; } = null!;
    public Event<Fault<SolicitarPagamento>> FalhaAoSolicitarPagamento { get; private set; } = null!;
    public Event<Fault<IniciarExecucao>> FalhaAoIniciarExecucao { get; private set; } = null!;
    public Event<Fault<EstornarPagamento>> FalhaAoEstornarPagamento { get; private set; } = null!;
    public Event<Fault<CancelarPagamento>> FalhaAoCancelarPagamento { get; private set; } = null!;
    public Event<Fault<CancelarOrcamento>> FalhaAoCancelarOrcamento { get; private set; } = null!;
    public Event<Fault<CancelarProducao>> FalhaAoCancelarProducao { get; private set; } = null!;

    // Toda mensagem e correlacionada pelo id da OS, que e o CorrelationId da saga. So a abertura cria a instancia.
    private void DeclararEventos()
    {
        Event(() => OrdemDeServicoAberta, e => e
            .CorrelateById(contexto => contexto.Message.OrdemDeServicoId)
            .SelectId(contexto => contexto.Message.OrdemDeServicoId));

        Correlacionar(() => DiagnosticoIniciado, m => m.OrdemDeServicoId);
        Correlacionar(() => DiagnosticoConcluido, m => m.OrdemDeServicoId);
        Correlacionar(() => OrcamentoGerado, m => m.OrdemDeServicoId);
        Correlacionar(() => OrcamentoNaoGerado, m => m.OrdemDeServicoId);
        Correlacionar(() => ClienteAprovouOrcamento, m => m.OrdemDeServicoId);
        Correlacionar(() => ClienteRecusouOrcamento, m => m.OrdemDeServicoId);
        Correlacionar(() => PagamentoSolicitado, m => m.OrdemDeServicoId);
        Correlacionar(() => PagamentoAprovado, m => m.OrdemDeServicoId);
        Correlacionar(() => PagamentoCancelado, m => m.OrdemDeServicoId);
        Correlacionar(() => PagamentoNaoSolicitado, m => m.OrdemDeServicoId);
        Correlacionar(() => PagamentoEstornado, m => m.OrdemDeServicoId);
        Correlacionar(() => EstornoFalhou, m => m.OrdemDeServicoId);
        Correlacionar(() => ExecucaoFinalizada, m => m.OrdemDeServicoId);
        Correlacionar(() => ExecucaoInviavel, m => m.OrdemDeServicoId);
        Correlacionar(() => CancelamentoSolicitado, m => m.OrdemDeServicoId);
        Correlacionar(() => PrazoExpirado, m => m.OrdemDeServicoId);
    }

    private void DeclararFalhas()
    {
        Correlacionar(() => FalhaAoSolicitarDiagnostico, m => m.Message.OrdemDeServicoId);
        Correlacionar(() => FalhaAoGerarOrcamento, m => m.Message.OrdemDeServicoId);
        Correlacionar(() => FalhaAoSolicitarPagamento, m => m.Message.OrdemDeServicoId);
        Correlacionar(() => FalhaAoIniciarExecucao, m => m.Message.OrdemDeServicoId);
        Correlacionar(() => FalhaAoEstornarPagamento, m => m.Message.OrdemDeServicoId);
        Correlacionar(() => FalhaAoCancelarPagamento, m => m.Message.OrdemDeServicoId);
        Correlacionar(() => FalhaAoCancelarOrcamento, m => m.Message.OrdemDeServicoId);
        Correlacionar(() => FalhaAoCancelarProducao, m => m.Message.OrdemDeServicoId);
    }

    // OnMissingInstance(Fault): o evento chegou antes de a saga existir (a abertura ainda esta no outbox ou em retry). O
    // padrao do MassTransit seria descartar em silencio e a OS ficaria sem saga para sempre; falhando, o retry do
    // endpoint tenta de novo ate a abertura ser processada.
    private void Correlacionar<T>(Expression<Func<Event<T>>> evento, Func<T, Guid> ordemDeServicoId) where T : class =>
        Event(evento, e => e
            .CorrelateById(contexto => ordemDeServicoId(contexto.Message))
            .OnMissingInstance(instancia => instancia.Fault()));

    private void DeclararDiagnostico()
    {
        During(AguardandoDiagnostico,
            When(DiagnosticoIniciado)
                .Activity(x => x.OfInstanceType<RegistrarDiagnosticoIniciadoActivity>())
                .TransitionTo(EmDiagnostico),

            // A Execucao publica DiagnosticoIniciado e DiagnosticoConcluido em momentos diferentes e o broker nao
            // garante a ordem entre eles. Se a conclusao chegar primeiro, ignorar deixaria a saga presa em EmDiagnostico
            // depois do Iniciado atrasado; por isso a conclusao adianta o inicio e segue.
            When(DiagnosticoConcluido)
                .Activity(x => x.OfInstanceType<RegistrarDiagnosticoIniciadoActivity>())
                .Activity(x => x.OfType<SolicitarOrcamentoActivity>())
                .TransitionTo(GerandoOrcamento),

            When(FalhaAoSolicitarDiagnostico)
                .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.FalhaTecnicaNoDiagnostico))
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            When(CancelamentoSolicitado)
                .Then(DefinirMotivoDoStaff)
                .Activity(x => x.OfInstanceType<CancelarProducaoNaExecucaoActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada));

        During(EmDiagnostico,
            When(DiagnosticoConcluido)
                .Activity(x => x.OfType<SolicitarOrcamentoActivity>())
                .TransitionTo(GerandoOrcamento),

            When(CancelamentoSolicitado)
                .Then(DefinirMotivoDoStaff)
                .Activity(x => x.OfInstanceType<CancelarProducaoNaExecucaoActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada));
    }

    private void DeclararOrcamento()
    {
        During(GerandoOrcamento,
            When(OrcamentoGerado)
                .Activity(x => x.OfType<RegistrarOrcamentoGeradoActivity>())
                .TransitionTo(AguardandoAprovacao),

            When(OrcamentoNaoGerado)
                .Then(contexto => contexto.Saga.DefinirMotivo(contexto.Message.Motivo))
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            When(FalhaAoGerarOrcamento)
                .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.FalhaTecnicaNoOrcamento))
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            // O orcamento pode ja existir no Billing mesmo sem a saga o conhecer (OrcamentoId nulo): o Billing localiza
            // pela OS e, se ainda nao existe, registra o cancelamento previo.
            When(CancelamentoSolicitado)
                .Then(DefinirMotivoDoStaff)
                .Activity(x => x.OfInstanceType<CancelarProducaoNaExecucaoActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrcamentoNoBillingActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada));

        During(AguardandoAprovacao,
            When(ClienteAprovouOrcamento)
                .Activity(x => x.OfInstanceType<ReservarEstoqueActivity>())
                .IfElse(
                    ReservouOEstoque,
                    reservado => reservado
                        .Activity(x => x.OfInstanceType<AplicarAprovacaoDoOrcamentoActivity>())
                        .TransitionTo(AguardandoPagamento),
                    semEstoque => semEstoque
                        .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.EstoqueInsuficiente))
                        .Activity(x => x.OfInstanceType<CancelarOrcamentoNoBillingActivity>())
                        .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                        .TransitionTo(Cancelada)),

            When(ClienteRecusouOrcamento)
                .Then(contexto => contexto.Saga.DefinirMotivo(contexto.Message.Motivo))
                .Activity(x => x.OfInstanceType<CancelarOrcamentoPorRecusaDoClienteActivity>())
                .Activity(x => x.OfInstanceType<AplicarRecusaDoOrcamentoActivity>())
                .TransitionTo(OrcamentoRecusado),

            // So o prazo VIGENTE decide: o de um orcamento ja decidido (ou de outro tipo) e ignorado.
            When(PrazoExpirado, contexto => EhOPrazoVigente(contexto.Message, TipoPrazo.Aprovacao, contexto.Saga.PrazoAprovacaoId))
                .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.PrazoDeAprovacaoExpirado))
                .Activity(x => x.OfInstanceType<CancelarOrcamentoNoBillingActivity>())
                .Activity(x => x.OfInstanceType<AplicarRecusaDoOrcamentoActivity>())
                .TransitionTo(OrcamentoRecusado),

            When(CancelamentoSolicitado)
                .Then(DefinirMotivoDoStaff)
                .Activity(x => x.OfInstanceType<CancelarProducaoNaExecucaoActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrcamentoNoBillingActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada));
    }

    private void DeclararPagamento()
    {
        During(AguardandoPagamento,
            // Nao muda de estado: o prazo de pagamento ja nasceu quando o SolicitarPagamento foi enviado.
            When(PagamentoSolicitado)
                .Activity(x => x.OfType<RegistrarPagamentoSolicitadoActivity>()),

            When(PagamentoAprovado)
                .Then(RegistrarPagamentoId)
                .Activity(x => x.OfInstanceType<IniciarExecucaoActivity>())
                .TransitionTo(EmExecucao),

            // Cancelar o pagamento passa pelo Billing, que e a autoridade: ele responde PagamentoCancelado ou, se o
            // dinheiro ja entrou, PagamentoAprovado. Nenhuma decisao automatica enquanto ele nao responder.
            When(PrazoExpirado, contexto => EhOPrazoVigente(contexto.Message, TipoPrazo.Pagamento, contexto.Saga.PrazoPagamentoId))
                .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.PrazoDePagamentoExpirado))
                .Activity(x => x.OfInstanceType<CancelarPagamentoNoBillingActivity>())
                .TransitionTo(CancelandoPagamento),

            When(PagamentoNaoSolicitado)
                .Then(contexto => contexto.Saga.DefinirMotivo(contexto.Message.Motivo))
                .Activity(x => x.OfInstanceType<LiberarEstoqueActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrcamentoNoBillingActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            When(FalhaAoSolicitarPagamento)
                .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.FalhaTecnicaNoPagamento))
                .Activity(x => x.OfInstanceType<LiberarEstoqueActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrcamentoNoBillingActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            // A producao tambem e desfeita: se um pagamento tardio chegar, a saga o estorna em vez de executar.
            When(CancelamentoSolicitado)
                .Then(contexto =>
                {
                    DefinirMotivoDoStaff(contexto);
                    contexto.Saga.CancelamentoPorStaff = true;
                })
                .Activity(x => x.OfInstanceType<CancelarPagamentoNoBillingActivity>())
                .Activity(x => x.OfInstanceType<CancelarProducaoNaExecucaoActivity>())
                .TransitionTo(CancelandoPagamento));

        During(CancelandoPagamento,
            When(PagamentoCancelado)
                .Activity(x => x.OfInstanceType<LiberarEstoqueActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            When(PagamentoNaoSolicitado)
                .Activity(x => x.OfInstanceType<LiberarEstoqueActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            When(FalhaAoSolicitarPagamento)
                .Activity(x => x.OfInstanceType<LiberarEstoqueActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            // Pagamento tardio: o Billing venceu a corrida do cancelamento. Quem pediu o cancelamento (staff) e a
            // producao ja foi desfeita, entao o dinheiro volta; se o cancelamento foi so por prazo, a OS segue como paga.
            When(PagamentoAprovado)
                .Then(RegistrarPagamentoId)
                .IfElse(
                    contexto => contexto.Saga.CancelamentoPorStaff,
                    pelaEquipe => pelaEquipe
                        .Activity(x => x.OfInstanceType<EstornarPagamentoActivity>())
                        .TransitionTo(Estornando),
                    peloPrazo => peloPrazo
                        .Then(contexto => contexto.Saga.MotivoCancelamento = null)
                        .Activity(x => x.OfInstanceType<IniciarExecucaoActivity>())
                        .TransitionTo(EmExecucao)),

            // O staff pediu o cancelamento depois de o prazo ja ter disparado o cancelamento do pagamento: so registra a
            // intencao e desfaz a producao; o CancelarPagamento ja esta a caminho.
            When(CancelamentoSolicitado)
                .Then(contexto =>
                {
                    DefinirMotivoDoStaff(contexto);
                    contexto.Saga.CancelamentoPorStaff = true;
                })
                .Activity(x => x.OfInstanceType<CancelarProducaoNaExecucaoActivity>()));
    }

    private void DeclararExecucao()
    {
        During(EmExecucao,
            When(ExecucaoFinalizada)
                .Activity(x => x.OfInstanceType<FinalizarOrdemActivity>())
                .TransitionTo(Concluida),

            When(ExecucaoInviavel)
                .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.ExecucaoInviavel(contexto.Message.Motivo)))
                .Activity(x => x.OfInstanceType<EstornarPagamentoActivity>())
                .TransitionTo(Estornando),

            // A Execucao nao recebeu o comando: nao ha reparo a esperar, entao o dinheiro volta.
            When(FalhaAoIniciarExecucao)
                .Then(contexto => contexto.Saga.DefinirMotivo(MotivosDaSaga.FalhaTecnicaNaExecucao))
                .Activity(x => x.OfInstanceType<EstornarPagamentoActivity>())
                .TransitionTo(Estornando));
    }

    private void DeclararEstorno()
    {
        During(Estornando,
            When(PagamentoEstornado)
                .Activity(x => x.OfInstanceType<LiberarEstoqueActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada),

            When(EstornoFalhou)
                .Activity(x => x.OfType<AlertarEstornoFalhouActivity>())
                .TransitionTo(EstornoPendenteManual),

            When(FalhaAoEstornarPagamento)
                .Activity(x => x.OfType<AlertarFalhaAoEstornarActivity>())
                .TransitionTo(EstornoPendenteManual));

        // O staff refaz o estorno na API do Billing (ou o reconciliador dele o refaz): o PagamentoEstornado resultante
        // e o que fecha a saga. Ela nunca vira Cancelada por conta propria enquanto o dinheiro nao voltou.
        During(EstornoPendenteManual,
            When(PagamentoEstornado)
                .Activity(x => x.OfInstanceType<LiberarEstoqueActivity>())
                .Activity(x => x.OfInstanceType<CancelarOrdemActivity>())
                .TransitionTo(Cancelada));
    }

    // Aprovar, recusar e cancelar sao decisoes de uma pessoa que a API aceita com 202 ("publicada para a saga"). Onde a saga
    // nao pode mais aplica-las (o prazo ja decidiu, o reparo comecou, a OS fechou) o evento segue ignorado, sem mudar nada,
    // mas deixa um aviso com o id da OS. Duplicata de qualquer outro evento continua ignorada em silencio. O aviso entra como
    // comportamento do estado (e nao dentro do DuringAny(Ignore)) porque o Ignore nao executa passo nenhum.
    private void DeclararDecisoesSemEfeito()
    {
        var semDecisaoDoCliente = TodosOsEstadosExceto(AguardandoAprovacao);
        During(semDecisaoDoCliente,
            When(ClienteAprovouOrcamento).Activity(x => x.OfType<LogarDecisaoIgnoradaActivity<ClienteAprovouOrcamento>>()),
            When(ClienteRecusouOrcamento).Activity(x => x.OfType<LogarDecisaoIgnoradaActivity<ClienteRecusouOrcamento>>()));

        var semCancelamentoDoStaff = TodosOsEstadosExceto(
            AguardandoDiagnostico, EmDiagnostico, GerandoOrcamento, AguardandoAprovacao, AguardandoPagamento, CancelandoPagamento);
        During(semCancelamentoDoStaff,
            When(CancelamentoSolicitado).Activity(x => x.OfType<LogarDecisaoIgnoradaActivity<CancelamentoSolicitado>>()));
    }

    private State[] TodosOsEstadosExceto(params State[] excecoes) =>
        States.Where(estado => !Equals(estado, Initial) && !Equals(estado, Final) && !excecoes.Contains(estado)).ToArray();

    // Todo evento que um estado nao trata acima e ignorado ali. O comportamento declarado tem prioridade sobre o
    // Ignore (o MassTransit so consulta os ignorados quando o estado nao tem comportamento para o evento), entao esta
    // lista nao esconde nenhuma transicao: cobre duplicatas, eventos fora de ordem e os que chegam depois de um estado
    // final (pagamento aprovado depois do cancelamento, estorno automatico do Billing, prazo ja decidido).
    // A maquina nao usa OnUnhandledEvent: um evento novo sem tratamento deve quebrar o teste de cobertura de eventos.
    private void IgnorarOQueNaoTemTransicao() =>
        DuringAny(
            Ignore(OrdemDeServicoAberta),
            Ignore(DiagnosticoIniciado),
            Ignore(DiagnosticoConcluido),
            Ignore(OrcamentoGerado),
            Ignore(OrcamentoNaoGerado),
            Ignore(ClienteAprovouOrcamento),
            Ignore(ClienteRecusouOrcamento),
            Ignore(PagamentoSolicitado),
            Ignore(PagamentoAprovado),
            Ignore(PagamentoCancelado),
            Ignore(PagamentoNaoSolicitado),
            Ignore(PagamentoEstornado),
            Ignore(EstornoFalhou),
            Ignore(ExecucaoFinalizada),
            Ignore(ExecucaoInviavel),
            Ignore(CancelamentoSolicitado),
            Ignore(PrazoExpirado));

    private static bool ReservouOEstoque(BehaviorContext<OrdemDeServicoSagaState, ClienteAprovouOrcamento> contexto) =>
        contexto.TryGetPayload<ReservaDeEstoqueDoEvento>(out var reserva) && reserva.Conseguiu;

    private static bool EhOPrazoVigente(PrazoExpirado prazo, TipoPrazo tipo, Guid? prazoVigente) =>
        prazo.Tipo == tipo && prazoVigente is { } vigente && prazo.PrazoId == vigente;

    private static void DefinirMotivoDoStaff(BehaviorContext<OrdemDeServicoSagaState, CancelamentoSolicitado> contexto) =>
        contexto.Saga.DefinirMotivo(MotivosDaSaga.CancelamentoDoStaff(contexto.Message.Motivo));

    // O Billing responde com o PagamentoId mesmo quando o PagamentoSolicitado se perdeu ou atrasou: e ele, e nao o
    // evento anterior, que a saga precisa para estornar.
    private static void RegistrarPagamentoId(BehaviorContext<OrdemDeServicoSagaState, PagamentoAprovado> contexto) =>
        contexto.Saga.PagamentoId = contexto.Message.PagamentoId;
}
