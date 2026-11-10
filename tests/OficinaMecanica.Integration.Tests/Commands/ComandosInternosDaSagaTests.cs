using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.Commands.AplicarAprovacaoDoOrcamento;
using OficinaMecanica.Application.Commands.AplicarRecusaDoOrcamento;
using OficinaMecanica.Application.Commands.CancelarOrdemPelaSaga;
using OficinaMecanica.Application.Commands.FinalizarOrdem;
using OficinaMecanica.Application.Commands.IniciarExecucaoDaOrdem;
using OficinaMecanica.Application.Commands.LiberarEstoque;
using OficinaMecanica.Application.Commands.MarcarDiagnosticoIniciado;
using OficinaMecanica.Application.Commands.RegistrarOrcamentoGerado;
using OficinaMecanica.Application.Commands.RegistrarPagamentoSolicitado;
using OficinaMecanica.Application.Commands.ReservarEstoque;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Commands;

// Roda os comandos internos pelo IMediator real (ValidationBehavior + AppDbContext InMemory), cada um num escopo
// novo e relendo o estado num escopo novo: e exatamente o caminho que a saga vai percorrer, um comando por consumo.
public class ComandosInternosDaSagaTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTimeOffset Prazo = new(2026, 3, 12, 15, 30, 0, TimeSpan.Zero);

    private readonly CustomWebApplicationFactory _factory;

    public ComandosInternosDaSagaTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record EstadoDaOrdem(
        StatusOrdemDeServico Status,
        bool EstoqueReservado,
        IReadOnlyList<StatusOrdemDeServico> Historico,
        Guid? PagamentoId,
        string? LinkPagamento,
        DateTime? DataConclusao,
        string? MotivoCancelamento,
        uint Version);

    private async Task<(OrdemDeServico Ordem, IReadOnlyList<Peca> Pecas)> SemearOrdemAsync(params (int Estoque, int Quantidade)[] itens)
    {
        var cliente = new ClienteBuilder().Build();
        var pecas = itens.Select(i => new PecaBuilder().ComEstoque(i.Estoque).Build()).ToList();
        var ordem = new OrdemDeServicoBuilder().DoCliente(cliente.Id).Recebida();
        ordem.AdicionarServico(Guid.NewGuid(), "Troca de Óleo", 100m);
        for (var i = 0; i < pecas.Count; i++)
            ordem.AdicionarPeca(pecas[i].Id, pecas[i].Nome, pecas[i].PrecoUnitario, itens[i].Quantidade);

        await _factory.SemearAsync(new object[] { cliente, ordem }.Concat(pecas).ToArray());
        return (ordem, pecas);
    }

    private async Task<T> Enviar<T>(IRequest<T> comando)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(comando);
    }

    private Task<EstadoDaOrdem> LerOrdem(Guid ordemId) =>
        _factory.ConsultarAsync(async db =>
        {
            var o = await db.OrdensDeServico.AsNoTracking().Include(x => x.HistoricoStatus).SingleAsync(x => x.Id == ordemId);
            return new EstadoDaOrdem(
                o.Status, o.EstoqueReservado, o.HistoricoStatus.OrderBy(h => h.DataAlteracao).Select(h => h.Status).ToList(),
                o.PagamentoId, o.LinkPagamento, o.DataConclusao, o.MotivoCancelamento, o.Version);
        });

    private Task<int[]> LerEstoques(IEnumerable<Peca> pecas)
    {
        var ids = pecas.Select(p => p.Id).ToList();
        return _factory.ConsultarAsync(async db =>
        {
            var estoques = await db.Pecas.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.QuantidadeEstoque);
            return ids.Select(id => estoques[id]).ToArray();
        });
    }

    private async Task<ResultadoDaReservaDeEstoque> ExecutarFluxoCompleto(Guid ordemId, Guid pagamentoId)
    {
        await Enviar(new MarcarDiagnosticoIniciadoCommand(ordemId));
        await Enviar(new RegistrarOrcamentoGeradoCommand(ordemId));
        var reserva = await Enviar(new ReservarEstoqueCommand(ordemId));
        await Enviar(new AplicarAprovacaoDoOrcamentoCommand(ordemId));
        await Enviar(new RegistrarPagamentoSolicitadoCommand(ordemId, pagamentoId, "https://pagamento.exemplo/checkout/1", Prazo));
        await Enviar(new IniciarExecucaoDaOrdemCommand(ordemId));
        await Enviar(new FinalizarOrdemCommand(ordemId));
        return reserva;
    }

    [Fact]
    public async Task FluxoCompleto_DeveLevarARecebidaAteFinalizadaBaixandoOEstoque()
    {
        var (ordem, pecas) = await SemearOrdemAsync((10, 3), (5, 2));
        var pagamentoId = Guid.NewGuid();

        var reserva = await ExecutarFluxoCompleto(ordem.Id, pagamentoId);

        reserva.Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        var estado = await LerOrdem(ordem.Id);
        estado.Status.Should().Be(StatusOrdemDeServico.Finalizada);
        estado.EstoqueReservado.Should().BeTrue();
        estado.PagamentoId.Should().Be(pagamentoId);
        estado.LinkPagamento.Should().Be("https://pagamento.exemplo/checkout/1");
        estado.DataConclusao.Should().NotBeNull();
        estado.Historico.Should().Equal(
            StatusOrdemDeServico.Recebida,
            StatusOrdemDeServico.EmDiagnostico,
            StatusOrdemDeServico.AguardandoAprovacao,
            StatusOrdemDeServico.AguardandoPagamento,
            StatusOrdemDeServico.EmExecucao,
            StatusOrdemDeServico.Finalizada);
        (await LerEstoques(pecas)).Should().Equal(7, 3);
    }

    [Fact]
    public async Task FluxoCompleto_ExecutadoDuasVezes_NaoDeveMudarNadaNaSegundaExecucao()
    {
        var (ordem, pecas) = await SemearOrdemAsync((10, 3), (5, 2));
        var pagamentoId = Guid.NewGuid();
        var primeira = await ExecutarFluxoCompleto(ordem.Id, pagamentoId);
        var estadoAposPrimeira = await LerOrdem(ordem.Id);
        var estoqueAposPrimeira = await LerEstoques(pecas);

        var segunda = await ExecutarFluxoCompleto(ordem.Id, pagamentoId);

        primeira.Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        segunda.Should().Be(ResultadoDaReservaDeEstoque.JaReservado);
        (await LerOrdem(ordem.Id)).Should().BeEquivalentTo(estadoAposPrimeira);
        (await LerEstoques(pecas)).Should().Equal(estoqueAposPrimeira);
    }

    [Fact]
    public async Task ReservaComEstoqueInsuficiente_SeguidaDeCancelamentoNoMesmoEscopo_NaoDevePersistirBaixaParcial()
    {
        var (ordem, pecas) = await SemearOrdemAsync((10, 3), (1, 2));
        await Enviar(new MarcarDiagnosticoIniciadoCommand(ordem.Id));
        await Enviar(new RegistrarOrcamentoGeradoCommand(ordem.Id));

        // Mesmo escopo (mesmo DbContext) nos dois comandos, como a saga fara: se a reserva tivesse baixado a
        // primeira peca antes de descobrir a falta da segunda, o SaveChanges do cancelamento a gravaria.
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var reserva = await mediator.Send(new ReservarEstoqueCommand(ordem.Id));
            reserva.Should().Be(ResultadoDaReservaDeEstoque.EstoqueInsuficiente);
            await mediator.Send(new CancelarOrdemPelaSagaCommand(ordem.Id, "Estoque insuficiente para a OS."));
        }

        await Enviar(new LiberarEstoqueCommand(ordem.Id));
        await Enviar(new LiberarEstoqueCommand(ordem.Id));

        var estado = await LerOrdem(ordem.Id);
        estado.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        estado.EstoqueReservado.Should().BeFalse();
        estado.MotivoCancelamento.Should().Be("Estoque insuficiente para a OS.");
        (await LerEstoques(pecas)).Should().Equal(10, 1);
    }

    [Fact]
    public async Task ReservaSeguidaDeCancelamento_LiberacaoRepetida_NaoDevolveEstoqueEmDobro()
    {
        var (ordem, pecas) = await SemearOrdemAsync((10, 3), (5, 2));
        await Enviar(new MarcarDiagnosticoIniciadoCommand(ordem.Id));
        await Enviar(new RegistrarOrcamentoGeradoCommand(ordem.Id));
        (await Enviar(new ReservarEstoqueCommand(ordem.Id))).Should().Be(ResultadoDaReservaDeEstoque.Reservado);
        await Enviar(new AplicarAprovacaoDoOrcamentoCommand(ordem.Id));
        (await LerEstoques(pecas)).Should().Equal(7, 3);
        (await LerOrdem(ordem.Id)).EstoqueReservado.Should().BeTrue();

        await Enviar(new CancelarOrdemPelaSagaCommand(ordem.Id, "Pagamento não realizado no prazo."));
        await Enviar(new CancelarOrdemPelaSagaCommand(ordem.Id, "Pagamento não realizado no prazo."));
        await Enviar(new LiberarEstoqueCommand(ordem.Id));
        var aposPrimeiraLiberacao = await LerOrdem(ordem.Id);
        await Enviar(new LiberarEstoqueCommand(ordem.Id));

        (await LerEstoques(pecas)).Should().Equal(10, 5);
        var estado = await LerOrdem(ordem.Id);
        estado.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        estado.EstoqueReservado.Should().BeFalse();
        estado.Should().BeEquivalentTo(aposPrimeiraLiberacao);
        estado.Historico.Count(s => s == StatusOrdemDeServico.Cancelada).Should().Be(1);
    }

    [Fact]
    public async Task AplicarRecusaDoOrcamento_ExecutadaDuasVezes_DeveRegistrarUmUnicoHistoricoDeRecusa()
    {
        var (ordem, _) = await SemearOrdemAsync();
        await Enviar(new MarcarDiagnosticoIniciadoCommand(ordem.Id));
        await Enviar(new RegistrarOrcamentoGeradoCommand(ordem.Id));

        await Enviar(new AplicarRecusaDoOrcamentoCommand(ordem.Id, "Valor acima do esperado"));
        var depoisDaPrimeira = await LerOrdem(ordem.Id);
        await Enviar(new AplicarRecusaDoOrcamentoCommand(ordem.Id, "Valor acima do esperado"));
        var depoisDaSegunda = await LerOrdem(ordem.Id);

        depoisDaPrimeira.Status.Should().Be(StatusOrdemDeServico.OrcamentoRecusado);
        depoisDaSegunda.Should().BeEquivalentTo(depoisDaPrimeira);
        depoisDaSegunda.Historico.Count(h => h == StatusOrdemDeServico.OrcamentoRecusado).Should().Be(1);
    }

    [Fact]
    public async Task CancelarOrdemPelaSaga_ComMotivoLongo_DeveCancelarTruncandoEmVezDeRejeitar()
    {
        var (ordem, _) = await SemearOrdemAsync();

        await Enviar(new CancelarOrdemPelaSagaCommand(ordem.Id, new string('m', 1000)));

        var estado = await LerOrdem(ordem.Id);
        estado.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        estado.MotivoCancelamento.Should().Be(new string('m', OrdemDeServico.TamanhoMaximoDoMotivo));
    }

    [Fact]
    public async Task CancelarOrdemPelaSaga_SemMotivo_DeveCancelarComOMotivoPadrao()
    {
        var (ordem, _) = await SemearOrdemAsync();

        await Enviar(new CancelarOrdemPelaSagaCommand(ordem.Id, null));

        var estado = await LerOrdem(ordem.Id);
        estado.Status.Should().Be(StatusOrdemDeServico.Cancelada);
        estado.MotivoCancelamento.Should().Be(CancelarOrdemPelaSagaCommandHandler.MotivoPadrao);
    }

    [Fact]
    public async Task ComandoComOrdemVazia_DeveSerRejeitadoPeloValidationBehavior()
    {
        var act = () => Enviar(new ReservarEstoqueCommand(Guid.Empty));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RegistrarPagamentoSolicitado_ComLinkQueNaoEhHttp_DeveSerRejeitadoESemMudarAOrdem()
    {
        var (ordem, _) = await SemearOrdemAsync();
        await Enviar(new MarcarDiagnosticoIniciadoCommand(ordem.Id));
        await Enviar(new RegistrarOrcamentoGeradoCommand(ordem.Id));
        await Enviar(new AplicarAprovacaoDoOrcamentoCommand(ordem.Id));
        var antes = await LerOrdem(ordem.Id);

        var act = () => Enviar(new RegistrarPagamentoSolicitadoCommand(ordem.Id, Guid.NewGuid(), "javascript:alert(1)", Prazo));

        await act.Should().ThrowAsync<ValidationException>();
        (await LerOrdem(ordem.Id)).Should().BeEquivalentTo(antes);
        antes.PagamentoId.Should().BeNull();
    }
}
