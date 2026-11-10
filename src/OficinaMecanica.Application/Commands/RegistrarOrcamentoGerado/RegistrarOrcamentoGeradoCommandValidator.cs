using FluentValidation;

namespace OficinaMecanica.Application.Commands.RegistrarOrcamentoGerado;

public class RegistrarOrcamentoGeradoCommandValidator : AbstractValidator<RegistrarOrcamentoGeradoCommand>
{
    public RegistrarOrcamentoGeradoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
