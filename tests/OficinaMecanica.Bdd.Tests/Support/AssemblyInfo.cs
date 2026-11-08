// A saga, o relogio e o harness de cada cenario sao independentes, mas o projeto roda sem paralelismo de proposito: o
// cenario espera mensagens assincronas com teto de 10 s e, com varios hosts da API subindo ao mesmo tempo numa maquina
// ocupada, essa espera viraria flakiness.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
