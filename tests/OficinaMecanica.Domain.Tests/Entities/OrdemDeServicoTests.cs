using FluentAssertions;
using OficinaMecanica.Domain.Enums;
using OficinaMecanica.Domain.Exceptions;

namespace OficinaMecanica.Domain.Tests.Entities;

public class OrdemDeServicoTests
{
    [Fact]
    public void Construtor_DeveIniciarComStatusRecebida()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();

        ordem.Status.Should().Be(StatusOrdemDeServico.Recebida);
        ordem.Numero.Should().StartWith("OS-");
        ordem.HistoricoStatus.Should().HaveCount(1);
        ordem.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void AdicionarServico_StatusRecebida_DeveAdicionar()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();

        ordem.AdicionarServico(Guid.NewGuid(), "Troca de óleo", 150.00m);

        ordem.ItensServico.Should().HaveCount(1);
        ordem.ValorTotal.Should().Be(150.00m);
    }

    [Fact]
    public void AdicionarPeca_StatusRecebida_DeveAdicionar()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();

        ordem.AdicionarPeca(Guid.NewGuid(), "Filtro de óleo", 45.00m, 2);

        ordem.ItensPeca.Should().HaveCount(1);
        ordem.ValorTotal.Should().Be(90.00m);
    }

    [Fact]
    public void FluxoCompleto_DeveTransitarCorretamente()
    {
        var ordem = new OrdemDeServicoBuilder().Entregue();

        ordem.Status.Should().Be(StatusOrdemDeServico.Entregue);
        ordem.DataConclusao.Should().NotBeNull();
        ordem.HistoricoStatus.Should().HaveCount(6);
    }

    [Fact]
    public void AvancarParaDiagnostico_DeStatusInvalido_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().AguardandoAprovacao();

        var act = () => ordem.AvancarParaDiagnostico();

        act.Should().Throw<DomainException>().WithMessage("*Não é possível*");
    }

    [Fact]
    public void EnviarParaAprovacao_SemServicos_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().EmDiagnostico();

        var act = () => ordem.EnviarParaAprovacao();

        act.Should().Throw<DomainException>().WithMessage("*pelo menos um serviço*");
    }

    [Fact]
    public void AdicionarServico_StatusEmExecucao_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().EmExecucao();

        var act = () => ordem.AdicionarServico(Guid.NewGuid(), "Novo Serviço", 200m);

        act.Should().Throw<DomainException>().WithMessage("*Só é possível adicionar*");
    }

    [Fact]
    public void AdicionarServico_Duplicado_DeveLancarExcecao()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        var servicoId = Guid.NewGuid();
        ordem.AdicionarServico(servicoId, "Serviço", 100m);

        var act = () => ordem.AdicionarServico(servicoId, "Serviço", 100m);

        act.Should().Throw<DomainException>().WithMessage("*já foi adicionado*");
    }

    [Fact]
    public void ValorTotal_DeveCalcularCorretamente()
    {
        var ordem = new OrdemDeServicoBuilder().Recebida();
        ordem.AdicionarServico(Guid.NewGuid(), "Troca de óleo", 150.00m, 1);
        ordem.AdicionarServico(Guid.NewGuid(), "Alinhamento", 80.00m, 1);
        ordem.AdicionarPeca(Guid.NewGuid(), "Filtro", 45.00m, 2);

        ordem.ValorTotal.Should().Be(150.00m + 80.00m + 90.00m);
    }
}
