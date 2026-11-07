namespace OficinaMecanica.Infrastructure.Tests.Support;

// Define variaveis de ambiente do processo e as restaura ao descartar. O Program le a configuracao antes de qualquer
// fonte que o WebApplicationFactory acrescente, e o appsettings.json vence o UseSetting; so a variavel de ambiente
// vence o appsettings. Os testes que a usam ficam na colecao Infra, que roda em sequencia.
public sealed class VariaveisDeAmbiente : IDisposable
{
    private readonly Dictionary<string, string?> _originais = new();

    public VariaveisDeAmbiente(IReadOnlyDictionary<string, string> valores)
    {
        foreach (var (nome, valor) in valores)
        {
            _originais[nome] = Environment.GetEnvironmentVariable(nome);
            Environment.SetEnvironmentVariable(nome, valor);
        }
    }

    public void Dispose()
    {
        foreach (var (nome, original) in _originais)
            Environment.SetEnvironmentVariable(nome, original);
    }
}
