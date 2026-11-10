# ADR 0007: Outbox/inbox transacional e publicacao antes do commit

## Status
Aceito

## Contexto
A Saga (ADR 0003) so e confiavel se "gravar o estado" e "avisar os outros servicos" acontecerem
juntos: sem isso, um processo que cai entre o commit e o publish deixa a OS num estado que nenhum
servico sabe, e um publish que sai antes de um commit que falha avisa sobre algo que nunca
existiu. O `AppDbContext` ainda despachava domain events (e-mail, metricas) depois do commit, e
dentro de um consumer do MassTransit esse despacho acontecia com a transacao do consumer ainda
aberta e podia se repetir a cada tentativa. Os achados abaixo vem do spike inicial de validacao (MassTransit 8.5.12
+ EF Core 9 + SQL Server 2022 + RabbitMQ 4), resumido na secao "Resultados do spike de validacao" deste ADR, e foram
reproduzidos com Testcontainers nos testes `Category=Infra` deste repositorio. O Azure SQL
serverless (erros transitorios reais como o 40613) nao foi exercitado em nenhum dos dois.

## Decisao
- **Outbox no mesmo banco, publish antes do save.** O command handler publica pela porta
  `IIntegrationEventPublisher` ANTES de `IUnitOfWork.SaveChangesAsync`. A implementacao
  (`MassTransitIntegrationEventPublisher`, sobre `IPublishEndpoint`) so grava a mensagem em
  `OutboxMessage` quando o bus outbox esta ligado (`AddEntityFrameworkOutbox<AppDbContext>` com
  `UseSqlServer()` e `UseBusOutbox()`); ela sai para o RabbitMQ pelo servico de entrega depois do
  commit. Se o save falha, a mensagem volta junto com o rollback. Publicar DEPOIS do save nao
  persiste nada (o commit ja passou e ninguem grava o outbox); e o controle negativo dos testes.
- **Inbox no consumer.** Todo endpoint usa os filtros nesta ordem: `UseMessageRetry` (por fora) ->
  `UseEntityFrameworkOutbox` (inbox e outbox do consumer) -> consumer ou saga. O inbox deduplica por
  `MessageId` + consumer, entao entrega duplicada (o broker garante "pelo menos uma vez") produz um
  unico efeito. Com o retry por fora, cada tentativa reprocessa a mensagem inteira numa transacao nova.
  O retry ignora `DomainException`, `ConflitoException` e `ValidationException`: repetir nao muda o resultado.
  No MassTransit 8.5.12 o pipeline resultante tem o retry por fora mesmo que o outbox seja configurado antes
  (medido pelo probe do bus); o helper `ConfigurarRetryEOutbox` mantem a ordem explicita e um teste le o
  pipeline resultante do probe.
- **`MessageId` do contrato vira o `MessageId` do envelope**, porque e ele que o inbox usa. O
  publisher le a propriedade `MessageId` do contrato por um acessor compilado uma vez por tipo.
- **Nunca definir `IsolationLevel` do outbox.** O padrao do MassTransit 8.5.12 e `RepeatableRead` e
  funciona; `Serializable` explicito quebra o `BusOutboxDeliveryService` (erro 650 do SQL Server:
  `READPAST` so vale em READ COMMITTED e REPEATABLE READ) e, sem o bus outbox, gerou de 31 a 54
  deadlocks em 200 OS. A saga usa `ConcurrencyMode.Pessimistic` com o isolamento padrao; um teste
  garante que o outbox nao fica em `Serializable`.
- **`EnableRetryOnFailure` so fora de consumer.** O `AppDbContext` liga o retry do EF quando
  `IScopedConsumeContextProvider.HasContext` e falso (caminho HTTP, que precisa do retry por causa do
  auto-pause do Azure SQL serverless); dentro de um consumer ele fica desligado. A hipotese original
  (o EF lancaria `InvalidOperationException` com a transacao do MassTransit) foi refutada: o
  MassTransit roda outbox, inbox e repositorio de saga dentro de `strategy.ExecuteAsync`. O problema
  real e que dentro do consumer o replay do EF e inutil (o `ChangeTracker` fica velho e vira
  `DbUpdateConcurrencyException`); quem repete com estado limpo e o `UseMessageRetry`. A excecao
  continua existindo no codigo que abre `BeginTransaction` fora da estrategia, e um teste a documenta.
