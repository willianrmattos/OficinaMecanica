# OficinaMecanica - Sistema de Gestao para Oficina Mecanica

## Visao Geral

Backend .NET 8 (EF Core 9) com DDD, Clean Architecture e CQRS (MediatR) para gestao de oficina mecanica. Em evolucao para o OS Service da saga: publica mensagens de integracao pelo outbox do MassTransit/RabbitMQ (ver secao "Mensageria") e hospeda a saga orquestrada da OS (ver secao "Saga (F3b)").

## Estrutura do Projeto

```
src/
  OficinaMecanica.Domain/          # Entidades, Value Objects, Enums, Domain Events, Interfaces
  OficinaMecanica.Application/     # Commands, Queries, Handlers (MediatR), DTOs, Validators (FluentValidation), Event Handlers, Interfaces (ex: IEmailService)
  OficinaMecanica.Infrastructure/  # EF Core 9 (Azure SQL Database), Repositories, JWT Auth (so validacao via JWKS), SmtpEmailService (MailKit), Messaging (MassTransit + RabbitMQ + outbox/inbox), Saga (state machine, activities, sweeper de prazos)
  OficinaMecanica.API/             # Controllers REST, ExceptionHandlingMiddleware, Swagger, Program.cs
tests/
  OficinaMecanica.Domain.Tests/       # Testes unitarios (entidades, VOs, regras de negocio)
  OficinaMecanica.Application.Tests/  # Testes dos handlers e validators (Moq)
  OficinaMecanica.Integration.Tests/  # Testes de integracao (WebApplicationFactory + InMemory, bus em memoria)
  OficinaMecanica.Infrastructure.Tests/ # Testes de infraestrutura: publisher/DI e a maquina de estados da saga sem Docker (harness + EF InMemory) + [Category=Infra] com Testcontainers (SQL Server e RabbitMQ reais)
  OficinaMecanica.Tests.Common/       # Builders de entidades com Bogus, compartilhados por Domain.Tests e Application.Tests
k8s/                                    # Manifests Kubernetes (Deployment, Service, Ingress, HPA) - ver secao "Kubernetes"
local/                                  # Configs da stack de observabilidade do docker-compose (Prometheus, Loki, Alloy, Grafana) - ver secao "Docker"
```

## Convencoes

- **Idioma do dominio**: Todo o dominio (entidades, propriedades, enums, value objects, mensagens de erro, regras de negocio) esta em portugues
- **Idioma da arquitetura**: Namespaces, design patterns, interfaces genericas, nomes de camadas em ingles
- **Exemplos**: Entidade `Cliente`, propriedade `Nome`, enum `StatusOrdemDeServico.Recebida`, interface `IClienteRepository`, namespace `OficinaMecanica.Domain.Entities`

## Comandos Uteis

```bash
# Build
dotnet build

# Testes (sem Docker: pula os de infraestrutura, que sobem SQL Server e RabbitMQ via Testcontainers)
dotnet test --filter "Category!=Infra"

# Testes de infraestrutura (precisam de Docker; ver secao "Mensageria")
dotnet test tests/OficinaMecanica.Infrastructure.Tests --filter "Category=Infra"

# Tudo, inclusive Infra
dotnet test

# Ferramentas versionadas em dotnet-tools.json (dotnet-ef 9.0.22 e reportgenerator); o dotnet-ef global do sistema e antigo demais para o EF Core 9
dotnet tool restore
dotnet ef migrations add <Nome> --project src/OficinaMecanica.Infrastructure --startup-project src/OficinaMecanica.API

# Testes com cobertura (OpenCover)
dotnet test tests/OficinaMecanica.Domain.Tests --collect:"XPlat Code Coverage" --results-directory TestResults/Domain -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet test tests/OficinaMecanica.Application.Tests --collect:"XPlat Code Coverage" --results-directory TestResults/Application -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet test tests/OficinaMecanica.Integration.Tests --collect:"XPlat Code Coverage" --results-directory TestResults/Integration -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet test tests/OficinaMecanica.Infrastructure.Tests --collect:"XPlat Code Coverage" --results-directory TestResults/Infrastructure -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Cobertura mesclada dos projetos (exclusoes: Migrations, Program.cs, DependencyInjection*, EF Configurations)
dotnet reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:TestResults/Report -reporttypes:TextSummary "-assemblyfilters:+OficinaMecanica.Domain;+OficinaMecanica.Application;+OficinaMecanica.Infrastructure;+OficinaMecanica.API" "-classfilters:-*Migrations*;-*DependencyInjection*;-*Configuration;-Program"

# Executar API localmente
dotnet run --project src/OficinaMecanica.API

# Docker (sobe API, SQL Server, RabbitMQ, SonarQube e a stack de observabilidade; ver secao "Docker")
docker compose up -d

# Rebuild da imagem da API apos mudancas de codigo
docker compose build --no-cache api && docker compose up -d

# Analise SonarQube (requer SonarQube UP e token em SONAR_TOKEN)
# Token em: http://localhost:9000/account/security  |  login: admin / SONAR_ADMIN_PASSWORD (ver .env)
dotnet sonarscanner begin /k:"oficina-mecanica" /n:"OficinaMecanica" /v:"1.0.0" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="$SONAR_TOKEN" /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" /d:sonar.cs.vstest.reportsPaths="**/*.trx" /d:sonar.exclusions="stress/**"
dotnet build --no-incremental -c Release
# (rodar os testes com cobertura acima)
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
```

## NuGet

O projeto usa um `nuget.config` local que aponta para `nuget.org` (o feed global do sistema tem um source privado desabilitado). Ao adicionar pacotes, usar:
```bash
dotnet add <projeto> package <pacote> --source https://api.nuget.org/v3/index.json
```

## Arquitetura e Padroes

- **Clean Architecture**: Domain -> Application -> Infrastructure -> API (dependencias de fora para dentro)
- **DDD**: Aggregates (Cliente, OrdemDeServico, Servico, Peca), Entities (Veiculo, ItemServico, ItemPeca, HistoricoStatus), Value Objects (Documento, Placa), Domain Events
- **CQRS**: Commands (escrita) e Queries (leitura) separados, handlers via MediatR
- **ValidationBehavior**: Pipeline MediatR que roda FluentValidation antes dos handlers. Restringido a `where TRequest : notnull` (e nao a `IRequest<TResponse>`) para tambem fechar para commands sem retorno (`IRequest`), e cada validator recebe um `ValidationContext` proprio (um contexto compartilhado repetiria as falhas do primeiro validator no resultado do segundo)
- **Repository Pattern**: Interfaces no Domain, implementacoes na Infrastructure
- **Unit of Work**: AppDbContext implementa IUnitOfWork e despacha Domain Events apos SaveChanges; se ha transacao do chamador aberta (consumer/saga do MassTransit), os eventos ficam guardados e saem so depois do COMMIT, em escopo DI novo (ver secao "Mensageria")
- **Concorrencia Otimista**: Propriedade `Version` como ConcurrencyToken
- **Testes**: Moq para mocks (Application.Tests), Bogus para dados fake, builders fluentes em `OficinaMecanica.Tests.Common/Builders/` (ClienteBuilder, VeiculoBuilder, PecaBuilder, ServicoBuilder, OrdemDeServicoBuilder)

## Fluxo da Ordem de Servico

```
Recebida -> EmDiagnostico -> AguardandoAprovacao -> AguardandoPagamento -> EmExecucao -> Finalizada -> Entregue
                                    |                       |
                                    +-> OrcamentoRecusado   +-> Cancelada
```

`Cancelada` tambem sai de `Recebida`, `EmDiagnostico` e `AguardandoAprovacao`; de `EmExecucao` so como compensacao (execucao inviavel/estorno). Transicoes controladas pelo dominio (`OrdemDeServico.cs`, dicionario `TransicoesPermitidas`). Fluxo completo da saga: `docs/saga-maquina-de-estados.puml`, decisoes em `docs/adr/0003|0006` e `docs/rfc/0005`.

