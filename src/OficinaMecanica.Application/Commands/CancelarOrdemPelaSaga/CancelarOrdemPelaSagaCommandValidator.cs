using FluentValidation;

namespace OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;

// Sem regra para o motivo de proposito: um motivo vazio ou longo demais nao pode impedir a compensacao
// de cancelar a OS; o handler normaliza o texto.
public class CancelarOrdemPelaSagaCommandValidator : AbstractValidator<CancelarOrdemPelaSagaCommand>
{
    public CancelarOrdemPelaSagaCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
    }
}
