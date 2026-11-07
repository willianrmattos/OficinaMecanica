using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OficinaMecanica.Application.Commands.CriarOrdemDeServico;
using OficinaMecanica.Application.Interfaces;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// O command handler publica ANTES do SaveChanges: com o bus outbox a mensagem so existe se o commit existir.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class OutboxAtomicoNoCaminhoHttpTests
{
    private const string Fila = "os-f3a-ordem-aberta";
    private static readonly TimeSpan JanelaSemEntrega = TimeSpan.FromSeconds(3);

    private readonly InfraFixture _infra;

    public OutboxAtomicoNoCaminhoHttpTests(InfraFixture infra) => _infra = infra;

    private Task<AmbienteDeInfra> CriarAmbienteAsync() =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            x => x.AddConsumer<ConsumerDeOrdemAberta>(),
            (contexto, cfg) => cfg.ReceiveEndpoint(Fila, endpoint =>
            {
                endpoint.ConfigurarRetryEOutbox(contexto);
                endpoint.ConfigureConsumer<ConsumerDeOrdemAberta>(contexto);
            }));

    private static async Task<(Cliente Cliente, Veiculo Veiculo, Servico Servico)> SemearCadastroAsync(AmbienteDeInfra ambiente)
    {
        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var servico = new ServicoBuilder().Build();
        await ambiente.SemearAsync(cliente, veiculo, servico);
        return (cliente, veiculo, servico);
    }

    private static CriarOrdemDeServicoCommand ComandoDeCriacao(Cliente cliente, Veiculo veiculo, Servico servico) =>
        new(cliente.Id, veiculo.Id, "Barulho na roda", new List<ItemServicoInput> { new(servico.Id) }, null);

    [Fact]
    public async Task CriarOrdem_DeveEntregarOrdemAbertaAposOCommit()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var (cliente, veiculo, servico) = await SemearCadastroAsync(ambiente);

        await using (var escopo = ambiente.CriarEscopo())
        {
            var dto = await escopo.ServiceProvider.GetRequiredService<IMediator>().Send(ComandoDeCriacao(cliente, veiculo, servico));

            (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count == 1)).Should().BeTrue("o outbox entrega a mensagem depois do commit");
            ambiente.Consumo.Consumos.Single().OrdemDeServicoId.Should().Be(dto.Id);
        }

        (await ambiente.ConsultarAsync(db => db.OrdensDeServico.CountAsync())).Should().Be(1);
        (await Esperar.AteAsync(async () => await ambiente.ConsultarAsync(db => db.Set<OutboxMessage>().CountAsync()) == 0))
            .Should().BeTrue("o servico de entrega apaga do outbox o que ja foi para o broker");
    }

    [Fact]
    public async Task CriarOrdem_ComFalhaNoSave_NaoDeveEntregarNemGravarNada()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var (cliente, veiculo, servico) = await SemearCadastroAsync(ambiente);
        await ExecutarSqlAsync(ambiente.ConnectionString,
            "CREATE TRIGGER trg_falha_ao_inserir_os ON OrdensDeServico AFTER INSERT AS THROW 50000, 'falha forcada pelo teste', 1;");

        await using (var escopo = ambiente.CriarEscopo())
        {
            var act = () => escopo.ServiceProvider.GetRequiredService<IMediator>().Send(ComandoDeCriacao(cliente, veiculo, servico));

            await act.Should().ThrowAsync<DbUpdateException>();
        }

        await Task.Delay(JanelaSemEntrega);
        ambiente.Consumo.Consumos.Should().BeEmpty("a mensagem foi publicada antes do save e voltou junto com o rollback");
        (await ambiente.ConsultarAsync(db => db.Set<OutboxMessage>().CountAsync())).Should().Be(0);
        (await ambiente.ConsultarAsync(db => db.OrdensDeServico.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task ControleNegativo_PublicarDepoisDoSave_NaoPersisteNemEntrega()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var (cliente, veiculo, _) = await SemearCadastroAsync(ambiente);

        await using (var escopo = ambiente.CriarEscopo())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new ServicoBuilder().Build());
            await db.SaveChangesAsync();

            // Ordem errada de proposito: o commit ja aconteceu e nada grava esta mensagem no outbox.
            await escopo.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>()
                .PublishAsync(MensagemDeAbertura(cliente, veiculo));
        }

        await Task.Delay(JanelaSemEntrega);
        ambiente.Consumo.Consumos.Should().BeEmpty();
        (await ambiente.ConsultarAsync(db => db.Set<OutboxMessage>().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task CriarOrdem_MessageIdDoContratoDeveSerOMessageIdDoEnvelopeEntregue()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var (cliente, veiculo, _) = await SemearCadastroAsync(ambiente);
        var mensagem = MensagemDeAbertura(cliente, veiculo);

        await using (var escopo = ambiente.CriarEscopo())
        {
            await escopo.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().PublishAsync(mensagem);
            await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count == 1)).Should().BeTrue();
        var consumo = ambiente.Consumo.Consumos.Single();
        consumo.MessageIdDoEnvelope.Should().Be(mensagem.MessageId);
        consumo.MessageIdDoContrato.Should().Be(mensagem.MessageId);
    }

    private static OrdemDeServicoAberta MensagemDeAbertura(Cliente cliente, Veiculo veiculo) =>
        new OrdemDeServicoAbertaBuilder().DoCliente(cliente.Id).DoVeiculo(veiculo.Id).ComPlaca(veiculo.Placa.Valor).Build();

    private static async Task ExecutarSqlAsync(string connectionString, string sql)
    {
        await using var conexao = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}
