using System.Text.RegularExpressions;
using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.ValueObjects;

public partial class Placa : ValueObject
{
    public string Valor { get; }

    private Placa(string valor)
    {
        Valor = valor.ToUpper();
    }

    public static Placa Criar(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException("A placa do veículo é obrigatória.");

        var placaLimpa = valor.Replace("-", "").Trim().ToUpper();

        if (!PlacaAntigaRegex().IsMatch(placaLimpa) && !PlacaMercosulRegex().IsMatch(placaLimpa))
            throw new DomainException("Placa inválida. Formatos aceitos: ABC1234 (antiga) ou ABC1D23 (Mercosul).");

        return new Placa(placaLimpa);
    }

    [GeneratedRegex(@"^[A-Z]{3}\d{4}$")]
    private static partial Regex PlacaAntigaRegex();

    [GeneratedRegex(@"^[A-Z]{3}\d[A-Z]\d{2}$")]
    private static partial Regex PlacaMercosulRegex();

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor;
}
