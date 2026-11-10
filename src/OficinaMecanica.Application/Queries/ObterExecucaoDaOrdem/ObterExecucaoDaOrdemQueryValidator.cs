using FluentValidation;

namespace OficinaMecanica.Application.Queries.ObterExecucaoDaOrdem;

public class ObterExecucaoDaOrdemQueryValidator : AbstractValidator<ObterExecucaoDaOrdemQuery>
{
    public ObterExecucaoDaOrdemQueryValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
