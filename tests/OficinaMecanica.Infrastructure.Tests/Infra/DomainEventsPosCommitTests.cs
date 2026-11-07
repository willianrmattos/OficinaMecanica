using FluentAssertions;
using MediatR;
using Microsoft.Data.SqlClient;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Events;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Infrastructure.Data;
using OficinaMecanica.Infrastructure.Messaging;
using OficinaMecanica.Infrastructure.Repositories;
using OficinaMecanica.Infrastructure.Tests.Support;

namespace OficinaMecanica.Infrastructure.Tests.Infra;

// Os domain events (e-mail e metricas) so podem sair depois que a transacao do chamador for confirmada: um
// consumer que repete ou uma transacao desfeita nao pode deixar e-mail para um estado que nao existiu.
[Collection(InfraCollection.Nome)]
[Trait("Category", "Infra")]
public class DomainEventsPosCommitTests
{
    private const string Fila = "os-f3a-diagnostico-pos-commit";
    private static readonly TimeSpan JanelaSemEmails = TimeSpan.FromSeconds(2);

    private readonly InfraFixture _infra;

    public DomainEventsPosCommitTests(InfraFixture infra) => _infra = infra;

    private Task<AmbienteDeInfra> CriarAmbienteAsync(Action<IServiceCollection>? ajustarServicos = null) =>
        AmbienteDeInfra.CriarAsync(
            _infra,
            x => x.AddConsumer<ConsumerDeDiagnostico>(),
            (contexto, cfg) => cfg.ReceiveEndpoint(Fila, endpoint =>
            {
                endpoint.ConfigurarRetryEOutbox(contexto);
                endpoint.ConfigureConsumer<ConsumerDeDiagnostico>(contexto);
            }),
            ajustarServicos);

    private static async Task<OrdemDeServico> SemearOrdemRecebidaAsync(AmbienteDeInfra ambiente)
    {
        var cliente = new ClienteBuilder().ComEmail("cliente@oficina.teste").Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var ordem = new OrdemDeServicoBuilder().DoCliente(cliente.Id).DoVeiculo(veiculo.Id).Recebida();
        await ambiente.SemearAsync(cliente, veiculo, ordem);
        return ordem;
    }