- **Enum persistido como int, nunca renumerar**: `OrcamentoRecusado=0 ... Entregue=6`, `AguardandoPagamento=7`, `Cancelada=8`. Por isso a listagem nao ordena mais pelo numero (ver abaixo).
- **Aprovar orcamento** (`AprovarOrcamento()`) vai pra `AguardandoPagamento` e **nao da mais baixa no estoque**. `IniciarExecucao()` leva `AguardandoPagamento -> EmExecucao`. Criar OS so *valida* estoque; a reserva acontece depois, na aprovacao.
- **Campos novos da OS**: `ParticipaDaSaga` (true no construtor; OS antigas ficam false pelo default da coluna e respondem 409 em aprovar/recusar/cancelar), `EstoqueReservado`, `PagamentoId`, `LinkPagamento`, `PagamentoExpiraEm`, `MotivoCancelamento`. `Cancelar(motivo)`, `RegistrarPagamentoSolicitado`, `ReservarEstoque`/`LiberarEstoque` (so o flag) sao idempotentes; `JaAlcancou(status)` e o que torna os commands internos no-op em reentrega.
- **Mensagens de integracao** (`OficinaMecanica.Contracts.*`): publicadas so pelo handler via `IIntegrationEventPublisher` (`Application/Interfaces`), **antes** do `SaveChangesAsync` (e ele que grava o outbox, mesmo sem mudar agregado). A implementacao real (`MassTransitIntegrationEventPublisher`, outbox do EF) e registrada por `AddMensageria` (ver secao "Mensageria"); os testes de integracao a trocam por um fake no `CustomWebApplicationFactory`. `TimeProvider.System` esta registrado na Infrastructure; `MessageId` = `Guid.NewGuid()`, `OcorridoEm` = `TimeProvider.GetUtcNow()`.
- **Commands HTTP so publicam, nao mudam status**: `AprovarOrcamento`/`RecusarOrcamento`/`CancelarOrdemDeServico` validam (OS existe -> `ParticipaDaSaga` -> status; `ConflitoException` -> 409) e publicam `ClienteAprovouOrcamento`/`ClienteRecusouOrcamento`/`CancelamentoSolicitado`. Quem muda o status e a saga (F3) chamando os **commands internos idempotentes**: `MarcarDiagnosticoIniciado`, `RegistrarOrcamentoGerado`, `ReservarEstoque` (tudo-ou-nada, devolve `ResultadoDaReservaDeEstoque`), `LiberarEstoque`, `AplicarAprovacaoDoOrcamento`, `AplicarRecusaDoOrcamento`, `RegistrarPagamentoSolicitado`, `IniciarExecucaoDaOrdem`, `FinalizarOrdem`, `CancelarOrdemPelaSaga` (nao rejeita motivo: vazio vira texto padrao e acima de 400 e truncado; so o HTTP `CancelarOrdemDeServico` exige e limita). Eles nao publicam nada e chamam `SaveChangesAsync`. Nao-encontrado continua `DomainException` (400), como no resto do repo.
- **Retry da saga (F3)**: o retry do MassTransit deve ter `r.Ignore<ConflitoException>()`, `r.Ignore<DomainException>()` e `r.Ignore<ValidationException>()` (do FluentValidation; regra de negocio violada ou validacao reprovada nao se resolvem repetindo).
- **E-mail**: `StatusOrdemAlteradoEventHandler` segue cobrindo toda transicao (`AguardandoPagamento` avisa que o link vem em seguida); `PagamentoSolicitadoEventHandler` manda o link + prazo (horario de Brasilia, offset fixo -03:00).
- **Listagem**: ordem por `CASE` explicito em `OrdemDeServicoRepository` (`EmExecucao > AguardandoPagamento > AguardandoAprovacao > EmDiagnostico > Recebida > OrcamentoRecusado = Cancelada`), substitui o `OrderByDescending(Status)` descrito na secao "Listagem de Ordens de Servico" abaixo.
- **Migration**: `AdicionaPagamentoEMensageria` cobre as colunas novas da OS (`ParticipaDaSaga` e `EstoqueReservado` com default `false` no schema, `PagamentoId`, `LinkPagamento`, `PagamentoExpiraEm`, `MotivoCancelamento`) e as tabelas do outbox/inbox (`OutboxMessage`, `OutboxState`, `InboxState`). Validada num SQL Server real a partir da `Initial`: OS antigas ficam com `ParticipaDaSaga = 0`.

## Autenticacao

- A API so **valida** JWT Bearer, nao emite mais nada — login/token viraram
  responsabilidade exclusiva do `OficinaMecanica.Seguranca` (repositorio
  irmao, Azure Function separada), extraido daqui. `AuthController`/
  `TokenService`/`ITokenService`/`AdminCredentials` foram removidos por
  completo desta base de codigo.
- Validacao via **RS256/JWKS**, nao mais uma chave simetrica fixa:
  `DependencyInjection.cs` monta um `ConfigurationManager<JsonWebKeySet>`
  (com um `JsonWebKeySetRetriever` proprio — a classe com esse nome citada
  em alguma documentacao do `Microsoft.IdentityModel.Protocols` nao existe
  de verdade no pacote, so tem retriever pronto pra documento OIDC completo,
  nao pra um JWKS cru) que busca e cacheia a chave publica periodicamente
  em `JwtSettings:JwksUri`, resolvendo por `kid` via
  `TokenValidationParameters.IssuerSigningKeyResolver`. `JwtSettings:Issuer`
  precisa bater com o que o Seguranca realmente emite
  (`"OficinaMecanica.Seguranca"`, nao mais `"OficinaMecanica.API"`).
- O `HttpDocumentRetriever` usado pelo `ConfigurationManager<JsonWebKeySet>`
  recusa por padrao qualquer `JwksUri` que nao seja `https://`
  (`RequireHttps = true`) — em producao nunca aparece (a APIM real e
  HTTPS), mas o `docker-compose.yml` local aponta pro Seguranca rodando sem
  TLS (`http://host.docker.internal:7071/...`, `extra_hosts:
  host-gateway` pra alcancar o outro projeto de Compose). Por isso
  `DependencyInjection.cs` monta esse retriever manualmente com
  `RequireHttps` condicional ao esquema da propria `JwksUri` configurada,
  em vez de deixar o `ConfigurationManager` criar um por conta propria.
- **Pegadinha de dev local**: o `ConfigurationManager<JsonWebKeySet>` cacheia
  o JWKS buscado (nao refaz o fetch a cada validacao). Quando o
  `OficinaMecanica.Seguranca` local roda com o fallback de chave RSA
  efemera (`KeyVault:Uri` vazio — ver `CLAUDE.md` dele), cada restart do
  container gera uma chave nova só que com o **mesmo `kid` fixo**
  (`"local-dev"`) — o monolito acha uma chave com o `kid` esperado (nao
  refaz o fetch) mas ela nao bate mais, e a validacao falha com
  `"The signature is invalid"` (nao "chave nao encontrada"). Se isso
  acontecer testando local, o fix e reiniciar o container `api` do
  monolito (`docker compose restart api`) pra forcar um fetch novo do JWKS
  atual, depois gerar um token novo no Seguranca.
