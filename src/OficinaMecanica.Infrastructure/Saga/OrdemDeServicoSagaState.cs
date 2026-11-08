using MassTransit;
using OficinaMecanica.Application.Extensions;

namespace OficinaMecanica.Infrastructure.Saga;

// Linha da saga no banco do OS Service. O CorrelationId e o proprio id da OS (nao um Guid novo), entao
// "a saga da OS X" e uma busca por chave primaria e a instancia nunca se duplica.
//
// Nao uso SetCompletedWhenFinalized: a linha fica depois de Concluida/Cancelada/OrcamentoRecusado para auditoria
// e para o evento tardio (pagamento aprovado depois do cancelamento) encontrar a instancia e ser ignorado em vez
// de cair no "instancia inexistente".
public class OrdemDeServicoSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public string Numero { get; set; } = string.Empty;
    public Guid? OrcamentoId { get; set; }
    public Guid? PagamentoId { get; set; }

    // Prazo vigente de cada tipo: PrazoExpirado so age se o PrazoId bater com o daqui. Fica nulo quando a
    // etapa termina, para um aviso atrasado do sweeper nunca decidir nada.
    public Guid? PrazoAprovacaoId { get; set; }
    public Guid? PrazoPagamentoId { get; set; }

    // Decide o destino do pagamento tardio: cancelamento do staff devolve o dinheiro, cancelamento por prazo segue como pago.
    public bool CancelamentoPorStaff { get; set; }

    // Motivo do encerramento (cancelamento, recusa ou estorno), guardado ate o passo que de fato o aplica na OS.
    public string? MotivoCancelamento { get; set; }

    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset AtualizadaEm { get; set; }

    // Motivo vem de outro servico como texto livre e a coluna tem limite: truncar aqui evita que um texto grande
    // vire erro tecnico de gravacao e deixe a saga presa justamente numa compensacao.
    public void DefinirMotivo(string? motivo) => MotivoCancelamento = NormalizadorDeMotivo.Truncar(motivo);
}
