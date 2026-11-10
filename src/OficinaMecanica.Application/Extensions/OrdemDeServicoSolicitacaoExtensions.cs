using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Application.Extensions;

// Guardas das solicitacoes HTTP que so publicam uma mensagem para a saga (aprovar, recusar, cancelar).
// Respondem 409 porque nao e o dado enviado que esta errado: e o estado atual da OS que nao comporta o pedido.
internal static class OrdemDeServicoSolicitacaoExtensions
{
    public static void ExigirParticipacaoNaSaga(this OrdemDeServico ordem)
    {
        if (!ordem.ParticipaDaSaga)
            throw new ConflitoException("Esta ordem de serviço foi aberta antes do fluxo com pagamento e não aceita esta operação pela API.");
    }

    public static void ExigirAguardandoDecisaoDoCliente(this OrdemDeServico ordem)
    {
        ordem.ExigirParticipacaoNaSaga();

        if (!ordem.AguardaDecisaoDoCliente)
            throw new ConflitoException($"O orçamento só pode ser aprovado ou recusado enquanto a OS está em 'AguardandoAprovacao'. Status atual: '{ordem.Status}'.");
    }

    public static void ExigirCancelamentoAntesDaExecucao(this OrdemDeServico ordem)
    {
        ordem.ExigirParticipacaoNaSaga();

        if (!ordem.PodeSerCanceladaAntesDaExecucao)
            throw new ConflitoException($"A OS só pode ser cancelada antes de entrar em execução. Status atual: '{ordem.Status}'.");
    }
}