- Endpoints publicos: consulta OS por numero (`/api/ordens-de-servico/numero/{numero}`, agora com `linkPagamento`/`pagamentoExpiraEm` quando houver), aprovacao (`/api/ordens-de-servico/{id}/aprovar`) e recusa de orcamento (`/api/ordens-de-servico/{id}/recusar`)
- **Quebras de API (ADR 0006)**: aprovar/recusar respondem **202 Accepted** com `{ ordemDeServicoId, numero, status }` (status ainda `AguardandoAprovacao`: a decisao so foi publicada pra saga) em vez de 200 + DTO; **409** se a OS e anterior a saga (`ParticipaDaSaga=false`) ou nao esta em `AguardandoAprovacao`. `PATCH /api/ordens-de-servico/{id}/status` so aceita `Entregue` (validator; os demais status dao 400). Novo `POST /api/ordens-de-servico/{id}/cancelar` (JWT, corpo `{ "motivo": "..." }` obrigatorio, 202; 409 se OS pre-saga ou de `EmExecucao` em diante)
- `/health` e `/metrics` tambem sao anonimos (nao passam por `[Authorize]`, mapeados via `MapHealthChecks`/`MapMetrics` fora do `MapControllers()`) — ver secao "Observabilidade"
- Demais endpoints exigem token JWT
- Testes de integracao (`CustomWebApplicationFactory.cs`) nao dependem do
  JWKS real pela rede: cada factory tem sua propria instancia de chave RSA
  (`TestSigningKey`, uma por classe de teste via `IClassFixture` — **nao**
  um campo `static` compartilhado, RSA nao e thread-safe pra assinar/validar
  concorrentemente entre classes rodando em paralelo, isso ja causou falha
  intermitente real) e um `PostConfigure<JwtBearerOptions>` que sobrescreve
  so a resolucao da chave pra essa instancia fixa. `TestTokenFactory.GerarToken(factory)`
  monta o JWT de teste assinado com a mesma chave.

## Listagem de Ordens de Servico

- `GET /api/ordens-de-servico` sem `filtroStatus` explicito exclui por padrao `Finalizada` e `Entregue` (exclusao logica via filtro de query, sem soft-delete fisico) e ordena por prioridade de status (`EmExecucao > AguardandoAprovacao > EmDiagnostico > Recebida > OrcamentoRecusado`, via `OrderByDescending(Status)` — a numeracao do enum ja reflete essa prioridade), mais antigas primeiro dentro do mesmo status (`ThenBy(DataAbertura)`)
- Passando `filtroStatus` explicitamente, o filtro sobrepoe a exclusao padrao (continua possivel consultar `Finalizada`/`Entregue` quando pedido)
- Logica em `OrdemDeServicoRepository.ListarAsync`/`ContarAsync` (namespace `OficinaMecanica.Infrastructure.Repositories`)

## Notificacao por E-mail

- Toda mudanca de status de uma OS dispara e-mail ao cliente (se tiver `Email` cadastrado), via um unico `INotificationHandler<StatusOrdemAlteradoEvent>` (`StatusOrdemAlteradoEventHandler`, `OficinaMecanica.Application.EventHandlers`) — cobre todas as transicoes (aprovacao, recusa, avanco de diagnostico etc.) sem precisar de um handler por evento especifico
- `IEmailService` (`OficinaMecanica.Application.Interfaces`) implementado por `SmtpEmailService` (`OficinaMecanica.Infrastructure.Services`, pacote `MailKit`) — sem provedor externo (SendGrid/etc), fala SMTP direto com um servidor configurado via `Smtp:Host`/`Smtp:Port`/`Smtp:Remetente`
- Resiliente de proposito: qualquer falha (SMTP fora do ar, e-mail mal formado, timeout de 5s) e so logada como Warning, nunca lanca excecao — o handler roda de forma sincrona dentro do `SaveChangesAsync` (`AppDbContext.cs`) no caminho HTTP, uma falha aqui nao pode derrubar a resposta HTTP do endpoint que mudou o status. Dentro de um consumer (transacao do MassTransit) o e-mail so sai depois do commit, uma vez por mensagem confirmada, e nao por tentativa
- Sem `Smtp:Host` configurado (default em `appsettings.json`), o envio e so ignorado com um Warning — a API funciona normalmente sem SMTP configurado
- Dev local: servico `mailpit` no `docker-compose.yml` (UI web em `http://localhost:8025` pra ver os e-mails capturados, nao entrega nada de verdade pra fora)
- AKS: mesmo Mailpit rodando dentro do cluster (`k8s/mailpit/`, imagem `axllent/mailpit`), pra poder demonstrar o fluxo de notificacao por e-mail sem depender de SMTP externo — `k8s/oficinamecanica-api/deployment.yaml` aponta `Smtp__Host`/`Smtp__Port` pro Service `mailpit` (mesmo namespace), UI web exposta via `k8s/mailpit/ingress.yaml` (host nip.io, mesmo padrao do Grafana/Prometheus)

## Busca de Cliente por Documento

- `GET /api/clientes/documento/{numero}` — busca por CPF (11 digitos) ou CNPJ (14 digitos), sem formatacao
- Handler: `ObterClientePorDocumentoQueryHandler` (namespace `OficinaMecanica.Application.Queries.ObterCliente`)
- Retorna 404 quando o documento e invalido ou nao encontrado (nao lanca excecao para documentos invalidos)

## Banco de Dados

- SQL Server via EF Core 9 (9.0.22; o `MassTransit.EntityFrameworkCore 8.5.12` exige EF Core Relational 9.x, mesmo no alvo `net8.0`) — o banco em si difere entre ambientes, sem nenhuma mudanca no codigo da aplicacao (so a connection string muda):
  - **AKS/producao**: **Azure SQL Database** real (`svsfiap.database.windows.net`, tier serverless sempre-gratis, provisionado via `OficinaMecanica.Infra/sqldb/`), connection string sincronizada do Key Vault (ver secao "Kubernetes")
  - **Docker Compose/desenvolvimento local**: container `sqlserver` (`mcr.microsoft.com/mssql/server:2022-latest`, edicao Developer) no proprio `docker-compose.yml`, com volume nomeado (`sqlserver_data`) pra persistir entre `docker compose down`/`up` — optei por rodar local em vez de sempre depender do Azure SQL estar acessivel/acordado (tier serverless com auto-pause), so pra desenvolver
- Configurations em `Infrastructure/Data/Configurations/`; as tabelas do outbox/inbox do MassTransit entram no `OnModelCreating` (`AddInboxStateEntity`, `AddOutboxMessageEntity`, `AddOutboxStateEntity`), no mesmo banco, para participar da transacao do `SaveChanges`
- **EF Core 9 recusa `Migrate()` quando o modelo tem mudancas sem migration** (`PendingModelChangesWarning`): depois de mexer em entidade ou configuration, gerar a migration (`dotnet ef migrations add`, ver "Comandos Uteis")
- `EnableRetryOnFailure` (auto-pause do Azure SQL serverless, erro 40613) fica ligado so fora de consumer; dentro dele a resiliencia vem do `UseMessageRetry` (ver "Mensageria")
- Testes de integracao com EF Core 9: o `AddDbContext` acumula `IDbContextOptionsConfiguration<T>`, entao o `CustomWebApplicationFactory` remove essa configuracao (alem de `DbContextOptions<T>`) antes de registrar o InMemory
- Migration aplicada automaticamente no startup (Program.cs, apenas em Development) — roda contra qualquer um dos dois bancos acima, ja que o docker-compose mantem `ASPNETCORE_ENVIRONMENT=Development`
- No Docker Compose, a connection string do `sqlserver` local e montada direto no `docker-compose.yml` (usuario `sa`, senha vinda de `MSSQL_SA_PASSWORD` no `.env`) — nao ha mais dependencia do Azure SQL pra desenvolvimento local
- `appsettings.json`/`appsettings.Development.json` tem a senha vazia (`Password=;`) na connection string — nunca commitar a senha real ali, o `.env`/`docker-compose.yml` sempre tem precedencia quando rodando via `docker compose`
- O `.env` tambem carrega as demais credenciais usadas pelo `docker-compose.yml` (nenhuma fica hardcoded no arquivo versionado): `MSSQL_SA_PASSWORD` (SQL Server local), `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS` (RabbitMQ local; **obrigatorias: o compose usa `${VAR:?}` e nao sobe sem elas; adicionar ao `.env` existente**, ver `.env.example`), `GRAFANA_ADMIN_PASSWORD` (Grafana local), `SONAR_DB_USER`/`SONAR_DB_PASSWORD` (Postgres do SonarQube) e `SONAR_ADMIN_PASSWORD` (senha definida pro admin do SonarQube no primeiro boot, via `sonar-setup`)

## Mensageria

Infraestrutura de mensageria do OS Service (F3a). Decisoes e achados do spike em `docs/adr/0007-outbox-inbox-transacional-e-publicacao-antes-do-commit.md`; MassTransit fixado em `8.5.12` (nunca 9.x, ADR 0004).

