using FluentValidation;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Commands.RecusarOrcamento;

public class RecusarOrcamentoCommandValidator : AbstractValidator<RecusarOrcamentoCommand>
{
    public RecusarOrcamentoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
        RuleFor(x => x.Motivo)
            .MaximumLength(OrdemDeServico.TamanhoMaximoDoMotivo)
            .WithMessage($"O motivo deve ter no máximo {OrdemDeServico.TamanhoMaximoDoMotivo} caracteres.");
    }
}
