namespace OficinaMecanica.Infrastructure.Saga;

// Secao "Saga" (variaveis Saga__*). Os prazos sao em horas porque sao prazos de negocio (o cliente decide em dias);
// o sweeper e que trabalha em segundos.
public sealed class SagaOptions
{
    public const string Secao = "Saga";

    // Quanto o cliente tem para aprovar o orcamento: tambem e a validade (ValidoAte) que o Billing grava nele.
    public int PrazoAprovacaoHoras { get; set; } = 72;

    // Quanto o cliente tem para pagar depois de aprovar: vira o ExpiraEm do link de pagamento.
    public int PrazoPagamentoHoras { get; set; } = 48;

    public int SweeperIntervaloSegundos { get; set; } = 30;

    // Quantos prazos vencidos uma varredura reivindica de uma vez.
    public int SweeperLote { get; set; } = 50;

    // Desliga so o BackgroundService (os prazos continuam sendo gravados). Existe para os testes dispararem o sweeper a mao.
    public bool SweeperHabilitado { get; set; } = true;
}
