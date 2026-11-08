using MassTransit;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Contracts.Commands;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// O Billing nem chegou a responder: o EstornarPagamento falhou tecnicamente (Fault<EstornarPagamento>) e a causa e a
// primeira excecao registrada nele.
public sealed class AlertarFalhaAoEstornarActivity : AlertarEstornoPendenteActivity<Fault<EstornarPagamento>>
{
    public AlertarFalhaAoEstornarActivity(ILogger<AlertarFalhaAoEstornarActivity> logger, TimeProvider relogio) : base(logger, relogio)
    {
    }

    protected override string? CausaDe(Fault<EstornarPagamento> mensagem) => mensagem.Exceptions?.FirstOrDefault()?.Message;
}
