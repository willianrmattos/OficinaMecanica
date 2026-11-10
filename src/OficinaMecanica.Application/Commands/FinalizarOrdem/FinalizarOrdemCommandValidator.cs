using FluentValidation;

namespace OficinaMecanica.Application.Commands.FinalizarOrdem;

public class FinalizarOrdemCommandValidator : AbstractValidator<FinalizarOrdemCommand>
{
    public FinalizarOrdemCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
