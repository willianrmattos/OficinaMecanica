# ADR 0007: Outbox/inbox transacional e publicacao antes do commit

## Status
Aceito

## Contexto
A Saga (ADR 0003) so e confiavel se "gravar o estado" e "avisar os outros servicos" acontecerem
juntos: sem isso, um processo que cai entre o commit e o publish deixa a OS num estado que nenhum
servico sabe, e um publish que sai antes de um commit que falha avisa sobre algo que nunca
existiu. O `AppDbContext` ainda despachava domain events (e-mail, metricas) depois do commit, e
dentro de um consumer do MassTransit esse despacho acontecia com a transacao do consumer ainda
aberta e podia se repetir a cada tentativa. Os achados abaixo vem do spike F0a (MassTransit 8.5.12
+ EF Core 9 + SQL Server 2022 + RabbitMQ 4, `E:\FIAP\Pos\_spikes\mt-ef-saga\FINDINGS.md`) e foram
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
  confirma que o processo esta de pe). O alerta operacional do broker fica por metrica (F9). Testes
  Infra param o container do RabbitMQ (200 + `Degraded`) e colocam o banco offline (503).
- **Guarda de ambiente.** `ValidadorDasOpcoesDaMensageria` (`ValidateOnStart`) so aceita
  `Transporte=EmMemoria` e `Persistencia=Nenhuma` em `Development` e `Testing`; nos demais
  ambientes a partida falha. A guarda e fail-closed: o binder aceita numeros fora do enum (`0`, `9`),
  entao `AddMensageria` decide o ramo por `switch` que lanca para valor indefinido (e traduz o erro do
  binder para texto invalido) em qualquer ambiente, em vez de tratar "o que nao e RabbitMq" como
  memoria; o validador tambem recusa o valor indefinido.
- **Versoes.** EF Core 9.0.22 (o `MassTransit.EntityFrameworkCore 8.5.12` exige EF Core Relational
  9.0.x) e MassTransit 8.5.12, conforme o ADR 0004.

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
- **A F3a nao pode ser implantada sozinha.** O exchange fanout de cada contrato so e criado no
  primeiro publish e, sem fila ligada, o RabbitMQ descarta a mensagem e o outbox esvazia (medido: a
  1a e a 2a OS criadas sem fila perderam o `OrdemDeServicoAberta`). A F3b (saga) precisa criar a
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
