using System.Text.RegularExpressions;

namespace OficinaMecanica.Infrastructure.Tests.Support;

public sealed record TransicaoDoDiagrama(string Origem, string Evento, string Destino);

// Le as transicoes de docs/saga-maquina-de-estados.puml, que e a especificacao da saga, para o teste do grafo compara-la com a
// maquina de verdade. Cada linha de transicao tem a forma
//   Origem --> Destino : Evento1 | Evento2 [condicao] (comentario)\n/ passos\n(efeito na OS)
// e so o texto ate o primeiro "\n" literal (barra invertida e n) diz o evento: os eventos sao separados por "|", condicoes
// entre colchetes e comentarios entre parenteses sao descartados e Fault<Comando> vira o evento FalhaAo<Comando>, como a
// maquina o nomeia. "[*]" de origem e o estado Initial do MassTransit; as linhas que terminam em "[*]" (estados finais) nao
// carregam evento e ficam de fora.
public static class DiagramaDaSaga
{
    public const string NomeDoArquivo = "saga-maquina-de-estados.puml";
    public const string EstadoInicial = "Initial";

    private static readonly Regex LinhaDeTransicao = new(
        @"^\s*(?<origem>\[\*\]|\w+)\s*-->\s*(?<destino>\[\*\]|\w+)\s*:\s*(?<rotulo>.+?)\s*$", RegexOptions.Compiled);

    private static readonly Regex CondicaoOuComentario = new(@"\[[^\]]*\]|\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Falha = new(@"^Fault<(?<comando>\w+)>$", RegexOptions.Compiled);
    private static readonly Regex NomeDeEvento = new(@"^\w+$", RegexOptions.Compiled);

    // Sobe os diretorios a partir do assembly de teste ate achar docs/saga-maquina-de-estados.puml (a raiz do repositorio).
    public static string Localizar()
    {
        var inicio = AppContext.BaseDirectory;
        for (var diretorio = new DirectoryInfo(inicio); diretorio is not null; diretorio = diretorio.Parent)
        {
            var candidato = Path.Combine(diretorio.FullName, "docs", NomeDoArquivo);
            if (File.Exists(candidato))
                return candidato;
        }

        throw new FileNotFoundException(
            $"Não encontrei docs/{NomeDoArquivo} subindo os diretórios a partir de '{inicio}'. O teste do grafo da saga compara a máquina com esse diagrama e precisa dele no repositório.");
    }

    public static IReadOnlyList<TransicaoDoDiagrama> Carregar() => Interpretar(File.ReadAllText(Localizar()));

    public static IReadOnlyList<TransicaoDoDiagrama> Interpretar(string puml)
    {
        var transicoes = new List<TransicaoDoDiagrama>();

        foreach (var linha in puml.Split('\n'))
        {
            var correspondencia = LinhaDeTransicao.Match(linha.TrimEnd('\r'));
            if (!correspondencia.Success || correspondencia.Groups["destino"].Value == "[*]")
                continue;

            var origem = correspondencia.Groups["origem"].Value is "[*]" ? EstadoInicial : correspondencia.Groups["origem"].Value;
            var destino = correspondencia.Groups["destino"].Value;

            var rotulo = correspondencia.Groups["rotulo"].Value.Split("\\n")[0];
            foreach (var evento in CondicaoOuComentario.Replace(rotulo, string.Empty).Split('|'))
                transicoes.Add(new TransicaoDoDiagrama(origem, NomeDoEvento(evento.Trim(), linha), destino));
        }

        return transicoes;
    }

    private static string NomeDoEvento(string texto, string linha)
    {
        if (Falha.Match(texto) is { Success: true } falha)
            return "FalhaAo" + falha.Groups["comando"].Value;

        if (!NomeDeEvento.IsMatch(texto))
            throw new FormatException($"'{texto}' não é um nome de evento na transição do diagrama: {linha.Trim()}");

        return texto;
    }
}
