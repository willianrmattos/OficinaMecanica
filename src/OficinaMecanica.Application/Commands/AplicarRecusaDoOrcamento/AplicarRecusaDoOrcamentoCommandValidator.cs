using FluentValidation;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Commands.AplicarRecusaDoOrcamento;

public class AplicarRecusaDoOrcamentoCommandValidator : AbstractValidator<AplicarRecusaDoOrcamentoCommand>
{
    public AplicarRecusaDoOrcamentoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
        RuleFor(x => x.Motivo)
            .MaximumLength(OrdemDeServico.TamanhoMaximoDoMotivo)
            .WithMessage($"O motivo deve ter no máximo {OrdemDeServico.TamanhoMaximoDoMotivo} caracteres.");
    }
}
