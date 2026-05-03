using MediatR;
using OficinaMecanica.Application.DTOs;

namespace OficinaMecanica.Application.Commands.CriarOrdemDeServico;

public record ItemServicoInput(Guid ServicoId, int Quantidade = 1);
public record ItemPecaInput(Guid PecaId, int Quantidade);

public record CriarOrdemDeServicoCommand(
    Guid ClienteId,
    Guid VeiculoId,
    string? Observacoes,
    List<ItemServicoInput> Servicos,
    List<ItemPecaInput>? Pecas
) : IRequest<OrdemDeServicoDto>;
