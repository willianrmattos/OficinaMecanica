using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.Contracts.Events;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Integration.Tests.Support;

namespace OficinaMecanica.Integration.Tests.Controllers;

public class CriarOrdemDeServicoTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CriarOrdemDeServicoTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private void Autenticar() =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokenFactory.GerarToken(_factory));

    [Fact]
    public async Task Criar_SemToken_DeveRetornar401()
    {
        var response = await _client.PostAsJsonAsync("/api/ordens-de-servico", new { ClienteId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Criar_ComDadosValidos_DeveRetornar201PublicarOrdemAbertaEManterOEstoque()
    {
        Autenticar();
        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).ComPlaca("ABC1D23").ComMarca("Honda").ComModelo("Civic").Build();
        var servico = new ServicoBuilder().ComNome("Alinhamento").ComPreco(120m).Build();
        var peca = new PecaBuilder().ComNome("Filtro de óleo").ComPreco(45m).ComEstoque(10).Build();
        await _factory.SemearAsync(cliente, veiculo, servico, peca);

        var response = await _client.PostAsJsonAsync("/api/ordens-de-servico", new
        {
            ClienteId = cliente.Id,
            VeiculoId = veiculo.Id,
            Observacoes = "Barulho na roda",
            Servicos = new[] { new { ServicoId = servico.Id, Quantidade = 1 } },
            Pecas = new[] { new { PecaId = peca.Id, Quantidade = 2 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var corpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ordemId = corpo.GetProperty("id").GetGuid();
        corpo.GetProperty("status").GetString().Should().Be("Recebida");

        var mensagem = _factory.Publicador.Publicadas<OrdemDeServicoAberta>().Should().ContainSingle(m => m.OrdemDeServicoId == ordemId).Subject;
        mensagem.Numero.Should().Be(corpo.GetProperty("numero").GetString());
        mensagem.Placa.Should().Be("ABC1D23");
        mensagem.ValorTotal.Should().Be(120m + 2 * 45m);
        mensagem.Servicos.Should().ContainSingle().Which.Nome.Should().Be("Alinhamento");
        mensagem.Pecas.Should().ContainSingle().Which.Quantidade.Should().Be(2);

        var persistida = await _factory.ConsultarAsync(db => db.OrdensDeServico.SingleAsync(o => o.Id == ordemId));
        persistida.ParticipaDaSaga.Should().BeTrue();
        persistida.EstoqueReservado.Should().BeFalse();
        var estoque = await _factory.ConsultarAsync(db => db.Pecas.Where(p => p.Id == peca.Id).Select(p => p.QuantidadeEstoque).SingleAsync());
        estoque.Should().Be(10);
    }

    [Fact]
    public async Task Criar_ComEstoqueInsuficiente_DeveRetornar400ENaoPublicar()
    {
        Autenticar();
        var cliente = new ClienteBuilder().Build();
        var veiculo = new VeiculoBuilder().DoCliente(cliente.Id).Build();
        var servico = new ServicoBuilder().Build();
        var peca = new PecaBuilder().ComEstoque(1).Build();
        await _factory.SemearAsync(cliente, veiculo, servico, peca);

        var response = await _client.PostAsJsonAsync("/api/ordens-de-servico", new
        {
            ClienteId = cliente.Id,
            VeiculoId = veiculo.Id,
            Servicos = new[] { new { ServicoId = servico.Id, Quantidade = 1 } },
            Pecas = new[] { new { PecaId = peca.Id, Quantidade = 5 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Publicador.Publicadas<OrdemDeServicoAberta>().Should().NotContain(m => m.ClienteId == cliente.Id);
    }
}
