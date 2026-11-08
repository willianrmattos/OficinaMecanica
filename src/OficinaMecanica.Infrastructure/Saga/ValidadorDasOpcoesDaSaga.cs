using Microsoft.Extensions.Options;

namespace OficinaMecanica.Infrastructure.Saga;

public sealed class ValidadorDasOpcoesDaSaga : IValidateOptions<SagaOptions>
{
    public ValidateOptionsResult Validate(string? name, SagaOptions opcoes)
    {
        var falhas = new List<string>();

        Exigir(opcoes.PrazoAprovacaoHoras, nameof(opcoes.PrazoAprovacaoHoras), falhas);
        Exigir(opcoes.PrazoPagamentoHoras, nameof(opcoes.PrazoPagamentoHoras), falhas);
        Exigir(opcoes.SweeperIntervaloSegundos, nameof(opcoes.SweeperIntervaloSegundos), falhas);
        Exigir(opcoes.SweeperLote, nameof(opcoes.SweeperLote), falhas);

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static void Exigir(int valor, string nome, List<string> falhas)
    {
        if (valor <= 0)
            falhas.Add($"{SagaOptions.Secao}:{nome} deve ser maior que zero (valor atual: {valor}).");
    }
}
