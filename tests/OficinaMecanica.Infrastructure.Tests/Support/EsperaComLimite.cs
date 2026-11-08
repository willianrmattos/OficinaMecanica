using FluentAssertions.Execution;

namespace OficinaMecanica.Infrastructure.Tests.Support;

// Optei por limitar toda espera de teste que depende de uma tarefa que o codigo sob teste poderia deixar de liberar (um
// TaskCompletionSource, uma avaliacao em andamento). Sem o limite, remover a protecao que o teste exercita faz o teste
// travar em vez de reprovar, e o run so terminaria no timeout do job. Com ele, a falta da protecao vira uma falha rapida
// com a explicacao do que deveria ter acontecido.
public static class EsperaComLimite
{
    // Acima de qualquer espera legitima dos testes (o maior limite do codigo sob teste e de 3 s) e curto o bastante para a
    // falha aparecer rapido.
    public static readonly TimeSpan Padrao = TimeSpan.FromSeconds(10);

    public static async Task AguardarAsync(this Task tarefa, string porque, TimeSpan? limite = null)
    {
        var espera = limite ?? Padrao;
        var primeira = await Task.WhenAny(tarefa, Task.Delay(espera));
        if (!ReferenceEquals(primeira, tarefa))
            Execute.Assertion.FailWith("Esperava que a tarefa terminasse em {0}, mas ela continua pendente: {1}", espera, porque);

        await tarefa;
    }

    public static async Task<T> AguardarAsync<T>(this Task<T> tarefa, string porque, TimeSpan? limite = null)
    {
        await ((Task)tarefa).AguardarAsync(porque, limite);
        return await tarefa;
    }
}
