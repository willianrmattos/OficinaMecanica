using FluentValidation;

namespace OficinaMecanica.Application.Commands.CriarOrdemDeServico;

public class CriarOrdemDeServicoCommandValidator : AbstractValidator<CriarOrdemDeServicoCommand>
{
    public CriarOrdemDeServicoCommandValidator()
    {
        RuleFor(x => x.ClienteId).NotEmpty().WithMessage("O cliente é obrigatório.");
        RuleFor(x => x.VeiculoId).NotEmpty().WithMessage("O veículo é obrigatório.");
        RuleFor(x => x.Servicos).NotEmpty().WithMessage("A OS deve ter pelo menos um serviço.");
        RuleForEach(x => x.Servicos).ChildRules(s =>
        {
            s.RuleFor(x => x.ServicoId).NotEmpty().WithMessage("O ID do serviço é obrigatório.");
            s.RuleFor(x => x.Quantidade).GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");
        });
    }
}
