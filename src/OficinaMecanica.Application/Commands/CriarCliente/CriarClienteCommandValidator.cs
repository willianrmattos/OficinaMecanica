using FluentValidation;

namespace OficinaMecanica.Application.Commands.CriarCliente;

public class CriarClienteCommandValidator : AbstractValidator<CriarClienteCommand>
{
    public CriarClienteCommandValidator()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(200).WithMessage("O nome deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Documento)
            .NotEmpty().WithMessage("O documento (CPF/CNPJ) é obrigatório.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("O email informado é inválido.");

        RuleFor(x => x.Telefone)
            .MaximumLength(20).When(x => !string.IsNullOrEmpty(x.Telefone))
            .WithMessage("O telefone deve ter no máximo 20 caracteres.");
    }
}
