using Xunit.Sdk;

namespace OficinaMecanica.Bdd.Tests.Support;

// A saga e as respostas dos servicos simulados chegam de forma assincrona: em vez de dormir um tempo fixo, cada passo de
// verificacao espera a condicao com teto de 10 s e, se estoura, reprova dizendo o que esperava e o que viu por ultimo.
public static class Esperar
{
    public static readonly TimeSpan Teto = TimeSpan.FromSeconds(10);

    public static async Task<T> AteAsync<T>(Func<Task<T>> ler, Func<T, bool> atendida, string esperado, Func<T, string>? descreverOQueViu = null)
    {
        var limite = DateTime.UtcNow + Teto;
        T ultimo;

        while (true)
        {
            ultimo = await ler();
            if (atendida(ultimo))
                return ultimo;

            if (DateTime.UtcNow >= limite)
                break;

            await Task.Delay(25);
        }

        var viu = descreverOQueViu is null ? ultimo?.ToString() : descreverOQueViu(ultimo);
        throw new XunitException($"Tempo esgotado ({Teto.TotalSeconds:0} s) esperando {esperado}. Último valor observado: {viu ?? "(nenhum)"}.");
    }

    public static Task<T> AteQue<T>(Func<T> ler, Func<T, bool> atendida, string esperado, Func<T, string>? descreverOQueViu = null) =>
        AteAsync(() => Task.FromResult(ler()), atendida, esperado, descreverOQueViu);
}