- **Domain events pos-commit, em escopo novo.** Se `Database.CurrentTransaction != null` quando o
  `SaveChangesAsync` termina (transacao do MassTransit), o `AppDbContext` guarda os domain events e o
  `DomainEventsPosCommitInterceptor` os despacha em `TransactionCommittedAsync`, descartando em
  rollback ou falha. O despacho usa um escopo DI novo: no escopo original o EF ainda mantem a
  transacao em `CurrentTransaction` durante o callback e um handler que escreve falha com "Cannot
  issue SAVE TRANSACTION when there is no active transaction". Sem transacao do chamador (HTTP) o
  comportamento e o de antes: despacha logo apos o `SaveChanges`. Um handler que lanca depois do
  commit e so logado, porque a transacao ja foi confirmada. No modo antigo, dentro do consumer, o
  spike contou 3 e-mails para uma OS (um por tentativa).
- **`WaitUntilStarted = true`.** Sem isso, `StartAsync` devolve antes de endpoints e bindings
  existirem e uma mensagem publicada logo depois pode se perder.
- **Ponto de troca para testes.** `AddMensageria(IConfiguration, ...)` le `Mensageria:Transporte`
  (`RabbitMq` ou `EmMemoria`) e `Mensageria:Persistencia` (`EntityFramework` ou `Nenhuma`): os testes
  que nao sobem Docker usam o transporte em memoria e nenhuma persistencia, porque o EF InMemory nao
  suporta o outbox transacional. Os dois ultimos parametros do metodo acrescentam consumers e
  endpoints de teste ao mesmo bus, sob o mesmo retry e o mesmo outbox. Os padroes sao os de producao.
- **`/health` nao depende de nada; `/health/ready` reflete o que impede aceitar escrita.** O MassTransit
  registra um check do bus com a tag `ready`, mas enquanto o servico so publica (sem receive endpoints)
  ele continua saudavel com o RabbitMQ parado (medido: 200 por 80 s). O `RabbitMqHealthCheck` abre uma
  conexao AMQP real, com timeout de 3 s, e responde `Degraded` quando o broker nao responde; a falha do
  check do bus tambem e rebaixada para `Degraded`. Como o outbox transacional segura as mensagens no
  banco, a API segue aceitando escrita com o broker fora: tirar o pod de rotacao por causa dele (503)
  transformaria a queda do RabbitMQ em indisponibilidade total. Por isso `/health/ready` so devolve 503
  quando o SQL Server cai (`SqlServerHealthCheck`, tag `ready`), e traz no corpo o status de cada
  check (o broker aparece `Degraded` com HTTP 200). `/health` exclui os checks `ready` (liveness so
  confirma que o processo esta de pe). O alerta operacional do broker fica por metrica (ainda pendente). Testes
  Infra param o container do RabbitMQ (200 + `Degraded`) e colocam o banco offline (503).
- **Guarda de ambiente.** `ValidadorDasOpcoesDaMensageria` (`ValidateOnStart`) so aceita
  `Transporte=EmMemoria` e `Persistencia=Nenhuma` em `Development` e `Testing`; nos demais
  ambientes a partida falha. A guarda e fail-closed: o binder aceita numeros fora do enum (`0`, `9`),
  entao `AddMensageria` decide o ramo por `switch` que lanca para valor indefinido (e traduz o erro do
  binder para texto invalido) em qualquer ambiente, em vez de tratar "o que nao e RabbitMq" como
  memoria; o validador tambem recusa o valor indefinido.
- **Versoes.** EF Core 9.0.22 (o `MassTransit.EntityFrameworkCore 8.5.12` exige EF Core Relational
  9.0.x) e MassTransit 8.5.12, conforme o ADR 0004.

## Resultados do spike de validacao
Antes de escrever a saga, um spike descartavel (fora deste repositorio) validou o MassTransit 8.5.12 com EF Core 9.0.22, SQL Server 2022 e RabbitMQ 4:
67 de 67 testes na ultima rodada (8 min 18 s), inclusive com `Process.Kill` real e varias instancias em paralelo. O Azure SQL serverless (erros transitorios
reais como o 40613) e o envio real de e-mail NAO foram exercitados. O resumo abaixo e autocontido; os achados foram depois reproduzidos com Testcontainers nos
testes `Category=Infra` deste repositorio.

