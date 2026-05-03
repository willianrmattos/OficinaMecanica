using FluentAssertions;
using OficinaMecanica.Application.Queries.ObterCliente;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Interfaces;
using OficinaMecanica.Domain.ValueObjects;

namespace OficinaMecanica.Application.Tests.Queries;

public class ObterClientePorDocumentoQueryHandlerTests
{
    private readonly Mock<IClienteRepository> _clienteRepositoryMock = new();
    private readonly ObterClientePorDocumentoQueryHandler _handler;

    public ObterClientePorDocumentoQueryHandlerTests()
    {
        _handler = new ObterClientePorDocumentoQueryHandler(_clienteRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_CpfValidoEncontrado_DeveRetornarClienteDto()
    {
        var cliente = new ClienteBuilder().ComNome("João Silva").ComDocumento("52998224725").ComEmail("joao@email.com").SemTelefone().Build();
        _clienteRepositoryMock
            .Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var result = await _handler.Handle(new ObterClientePorDocumentoQuery("52998224725"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Nome.Should().Be("João Silva");
        result.Documento.Should().Be("529.982.247-25");
        result.TipoDocumento.Should().Be("CPF");
    }

    [Fact]
    public async Task Handle_CnpjValidoEncontrado_DeveRetornarClienteDto()
    {
        var cliente = new ClienteBuilder().ComNome("Empresa Ltda").ComDocumento("11222333000181").SemEmail().SemTelefone().Build();
        _clienteRepositoryMock
            .Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cliente);

        var result = await _handler.Handle(new ObterClientePorDocumentoQuery("11222333000181"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.TipoDocumento.Should().Be("CNPJ");
    }

    [Fact]
    public async Task Handle_DocumentoValidoNaoEncontrado_DeveRetornarNull()
    {
        _clienteRepositoryMock
            .Setup(r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cliente?)null);

        var result = await _handler.Handle(new ObterClientePorDocumentoQuery("52998224725"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DocumentoInvalido_DeveRetornarNullSemChamarRepositorio()
    {
        var result = await _handler.Handle(new ObterClientePorDocumentoQuery("00000000000"), CancellationToken.None);

        result.Should().BeNull();
        _clienteRepositoryMock.Verify(
            r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Handle_NumeroVazio_DeveRetornarNullSemChamarRepositorio()
    {
        var result = await _handler.Handle(new ObterClientePorDocumentoQuery(""), CancellationToken.None);

        result.Should().BeNull();
        _clienteRepositoryMock.Verify(
            r => r.ObterPorDocumentoAsync(It.IsAny<Documento>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
