# ADR 0008: Sweeper de prazos, estados e compensacoes da saga do OS Service

## Status
Aceito

## Contexto
O ADR 0003 fixou a saga orquestrada no OS Service e o ADR 0007 fixou o outbox/inbox e a publicacao antes
do commit. A implementacao da saga (F3b) precisou decidir o que o desenho deixava em aberto: como um prazo
(aprovacao do orcamento, pagamento) vira um evento sem timer em memoria, como a saga convive com mensagens
duplicadas, tardias e fora de ordem, o que fazer quando um servico falha de forma tecnica (`Fault<T>`) e como
os comandos enviados a Billing e Execucao ficam seguros contra reentrega. Os achados vem do spike F0a
(`E:\FIAP\Pos\_spikes\mt-ef-saga\FINDINGS.md`) e foram reproduzidos com Testcontainers (SQL Server 2022 e
RabbitMQ 4) em `tests/OficinaMecanica.Infrastructure.Tests`.

## Decisao
### Estado, endpoint e concorrencia
- O estado da saga (`OrdemDeServicoSagas`) e os prazos (`SagaPrazos`) vivem no MESMO banco e no MESMO
  `AppDbContext` da OS: saga, OS, estoque, prazo e mensagens do outbox comitam juntos ou nao comitam. O
  `CorrelationId` e o proprio `OrdemDeServicoId`. Nao ha `SetCompletedWhenFinalized`: a linha fica depois de
  `Concluida`, `Cancelada` e `OrcamentoRecusado` para auditoria e para o evento tardio encontrar a instancia.
- Repositorio EF do MassTransit 8.5.12 e isolamento padrao dele. Dois eventos da mesma OS se serializam no banco porque o
  repositorio le a linha da saga com `WITH (UPDLOCK, ROWLOCK)` dentro da transacao do consumer. Esse bloqueio e o
  comportamento PADRAO do MassTransit (modo `Pessimistic` e comando do SQL Server): o SQL e identico com e sem as duas
  linhas de configuracao em `SagaExtensions`, que existem so para explicitar a intencao. Quem garante o bloqueio nao e a
  configuracao e sim um teste com SQL real que captura os comandos do `AppDbContext` (`DbCommandInterceptor`) e exige
  `UPDLOCK` em toda leitura de `OrdemDeServicoSagas` feita na transacao do consumer; ele reprova se o modo virar
  `Optimistic`. `Serializable` esta proibido (quebra o servico de entrega do outbox,
  ADR 0007). Outro teste com SQL real dispara aprovacao + prazo + copia duplicada da mesma OS em 12 OS ao mesmo tempo e
  exige exatamente uma transicao coerente por OS, nada em `_error` (um deadlock eventual e repetido pelo retry; ver Consequencias).
- A fila `os-saga` (`Filas.Saga`) nasce quando o bus sobe (`WaitUntilStarted`), com o retry por fora e o
  outbox/inbox por dentro (`ConfigurarRetryEOutbox`). Isso resolve o risco da F3a: o exchange de
  `OrdemDeServicoAberta` ja tem fila ligada antes da API aceitar a primeira OS. A saga entra pelos ganchos de
  `AddMensageria` (`AddMensageriaComSaga`), sem segundo bus e sem quebrar o ponto de troca de transporte e
  persistencia nem a guarda fail-closed de producao. Com `Mensageria:Persistencia = Nenhuma` (so Development e
  Testing) o repositorio da saga e o em memoria.

### Quem altera a OS
- Cada passo com efeito colateral e uma activity fina (`Infrastructure/Saga/Activities`) que chama UM command MediatR
  idempotente da Application no mesmo escopo, DbContext e transacao do consumer (opcao A do spike). Nao ha EF dentro da
  activity: o prazo vai pela porta `IPrazoRepository` e a leitura da OS por uma query da Application
  (`ObterSnapshotDaOrdemQuery`). Domain e Application nao referenciam o MassTransit.