| Item | Resultado |
|------|-----------|
| 1. `EnableRetryOnFailure` do EF x transacao do MassTransit | Passou, hipotese refutada: nao ha `InvalidOperationException`, porque o MassTransit 8.5.12 roda outbox, inbox, repositorio de saga e servico de entrega dentro de `strategy.ExecuteAsync`; a excecao so ocorre em codigo proprio com `BeginTransaction` fora de `CreateExecutionStrategy().ExecuteAsync`. Dentro do consumer o replay do EF e inutil (o `ChangeTracker` velho vira `DbUpdateConcurrencyException`, recuperada pelo `UseMessageRetry`); numa activity de saga a excecao chega embrulhada em `EventExecutionException`. Recomendado: `EnableRetryOnFailure` so quando `IScopedConsumeContextProvider.HasContext` e falso (caminho HTTP), mais `UseMessageRetry` nos endpoints |
| 2. Saga e agregado na mesma transacao | Passou (opcao em que a activity chama o handler MediatR no MESMO escopo, com `ConcurrencyMode.Pessimistic`): o `SaveChanges` do handler fica dentro da transacao do MassTransit, sem flag no `IUnitOfWork`. Excecao depois da escrita: agregado, saga e saida nao mudam e a mensagem vai para `_error` apos 3 tentativas (sobra so a linha de `InboxState` com `Consumed` nulo, inserida em transacao propria antes do consumer). Controle negativo (handler com OUTRO `DbContext`): agregado gravado 3 vezes mesmo com a mensagem em `_error`. Nao foi preciso o plano alternativo |
| 3. Concorrencia e duplicata | Passou, com uma falha: 50 pares de eventos distintos da mesma saga (em paralelo e em sequencia) deram 0 mensagens em `_error`, 0 em `_skipped`, 0 deadlocks e 50 de 50 resultados consistentes; duplicata (3 simultaneas e 1 tardia): o inbox deduplica. **`IsolationLevel.Serializable` explicito FALHA**: quebra o servico de entrega do outbox (erro 650 do SQL Server, `READPAST` so vale em READ COMMITTED e REPEATABLE READ) e, sem o bus outbox, gerou 31 a 54 deadlocks em 200 OS. O padrao real do MassTransit 8.5.12 e `RepeatableRead`. Carga tipo HTTP (200 OS): Pessimistic com RepeatableRead (padrao) e Pessimistic com ReadCommitted deram 0 deadlocks e 10 retries; Optimistic deu 115 retries |
| 4. Publicar antes do `SaveChanges` (caminho HTTP) | Passou: grava o `OutboxMessage` no mesmo commit, o servico de entrega publica e, se o `SaveChanges` falha, a mensagem some junto. Sem `UseBusOutbox` o evento sai mesmo com o save falho. Publicar DEPOIS do `SaveChanges` nao persiste (um `SaveChanges` posterior no mesmo escopo grava a linha encalhada) |
| 5. Domain events pos-commit sem e-mail fantasma | Passou, com ressalva: o padrao `Database.CurrentTransaction != null` guarda os eventos e os despacha em `TransactionCommittedAsync` (descartados em rollback), com 0 e-mails em rollback no HTTP e no consumer; no modo antigo, dentro do consumer, saem 3 e-mails fantasma (um por tentativa). O despacho pos-commit deve usar um escopo de injecao de dependencia NOVO: no mesmo escopo, um handler que escreve falha com "Cannot issue SAVE TRANSACTION when there is no active transaction" (handler que so le funciona) |
| 6. Seguranca contra queda | Passou, com `Process.Kill` real em 4 pontos: depois do commit, o outbox e entregue na reinicializacao, com 1 efeito; antes do commit, nada foi gravado; depois da confirmacao do broker e antes de marcar como entregue, 3 copias e 1 efeito; no meio do consumer, a mensagem e reprocessada 1 vez |
| 7. Sweeper de prazos | Passou, com uma pegadinha: 2 e 4 instancias mais 2 processos, 600 e 1500 linhas, cada uma processada 1 vez e 0 deadlocks; sem as dicas de lock, 59 erros de chave primaria. **As dicas travam o que o plano LE, nao so o `TOP N`**: sem `INDEX(IX_SagaPrazos_Processado_ExpiraEm)` uma instancia pegou todas as linhas e as outras ficaram com 0 |
| 8. Pacotes | Passou: `MassTransit`, `MassTransit.RabbitMQ` e `MassTransit.EntityFrameworkCore` em 8.5.12; `Microsoft.EntityFrameworkCore.SqlServer` 9.0.22; MediatR 12.2.0; FluentValidation 11.9.0; restore limpo (sem NU1605 nem NU1608) e nenhum pacote `MassTransit*` no major 9 |

