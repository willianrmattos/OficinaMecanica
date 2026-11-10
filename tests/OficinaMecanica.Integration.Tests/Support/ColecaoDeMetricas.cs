namespace OficinaMecanica.Integration.Tests.Support;

// MetricasDaSaga guarda o retrato das sagas em estado estatico, que o ciclo de MetricasDeNegocioBackgroundService sobrescreve. Dois testes que
// rodam esse ciclo ao mesmo tempo gravam um por cima do outro (um banco vazio apaga o que o outro semeou), entao os que dependem dele ficam
// numa colecao que desliga o paralelismo: nenhum outro teste do assembly roda enquanto ela executa. Mesmo desenho do Infrastructure.Tests.
[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class ColecaoDeMetricas
{
    public const string Nome = "Metricas da saga (sem paralelismo)";
}
