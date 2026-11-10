using FluentValidation;

namespace OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;

public class AplicarAprovacaoDoOrcamentoCommandValidator : AbstractValidator<AplicarAprovacaoDoOrcamentoCommand>
{
    public AplicarAprovacaoDoOrcamentoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
