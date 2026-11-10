using MediatR;
using OficinaMecanica.Application.Extensions;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Exceptions;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.ReservarEstoque;

public class ReservarEstoqueCommandHandler : IRequestHandler<ReservarEstoqueCommand, ResultadoDaReservaDeEstoque>
{
    private readonly IOrdemDeServicoRepository _ordemRepository;
    private readonly IPecaRepository _pecaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReservarEstoqueCommandHandler(
        IOrdemDeServicoRepository ordemRepository,
        IPecaRepository pecaRepository,
        IUnitOfWork unitOfWork)
    {
        _ordemRepository = ordemRepository;
        _pecaRepository = pecaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResultadoDaReservaDeEstoque> Handle(ReservarEstoqueCommand request, CancellationToken cancellationToken)
    {
        var ordem = await _ordemRepository.ObterObrigatoriaAsync(request.OrdemDeServicoId, cancellationToken);

        if (ordem.EstoqueReservado)
            return ResultadoDaReservaDeEstoque.JaReservado;

        // O status vem antes do estoque: uma OS que nem pode reservar (cancelada, em diagnostico) e erro
        // de uso, nao "estoque insuficiente", e a saga trataria os dois casos de formas opostas.
        ordem.ValidarPodeReservarEstoque();

        var necessidades = new List<(Peca Peca, int Quantidade)>();
        foreach (var item in ordem.ItensPeca)
        {
            var peca = await _pecaRepository.ObterPorIdAsync(item.PecaId, cancellationToken)
                ?? throw new DomainException($"Peça '{item.NomePeca}' não encontrada no sistema.");
            necessidades.Add((peca, item.Quantidade));
        }

        // Tudo ou nada: confiro todas as pecas antes de mexer em qualquer uma. As entidades continuam
        // rastreadas no DbContext, entao uma baixa parcial seria gravada por um SaveChanges posterior
        // no mesmo escopo (a saga cancela a OS e salva logo em seguida).
        if (necessidades.Any(n => n.Peca.QuantidadeEstoque < n.Quantidade))
            return ResultadoDaReservaDeEstoque.EstoqueInsuficiente;

        ordem.ReservarEstoque();
        foreach (var (peca, quantidade) in necessidades)
            peca.RemoverEstoque(quantidade);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ResultadoDaReservaDeEstoque.Reservado;
    }
}
