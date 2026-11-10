using FluentAssertions;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Tests.Saga;

public class IdentificadorDeMensagemTests
{
    private static readonly Guid Os = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Para_MesmaEntrada_DeveDevolverSempreOMesmoId()
    {
        IdentificadorDeMensagem.Para(Os, "SolicitarDiagnostico").Should().Be(IdentificadorDeMensagem.Para(Os, "SolicitarDiagnostico"));
    }

    [Fact]
    public void Para_ValorFixo_DeveSerEstavelEntreProcessosEVersoes()
    {
        // Fixo de proposito: derivar de string.GetHashCode (que muda a cada processo) ou trocar o algoritmo quebraria a
        // deduplicacao de um comando reenviado depois de um deploy.
        IdentificadorDeMensagem.Para(Os, "SolicitarDiagnostico").Should().Be(Guid.Parse("33a84fad-a266-0112-8ce5-fc6bd8345694"));
    }

    [Fact]
    public void Para_OsDiferente_DeveDevolverIdDiferente()
    {
        IdentificadorDeMensagem.Para(Os, "GerarOrcamento").Should().NotBe(IdentificadorDeMensagem.Para(Guid.NewGuid(), "GerarOrcamento"));
    }

    [Fact]
    public void Para_ComandoDiferente_DeveDevolverIdDiferente()
    {
        IdentificadorDeMensagem.Para(Os, "GerarOrcamento").Should().NotBe(IdentificadorDeMensagem.Para(Os, "CancelarOrcamento"));
    }

    [Fact]
    public void Para_DiscriminadorDiferenteOuAusente_DeveDevolverIdDiferente()
    {
        var janela1 = Guid.NewGuid();
        var janela2 = Guid.NewGuid();

        IdentificadorDeMensagem.Para(Os, "CancelarPagamento", janela1).Should().NotBe(IdentificadorDeMensagem.Para(Os, "CancelarPagamento", janela2));
        IdentificadorDeMensagem.Para(Os, "CancelarPagamento", janela1).Should().NotBe(IdentificadorDeMensagem.Para(Os, "CancelarPagamento"));
        IdentificadorDeMensagem.Para(Os, "CancelarPagamento", janela1).Should().Be(IdentificadorDeMensagem.Para(Os, "CancelarPagamento", janela1));
    }

    [Fact]
    public void Para_NuncaDeveDevolverGuidVazio()
    {
        IdentificadorDeMensagem.Para(Guid.Empty, "").Should().NotBe(Guid.Empty);
    }
}
