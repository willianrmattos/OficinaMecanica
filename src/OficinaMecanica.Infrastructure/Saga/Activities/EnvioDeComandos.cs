using MassTransit;
using OficinaMecanica.Infrastructure.Messaging;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

internal static class EnvioDeComandos
{
    // Comando vai por Send para a fila do dono (Filas.*), nao por Publish: ha um destinatario so, e e ele quem
    // responde com um evento. O MessageId do contrato (deterministico, ver IdentificadorDeMensagem) vira o do
    // envelope, que e o que o inbox do destino usa para descartar a reentrega. O contexto do consumer, com o
    // outbox ligado, grava o envio na mesma transacao da saga: o comando so sai se o estado novo comitar.
    //
    // Todo comando da saga carrega um MessageId deterministico. Um comando sem ele sairia com um id sorteado pelo
    // MassTransit e a reentrega deixaria de ser descartada pelo destino, entao recuso o envio em vez de seguir sem
    // idempotencia.
    public static async Task EnviarAsync<T>(this ConsumeContext contexto, string fila, T comando) where T : class
    {
        var messageId = MessageIdDoContrato<T>.Obter(comando)
            ?? throw new InvalidOperationException(
                $"O comando {typeof(T).Name} não tem MessageId: sem ele o inbox do destino não descarta a reentrega.");

        var endpoint = await contexto.GetSendEndpoint(new Uri($"queue:{fila}"));
        await endpoint.Send(comando, Pipe.Execute<SendContext<T>>(envio => envio.MessageId = messageId), contexto.CancellationToken);
    }
}
