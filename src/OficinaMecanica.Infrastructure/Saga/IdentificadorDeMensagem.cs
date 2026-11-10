using System.Security.Cryptography;
using System.Text;

namespace OficinaMecanica.Infrastructure.Saga;

// MessageId dos comandos que a saga envia, derivado da OS, do nome do comando e, quando o mesmo comando pode se repetir
// por motivo legitimo, de um discriminador (a janela de pagamento, o pagamento a estornar). Optei por derivar em vez de
// sortear: se o envio for refeito (a transacao da saga volta e a mensagem e reprocessada), o comando sai com o mesmo id
// e o inbox de quem o recebe o descarta como duplicata em vez de executar a mesma ordem duas vezes.
internal static class IdentificadorDeMensagem
{
    public static Guid Para(Guid ordemDeServicoId, string comando, Guid? discriminador = null)
    {
        var texto = discriminador is { } d
            ? $"{ordemDeServicoId:N}:{comando}:{d:N}"
            : $"{ordemDeServicoId:N}:{comando}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return new Guid(hash.AsSpan(0, 16));
    }
}
