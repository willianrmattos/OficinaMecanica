using OficinaMecanica.Application.Commands.ReservarEstoque;

namespace OficinaMecanica.Infrastructure.Saga.Activities;

// Resultado da reserva no payload do contexto do evento: decide o ramo seguinte da mesma mensagem (IfElse) sem virar coluna.
public sealed class ReservaDeEstoqueDoEvento
{
    public ResultadoDaReservaDeEstoque? Resultado { get; set; }

    public bool Conseguiu => Resultado is ResultadoDaReservaDeEstoque.Reservado or ResultadoDaReservaDeEstoque.JaReservado;
}