- O resultado da reserva de estoque vai para o payload do contexto do evento (nao para uma coluna da saga) e decide o
  ramo seguinte com `IfElse`. Reservar acontece ANTES de pedir o pagamento: faltando peca, ninguem e cobrado.
- `IPrazoRepository.AdicionarAsync` grava na hora (como os commands): dentro do consumer ainda e a transacao da saga e nada
  depende de quem fecha a unidade de trabalho salvar de novo.

### Eventos sem transicao e eventos antes da saga
- Evento duplicado, fora de ordem ou tardio e IGNORADO (`DuringAny(Ignore(...))`): nao muda estado, nao envia nada e
  nunca vai para `_error`. O comportamento declarado de um estado tem prioridade sobre o `Ignore`, entao a lista nao
  esconde transicao. Um teste exige que todo estado trate ou ignore todo evento (`NextEvents`) e outro publica cada evento
  em cada estado onde ele nao tem transicao. `PagamentoEstornado` e `EstornoFalhou` fora de `Estornando` (estorno
  automatico por pagamento tardio, reconciliador ou staff do Billing) sao ignorados; `PagamentoEstornado` em
  `EstornoPendenteManual` fecha a saga.
- Evento que chega ANTES da saga existir usa `OnMissingInstance(m => m.Fault())`: o padrao do MassTransit descartaria em
  silencio e a OS ficaria sem andar para sempre. Falhando, o `UseMessageRetry` do endpoint tenta de novo ate
  `OrdemDeServicoAberta` criar a instancia (intervalos de 500 ms ate 60 s por padrao).
- `DiagnosticoConcluido` em `AguardandoDiagnostico` (chegou antes de `DiagnosticoIniciado`, que sao publicados pela
  Execucao em momentos diferentes e sem ordem garantida) adianta o inicio e segue para `GerandoOrcamento`. Ignorar a
  conclusao deixaria a saga presa em `EmDiagnostico` depois do `DiagnosticoIniciado` atrasado.

### Prazos e o sweeper
- Prazos vivem em `SagaPrazos(CorrelationId, PrazoId PK, Tipo Aprovacao|Pagamento, ExpiraEm, Processado)` com o indice
  `IX_SagaPrazos_Processado_ExpiraEm`. A saga grava o prazo na mesma transacao da transicao: `OrcamentoGerado` cria o de
  aprovacao (com o `ValidoAte` que o Billing confirmou) e a aprovacao do cliente cria o de pagamento (junto do
  `SolicitarPagamento`, com o mesmo `ExpiraEm`).
- A saga guarda o prazo VIGENTE de cada tipo (`PrazoAprovacaoId`, `PrazoPagamentoId`) e `PrazoExpirado` so age se o
  `PrazoId` e o `Tipo` baterem com o vigente; ao encerrar a etapa o campo volta a nulo. Isso resolve a corrida entre o
  timer e a decisao do cliente e o aviso atrasado de um prazo ja decidido, sem apagar linhas.
- O sweeper (`SweeperDePrazosBackgroundService` + `SweeperDePrazos`) reivindica os vencidos com
  `UPDLOCK, READPAST, ROWLOCK, INDEX(IX_SagaPrazos_Processado_ExpiraEm)`, marca `Processado` e publica `PrazoExpirado` pelo
  outbox na mesma transacao; nunca escreve na linha da saga. As dicas travam tudo o que o plano LE, nao so as N linhas
  devolvidas: sem a dica `INDEX(...)` uma instancia trava todas as linhas e as outras ficam com zero. Com ela, duas
  instancias seguram lotes disjuntos e cada prazo e tratado exatamente uma vez (teste com SQL real: uma instancia segura 50
  linhas com a transacao aberta e outra reivindica as outras 50 sem esperar). O relogio e o `TimeProvider`, passado ao SQL
  como parametro (nao `SYSUTCDATETIME()`), e o `MessageId` do `PrazoExpirado` e derivado do prazo.
