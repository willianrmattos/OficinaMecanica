using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Tests.Common.Builders;

public class OrdemDeServicoBuilder
{
    private Guid _clienteId = Guid.NewGuid();
    private Guid _veiculoId = Guid.NewGuid();
    private string? _observacoes = null;

    public OrdemDeServicoBuilder DoCliente(Guid clienteId) { _clienteId = clienteId; return this; }
    public OrdemDeServicoBuilder DoVeiculo(Guid veiculoId) { _veiculoId = veiculoId; return this; }
    public OrdemDeServicoBuilder ComObservacoes(string obs) { _observacoes = obs; return this; }

    public OrdemDeServico Recebida() => new(_clienteId, _veiculoId, _observacoes);

    public OrdemDeServico EmDiagnostico()
    {
        var o = Recebida();
        o.AvancarParaDiagnostico();
        return o;
    }

    public OrdemDeServico AguardandoAprovacao(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = EmDiagnostico();
        o.AdicionarServico(Guid.NewGuid(), nomeServico, preco);
        o.EnviarParaAprovacao();
        return o;
    }

    public OrdemDeServico EmExecucao(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = AguardandoAprovacao(nomeServico, preco);
        o.AprovarOrcamento();
        return o;
    }

    public OrdemDeServico OrcamentoRecusado(string nomeServico = "Troca de Óleo", decimal preco = 100m, string? motivo = null)
    {
        var o = AguardandoAprovacao(nomeServico, preco);
        o.RecusarOrcamento(motivo);
        return o;
    }

    public OrdemDeServico Finalizada(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = EmExecucao(nomeServico, preco);
        o.Finalizar();
        return o;
    }

    public OrdemDeServico Entregue(string nomeServico = "Troca de Óleo", decimal preco = 100m)
    {
        var o = Finalizada(nomeServico, preco);
        o.Entregar();
        return o;
    }
}
