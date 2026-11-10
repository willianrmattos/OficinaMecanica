using FluentValidation;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Commands.CancelarOrdemDeServico;

public class CancelarOrdemDeServicoCommandValidator : AbstractValidator<CancelarOrdemDeServicoCommand>
{
    public CancelarOrdemDeServicoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("O motivo do cancelamento é obrigatório.")
            .MaximumLength(OrdemDeServico.TamanhoMaximoDoMotivo)
            .WithMessage($"O motivo deve ter no máximo {OrdemDeServico.TamanhoMaximoDoMotivo} caracteres.");
    }
}
