using FluentValidation;

namespace OficinaMecanica.Application.Commands.AprovarOrcamento;

public class AprovarOrcamentoCommandValidator : AbstractValidator<AprovarOrcamentoCommand>
{
    public AprovarOrcamentoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
