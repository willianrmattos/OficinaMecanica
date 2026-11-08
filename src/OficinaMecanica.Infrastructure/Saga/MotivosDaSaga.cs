namespace OficinaMecanica.Infrastructure.Saga;

// Texto dos motivos que a propria saga origina (os que vem de outros servicos chegam nas mensagens). Ficam juntos
// porque sao o que o cliente e o staff leem no historico da OS e no extrato do Billing.
internal static class MotivosDaSaga
{
    public const string EstoqueInsuficiente = "Estoque insuficiente para as peças da ordem de serviço.";
    public const string OrcamentoRecusadoPeloCliente = "Orçamento recusado pelo cliente.";
    public const string PrazoDeAprovacaoExpirado = "Prazo para aprovação do orçamento expirado.";
    public const string PrazoDePagamentoExpirado = "Pagamento não realizado dentro do prazo.";
    public const string FalhaTecnicaNoDiagnostico = "Falha técnica ao solicitar o diagnóstico.";
    public const string FalhaTecnicaNoOrcamento = "Falha técnica ao gerar o orçamento.";
    public const string FalhaTecnicaNoPagamento = "Falha técnica ao solicitar o pagamento.";
    public const string FalhaTecnicaNaExecucao = "Falha técnica ao iniciar a execução do serviço.";

    public static string ExecucaoInviavel(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) ? "Execução inviável." : $"Execução inviável: {motivo.Trim()}";

    public static string CancelamentoDoStaff(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) ? "Cancelamento solicitado pelo atendimento." : motivo.Trim();
}
