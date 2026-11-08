# ADR 0003: Saga orquestrada hospedada no OS Service

## Status
Aceito

## Contexto
Abrir uma OS, gerar orcamento, aguardar aprovacao, cobrar e executar agora cruza tres
servicos com bancos independentes. Sem transacao distribuida, a consistencia vem de uma
Saga: sequencia de transacoes locais com compensacao quando uma etapa falha. Ha dois
estilos: **orquestrada** (um coordenador central comanda cada etapa) e **coreografada**
(cada servico reage a eventos dos outros, sem coordenador).

## Decisao
Saga **orquestrada**, com o orquestrador no **OS Service** (dono do agregado
`OrdemDeServico`): `MassTransitStateMachine<OrdemDeServicoSagaState>`, correlacionada
pelo id da OS, persistida no mesmo banco do servico (EF Core) junto com o outbox.
O orquestrador envia **comandos** a Billing e Execucao e reage aos **eventos** de
resposta. Estados, transicoes e compensacoes estao em `docs/saga-maquina-de-estados.puml`.

Regras que a decisao impoe:
- toda etapa com efeito colateral tem compensacao (estoque, orcamento, pagamento/estorno,
  producao); a compensacao nunca e descartada em silencio. "Retentavel" significa retry do
  endpoint de destino mais redrive manual da fila `_error` (o comando tem id deterministico,
  entao reprocessar e idempotente): a saga NAO reenvia sozinha, porque reenvio automatico viraria
  loop com mensagem venenosa, e permanece no estado de compensacao (ex.: `CancelandoPagamento`),
  com log de erro estruturado, ate o comando ser processado e a resposta chegar (ADR 0008);
- falha de negocio vira evento explicito (`OrcamentoNaoGerado`, `ExecucaoInviavel`);
  `Fault<T>` e so falha tecnica;
- timeout de prazo (aprovacao e pagamento) cancela; timeout de diagnostico/reparo so alerta
  (reparo em andamento nao se desfaz por timer);
- o Billing e a autoridade sobre o pagamento: cancelar pagamento passa pelo Billing, que
  responde se ainda estava pendente ou se ja foi aprovado (pagamento tardio);
- estorno que falha leva a `EstornoPendenteManual` e nao vira `Cancelada` por conta propria: so
  depois que o staff refaz o estorno na API do Billing (`PagamentoEstornado`) a saga cancela;
- cancelar antes de criar: um comando de cancelamento pode chegar ao Billing antes de
  `GerarOrcamento`/`SolicitarPagamento` terem sido processados. O Billing localiza pelo
  `OrdemDeServicoId`; se nao existe o agregado, registra um cancelamento previo (tombstone) e o
  comando de criacao que chegar depois vira `OrcamentoNaoGerado`/`PagamentoNaoSolicitado`. Por isso
  `OrcamentoId` e `PagamentoId` sao opcionais nos comandos e na resposta `PagamentoCancelado`;
- `CancelarPagamento` tambem cancela o orcamento se ele estiver `Aprovado` (nunca `Pago`);
- os estados de compensacao (`CancelandoPagamento`, `Estornando`) tem SLA so de alerta (metrica
  de saga presa); nenhuma decisao automatica se o Billing demorar a responder.

Implementacao (F3b): a maquina de estados esta em `Infrastructure/Saga` e os detalhes (prazos e
sweeper, `PrazoId` vigente, eventos duplicados, tardios e antecipados, `Fault<T>` por comando, ids
deterministicos dos comandos) estao no ADR 0008. Tres pontos refinam o que este ADR e o plano
inicial diziam: (1) evento que chega ANTES de a saga existir nao e descartado
(`OnMissingInstance(Discard)`): ele falha e o retry do endpoint o reprocessa, porque descartar
deixaria a OS sem andar; so evento sem transicao no estado atual e ignorado; (2) o diagrama ganhou
as transicoes de `Fault<T>`, a do `DiagnosticoConcluido` que chega antes do `DiagnosticoIniciado` e a
do cancelamento do staff durante `CancelandoPagamento`; (3) o `Fault<T>` de um comando de compensacao
(`CancelarPagamento`, `CancelarOrcamento`, `CancelarProducao`) so gera log de erro estruturado: nao ha
reenvio automatico nem transicao, a saga fica onde estava e o comando falho espera na `_error` do destino
pelo redrive manual (runbook no ADR 0008). As metricas da saga existem (`System.Diagnostics.Metrics`, Meter `OficinaMecanica.Saga`): `saga_compensacao_falhou` conta cada `Fault` de compensacao e o gauge `saga_compensacao_presa` mostra as sagas paradas em estado de compensacao alem do limite (nomes, tags e como alertar na secao "Metricas da saga" do ADR 0008); dashboard e alerta (NRQL) ficam para a documentacao final. Ate la o alerta e o log de erro mais essas duas metricas.

## Consequencias
### Positivas
- O fluxo inteiro fica em um unico lugar, legivel e testavel (cenarios BDD cobrem feliz e
  compensacoes sem subir os tres servicos).
- Compensacao explicita e auditavel: o estado da saga mostra exatamente onde cada OS parou.
- Servicos de apoio (Billing, Execucao) ficam simples: recebem um comando, respondem com
  um evento, sem conhecer o fluxo completo.

### Negativas
- O OS Service concentra conhecimento do processo (acoplamento de orquestracao) e vira
  ponto sensivel de disponibilidade para o fluxo (mitigado por outbox: nada se perde, so atrasa).
- Mais mensagens e mais estados do que uma coreografia minima; exige disciplina de
  idempotencia em todo comando e consumer.

## Alternativas consideradas
- **Saga coreografada**: mais desacoplada, mas o fluxo fica espalhado em tres repositorios,
  a compensacao e dificil de rastrear e de testar de ponta a ponta, e adicionar uma etapa
  exige alterar varios servicos. Para um fluxo linear com compensacoes bem definidas, o
  custo de rastreabilidade pesa mais que o ganho de desacoplamento.
- **Orquestrador como servico separado**: isolaria o processo, mas criaria um quarto
  servico a operar no no unico do AKS, sem ganho: o OS Service ja e dono do agregado que
  a saga conduz e pode alterar OS, estoque e saga na mesma transacao.
