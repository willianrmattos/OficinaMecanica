using Microsoft.Extensions.Options;

namespace OficinaMecanica.Infrastructure.Execucao;

public sealed class ValidadorDasOpcoesDaExecucao : IValidateOptions<OpcoesDaExecucao>
{
    public ValidateOptionsResult Validate(string? name, OpcoesDaExecucao opcoes)
    {
        var falhas = new List<string>();

        if (!Uri.TryCreate(opcoes.BaseUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            falhas.Add($"{OpcoesDaExecucao.Secao}:{nameof(opcoes.BaseUrl)} deve ser uma URL absoluta http ou https (valor atual: '{opcoes.BaseUrl}').");

        ExigirPositivo(opcoes.TimeoutSegundos, nameof(opcoes.TimeoutSegundos), falhas);
        ExigirPositivo(opcoes.TimeoutPorTentativaSegundos, nameof(opcoes.TimeoutPorTentativaSegundos), falhas);
        ExigirPositivo(opcoes.TentativasDeRetry, nameof(opcoes.TentativasDeRetry), falhas);
        ExigirPositivo(opcoes.AtrasoDoRetryMs, nameof(opcoes.AtrasoDoRetryMs), falhas);
        ExigirPositivo(opcoes.CircuitoJanelaSegundos, nameof(opcoes.CircuitoJanelaSegundos), falhas);
        ExigirPositivo(opcoes.CircuitoPausaSegundos, nameof(opcoes.CircuitoPausaSegundos), falhas);

        // O Polly exige pelo menos duas chamadas na janela para o circuito poder abrir.
        if (opcoes.CircuitoMinimoDeChamadas < 2)
            falhas.Add($"{OpcoesDaExecucao.Secao}:{nameof(opcoes.CircuitoMinimoDeChamadas)} deve ser pelo menos 2 (valor atual: {opcoes.CircuitoMinimoDeChamadas}).");

        if (opcoes.CircuitoTaxaDeFalha is <= 0 or > 1 || double.IsNaN(opcoes.CircuitoTaxaDeFalha))
            falhas.Add($"{OpcoesDaExecucao.Secao}:{nameof(opcoes.CircuitoTaxaDeFalha)} deve estar entre 0 (exclusivo) e 1 (valor atual: {opcoes.CircuitoTaxaDeFalha}).");

        if (opcoes.TimeoutPorTentativaSegundos > opcoes.TimeoutSegundos && opcoes.TimeoutSegundos > 0)
            falhas.Add($"{OpcoesDaExecucao.Secao}:{nameof(opcoes.TimeoutPorTentativaSegundos)} ({opcoes.TimeoutPorTentativaSegundos}) não pode passar de {nameof(opcoes.TimeoutSegundos)} ({opcoes.TimeoutSegundos}).");

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static void ExigirPositivo(int valor, string nome, List<string> falhas)
    {
        if (valor <= 0)
            falhas.Add($"{OpcoesDaExecucao.Secao}:{nome} deve ser maior que zero (valor atual: {valor}).");
    }
}
