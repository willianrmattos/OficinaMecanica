using MediatR;
using OficinaMecanica.Application.DTOs;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;

namespace OficinaMecanica.Application.Commands.CriarServico;

public class CriarServicoCommandHandler : IRequestHandler<CriarServicoCommand, ServicoDto>
{
    private readonly IServicoRepository _servicoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarServicoCommandHandler(IServicoRepository servicoRepository, IUnitOfWork unitOfWork)
    {
        _servicoRepository = servicoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServicoDto> Handle(CriarServicoCommand request, CancellationToken cancellationToken)
    {
        var servico = new Servico(request.Nome, request.Descricao, request.Preco, request.TempoEstimadoMinutos);

        await _servicoRepository.AdicionarAsync(servico, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServicoDto(servico.Id, servico.Nome, servico.Descricao, servico.Preco, servico.TempoEstimadoMinutos, servico.Ativo);
    }
}
