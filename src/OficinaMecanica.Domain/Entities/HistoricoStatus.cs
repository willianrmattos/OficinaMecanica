using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Domain.Entities;

public class HistoricoStatus : Entity
{
    public Guid OrdemDeServicoId { get; private set; }
    public StatusOrdemDeServico Status { get; private set; }
    public string Observacao { get; private set; }
    public DateTime DataAlteracao { get; private set; }

    private HistoricoStatus() { } // EF Core

    public HistoricoStatus(Guid ordemDeServicoId, StatusOrdemDeServico status, string observacao)
    {
        OrdemDeServicoId = ordemDeServicoId;
        Status = status;
        Observacao = observacao;
        DataAlteracao = DateTime.UtcNow;
    }
}
