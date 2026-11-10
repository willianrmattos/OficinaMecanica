using MediatR;

namespace OficinaMecanica.Application.Commands.MarcarDiagnosticoIniciado;

// Comando interno, chamado pela saga: nao e exposto por HTTP e nao publica mensagens (quem publica e a saga).
// Todos os comandos internos sao idempotentes, porque a saga reentrega mensagens.
public record MarcarDiagnosticoIniciadoCommand(Guid OrdemDeServicoId) : IRequest<Unit>;