- Opcoes `Saga__*` com `ValidateOnStart`, todas > 0: `PrazoAprovacaoHoras` 72, `PrazoPagamentoHoras` 48,
  `SweeperIntervaloSegundos` 30, `SweeperLote` 50; mais `SweeperHabilitado` (true), que so existe para os testes dispararem o
  sweeper a mao.
- Prazo substituido (o cliente aprovou antes de vencer) continua na tabela ate vencer: o evento chega e e ignorado. Optei
  por nao cancelar a linha para o sweeper nunca disputar uma linha com a transacao da saga.

### Estados de compensacao e falha tecnica
- `CancelandoPagamento`, `Estornando` e `EstornoPendenteManual` nao tem decisao automatica: o SLA e so de alerta (log de
  erro estruturado e, quando existir, a metrica de saga presa). O Billing e a autoridade sobre o pagamento e
  `EstornoPendenteManual` nunca vira `Cancelada` por conta propria.
- `Fault<T>` e falha tecnica do comando enviado. Todo `Fault` vai para o log de erro (ids da OS e da mensagem, tipo e
  texto da excecao) e depois, por comando: `SolicitarDiagnostico` e `GerarOrcamento` cancelam a OS; `SolicitarPagamento`
  age como `PagamentoNaoSolicitado` (libera o estoque, cancela o orcamento e a OS); `IniciarExecucao` age como
  `ExecucaoInviavel` (estorna, pois a Execucao nao recebeu o reparo); `EstornarPagamento` leva a `EstornoPendenteManual`;
  `CancelarPagamento`, `CancelarOrcamento` e `CancelarProducao` so logam: a saga NAO reenvia o comando e NAO transita,
  permanece no estado em que estava (secao seguinte). Vale tambem para o `Fault` de `CancelarProducao` por reparo em
  andamento, que a Execucao recusa com `DomainException`. Em estado onde o `Fault` nao tem compensacao ele e logado e ignorado.
- Decisao de uma pessoa que a API ja aceitou com 202 e a saga nao pode mais aplicar (aprovar ou recusar depois de o prazo
  ter decidido, cancelar com o reparo em andamento ou a OS ja fechada) continua IGNORADA, sem mudar nada, mas deixa um
  aviso (`Warning`) com template estavel: `Decisão {Decisao} da ordem de serviço {OrdemDeServicoId} ignorada: a saga está
  em {Estado} e a decisão não tem mais efeito, mensagem {MessageIdDaDecisao}`. So essas tres decisoes avisam; duplicata de
  qualquer outro evento segue ignorada em silencio (`DuringAny(Ignore)`), e o aviso e um comportamento declarado por
  estado, que tem prioridade sobre o `Ignore` sem esconder transicao.
- A producao so e cancelada no cancelamento do staff. Recusa, prazo vencido, estoque insuficiente e pagamento expirado
  deixam na Execucao o registro de um diagnostico que de fato aconteceu, fora das filas de trabalho. Cancelamento do staff
  em `CancelandoPagamento` (depois do prazo) registra a intencao (`CancelamentoPorStaff`) e desfaz a producao, para que um
  pagamento tardio seja estornado em vez de executado; em `EmExecucao` e nos estados de compensacao e ignorado, com o aviso acima.

### Compensacao que falha: alerta e redrive manual
A compensacao nunca e descartada em silencio, mas a saga tambem nao a reenvia sozinha: um reenvio automatico viraria loop
com mensagem venenosa (o mesmo comando que falha por dado ruim ou regra violada falharia a cada volta, enchendo broker e
log). "Retentavel" quer dizer: (1) o endpoint do destino repete a mensagem (retry com intervalos exponenciais; erro de
negocio como `DomainException` nao repete) e (2) esgotado o retry ela fica na fila `<fila>_error` do destino
(`billing-comandos_error`, `execucao-comandos_error`), de onde um operador a devolve a fila de comandos depois de tratar a
causa. Enquanto isso a saga fica onde estava (ex.: `CancelandoPagamento`): quando o comando for processado, a resposta
normal (`PagamentoCancelado`...) chega e a saga segue.

