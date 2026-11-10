using FluentValidation;

namespace OficinaMecanica.Application.Queries.ObterSnapshotDaOrdem;

public class ObterSnapshotDaOrdemQueryValidator : AbstractValidator<ObterSnapshotDaOrdemQuery>
{
    public ObterSnapshotDaOrdemQueryValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
