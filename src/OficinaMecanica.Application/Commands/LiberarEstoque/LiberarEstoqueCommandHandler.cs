using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.LiberarEstoque;

public class LiberarEstoqueCommandHandler : IRequestHandler<LiberarEstoqueCommand, Unit>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LiberarEstoqueCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IPecaRepository pecaRepository,
        IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(LiberarEstoqueCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        // Sem reserva nao ha o que devolver: devolver de novo criaria estoque que nunca existiu.
        if (!ordem.EstoqueReservado)
            return Unit.Value;

        var devolucoes = new List<(Peca Peca, int Quantidade)>();
        foreach (var item in ordem.ItensPeca)
        {
            var peca = await _pecaRepository.ObterPorIdAsync(item.PecaId, cancellationToken)
                ?? throw new DomainException($"Peça '{item.NomePeca}' não encontrada no sistema.");
            devolucoes.Add((peca, item.Quantidade));
        }

        foreach (var (peca, quantidade) in devolucoes)
            peca.AdicionarEstoque(quantidade);

        ordem.LiberarEstoque();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
