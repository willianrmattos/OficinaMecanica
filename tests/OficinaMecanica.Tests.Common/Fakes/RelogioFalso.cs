namespace OficinaMecanica.Tests.Common.Fakes;

// Relogio fixo para os testes dos handlers que carimbam a hora nas mensagens. Escrevi o meu em vez de
// trazer o FakeTimeProvider do pacote de testes da Microsoft: so preciso fixar o instante.
public sealed class RelogioFalso : TimeProvider
{
    private readonly DateTimeOffset _agora;

    public RelogioFalso(DateTimeOffset? agora = null)
    {
        _agora = agora ?? new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _agora;
}
