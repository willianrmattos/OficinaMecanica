using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Extensions;

// Motivo de compensacao vem de outros servicos como texto livre: nunca pode ser rejeitado nem estourar a coluna,
// senao justamente a compensacao deixaria a OS (ou a saga) sem andar. Por isso aparar e truncar sao uma regra so,
// usada pelo comando que cancela a OS e pela saga que guarda o motivo ate o cancelamento acontecer.
public static class NormalizadorDeMotivo
{
    public const string MotivoPadrao = "Cancelamento realizado pelo fluxo automático da ordem de serviço.";

    // Devolve nulo para texto vazio: quem precisa de um texto sempre presente usa Normalizar.
    public static string? Truncar(string? motivo)
    {
        var texto = motivo?.Trim();
        if (string.IsNullOrEmpty(texto))
            return null;

        if (texto.Length <= OrdemDeServico.TamanhoMaximoDoMotivo)
            return texto;

        // Recuo um caractere quando o corte cairia no meio de um par substituto (emoji), para nao gravar
        // meio caractere no historico.
        var limite = OrdemDeServico.TamanhoMaximoDoMotivo;
        if (char.IsHighSurrogate(texto[limite - 1]))
            limite--;

        return texto[..limite];
    }

    public static string Normalizar(string? motivo) => Truncar(motivo) ?? MotivoPadrao;
}