Pegadinhas e condicoes que o desenho passou a respeitar:
- Um evento que chega ANTES de a saga existir e descartado em silencio (sem `_error` nem `_skipped`) e a saga trava: `OnMissingInstance(Fault)` mais `UseMessageRetry`
  (descartar so em estado final).
- Um evento tardio com `MessageId` novo em estado final: o `Ignore` de um evento ou de um subconjunto nao basta; todo evento sem transicao no estado precisa estar
  declarado, senao o MassTransit lanca `UnhandledEventException`.
- `MassTransitHostOptions.WaitUntilStarted = true`: uma mensagem publicada logo depois do `StartAsync` pode se perder.
- Ordem dos filtros no endpoint: `UseMessageRetry`, depois `UseEntityFrameworkOutbox`, depois a saga.
- O projeto que ja existia estava em EF Core 8.0.0: precisou subir para 9.0.x (o `MassTransit.EntityFrameworkCore` 8.5.12 exige o 9) e conferir o `Microsoft.Data.SqlClient`
  5.1.9 e o `IdentityModel` contra o `JwtBearer` e o `Microsoft.IdentityModel.Protocols` 7.0.3.

Conclusao do spike: a opcao de a activity chamar o handler MediatR no mesmo escopo e viavel, sem mudar os handlers nem o `IUnitOfWork`. Decisoes que dela decorrem: nunca
`Serializable`; EF retry so fora de consumer; despacho pos-commit em escopo novo; subir o EF Core; dica `INDEX(...)` no sweeper; `OnMissingInstance(Fault)` mais retry
para o evento que chega antes da saga.

## Consequencias
### Positivas
- Estado e mensagem nascem e morrem juntos: crash apos o commit entrega na reinicializacao, crash
  antes do commit nao deixa mensagem (o spike matou o processo em quatro pontos).
- Consumers idempotentes por construcao do inbox, sem codigo de dedupe em cada handler.
- Os handlers de e-mail e metricas e seus testes continuam iguais; so o momento do despacho muda dentro
  de consumer.

### Negativas
- Todo handler que publica precisa chamar `SaveChangesAsync` mesmo sem mudar agregado, porque e o save
  que grava o outbox; esquecer a ordem (publicar depois) perde a mensagem em silencio. Os testes de
  Infra cobrem o caminho do `CriarOrdemDeServicoCommandHandler`, mas cada novo handler precisa respeitar a regra.
- A entrega tem latencia (varredura do outbox, 1 s por padrao) e e "pelo menos uma vez": o consumidor
  precisa do inbox.
- A API passa a exigir o RabbitMQ no ar para iniciar (`WaitUntilStarted`); em ambiente sem broker o
  processo cai apos ~60 s em vez de subir degradado. No Kubernetes isso pede `startupProbe` com
  folga de pelo menos 60 s, `/health` como liveness e `/health/ready` como readiness (cada probe abre
  conexao com o banco e impede o auto-pause do Azure SQL serverless: usar `periodSeconds` alto). Os checks
  de prontidao rodam em paralelo e levam ate 3 s, e o `timeoutSeconds` padrao do Kubernetes e 1 s: a
  readiness precisa de `timeoutSeconds >= 5`.
- **A implementacao do outbox nao pode ser implantada sozinha, sem a saga.** O exchange fanout de cada contrato so e criado no
  primeiro publish e, sem fila ligada, o RabbitMQ descarta a mensagem e o outbox esvazia (medido: a
  1a e a 2a OS criadas sem fila perderam o `OrdemDeServicoAberta`). A implementacao da saga precisa criar a
  fila antes de a API aceitar trafego, ou reconciliar as OS `Recebida` que ficaram sem saga.

## Alternativas consideradas
- **Publicar depois do commit**: perde a mensagem se o processo cair entre os dois passos.
- **Outbox escrito a mao**: reimplementa entrega, bloqueio de linha e limpeza que o MassTransit ja traz testados.
- **`IsolationLevel.Serializable` no outbox**: reprovado no spike (erro 650 e deadlocks).
- **Estrategia de execucao propria que desliga o retry quando ha transacao do chamador**
  (`TransactionAwareRetryingStrategy`, no spike): funciona, mas herda de tipo interno do EF; escolher o
  retry por escopo (`HasContext`) tem o mesmo efeito sem depender de API interna.
- **Despachar domain events no mesmo escopo, apos o commit**: falha para handler que escreve no banco.
- **Manter o despacho imediato dentro do consumer**: manda e-mail de tentativa que sera desfeita.
