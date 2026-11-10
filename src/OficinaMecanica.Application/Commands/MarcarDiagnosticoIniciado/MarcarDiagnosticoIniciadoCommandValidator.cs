using FluentValidation;

namespace OficinaMecanica.Application.Commands.MarcarDiagnosticoIniciado;

public class MarcarDiagnosticoIniciadoCommandValidator : AbstractValidator<MarcarDiagnosticoIniciadoCommand>
{
    public MarcarDiagnosticoIniciadoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
