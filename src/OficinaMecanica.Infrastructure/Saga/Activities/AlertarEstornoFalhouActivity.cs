using Microsoft.Extensions.Logging;
using OficinaMecanica.Contracts.Events;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// O Billing respondeu que o estorno falhou (EstornoFalhou): a causa e o texto que ele mandou.
public sealed class AlertarEstornoFalhouActivity : AlertarEstornoPendenteActivity<EstornoFalhou>
{
    public AlertarEstornoFalhouActivity(ILogger<AlertarEstornoFalhouActivity> logger, TimeProvider relogio) : base(logger, relogio)
    {
    }

    protected override string? CausaDe(EstornoFalhou mensagem) => mensagem.Erro;
}
