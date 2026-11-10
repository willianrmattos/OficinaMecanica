namespace OficinaMecanica.Infrastructure.Tests.Support;

public static class Esperar
{
    // Testes de mensageria dependem de entrega assincrona: espera a condicao em vez de dormir um tempo fixo.
    public static async Task<bool> AteAsync(Func<bool> condicao, TimeSpan? limite = null)
    {
        var fim = DateTime.UtcNow + (limite ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < fim)
        {
            if (condicao())
                return true;

            await Task.Delay(50);
        }

        return condicao();
    }

    public static async Task<bool> AteAsync(Func<Task<bool>> condicao, TimeSpan? limite = null)
    {
        var fim = DateTime.UtcNow + (limite ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < fim)
        {
            if (await condicao())
                return true;

            await Task.Delay(50);
        }

        return await condicao();
    }
}
