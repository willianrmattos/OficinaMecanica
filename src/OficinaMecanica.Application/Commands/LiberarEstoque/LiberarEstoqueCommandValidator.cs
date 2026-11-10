using FluentValidation;

namespace OficinaMecanica.Application.Commands.LiberarEstoque;

public class LiberarEstoqueCommandValidator : AbstractValidator<LiberarEstoqueCommand>
{
    public LiberarEstoqueCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
