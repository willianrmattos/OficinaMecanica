using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Infrastructure.Data;

namespace OficinaMecanica.Integration.Tests.Support;

public static class BancoDeTestesExtensions
{
    public static async Task SemearAsync(this CustomWebApplicationFactory factory, params object[] entidades)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    public static async Task<T> ConsultarAsync<T>(this CustomWebApplicationFactory factory, Func<AppDbContext, Task<T>> consulta)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await consulta(db);
    }
}
