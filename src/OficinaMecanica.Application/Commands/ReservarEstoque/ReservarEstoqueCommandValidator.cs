using FluentValidation;

namespace OficinaMecanica.Application.Commands.ReservarEstoque;

public class ReservarEstoqueCommandValidator : AbstractValidator<ReservarEstoqueCommand>
{
    public ReservarEstoqueCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
