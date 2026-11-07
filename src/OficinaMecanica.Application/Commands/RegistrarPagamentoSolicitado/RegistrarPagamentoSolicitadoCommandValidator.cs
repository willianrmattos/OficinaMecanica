using FluentValidation;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Application.Commands.RegistrarPagamentoSolicitado;

public class RegistrarPagamentoSolicitadoCommandValidator : AbstractValidator<RegistrarPagamentoSolicitadoCommand>
{
    public RegistrarPagamentoSolicitadoCommandValidator()
    {
        RuleFor(x => x.OrdemDeServicoId).NotEmpty().WithMessage("A ordem de serviço é obrigatória.");
        RuleFor(x => x.PagamentoId).NotEmpty().WithMessage("O identificador do pagamento é obrigatório.");
        RuleFor(x => x.ExpiraEm).NotEmpty().WithMessage("O prazo do pagamento é obrigatório.");
        RuleFor(x => x.LinkPagamento)
            .NotEmpty().WithMessage("O link de pagamento é obrigatório.")
            .MaximumLength(OrdemDeServico.TamanhoMaximoDoLinkDePagamento)
            .WithMessage($"O link de pagamento deve ter no máximo {OrdemDeServico.TamanhoMaximoDoLinkDePagamento} caracteres.")
            // O link vai por e-mail ao cliente: restrinjo a http(s) absoluto para nunca enviar outro esquema.
            .Must(EhUrlHttpAbsoluta).WithMessage("O link de pagamento deve ser uma URL http ou https válida.");
    }

    private static bool EhUrlHttpAbsoluta(string? link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