- **Composicao**: `Infrastructure/Messaging/MensageriaExtensions.cs`, `AddMensageria(services, configuration, registrarConsumers?, configurarEndpoints?)`, chamado no `Program.cs` depois de `AddInfrastructure`. Registra o bus RabbitMQ, `MassTransitHostOptions.WaitUntilStarted = true`, o outbox/inbox do EF (`AddEntityFrameworkOutbox<AppDbContext>` com `UseSqlServer()` + `UseBusOutbox()`, **sem** definir `IsolationLevel`: `Serializable` quebra o servico de entrega) e `MassTransitIntegrationEventPublisher` como `IIntegrationEventPublisher`.
- **Publicar sempre antes do `SaveChangesAsync`** (e o save que grava o `OutboxMessage`); o publisher copia o `MessageId` do contrato para o do envelope (e por ele que o inbox deduplica).
- **Endpoints**: use `ConfigurarRetryEOutbox(contexto)` (extension de `IReceiveEndpointConfigurator`) antes de `ConfigureConsumer`/`ConfigureSaga`: ela monta `UseMessageRetry` (intervalos exponenciais, ignora `DomainException`/`ConflitoException`/`ValidationException`) por fora de `UseEntityFrameworkOutbox`. O pipeline resultante e retry por fora, outbox/inbox por dentro, consumer por ultimo, e `ConfigurarRetryEOutboxTests` o le do probe do bus (`GetProbeResult`). Medido no MassTransit 8.5.12: ele monta o retry por fora mesmo se `UseEntityFrameworkOutbox` for chamado antes de `UseMessageRetry`, entao a ordem das chamadas nao muda o pipeline; o helper a mantem explicita e o teste protege contra uma versao ou configuracao que mude isso. `OrdemDosFiltrosDoEndpointTests` prova o efeito: so a publicacao da tentativa que confirma sai do banco.
- **EF retry** so fora de consumer (`IScopedConsumeContextProvider.HasContext == false`, em `DependencyInjection.cs`); dentro, quem repete e o `UseMessageRetry`.
- **Domain events pos-commit** (`AppDbContext` + `DomainEventsPosCommitInterceptor`): com transacao do chamador aberta os eventos ficam guardados e saem em `TransactionCommittedAsync`, em escopo DI novo; descartados em rollback ou commit que falha (o buffer de um rollback nao vaza para o commit seguinte do mesmo `DbContext`). Handler que falha depois do commit e so logado; um handler que escreve no banco so funciona por causa do escopo novo.
- **Variaveis** (`docker-compose.yml`/`appsettings.json`): `RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__Username`, `RabbitMq__Password`; opcionais `Mensageria__RetryIntervalosMs__0..n` (padrao 500 ms, 2 s, 10 s, 30 s, 60 s), `Mensageria__OutboxQueryDelayMs` (1000) e `Mensageria__TimeoutDeInicioSegundos` (60). A API **nao sobe sem o RabbitMQ** (`WaitUntilStarted`, cai apos ~60 s sem broker); local, ele vem do `docker-compose.yml` (secao "Docker"). **`dotnet run` local exige `RabbitMq__Username` e `RabbitMq__Password`** (o `appsettings.json` os deixa vazios, como a senha do banco): sem eles o MassTransit tenta `guest`/`guest`, que o RabbitMQ do compose nao aceita (as credenciais vem de `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS` do `.env`).
- **Ponto de troca para testes**: `Mensageria__Transporte` (`RabbitMq` padrao | `EmMemoria`) e `Mensageria__Persistencia` (`EntityFramework` padrao | `Nenhuma`). O `CustomWebApplicationFactory` os troca via `UseSetting` (nao via `ConfigureAppConfiguration`, que so entra depois de o `Program` ler a configuracao) porque o EF InMemory nao suporta o outbox transacional. `AddMassTransitTestHarness()` chamado depois de `AddMensageria` com esses valores funciona (a base do BDD da F3b).
- **Guarda de ambiente**: `ValidadorDasOpcoesDaMensageria` (`IValidateOptions` + `ValidateOnStart`) recusa a partida fora de `Development`/`Testing` se `Transporte != RabbitMq` ou `Persistencia != EntityFramework`, e e fail-closed em qualquer ambiente: `AddMensageria` lanca `InvalidOperationException` (pt-BR) para numero fora do enum (`0`, `9`, que o binder aceita) e para texto que nao e valor do enum, em vez de cair num ramo padrao "em memoria" (`OpcoesDaMensageria.UsaRabbitMq`/`UsaOutboxDoEntityFramework` sao `switch` que lanca), e o validador tambem recusa o valor indefinido. Os testes que simulam producao usam `Staging`.
- **Health check de verdade**: o check do bus do MassTransit (tag `ready`) continua saudavel com o RabbitMQ parado enquanto o servico nao tem receive endpoints (medido: 200 por 80 s). Por isso `RabbitMqHealthCheck` abre uma conexao AMQP real (timeout de 3 s, `RabbitMQ.Client` que ja vem com o `MassTransit.RabbitMQ`) com a tag `ready` e responde `Degraded` (nao `Unhealthy`) quando o broker nao responde: `/health/ready` continua 200 e so o SQL Server fora vira 503 (ver "Observabilidade"). So existe com `Mensageria__Transporte=RabbitMq`.
- **Testes**: `tests/OficinaMecanica.Infrastructure.Tests`. Sem Docker (publisher + `MessageId`, registro no DI, retry do EF por escopo, validador de ambiente, despacho de domain events do `AppDbContext` sobre EF InMemory): `dotnet test --filter "Category!=Infra"`. Com Docker, `[Trait("Category","Infra")]`: Testcontainers com SQL Server (`mcr.microsoft.com/mssql/server:2022-latest` fixado por digest, `MSSQL_MEMORY_LIMIT_MB=1024`) e RabbitMQ (`rabbitmq:4.4.0-management`), containers `os-f3a-*`, um banco e um virtual host novos por teste; rodar com `dotnet test tests/OficinaMecanica.Infrastructure.Tests --filter "Category=Infra"` (cobrem: outbox atomico no caminho HTTP com rollback, publish depois do save como controle negativo, mensagem duplicada consumida uma vez, excecoes de negocio vao direto para `_error` e falha tecnica esgota o retry, ordem dos filtros, retry do EF fora de consumer com conexao derrubada, domain events pos-commit em escopo novo sem e-mail fantasma, `MessageId` do envelope, `/health/ready` com o RabbitMQ parado (200, rabbitmq `Degraded`) e com o banco offline (503) e a migration aplicada a partir da `Initial`). Os consumers desses testes (F3a) sao de teste; a saga tem os dela (secao "Saga (F3b)").
- **Fila ligada antes do trafego (resolvido na F3b)**: o exchange fanout de cada contrato so nasce no primeiro publish e, sem fila ligada, o RabbitMQ descarta a mensagem e o outbox esvazia (uma OS criada antes de a fila existir perde o `OrdemDeServicoAberta`). O endpoint `os-saga` (`Filas.Saga`) da saga e criado na subida do bus (`WaitUntilStarted`), com o binding de `OrdemDeServicoAberta`, antes da API aceitar a primeira OS; o compose prova a fila existindo ao subir. No k8s (F7): `/health` como liveness, `startupProbe` com folga de pelo menos 60 s (sem broker no boot a API cai apos ~60 s por `WaitUntilStarted`) e readiness em `/health/ready` (503 so com o SQL fora; broker fora fica `Degraded`/200). Cada probe de readiness abre uma conexao com o banco e, no Azure SQL serverless, impede o auto-pause: usar `periodSeconds` alto. Os checks de prontidao (SQL e broker, em paralelo) levam ate 3 s e o `timeoutSeconds` padrao do Kubernetes e 1 s: a readiness precisa de `timeoutSeconds >= 5`, e o `startupProbe`, de janela de pelo menos 60 s.
- **Feito na F3b**: saga, activities, endpoint `os-saga`, sweeper de prazos e migration (secao "Saga (F3b)"). **Fica para a F3c**: BDD (Reqnroll), consulta sincrona a Execucao e metricas. A metrica de saga presa / compensacao falhou entra junto das demais metricas da saga (fase de codigo seguinte); dashboard e alerta (NRQL) ficam para a documentacao final. **Fica para F6/F7**: RabbitMQ no AKS e as variaveis `RabbitMq__*` no `k8s/oficinamecanica-api/deployment.yaml` (hoje ausentes: sem elas o pod nao sobe), step de CI que aplica migrations e job dos testes Infra.

