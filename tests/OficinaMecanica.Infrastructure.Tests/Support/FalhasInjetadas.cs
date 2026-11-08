using System.Data.Common;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Controle compartilhado das falhas que os testes de atomicidade injetam.
public sealed class FalhasInjetadas
{
    // Faz o command AplicarAprovacaoDoOrcamento lancar DEPOIS de o handler ter gravado (OS e estoque ja reservado no banco, dentro
    // da transacao da saga), a cada tentativa.
    public volatile bool FalharDepoisDoHandlerDeAprovacao;

    // Faz o commit da transacao da saga (a que ja traz a saga em AguardandoPagamento) falhar uma unica vez.
    public volatile bool FalharUmCommitDaSagaEmAguardandoPagamento;

    public int CommitsQueFalharam;
}

public sealed class FalhaDepoisDoHandlerDeAprovacaoBehavior : IPipelineBehavior<AplicarAprovacaoDoOrcamentoCommand, Unit>
{
    private readonly FalhasInjetadas _falhas;

    public FalhaDepoisDoHandlerDeAprovacaoBehavior(FalhasInjetadas falhas) => _falhas = falhas;

    public async Task<Unit> Handle(AplicarAprovacaoDoOrcamentoCommand request, RequestHandlerDelegate<Unit> next, CancellationToken cancellationToken)
    {
        var resposta = await next();

        if (_falhas.FalharDepoisDoHandlerDeAprovacao)
            throw new FalhaTecnicaSimuladaException();

        return resposta;
    }
}

// Derruba o commit da transacao da saga: nesse ponto OS, estoque, saga, prazo e as mensagens do outbox ja foram gravados dentro da
// transacao. O que o teste afirma e que o rollback leva tudo junto e que o retry nao duplica nenhum efeito.
public sealed class FalhaNoCommitDaSagaInterceptor : DbTransactionInterceptor
{
    private readonly FalhasInjetadas _falhas;

    public FalhaNoCommitDaSagaInterceptor(FalhasInjetadas falhas) => _falhas = falhas;

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        var trazASagaEmAguardandoPagamento = eventData.Context is { } contexto
            && contexto.ChangeTracker.Entries<OrdemDeServicoSagaState>().Any(e => e.Entity.CurrentState == "AguardandoPagamento");

        if (_falhas.FalharUmCommitDaSagaEmAguardandoPagamento && trazASagaEmAguardandoPagamento
            && Interlocked.Increment(ref _falhas.CommitsQueFalharam) == 1)
        {
            throw new FalhaTecnicaSimuladaException();
        }

        return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }
}
