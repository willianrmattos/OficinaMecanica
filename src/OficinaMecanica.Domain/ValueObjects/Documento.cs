using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.ValueObjects;

public class Documento : ValueObject
{
    public string Numero { get; }
    public TipoDocumento Tipo { get; }

    private Documento(string numero, TipoDocumento tipo)
    {
        Numero = numero;
        Tipo = tipo;
    }

    public static Documento Criar(string numero)
    {
        var apenasDigitos = new string(numero.Where(char.IsDigit).ToArray());

        return apenasDigitos.Length switch
        {
            11 when ValidarCpf(apenasDigitos) => new Documento(apenasDigitos, TipoDocumento.CPF),
            14 when ValidarCnpj(apenasDigitos) => new Documento(apenasDigitos, TipoDocumento.CNPJ),
            _ => throw new DomainException("Documento inválido. Informe um CPF (11 dígitos) ou CNPJ (14 dígitos) válido.")
        };
    }

    private static bool ValidarCpf(string cpf)
    {
        if (cpf.Distinct().Count() == 1) return false;

        var multiplicadores1 = new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var multiplicadores2 = new[] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCpf = cpf[..9];
        var soma = 0;

        for (int i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicadores1[i];

        var resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;
        var digito = resto.ToString();
        tempCpf += digito;

        soma = 0;
        for (int i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicadores2[i];

        resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;
        digito += resto.ToString();

        return cpf.EndsWith(digito);
    }

    private static bool ValidarCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1) return false;

        var multiplicadores1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var multiplicadores2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCnpj = cnpj[..12];
        var soma = 0;

        for (int i = 0; i < 12; i++)
            soma += int.Parse(tempCnpj[i].ToString()) * multiplicadores1[i];

        var resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;
        var digito = resto.ToString();
        tempCnpj += digito;

        soma = 0;
        for (int i = 0; i < 13; i++)
            soma += int.Parse(tempCnpj[i].ToString()) * multiplicadores2[i];

        resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;
        digito += resto.ToString();

        return cnpj.EndsWith(digito);
    }

    public string Formatado => Tipo == TipoDocumento.CPF
        ? Convert.ToUInt64(Numero).ToString(@"000\.000\.000\-00")
        : Convert.ToUInt64(Numero).ToString(@"00\.000\.000\/0000\-00");

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Numero;
        yield return Tipo;
    }
}
