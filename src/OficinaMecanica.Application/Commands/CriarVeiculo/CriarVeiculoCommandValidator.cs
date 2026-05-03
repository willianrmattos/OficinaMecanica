using FluentValidation;

namespace OficinaMecanica.Application.Commands.CriarVeiculo;

public class CriarVeiculoCommandValidator : AbstractValidator<CriarVeiculoCommand>
{
    public CriarVeiculoCommandValidator()
    {
        RuleFor(x => x.ClienteId).NotEmpty().WithMessage("O cliente é obrigatório.");
        RuleFor(x => x.Placa).NotEmpty().WithMessage("A placa é obrigatória.");
        RuleFor(x => x.Marca).NotEmpty().WithMessage("A marca é obrigatória.").MaximumLength(100);
        RuleFor(x => x.Modelo).NotEmpty().WithMessage("O modelo é obrigatório.").MaximumLength(100);
        RuleFor(x => x.Ano).InclusiveBetween(1900, DateTime.Now.Year + 1).WithMessage("Ano inválido.");
    }
}