## Saga (F3b)

Orquestrador da OS em `Infrastructure/Saga`. A ESPECIFICACAO e `docs/saga-maquina-de-estados.puml` (cada transicao existe na maquina e tem teste de comportamento; `SagaGrafoDaMaquinaTests` LE o puml e compara com o grafo da maquina dois recortes, que eventos movem cada estado e para onde cada evento leva, e o trio origem + evento -> destino, que o grafo nao expoe, fica com os testes de comportamento de cada transicao; o rotulo de cada seta e `Evento1 | Evento2 [condicao]`, com `Fault<Comando>` valendo o evento `FalhaAo<Comando>`); decisoes em `docs/adr/0003` e `docs/adr/0008`; aqui fica so o que e preciso para mexer no codigo.

- **Composicao**: o `Program.cs` chama `AddMensageriaComSaga(configuration)` = `AddServicosDaSaga` (opcoes `Saga__*` com `ValidateOnStart`, `IPrazoRepository`, sweeper, activities no escopo) + `AddMensageria` com a saga nos ganchos (`AddSagaDaOrdemDeServico` em `registrarConsumers`, `ReceiveEndpointDaSaga` em `configurarEndpoints`). Teste que precisa de mais consumers usa as tres pecas direto (ver `Support/AmbienteDaSagaInfra`). Com `Mensageria__Persistencia=Nenhuma` (so Development/Testing) o repositorio da saga e o em memoria.
- **Estado** (`OrdemDeServicoSagaState`, tabela `OrdemDeServicoSagas`, mesmo `AppDbContext`, mapa aplicado em `OnModelCreating`): `CorrelationId` = `OrdemDeServicoId`, `CurrentState`, `Numero`, `OrcamentoId?`, `PagamentoId?`, `PrazoAprovacaoId?`/`PrazoPagamentoId?` (prazo VIGENTE de cada tipo; nulo quando a etapa termina), `CancelamentoPorStaff`, `MotivoCancelamento?` (motivo do encerramento, guardado ate o passo que o aplica), `CriadaEm`, `AtualizadaEm`. Sem `SetCompletedWhenFinalized`: a linha fica nos finais.
- **Estados**: `AguardandoDiagnostico`, `EmDiagnostico`, `GerandoOrcamento`, `AguardandoAprovacao`, `AguardandoPagamento`, `CancelandoPagamento`, `EmExecucao`, `Estornando`, `EstornoPendenteManual` e finais `Concluida`, `Cancelada`, `OrcamentoRecusado`. Evento sem transicao no estado e IGNORADO (`DuringAny(Ignore)`; o teste `Maquina_DeveDeclararTodoEventoEmTodoEstado...` quebra se um evento novo ficar sem tratamento); as tres decisoes de uma pessoa (`ClienteAprovouOrcamento`, `ClienteRecusouOrcamento`, `CancelamentoSolicitado`) ignoradas onde nao tem mais efeito deixam um log `Warning` com o id da OS (`LogarDecisaoIgnoradaActivity`, declarado por estado em `DeclararDecisoesSemEfeito`); evento antes de a saga existir usa `OnMissingInstance(Fault)` + retry do endpoint; `PrazoExpirado` so age com o `PrazoId` e o `Tipo` vigentes.
- **Activities** (`Saga/Activities`, uma por passo, base `AtividadeDaSaga`): chamam UM command idempotente da Application no mesmo escopo/DbContext/transacao do consumer (`IMediator`), enviam o comando com `Send` para `queue:{Filas.X}` e agendam prazos por `IPrazoRepository` (sem EF na activity). Excecao dentro dela desfaz tudo (nada de rollback manual) e o retry reprocessa. A leitura da OS para montar mensagens e `ObterSnapshotDaOrdemQuery`.
- **Ids dos comandos**: `IdentificadorDeMensagem.Para(os, nomeDoComando, discriminador?)` (SHA-256), copiado para o `MessageId` do envelope; discriminador = janela de pagamento em `SolicitarPagamento`/`CancelarPagamento`, `PagamentoId` em `EstornarPagamento`. `OcorridoEm` = `TimeProvider`.
- **`Fault<T>`** (falha tecnica; log de erro estruturado sempre): `SolicitarDiagnostico`/`GerarOrcamento` cancelam a OS; `SolicitarPagamento` age como `PagamentoNaoSolicitado`; `IniciarExecucao` age como `ExecucaoInviavel` (estorna); `EstornarPagamento` -> `EstornoPendenteManual`; `CancelarPagamento`/`CancelarOrcamento`/`CancelarProducao` so logam: SEM reenvio automatico (loop com mensagem venenosa) e sem transicao, a saga fica onde estava e o comando falho espera na `<fila>_error` do destino ate o redrive manual (idempotente: id deterministico). O log de erro tem template estavel (`Falha técnica no comando {Comando} da ordem de serviço {OrdemDeServicoId} (saga em {Estado}), mensagem {MessageIdDoComando} ...`; alerta por texto). Detalhes e runbook de redrive no ADR 0008. A producao so e cancelada no cancelamento do staff.
- **Sweeper de prazos** (`SweeperDePrazosBackgroundService` -> `SweeperDePrazos` -> `IPrazoRepository.ProcessarVencidosAsync`): a cada `Saga__SweeperIntervaloSegundos` reivindica ate `Saga__SweeperLote` linhas vencidas de `SagaPrazos` com `UPDLOCK, READPAST, ROWLOCK, INDEX(IX_SagaPrazos_Processado_ExpiraEm)` (SQL cru, `TimeProvider` como parametro), marca `Processado` e publica `PrazoExpirado` pelo outbox na MESMA transacao; nunca escreve na linha da saga; varias replicas (HPA) sao seguras. Lote cheio repete na hora. As dicas travam o que o plano LE: sem o `INDEX(...)` uma instancia trava tudo e as outras ficam com zero. Fora de provider relacional (EF InMemory) o repositorio le sem lock.
- **Variaveis** (`appsettings.json`, secao `Saga`): `Saga__PrazoAprovacaoHoras` (72, tambem o `ValidoAte` do orcamento), `Saga__PrazoPagamentoHoras` (48), `Saga__SweeperIntervaloSegundos` (30), `Saga__SweeperLote` (50), todas > 0; `Saga__SweeperHabilitado` (true) so desliga o `BackgroundService` (testes disparam o sweeper a mao).
- **Divergencias do puml** (puml e ADR 0003 ja atualizados): `DiagnosticoConcluido` antes de `DiagnosticoIniciado` adianta o inicio; `CancelamentoSolicitado` em `CancelandoPagamento` marca o staff e desfaz a producao; transicoes de `Fault<T>`; `OnMissingInstance` e `Fault` (nao `Discard`).
- **Migration** `AdicionaSagaEPrazos` (`OrdemDeServicoSagas`, `SagaPrazos`, indice `IX_SagaPrazos_Processado_ExpiraEm`), validada em SQL Server real a partir de `AdicionaPagamentoEMensageria` (colunas, defaults, indices, `Down`).
- **Testes da saga**: sem Docker (`Category!=Infra`, em `Infrastructure.Tests/Saga`) a maquina REAL roda no `ITestHarness` com repositorio de saga em memoria, EF InMemory e handlers/commands reais (`Support/AmbienteDaSaga` + `CenarioDaSaga`; o teste faz o papel do Billing e da Execucao publicando os eventos e lendo `Harness.Sent`); o harness SUBSTITUI o bus do `AddMensageria`, por isso o endpoint `os-saga` e redeclarado no bloco do harness. Com Docker (`Category=Infra`, em `Infrastructure.Tests/Infra`: `SagaPonteAPonteTests`, `SagaConcorrenciaTests`, `SagaAtomicidadeTests`, `SweeperComSqlTests`, `MigrationTests`): SQL Server 2022 + RabbitMQ 4 (containers `os-f3b-*`), com consumers de teste (`ServicosSimulados`) nas filas reais do Billing e da Execucao; um consumer pode lancar excecao tecnica (`LancarExcecaoTecnicaEm<T>`), o que exercita o `Fault<T>` REAL (consumer lanca, `_error` do destino, `Fault<T>` na fila `os-saga`): `SolicitarPagamento` compensa (Cancelada, estoque devolvido) e `CancelarPagamento` so loga e deixa a saga em `CancelandoPagamento`, com um teste de redrive que devolve a mensagem da `_error` (`InfraFixture.RedriveDaFilaDeErroAsync`) e fecha a saga. `SagaConcorrenciaTests` captura o SQL (`CapturadorDeSql`, `DbCommandInterceptor`) e exige `UPDLOCK` na leitura da linha da saga: o bloqueio vem do padrao do MassTransit 8.5.12 (modo `Pessimistic` e SQL Server; o mesmo SQL sai sem as duas linhas explicitas de `SagaExtensions`) e o teste reprova se o modo virar `Optimistic`. Rodar: `dotnet test --filter "Category!=Infra"` (segundos) e `dotnet test tests/OficinaMecanica.Infrastructure.Tests --filter "Category=Infra"` (~5 min; precisa de Docker e ~1,5 GB livres).
- **Medicao da F3b (2026-11-08)**: testes Contracts 71, Domain 145, Application 287, Integration 93, Infrastructure 302 (243 sem Docker + 59 `Category=Infra`), 898 no total. Cobertura de linhas mesclada (`reportgenerator`, todos os projetos de teste incluindo Infra): **bruta 95,5%** (5702/5966; Application 96,4%, Infrastructure 97,7%, Domain 91,2%, API 74,1%) e **93,4% com as exclusoes documentadas** (Migrations, `DependencyInjection*`, `*Configuration`, `Program`: 2820/3018). Mutacao rapida numa copia fora do repo: mutantes na ordem das transicoes, no `PrazoId` vigente, em `CancelamentoPorStaff`, na reserva, nas compensacoes ausentes, no `Ignore`, no `OnMissingInstance`, nos ids, nas dicas do claim (inclusive o `INDEX`), no `<=` do vencimento, no modo `Optimistic` do repositorio e na lista de estados de `DeclararDecisoesSemEfeito` (um mutante por estado), todos mortos pelos testes. O `UPDLOCK` na leitura da linha da saga e o padrao do MassTransit (o SQL e o mesmo com e sem as duas linhas explicitas de `SagaExtensions`) e quem o garante e o teste que captura o SQL.
- **Para a F3c (BDD, consulta sincrona, metricas)**: o BDD sobe a API pelo `CustomWebApplicationFactory` (`Transporte=EmMemoria`, `Persistencia=Nenhuma`, `Saga:SweeperHabilitado=false`) e `AddMassTransitTestHarness(...)` DEPOIS de `AddMensageria`: o harness remove o bus e o repositorio de saga do `AddMensageria`, entao a saga e o endpoint precisam ser redeclarados no bloco do harness (`bus.AddSagaDaOrdemDeServico(repositorioEmMemoria: true)` + `bus.UsingInMemory((c, e) => e.ReceiveEndpointDaSaga(c))`), como em `AmbienteDaSaga`; se nao, o harness cria o endpoint padrao `OrdemDeServicoSagaState` sem o retry. O sweeper em teste usa `IPrazoRepository` (fake em memoria em `Support/PrazoRepositoryEmMemoria` ou o proprio `PrazoRepository` sobre EF InMemory) com `FakeTimeProvider`. A consulta sincrona a Execucao pode ler o estado da saga (`OrdemDeServicoSagas`) como fallback. Metricas: `AtualizadaEm` + `CurrentState` identificam saga presa; os alertas de `EstornoPendenteManual` e de `Fault<T>` hoje saem como log de erro estruturado (procurar "EstornoPendenteManual" e "Falha técnica no comando"). A metrica de saga presa / compensacao falhou entra junto das demais metricas da saga (fase de codigo seguinte); dashboard e alerta (NRQL) ficam para a documentacao final. Ela ainda NAO existe (ADR 0008). Ainda sem `k8s/`: `Saga__*` tem padrao, mas o Deployment precisa das variaveis `RabbitMq__*` (F7).