- **Sinal de alerta**: `LogarFalhaTecnicaActivity` emite, para todo `Fault<T>` e antes de qualquer compensacao, um log de
  `Error` com template estavel: `Falha técnica no comando {Comando} da ordem de serviço {OrdemDeServicoId} (saga em
  {Estado}), mensagem {MessageIdDoComando} no host {Host}: {TipoDaExcecao}: {MensagemDaExcecao}`. O alerta por texto (New
  Relic) procura `Falha técnica no comando`; `{Comando}` distingue `CancelarPagamento`/`CancelarOrcamento`/`CancelarProducao`
  (compensacao presa) dos demais, e os ids localizam a OS e a mensagem na `_error` (propriedade `message_id`). O passo nao
  carimba `AtualizadaEm`, entao `CurrentState` + `AtualizadaEm` parados em `OrdemDeServicoSagas` tambem acham a saga presa.
- **Metrica**: a metrica de saga presa / compensacao falhou entra junto das demais metricas da saga (fase de codigo seguinte); dashboard e alerta (NRQL) ficam para a documentacao final. Ela conta sagas por
  estado e idade e os `Fault` de compensacao; ainda nao existe: hoje o alerta e so o log.

**Runbook de redrive** (operador):
1. Do alerta, pegar `OrdemDeServicoId` e `MessageIdDoComando`; ver o estado da saga
   (`SELECT CurrentState, AtualizadaEm FROM OrdemDeServicoSagas WHERE CorrelationId = '<id>'`).
2. Tratar a causa antes (servico fora do ar, dado invalido). Reprocessar sem isso devolve a mensagem a `_error` e gera um
   novo `Fault` (novo alerta). Reparo em andamento (`CancelarProducao` recusado com `DomainException`) nao se resolve por
   redrive: nao ha o que reprocessar, o operador descarta a mensagem depois de conferir a OS.
3. No RabbitMQ Management UI (Queues > `<fila>_error` > Get messages) conferir a mensagem: o `message_id` bate com o do log e
   o cabecalho `MT-Fault-Message` traz a excecao.
4. Devolver a mensagem a fila de comandos: Management UI, Queues > `<fila>_error` > Move messages, destino `<fila>` (exige
   os plugins `rabbitmq_shovel` e `rabbitmq_shovel_management`; num broker sem eles, `rabbitmq-plugins enable` os liga sem
   reiniciar); ou um shovel dinamico equivalente (`rabbitmqctl set_parameter shovel` com `src-queue=<fila>_error`,
   `dest-queue=<fila>`, `src-delete-after=queue-length`). O movimento preserva corpo e propriedades, inclusive o
   `message_id` original.
5. Confirmar: a saga sai do estado (ex.: `CancelandoPagamento` para `Cancelada`) e a `_error` esvazia.

**Por que e idempotente**: o comando tem `MessageId` deterministico (SHA-256 de OS + nome + discriminador), o inbox do destino
descarta um `MessageId` ja consumido e os handlers do destino sao idempotentes (contrato da saga). Reprocessar um comando
que falhou, ou que chegou a ter efeito parcial, e entregar o mesmo comando com o mesmo id: nunca duplica. Um teste com
SQL e RabbitMQ reais prova o ciclo (consumer do Billing lanca em `CancelarPagamento`, a saga so loga e fica em
`CancelandoPagamento` sem reenviar, a mensagem e devolvida da `_error` e a saga fecha em `Cancelada`); ele move a mensagem
com o cliente AMQP, que e o que o shovel faz, e nao exercita o plugin de shovel em si.

### Ids deterministicos
- Todo comando enviado vai por `Send` para `queue:{Filas.X}` com `MessageId` derivado de SHA-256 de
  `OrdemDeServicoId + nome do comando [+ discriminador]` (`IdentificadorDeMensagem`): o discriminador e a janela de
  pagamento em `SolicitarPagamento` e `CancelarPagamento` e o `PagamentoId` em `EstornarPagamento`. O `MessageId` do
  contrato vira o do envelope, que o inbox do destino usa para descartar a reentrega. Um teste fixa um valor conhecido
  (nao pode depender de `string.GetHashCode`). `OcorridoEm` vem do `TimeProvider`.