    // Faz o que o consumer da saga faz: abre a transacao do chamador (a do MassTransit num consumer), muda o status
    // da OS pelo repositorio real e salva. O teste decide como a transacao termina. A estrategia de execucao envolve
    // tudo porque, fora de consumer, o retry do EF esta ativo e exige isso.
    private static async Task AvancarDentroDeTransacaoAsync(AmbienteDeInfra ambiente, Guid ordemId, Func<IDbContextTransaction, Task> terminar)
    {
        await using var escopo = ambiente.CriarEscopo();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>();

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transacao = await db.Database.BeginTransactionAsync();

            var ordem = await repositorio.ObterPorIdAsync(ordemId);
            ordem!.AvancarParaDiagnostico();
            await db.SaveChangesAsync();

            await terminar(transacao);
        });
    }

    [Fact]
    public async Task TransacaoDoChamador_DeveDespacharOEmailSoDepoisDoCommit()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await AvancarDentroDeTransacaoAsync(ambiente, ordem.Id, async transacao =>
        {
            ambiente.Emails.Enviados.Should().BeEmpty("a transacao ainda pode ser desfeita");

            await transacao.CommitAsync();
        });

        var email = ambiente.Emails.Enviados.Should().ContainSingle().Subject;
        email.Destinatario.Should().Be("cliente@oficina.teste");
        email.Corpo.Should().Contain("Em diagnóstico");
    }

    [Fact]
    public async Task TransacaoDoChamadorDesfeita_NaoDeveDespacharEmail()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await AvancarDentroDeTransacaoAsync(ambiente, ordem.Id, transacao => transacao.RollbackAsync());

        await Task.Delay(JanelaSemEmails);
        ambiente.Emails.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task TransacaoDoChamadorDesfeitaDeFormaSincrona_NaoDeveDespacharEmail()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await AvancarDentroDeTransacaoAsync(ambiente, ordem.Id, transacao =>
        {
            transacao.Rollback();
            return Task.CompletedTask;
        });

        await Task.Delay(JanelaSemEmails);
        ambiente.Emails.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task TransacaoDoChamadorConfirmadaDeFormaSincrona_DeveDespacharOEmail()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await AvancarDentroDeTransacaoAsync(ambiente, ordem.Id, transacao =>
        {
            transacao.Commit();
            return Task.CompletedTask;
        });

        ambiente.Emails.Enviados.Should().ContainSingle();
    }

    [Fact]
    public async Task HandlerQueEscreveNoBanco_DeveFuncionarNoDespachoPosCommit()
    {
        await using var ambiente = await CriarAmbienteAsync(services =>
            services.AddTransient<INotificationHandler<StatusOrdemAlteradoEvent>, GravadorDeServicoNoPosCommitHandler>());
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await AvancarDentroDeTransacaoAsync(ambiente, ordem.Id, transacao => transacao.CommitAsync());

        var gravados = await ambiente.ConsultarAsync(db => db.Servicos.Where(s => s.Nome.StartsWith(GravadorDeServicoNoPosCommitHandler.PrefixoDoNome)).ToListAsync());
        gravados.Should().ContainSingle("o handler escreveu no banco no escopo novo, depois do commit");
        ambiente.Logs.Registros.Should().NotContain(r => r.Nivel == LogLevel.Error, "no escopo original o save do handler falharia por causa da transacao ainda aberta");
    }

    [Fact]
    public async Task RollbackSeguidoDeCommitNoMesmoDbContext_NaoDeveVazarOsEventosDoRollback()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var cliente = new ClienteBuilder().ComEmail("cliente@oficina.teste").Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var desfeita = new OrdemDeServicoBuilder().DoCliente(cliente.Id).DoVeiculo(veiculo.Id).Recebida();
        var confirmada = new OrdemDeServicoBuilder().DoCliente(cliente.Id).DoVeiculo(veiculo.Id).Recebida();
        await ambiente.SemearAsync(cliente, veiculo, desfeita, confirmada);

        await using (var escopo = ambiente.CriarEscopo())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var repositorio = escopo.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>();

            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using (var primeira = await db.Database.BeginTransactionAsync())
                {
                    (await repositorio.ObterPorIdAsync(desfeita.Id))!.AvancarParaDiagnostico();
                    await db.SaveChangesAsync();
                    await primeira.RollbackAsync();
                }

                await using (var segunda = await db.Database.BeginTransactionAsync())
                {
                    (await repositorio.ObterPorIdAsync(confirmada.Id))!.AvancarParaDiagnostico();
                    await db.SaveChangesAsync();
                    await segunda.CommitAsync();
                }
            });
        }

        var email = ambiente.Emails.Enviados.Should().ContainSingle("so o evento da transacao confirmada pode sair").Subject;
        email.Assunto.Should().Contain(confirmada.Numero).And.NotContain(desfeita.Numero);
    }

    [Fact]
    public async Task RollbackSincronoSeguidoDeCommitSincronoNoMesmoDbContext_NaoDeveVazarOsEventosDoRollback()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var cliente = new ClienteBuilder().ComEmail("cliente@oficina.teste").Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var desfeita = new OrdemDeServicoBuilder().DoCliente(cliente.Id).DoVeiculo(veiculo.Id).Recebida();
        var confirmada = new OrdemDeServicoBuilder().DoCliente(cliente.Id).DoVeiculo(veiculo.Id).Recebida();
        await ambiente.SemearAsync(cliente, veiculo, desfeita, confirmada);

        await using (var escopo = ambiente.CriarEscopo())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var repositorio = escopo.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>();

            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                using (var primeira = db.Database.BeginTransaction())
                {
                    (await repositorio.ObterPorIdAsync(desfeita.Id))!.AvancarParaDiagnostico();
                    await db.SaveChangesAsync();
                    primeira.Rollback();
                }

                using (var segunda = db.Database.BeginTransaction())
                {
                    (await repositorio.ObterPorIdAsync(confirmada.Id))!.AvancarParaDiagnostico();
                    await db.SaveChangesAsync();
                    segunda.Commit();
                }
            });
        }

        var email = ambiente.Emails.Enviados.Should().ContainSingle("so o evento da transacao confirmada pode sair").Subject;
        email.Assunto.Should().Contain(confirmada.Numero).And.NotContain(desfeita.Numero);
    }

    [Fact]
    public async Task CommitSincronoQueFalha_DeveDescartarOsEventosGuardados()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        // Mesmo contexto sem retry do teste async: o interesse aqui e o TransactionFailed sincrono do Commit().
        await using var escopo = ambiente.CriarEscopo();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ambiente.ConnectionString)
            .AddInterceptors(ambiente.Servicos.GetRequiredService<DomainEventsPosCommitInterceptor>())
            .Options;
        await using var db = new AppDbContext(opcoes, escopo.ServiceProvider.GetRequiredService<IMediator>());
        var repositorio = new OrdemDeServicoRepository(db);

        using var transacao = db.Database.BeginTransaction();
        (await repositorio.ObterPorIdAsync(ordem.Id))!.AvancarParaDiagnostico();
        await db.SaveChangesAsync();
        await MatarSessaoAsync(ambiente.ConnectionString, db);

        var commit = () => transacao.Commit();

        commit.Should().Throw<Exception>("a sessao foi morta antes do commit");
        db.RetirarEventosAguardandoCommit().Should().BeEmpty("o commit falhou e os eventos da transacao foram descartados");
        ambiente.Emails.Enviados.Should().BeEmpty();
    }

    private static async Task MatarSessaoAsync(string connectionString, AppDbContext db)
    {
        var spid = (await db.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS [Value]").ToListAsync()).Single();
        await using var outraConexao = new SqlConnection(connectionString);
        await outraConexao.OpenAsync();
        await using var comando = outraConexao.CreateCommand();
        comando.CommandText = $"KILL {spid}";
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task CommitQueFalha_DeveDescartarOsEventosGuardados()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        // Contexto sem retry (como o de um consumer) criado a mao: o retry do EF repetiria a operacao inteira
        // quando a conexao morresse, e aqui o que interessa e o evento TransactionFailed do commit.
        await using var escopo = ambiente.CriarEscopo();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ambiente.ConnectionString)
            .AddInterceptors(ambiente.Servicos.GetRequiredService<DomainEventsPosCommitInterceptor>())
            .Options;
        await using var db = new AppDbContext(opcoes, escopo.ServiceProvider.GetRequiredService<IMediator>());
        var repositorio = new OrdemDeServicoRepository(db);

        await using var transacao = await db.Database.BeginTransactionAsync();
        (await repositorio.ObterPorIdAsync(ordem.Id))!.AvancarParaDiagnostico();
        await db.SaveChangesAsync();
        var spid = (await db.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS [Value]").ToListAsync()).Single();
        await using (var outraConexao = new SqlConnection(ambiente.ConnectionString))
        {
            await outraConexao.OpenAsync();
            await using var comando = outraConexao.CreateCommand();
            comando.CommandText = $"KILL {spid}";
            await comando.ExecuteNonQueryAsync();
        }

        var commit = () => transacao.CommitAsync();

        await commit.Should().ThrowAsync<Exception>("a sessao foi morta antes do commit");
        db.RetirarEventosAguardandoCommit().Should().BeEmpty("o commit falhou e os eventos da transacao foram descartados");
        ambiente.Emails.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task SemTransacaoDoChamador_DeveDespacharOEmailLogoAposOSaveChanges()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await using (var escopo = ambiente.CriarEscopo())
        {
            var repositorio = escopo.ServiceProvider.GetRequiredService<IOrdemDeServicoRepository>();

            var ordemDoBanco = await repositorio.ObterPorIdAsync(ordem.Id);
            ordemDoBanco!.AvancarParaDiagnostico();
            await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

            ambiente.Emails.Enviados.Should().ContainSingle("fora de uma transacao do chamador o comportamento e o de sempre");
        }
    }

    [Fact]
    public async Task HandlerQueFalhaDepoisDoCommit_NaoDeveDesfazerATransacao()
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Emails.FalharAoEnviar = true;
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await AvancarDentroDeTransacaoAsync(ambiente, ordem.Id, transacao => transacao.CommitAsync());

        var status = await ambiente.ConsultarAsync(db => db.OrdensDeServico.Where(o => o.Id == ordem.Id).Select(o => o.Status).SingleAsync());
        status.Should().Be(StatusOrdemDeServico.EmDiagnostico);
        ambiente.Logs.Registros.Should().Contain(r => r.Nivel == LogLevel.Error && r.Excecao is FalhaTecnicaSimuladaException);
    }

    [Fact]
    public async Task ConsumerRepetido_NaoDeveGerarEmailFantasmaPorTentativa()
    {
        await using var ambiente = await CriarAmbienteAsync();
        ambiente.Consumo.TentativasQueFalhamDepoisDeEscrever = 2;
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), ordem.Id));

        (await Esperar.AteAsync(() => ambiente.Consumo.Consumos.Count == 3)).Should().BeTrue("duas tentativas desfeitas e uma que confirma");
        (await Esperar.AteAsync(() => ambiente.Emails.Enviados.Count >= 1)).Should().BeTrue("a tentativa que confirmou despacha o e-mail");
        await Task.Delay(JanelaSemEmails);

        ambiente.Emails.Enviados.Should().ContainSingle("so a tentativa confirmada tem efeito; as duas desfeitas nao mandam e-mail");
    }

    [Fact]
    public async Task ConsumerSemFalha_DeveEnviarOEmailUmaVezAposOCommitDoMassTransit()
    {
        await using var ambiente = await CriarAmbienteAsync();
        var ordem = await SemearOrdemRecebidaAsync(ambiente);

        await ambiente.Servicos.GetRequiredService<IBus>().Publish(new MensagemDeTeste(Guid.NewGuid(), ordem.Id));

        (await Esperar.AteAsync(() => ambiente.Emails.Enviados.Count >= 1)).Should().BeTrue();
        await Task.Delay(JanelaSemEmails);
        ambiente.Emails.Enviados.Should().ContainSingle();
        ambiente.Consumo.Consumos.Should().ContainSingle().Which.TransacaoAberta.Should().BeTrue();
    }
}
