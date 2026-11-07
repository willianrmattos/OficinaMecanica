using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace OficinaMecanica.Infrastructure.Tests.Support;

[CollectionDefinition(Nome)]
public sealed class InfraCollection : ICollectionFixture<InfraFixture>
{
    public const string Nome = "Infra";
}

// Um SQL Server e um RabbitMQ para todos os testes de infraestrutura (a colecao os roda em sequencia): cada teste
// ganha banco e virtual host proprios, entao compartilhar os containers nao mistura estado e poupa a memoria de uma
// maquina que ja divide o Docker com outros servicos.
public sealed class InfraFixture : IAsyncLifetime
{
    // Credenciais do RabbitMQ efemero do teste: o usuario e fixo e a senha nasce a cada execucao.
    private const string Usuario = "oficina";
    private static readonly string Senha = Guid.NewGuid().ToString("N");

    private readonly MsSqlContainer _sql;
    private readonly RabbitMqContainer _rabbit;

    public InfraFixture()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];

        _sql = new MsSqlBuilder(ImagensDeInfra.SqlServer)
            .WithName($"os-f3a-sql-{sufixo}")
            .WithEnvironment("MSSQL_MEMORY_LIMIT_MB", "1024")
            .Build();

        _rabbit = new RabbitMqBuilder(ImagensDeInfra.RabbitMq)
            .WithName($"os-f3a-rabbit-{sufixo}")
            .WithUsername(Usuario)
            .WithPassword(Senha)
            .Build();
    }

    public string RabbitHost => _rabbit.Hostname;
    public ushort RabbitPort => _rabbit.GetMappedPublicPort(5672);
    public string RabbitUsuario => Usuario;
    public string RabbitSenha => Senha;

    public async Task InitializeAsync() => await Task.WhenAll(_sql.StartAsync(), _rabbit.StartAsync());

    public async Task DisposeAsync()
    {
        await _sql.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    public async Task<string> CriarBancoAsync()
    {
        var nome = $"os_{Guid.NewGuid():N}";

        await using (var conexao = new SqlConnection(_sql.GetConnectionString()))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = $"CREATE DATABASE [{nome}]";
            await comando.ExecuteNonQueryAsync();
        }

        return new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = nome }.ConnectionString;
    }

    public async Task<string> CriarVirtualHostAsync()
    {
        var nome = $"vh_{Guid.NewGuid():N}";

        var criacao = await _rabbit.ExecAsync(new[] { "rabbitmqctl", "add_vhost", nome });
        var permissao = await _rabbit.ExecAsync(new[] { "rabbitmqctl", "set_permissions", "-p", nome, RabbitUsuario, ".*", ".*", ".*" });
        if (criacao.ExitCode != 0 || permissao.ExitCode != 0)
            throw new InvalidOperationException($"Nao consegui criar o virtual host {nome}: {criacao.Stderr} {permissao.Stderr}");

        return nome;
    }

    // Mensagens que o RabbitMQ guarda na fila (inclusive as nao consumidas), lidas pelo rabbitmqctl.
    public async Task<int> MensagensNaFilaAsync(string virtualHost, string fila)
    {
        var resultado = await _rabbit.ExecAsync(new[] { "rabbitmqctl", "list_queues", "-p", virtualHost, "name", "messages", "--formatter", "csv", "--silent" });
        var linha = resultado.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim().Split(','))
            .FirstOrDefault(c => c.Length == 2 && c[0].Trim('"') == fila);

        return linha is null ? 0 : int.Parse(linha[1].Trim('"'));
    }
}
