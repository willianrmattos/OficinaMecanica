using System.Reflection;
using OficinaMecanica.Domain.Common;
using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.Enums;

namespace OficinaMecanica.Tests.Common.Builders;

// O banco devolve as linhas do historico na ordem do indice da chave (um Guid aleatorio), nao na do tempo, e o EF so preenche a
// lista privada do agregado na ordem em que elas chegam. Este apoio monta essa situacao sem depender do acaso: linhas com data e
// chave escolhidas, entregues ao agregado na ordem que o teste quiser.
public static class HistoricoEmbaralhado
{
    public static HistoricoStatus Linha(Guid ordemId, StatusOrdemDeServico status, DateTime dataAlteracao, Guid? id = null)
    {
        var linha = new HistoricoStatus(ordemId, status, $"Transicao para {status}.");
        typeof(HistoricoStatus).GetProperty(nameof(HistoricoStatus.DataAlteracao))!.SetValue(linha, dataAlteracao);
        if (id is not null)
            typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(linha, id.Value);

        return linha;
    }

    // Substitui o historico do agregado pelas linhas dadas, na ordem dada: e o que o EF faz ao materializar o Include.
    public static OrdemDeServico ComHistoricoNaOrdem(this OrdemDeServico ordem, params HistoricoStatus[] linhas)
    {
        var lista = (List<HistoricoStatus>)typeof(OrdemDeServico)
            .GetField("_historicoStatus", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(ordem)!;

        lista.Clear();
        lista.AddRange(linhas);
        return ordem;
    }

    // Refaz o historico da OS com uma linha por status ja percorrido (uma hora de distancia entre elas, ou todas na mesma data) e o
    // entrega ao agregado do fim para o comeco, o pior caso de ordem de chegada.
    public static OrdemDeServico ComHistoricoEmOrdemInversa(this OrdemDeServico ordem, DateTime inicio, bool mesmaData = false)
    {
        var fluxo = ordem.HistoricoStatus.Select(h => h.Status).ToList();
        var linhas = fluxo
            .Select((status, i) => Linha(ordem.Id, status, mesmaData ? inicio : inicio.AddHours(i)))
            .Reverse()
            .ToArray();

        return ordem.ComHistoricoNaOrdem(linhas);
    }

    // Guid cuja posicao no indice do SQL Server cresce com o numero: o SQL Server compara um uniqueidentifier pelos seis ultimos
    // bytes primeiro, e eles sao o ultimo grupo do texto. Serve para dar ao historico chaves em ordem inversa a do tempo.
    public static Guid ChaveNaPosicao(int posicao) => new($"00000000-0000-0000-0000-{posicao:D12}");
}