## Consequencias
### Positivas
- Cada transicao e uma transacao: o spike e os testes com SQL real provam que excecao dentro da activity depois da
  escrita desfaz OS, estoque, saga, prazo e mensagens, e que o retry nao duplica nenhum comando nem e-mail.
- Nenhum timer em memoria: um reinicio de pod ou varias replicas nao perdem nem duplicam prazos.
- A maquina de estados e uma traducao literal do `.puml`, testada transicao por transicao com o harness do MassTransit
  sem Docker, e ponta a ponta com SQL Server e RabbitMQ reais, inclusive com o `Fault<T>` real de um consumer que lanca.
  Um teste le o proprio `.puml` e compara com o grafo da maquina dois recortes: que eventos movem cada estado e para onde
  cada evento leva. O trio origem + evento -> destino nao aparece no grafo e e garantido pelos testes de comportamento de
  cada transicao.
- Reentrega e duplicata nao tem efeito: inbox no endpoint, comandos locais idempotentes, ids deterministicos e estado.

### Negativas
- Pegar o `INDEX(...)` no SQL cru acopla o claim ao nome do indice (constante unica, mais teste de esquema) e ao SQL Server.
- O isolamento do outbox (RepeatableRead, padrao do MassTransit) mantem lock de leitura ate o commit: duas OS diferentes
  podem, de vez em quando, ser vitimas de deadlock (SQL 1205) lendo o historico por varredura em tabela pequena. O retry do
  endpoint repete a mensagem inteira em transacao nova e o resultado e o mesmo; o teste de concorrencia exige recuperacao (nada
  em `_error`, estado coerente por OS) e aceita o log do erro 1205, nao a ausencia dele. Mudar o isolamento fica fora desta
  decisao (ADR 0007).
- `SagaPrazos` cresce: as linhas processadas ficam para auditoria e nao ha rotina de retencao ainda (follow-up).
- O OS Service concentra a orquestracao (ADR 0003); cada comando novo precisa de activity, id deterministico e testes.
- A saga nunca mexe na Execucao alem de `CancelarProducao`: um diagnostico de OS recusada continua registrado la.
- Compensacao que falha de forma tecnica (`Fault` de `CancelarPagamento`, `CancelarOrcamento` ou `CancelarProducao`) depende de
  uma pessoa fazer o redrive: so o log de erro avisa ate a metrica de saga presa existir. E o preco de nao reenviar sozinho
  (sem loop com mensagem venenosa) e de nunca decidir pelo Billing.
- `Fault<T>` conservador pode estornar mais do que o necessario (`IniciarExecucao` que falhou por indisponibilidade
  transitoria esgotada); o custo e preferir devolver o dinheiro a reter pagamento de servico que nao comecou.

## Alternativas consideradas
- **Timer da saga por mensagem agendada (`Schedule`, quartz/delayed message)**: exige extensao do broker ou do MassTransit
  e deixa o timer fora do banco da OS; o sweeper sobre uma tabela comita junto da transicao.
- **Optimistic concurrency**: rendeu o dobro de retries no spike e, sem `RowVersion` na linha da saga, nao detecta dois
  eventos simultaneos.
- **`OnMissingInstance(Discard)`**: perde em silencio o evento que chegou antes da saga.
- **Ignorar tudo com `OnUnhandledEvent`**: esconderia evento novo sem tratamento; a lista explicita mais o teste de
  cobertura de eventos o expoe.
- **Sweeper escrevendo na linha da saga**: disputaria o bloqueio da propria saga; publicar o evento e deixar a saga decidir
  mantem um unico escritor por transicao.
- **Cancelar a linha de prazo substituida**: evitaria o evento ignorado, mas faria a transacao da saga disputar linhas
  travadas pelo sweeper.
