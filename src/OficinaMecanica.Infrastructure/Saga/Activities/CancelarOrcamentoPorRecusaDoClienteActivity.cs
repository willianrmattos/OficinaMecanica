namespace OficinaMecanica.Infrastructure.Saga.Activities;

// A recusa do cliente pode chegar sem motivo. A saga deixa o motivo da OS vazio nesse caso (o dominio escreve sozinho
// "Orcamento recusado pelo cliente." no historico), mas o Billing precisa de um texto proprio: o padrao generico diria
// "fluxo automatico" para um cancelamento que foi uma decisao do cliente.
public sealed class CancelarOrcamentoPorRecusaDoClienteActivity : CancelarOrcamentoNoBillingActivity
{
    public CancelarOrcamentoPorRecusaDoClienteActivity(TimeProvider relogio) : base(relogio)
    {
    }

    protected override string MotivoPadrao => MotivosDaSaga.OrcamentoRecusadoPeloCliente;
}
