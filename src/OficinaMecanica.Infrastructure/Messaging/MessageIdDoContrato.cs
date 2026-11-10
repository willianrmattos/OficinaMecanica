using System.Linq.Expressions;
using System.Reflection;

namespace OficinaMecanica.Infrastructure.Messaging;

// Le a propriedade MessageId de um contrato sem reflexao por chamada: o delegate e compilado uma unica vez por tipo
// (a classe estatica generica e o cache). Contrato sem MessageId do tipo Guid, ou com Guid.Empty, devolve null e o
// MassTransit gera o id do envelope sozinho.
internal static class MessageIdDoContrato<T> where T : class
{
    private static readonly Func<T, Guid>? Acessor = CriarAcessor();

    public static Guid? Obter(T mensagem)
    {
        var id = Acessor?.Invoke(mensagem);
        return id is { } valor && valor != Guid.Empty ? valor : null;
    }

    private static Func<T, Guid>? CriarAcessor()
    {
        var propriedade = typeof(T).GetProperty("MessageId", BindingFlags.Public | BindingFlags.Instance);
        if (propriedade is null || propriedade.PropertyType != typeof(Guid) || !propriedade.CanRead)
            return null;

        var parametro = Expression.Parameter(typeof(T), "mensagem");
        return Expression.Lambda<Func<T, Guid>>(Expression.Property(parametro, propriedade), parametro).Compile();
    }
}
