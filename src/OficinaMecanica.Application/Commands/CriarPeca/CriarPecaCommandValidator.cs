using FluentValidation;

namespace OficinaMecanica.Application.Commands.CriarPeca;

public class CriarPecaCommandValidator : AbstractValidator<CriarPecaCommand>
{
    public CriarPecaCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome da peça é obrigatório.").MaximumLength(200);
        RuleFor(x => x.PrecoUnitario).GreaterThan(0).WithMessage("O preço unitário deve ser maior que zero.");
        RuleFor(x => x.QuantidadeEstoque).GreaterThanOrEqualTo(0).WithMessage("A quantidade em estoque não pode ser negativa.");
        RuleFor(x => x.EstoqueMinimo).GreaterThanOrEqualTo(0).WithMessage("O estoque mínimo não pode ser negativo.");
    }
}
