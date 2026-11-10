using FluentValidation;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Application.Commands.AtualizarStatusOrdem;

public class AtualizarStatusOrdemCommandValidator : AbstractValidator<AtualizarStatusOrdemCommand>
{
    public AtualizarStatusOrdemCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
        RuleFor(x => x.NovoStatus)
            .Equal((int)StatusOrdemDeServico.Entregue)
            .WithMessage($"Só é possível atualizar o status para 'Entregue' ({(int)StatusOrdemDeServico.Entregue}). Os demais status são alterados pelo fluxo automático da OS.");
    }
}
