using Reqnroll;

namespace OficinaMecanica.Bdd.Tests.Support;

[Binding]
public sealed class Hooks
{
    private readonly ContextoDoCenario _contexto;

    public Hooks(ContextoDoCenario contexto)
    {
        _contexto = contexto;
    }

    [BeforeScenario(Order = 0)]
    public Task IniciarAApiDoCenarioAsync() => _contexto.IniciarAsync();

    [AfterScenario]
    public async Task EncerrarAApiDoCenarioAsync() => await _contexto.DisposeAsync();
}