## Observabilidade

**AKS/producao: New Relic, via OpenTelemetry (sem agente proprietario)**

- Decisao deliberada de nao instalar o agente .NET proprietario da New
  Relic: a API so tem o SDK OpenTelemetry (padrao aberto, ja usado antes só
  pra tracing) exportando via OTLP pra um **OpenTelemetry Collector**
  rodando no cluster (`OficinaMecanica.Infra/helm/otel-collector.tf`), que
  por sua vez reexporta pra New Relic. Trocar de backend de observabilidade
  no futuro so exige mudar a config do Collector, nao o codigo da API nem
  reinstalar agente nenhum.
- `Program.cs`: bloco condicional (só ativa se `Otel:Endpoint` estiver
  configurado, mesmo padrao resiliente do `Smtp:Host`) monta
  `AddOpenTelemetry()` com `.WithTracing()` (ASP.NET Core + SqlClient + `AddSource("MassTransit")`, spans de publish/consume entre filas) e
  `.WithMetrics()` (ASP.NET Core + `OpenTelemetry.Instrumentation.Runtime`,
  cobre CPU/GC/heap do processo), ambos exportando via `AddOtlpExporter`.
  Serilog ganhou um `WriteTo.OpenTelemetry(...)` adicional (pacote
  `Serilog.Sinks.OpenTelemetry`, ao lado do `WriteTo.Console()` que
  continua existindo pro `kubectl logs`) — popula `trace_id`/`span_id`
  automaticamente a partir do `Activity.Current`, sem enricher adicional
  (comportamento padrao do sink), garantindo a correlacao entre logs e
  traces de uma mesma requisicao.
- Metricas de negocio via `System.Diagnostics.Metrics`
  (`OficinaMecanica.Application.Observabilidade.MetricasNegocio` — `Meter`
  registrado no `Program.cs` via `AddMeter(MetricasNegocio.MeterName)`):
  `Counter<long>` pra OS criada, orcamento recusado e falha de e-mail
  (emitidos nos respectivos `EventHandler`s/`SmtpEmailService`), e um
  `Histogram<double>` de tempo medio de execucao por status
  (`tempo_execucao_horas`, com uma tag `status` — uma metrica so, varias
  dimensoes via NRQL `FACET`, em vez de uma metrica por status), recalculado
  periodicamente por `MetricasDeNegocioBackgroundService`
  (`IOrdemDeServicoRepository.ObterTempoMedioPorStatusAsync`).
- **Gotcha confirmado na pratica**: `Counter<T>` (os 3 acima) apareciam
  sempre zerados na New Relic (`sum()`/valor bruto = 0, mesmo com
  incrementos reais acontecendo) - causa raiz e a temporalidade
  **cumulativa** default do exporter OTLP do .NET, que a New Relic nao
  converte pra delta de forma confiavel em metricas customizadas (so nas
  nativas do agente proprietario, que nao usamos). Corrigido via env var
  `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=delta`
  (`k8s/oficinamecanica-api/deployment.yaml`) - o `Histogram<double>` acima
  nao precisa disso, funciona corretamente com o default.
