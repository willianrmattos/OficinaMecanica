using OficinaMecanica.Contracts.Common;

namespace OficinaMecanica.Infrastructure.Saga;

// Um prazo agendado pela saga (aprovacao do orcamento ou pagamento). A saga o grava na mesma transacao em que
// muda de estado; o sweeper o encontra quando ExpiraEm passa e publica PrazoExpirado. Sem chave estrangeira para a
// saga de proposito: o sweeper trava estas linhas com UPDLOCK e nao pode disputar a linha da saga.
public class SagaPrazo
{
    private SagaPrazo() { } // EF Core

    public SagaPrazo(Guid correlationId, Guid prazoId, TipoPrazo tipo, DateTimeOffset expiraEm)
    {
        CorrelationId = correlationId;
        PrazoId = prazoId;
        Tipo = tipo;
        ExpiraEm = expiraEm;
    }

    public Guid CorrelationId { get; private set; }
    public Guid PrazoId { get; private set; }
    public TipoPrazo Tipo { get; private set; }
    public DateTimeOffset ExpiraEm { get; private set; }
    public bool Processado { get; private set; }

    public void MarcarProcessado() => Processado = true;
}
