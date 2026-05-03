using FluentValidation;

namespace OficinaMecanica.Application.Commands.CriarServico;

public class CriarServicoCommandValidator : AbstractValidator<CriarServicoCommand>
{
    public CriarServicoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("O nome do serviço é obrigatório.").MaximumLength(200);
        RuleFor(x => x.Preco).GreaterThan(0).WithMessage("O preço deve ser maior que zero.");
        RuleFor(x => x.TempoEstimadoMinutos).GreaterThan(0).WithMessage("O tempo estimado deve ser maior que zero.");
    }
}
