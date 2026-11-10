using FluentValidation;

namespace OficinaMecanica.Application.Commands.IniciarExecucaoDaOrdem;

public class IniciarExecucaoDaOrdemCommandValidator : AbstractValidator<IniciarExecucaoDaOrdemCommand>
{
    public IniciarExecucaoDaOrdemCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