- `GET /health`: health check simples (`AddHealthChecks()`/`MapHealthChecks`, sem verificacao de dependencias como banco) — so confirma que o processo esta de pe. Sem autenticacao. Os checks de prontidao tem a tag `ready`: `sqlserver` (`SqlServerHealthCheck`, `CanConnectAsync` com limite de 3 s), `rabbitmq` (`RabbitMqHealthCheck`, conexao AMQP de verdade) e `masstransit-bus`. `/health` os exclui de proposito (uma queda de dependencia nao pode reiniciar o pod pela probe de liveness) e `GET /health/ready` (tambem anonimo, corpo JSON com o status de cada check) os inclui: **503 so quando o SQL Server cai** (sem ele a API nao aceita escrita); **broker fora = `Degraded`, HTTP 200**, porque o outbox segura as mensagens e tirar o pod de rotacao por causa do RabbitMQ transformaria a queda do broker em indisponibilidade total (o check do bus do MassTransit tambem tem a falha rebaixada para `Degraded`). O alerta operacional do broker fica por metrica (F9).
- `GET /metrics`: metricas no formato Prometheus (`prometheus-net.AspNetCore`, `UseHttpMetrics()`/`MapMetrics()`) — sem nenhum Prometheus no AKS pra fazer scrape disso hoje (ver historico do repositorio), mantido so porque o docker-compose local ainda usa.
- CPU/memoria de pods/nodes no AKS: integracao de Kubernetes da New Relic (`nri-bundle`, `OficinaMecanica.Infra/helm/newrelic.tf`), independente do Collector acima.
- Alertas/dashboards: o MCP oficial da New Relic existe mas as ferramentas de criacao de dashboard/alerta sao premium-gated (nao funcionaram mesmo autenticado) - na pratica, o dashboard foi montado como JSON versionado (`E:\FIAP\Pos\dashboard.json`, raiz do workspace, importado manualmente via New Relic UI: Dashboards > Import dashboard) e o alerta foi criado direto via NerdGraph (`curl` + User API Key `NRAK-...`, mutations `alertsPolicyCreate`/`alertsNrqlConditionStaticCreate`) - nao Terraform, nao UI manual pro alerta.

**Dev local (docker-compose): stack self-hosted, sem relacao com o New Relic acima**

- Serilog com sink para Console.
- Stack completa de observabilidade no `docker-compose.yml` (Prometheus + Loki + Alloy + Grafana + Jaeger, servicos `prometheus`/`loki`/`alloy`/`grafana`/`jaeger`) — continua existindo so pra desenvolvimento local, decisao deliberada de nao remover (o New Relic acima so é usado no ambiente AKS). Grafana local ja sai com Prometheus, Loki e Jaeger provisionados como datasources automaticamente (`local/grafana-datasources.yml`) — UI web em `http://localhost:3000` (login `admin` / `GRAFANA_ADMIN_PASSWORD`)
- **Cuidado com a app "Traces Drilldown"** do Grafana (menu lateral, instalada automaticamente como plugin): so funciona com datasource Tempo, nao reconhece datasource Jaeger — pra ver traces, usar a aba **Explore** (icone de bussola) normal, selecionando o datasource Jaeger manualmente

## Docker

Servicos no docker compose (`docker compose up -d` sobe todos):
- **oficinamecanica-api**: API .NET 8, porta 5000 (mapeada para 8080 interno) — le SQL/JwksUri/Smtp/Otel/RabbitMq, todos via `.env`
- **oficinamecanica-rabbitmq**: RabbitMQ 4 (`rabbitmq:4.4.0-management`, tag pinada), AMQP na porta 5672 e UI de gerenciamento em `http://localhost:15672` (login `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS` do `.env`), volume nomeado `rabbitmq_data`, healthcheck `rabbitmq-diagnostics ping`; a `api` so sobe depois dele saudavel. **Este compose e o dono do broker compartilhado**: `OficinaMecanica.Billing` e `OficinaMecanica.Execucao` (outros projetos de Compose) o alcancam por `host.docker.internal:5672` (`extra_hosts: host-gateway`, mesmo precedente do `JwksUri`), com as mesmas credenciais
- **oficinamecanica-sqlserver**: SQL Server 2022 (edicao Developer), banco local — separado do Azure SQL Database usado em producao/AKS, que continua intacto (ver secao "Banco de Dados")
- **oficinamecanica-sonar**: SonarQube 10 Community, porta 9000
- **oficinamecanica-sonar-db**: PostgreSQL 15 (banco do SonarQube), interno
- **oficinamecanica-sonar-setup**: container de inicializacao unica — cria o projeto `oficina-mecanica` e configura a senha do admin no primeiro boot
- **oficinamecanica-trivy**: scanner de vulnerabilidades (profile `security`, nao sobe com `docker compose up -d`) — ver secao "Analise de Vulnerabilidades" no README
- **oficinamecanica-mailpit**: servidor SMTP de desenvolvimento (`axllent/mailpit`), captura os e-mails enviados pela API sem entregar de verdade — UI web na porta 8025 (ver secao "Notificacao por E-mail")
- **oficinamecanica-jaeger**: tracing distribuido de desenvolvimento (`jaegertracing/all-in-one`), recebe os spans exportados pela API via OTLP — UI web na porta 16686 (ver secao "Observabilidade")
- **oficinamecanica-prometheus**: Prometheus, coleta metricas via scrape de `api:8080/metrics` (config em `local/prometheus.yml`) — porta 9090
- **oficinamecanica-loki**: Loki (config em `local/loki-config.yaml`, mesmos parametros do `OficinaMecanica.Infra/helm/loki.yaml.tpl` usado no AKS), porta 3100
- **oficinamecanica-alloy**: Grafana Alloy, versao local do `OficinaMecanica.Infra/helm/alloy.yaml.tpl` — em vez de ler arquivo de log do node (impossivel fora do Kubernetes), le os logs de todos os containers direto da API do Docker (`discovery.docker`/`loki.source.docker`, config em `local/alloy-config.river`), via socket do Docker montado
- **oficinamecanica-grafana**: Grafana, ja com Prometheus/Loki/Jaeger provisionados como datasources automaticamente (`local/grafana-datasources.yml`, montado em `/etc/grafana/provisioning/datasources/`) — porta 3000, login `admin` / `GRAFANA_ADMIN_PASSWORD` (`.env`)

Credenciais SonarQube: `admin` / valor de `SONAR_ADMIN_PASSWORD` no `.env` (default sugerido no `.env.example`: `Admin@Sonar2024`)  
Dashboard do projeto: `http://localhost:9000/dashboard?id=oficina-mecanica`  
Relatorio de qualidade: `docs/sonarqube-report.md`

Notas importantes:
- `InvariantGlobalization` deve ser `false` no `OficinaMecanica.API.csproj` — o SqlClient precisa da cultura `en-us`. Definir como `true` quebra a conexao com o banco mesmo que `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` esteja no docker-compose (a propriedade do csproj e compilada no binario e tem precedencia)
- `SONAR_ES_BOOTSTRAP_CHECKS_DISABLE=true` e necessario para o SonarQube no Docker Desktop (Windows/macOS) onde `vm.max_map_count` nao e configuravel pelo usuario

## Infraestrutura (Terraform / Azure)

O Terraform que provisiona a infraestrutura real do projeto no Azure **nao
vive mais nesse repositorio** — migrou pra um repositorio proprio,
`OficinaMecanica.Infra` (sibling deste, `E:\FIAP\Pos\OficinaMecanica.Infra`),
pra centralizar a infra de todos os futuros servicos/APIs (ex:
`OficinaMecanica.Seguranca`) num unico lugar em vez de cada repo de codigo
carregar seu proprio `infra/`. A migracao foi so de arquivos — mesmo
backend de state remoto (`stfiap`/container `tfstate`), nenhum recurso no
Azure foi recriado (`terraform plan` vazio antes/depois da mudanca de
repositorio).

Ver `CLAUDE.md`/`README.md` do `OficinaMecanica.Infra` pra detalhes de
modulos, comandos de `terraform init/plan/apply` e notas operacionais sobre
a assinatura Azure for Students (restricao de regiao, capacidade por
SKU/regiao, etc.).

O que continua neste repositorio: os manifests do Kubernetes (`k8s/`, ver
secao abaixo) e o CI/CD da aplicacao (`.github/workflows/ci.yml`, ver secao
"CI/CD") — o deploy da *aplicacao* continua automatizado por push na
`main`; so o provisionamento de infra e que passou a ser manual num
repositorio separado (ja era manual antes tambem, so mudou de onde roda).

