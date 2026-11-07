using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;

public class CancelarOrdemPelaSagaCommandHandler : IRequestHandler<CancelarOrdemPelaSagaCommand, Unit>
{
    public const string MotivoPadrao = "Cancelamento realizado pelo fluxo automático da ordem de serviço.";

    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelarOrdemPelaSagaCommandHandler(IOrdemDeServicoRepository ordemRepository, IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _unitOfWork = unitOfWork;
    }

    // Nao libera estoque: a liberacao e um passo proprio da saga (LiberarEstoqueCommand), porque nem todo
    // cancelamento tem reserva a devolver e porque ela precisa poder ser repetida sozinha ate dar certo.
    public async Task<Unit> Handle(CancelarOrdemPelaSagaCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.Status == StatusOrdemDeServico.Cancelada)
            return Unit.Value;

        ordem.Cancelar(NormalizarMotivo(request.Motivo));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }

    // Optei por normalizar em vez de rejeitar: o motivo chega de outros servicos como texto livre, e um motivo
    // vazio ou grande demais nao pode deixar a OS sem cancelar justamente na compensacao.
    private static string NormalizarMotivo(string? motivo)
    {
        var texto = motivo?.Trim();
        if (string.IsNullOrEmpty(texto))
            return MotivoPadrao;

        if (texto.Length <= OrdemDeServico.TamanhoMaximoDoMotivo)
            return texto;

        // Recuo um caractere quando o corte cairia no meio de um par substituto (emoji), para nao gravar
        // meio caractere no historico.
        var limite = OrdemDeServico.TamanhoMaximoDoMotivo;
        if (char.IsHighSurrogate(texto[limite - 1]))
            limite--;

        return texto[..limite];
    }
}
