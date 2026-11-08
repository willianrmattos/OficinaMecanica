namespace OficinaMecanica.Infrastructure.Saga;

// Porta dos prazos agendados pela saga. Existe para o sweeper e as activities poderem ser testados sem SQL: a
// implementacao real (PrazoRepository) fala SQL Server com dicas de lock, e os testes trocam por uma em memoria.
public interface IPrazoRepository
{
    // Agenda o prazo na unidade de trabalho corrente (a transacao da saga): so fica definitivo quando ela comita.
    Task AdicionarAsync(SagaPrazo prazo, CancellationToken cancellationToken = default);

    // Reivindica ate `lote` prazos vencidos que ninguem processou, marca cada um como processado e chama
    // `aoExpirar` para ele, tudo na MESMA transacao: se publicar falha, a marca volta junto. Duas instancias do
    // sweeper (varias replicas) nunca recebem o mesmo prazo. Devolve quantos prazos reivindicou.
    Task<int> ProcessarVencidosAsync(
        DateTimeOffset agora,
        int lote,
        Func<SagaPrazo, CancellationToken, Task> aoExpirar,
        CancellationToken cancellationToken = default);
}