## Kubernetes

Pasta `k8s/` — manifests da aplicacao (nao infra de cluster, essa fica no
repositorio `OficinaMecanica.Infra` via Terraform). Aplicados automaticamente pelo job `deploy-to-aks`
do CI/CD a cada push na `main` (ver secao "CI/CD" abaixo) — `kubectl apply -f`
manual continua funcionando igual, se precisar rodar fora do pipeline.
Organizada em subpastas por assunto:

```
k8s/
  oficinamecanica-api/   # manifests da API
  mailpit/                # SMTP de desenvolvimento (captura os e-mails da API dentro do cluster)
```

`k8s/monitoring/` (ServiceMonitor + dashboard-as-code do Grafana) e
`k8s/jaeger/` (tracing all-in-one) existiram no passado e foram removidos
junto com o Prometheus/Grafana/Loki/Alloy/Jaeger do AKS, substituidos pelo
New Relic via OpenTelemetry Collector (ver secao "Observabilidade").

### `k8s/oficinamecanica-api/`

- **`deployment.yaml`**: `oficinamecanica-api`, 2 replicas (valor inicial — quem
  controla depois e o HPA), imagem `acrfiap.azurecr.io/oficinamecanica-api:latest`.
  Sem `imagePullSecrets` (kubelet do AKS ja tem `AcrPull` via Terraform). Env var
  sensivel (`ConnectionStrings__DefaultConnection`) vem de `secretKeyRef` apontando pro Secret
  `oficinamecanica-secrets` — esse Secret e sincronizado automaticamente pelo
  CSI Secrets Store driver (ver `secret-provider-class.yaml` abaixo), nao mais
  aplicado a mao. Por isso o Deployment tambem monta um volume `secrets-store`
  (nao lido diretamente pelo container - so existe pra disparar essa
  sincronizacao). Readiness/liveness probe em `GET /health`. `Otel__Endpoint`
  aponta pro OpenTelemetry Collector (`OficinaMecanica.Infra/helm/otel-collector.tf`),
  nao mais pro Jaeger (ver secao "Observabilidade").
- **`service.yaml`**: ClusterIP, porta 80 -> 8080 (so alcancavel via Ingress),
  porta nomeada `http` (necessario pro `ServiceMonitor` referenciar por nome).
- **`ingress.yaml`**: `ingressClassName: nginx`, roteia tudo pro Service. Depende
  do `ingress-nginx` instalado via `OficinaMecanica.Infra/helm/`.
- **`hpa.yaml`**: HorizontalPodAutoscaler, 2 a 5 replicas por CPU (70%) e memoria
  (80%). Depende do metrics-server (vem habilitado por padrao no AKS).
- **`secret-provider-class.yaml`**: `SecretProviderClass` que le os 3 campos
  sensiveis direto do Key Vault (`kvfiap`, via `OficinaMecanica.Infra/keyvault_secrets.tf`),
  usando a managed identity do proprio addon `key_vault_secrets_provider`
  (sem Workload Identity dedicada, escopo simples). O campo `secretObjects`
  sincroniza esses valores pro Secret nativo `oficinamecanica-secrets` — o
  Deployment continua lendo esse Secret normalmente, sem saber que a origem
  mudou. Substitui o antigo `secret.yaml`/`secret.yaml.example` aplicado a
  mao (ver secao "Banco de Dados"/"Infraestrutura" — o Key Vault ja estava
  provisionado, essa era a pendencia de conectar ele ao Deployment).

O Deployment le seus segredos do Key Vault (`OficinaMecanica.Infra/keyvault/`) via CSI Secrets
Store driver — ver `secret-provider-class.yaml` acima e
`OficinaMecanica.Infra/keyvault_secrets.tf` (ver secao "Infraestrutura (Terraform / Azure)").

### `k8s/mailpit/`

- **`deployment.yaml`**: `mailpit` (imagem `axllent/mailpit`), 1 replica —
  servidor SMTP de desenvolvimento, sem persistencia (mesmo papel que ja
  tinha no `docker-compose.yml`, so que agora tambem dentro do AKS, pra dar
  pra demonstrar o fluxo de notificacao por e-mail sem precisar de SMTP
  externo nem rodar o compose em paralelo).
- **`service.yaml`**: ClusterIP, expoe as portas 1025 (SMTP, consumida pelo
  `oficinamecanica-api` via `Smtp__Host: mailpit`) e 8025 (UI web).
- **`ingress.yaml`**: expoe so a porta 8025 (UI web) via `ingress-nginx`,
  host nip.io (`mailpit.<ip>.nip.io`) — a porta 1025 (SMTP) fica so
  acessivel de dentro do cluster, sem sentido expor SMTP num Ingress HTTP.

## CI/CD

`.github/workflows/ci.yml`, 3 jobs sequenciais (`needs:`). Gatilhos:
`push`/`pull_request` pra `main` (automatico) e `workflow_dispatch` (botao
"Run workflow" na aba Actions do GitHub, pra rodar sob demanda sem precisar
de um commit novo — util por exemplo depois de um `terraform apply` que nao
mexeu em codigo da aplicacao).

1. **`build-and-test`**: roda em todo push/PR pra `main`. Restore, build
   (Release), os 3 projetos de teste (`dotnet test`, cache de pacotes NuGet),
   resultados publicados como Job Summary via `dorny/test-reporter`
   (`.trx`) e como artifact (`test-results`, retencao de 90 dias).
2. **`build-and-push-image`**: só em push de verdade na `main` ou
   `workflow_dispatch` (nao em PR). Autentica no Azure via `azure/login@v2`
   (OIDC — ver `OficinaMecanica.Infra/github_oidc/`), `az acr login`, e publica a imagem no
   `acrfiap` com 2 tags: `${{ github.sha }}` (hash do commit) e `latest`,
   via `docker/build-push-action` (cache de camadas `type=gha`).
3. **`deploy-to-aks`**: idem (push ou `workflow_dispatch` na `main`). Autentica via
   `azure/aks-set-context@v4` (`admin: true`, usa contas locais do cluster,
   nao Azure RBAC de autorizacao dentro do Kubernetes), aplica
   `k8s/oficinamecanica-api/` e `k8s/mailpit/`, e usa
   `kubectl set image` apontando pro hash do commit (nao o `:latest` fixo
   do YAML) + `kubectl rollout status` pra confirmar.

Autenticacao via **OIDC** (`OficinaMecanica.Infra/github_oidc/`) — o GitHub emite um token de
identidade por execucao, sem nenhum secret de longa duracao guardado no
repositorio. As 3 variables do repositorio (`AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, configuradas via `gh variable
set`, nao sao secrets — sozinhas nao dao acesso a nada sem o token OIDC) vem
dos outputs do modulo (`terraform output`).

**API server do AKS sem restricao de IP**: nao criei nenhum mecanismo de
`authorized_ip_ranges` pro cluster — o runner do GitHub Actions (hospedado,
IP dinamico) nao teria como ser adicionado a uma lista fixa sem um passo
extra no workflow pra liberar/revogar IP a cada execucao (`az aks update`),
adicionando minutos de espera por deploy pra uma protecao que ja e coberta
pela autenticacao: sem uma credencial Azure AD valida com a role certa
(`Cluster Admin Role` acima), o IP sozinho nao abre o cluster pra ninguem.
Restringir por IP so faria sentido com um runner self-hosted dentro da
mesma rede (VNet) do cluster — fora de escopo aqui.

**Limitacao conhecida**: o `deploy-to-aks` falha se o cluster estiver parado
(`az aks stop`, usado pra nao gerar custo ocioso) — decisao consciente de
manter automatico mesmo assim, em vez de gatilho manual.

Analise SonarQube **nao** esta no pipeline ainda: o SonarQube atual só roda
localmente (`localhost:9000`, dentro do Docker), inalcancavel pelo runner do
GitHub Actions — precisaria migrar pra SonarCloud ou expor a instancia.
