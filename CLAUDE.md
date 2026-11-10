# OficinaMecanica - Sistema de Gestao para Oficina Mecanica

## Visao Geral

Backend .NET 8 (EF Core 9) com DDD, Clean Architecture e CQRS (MediatR) para gestao de oficina mecanica. Em evolucao para o OS Service da saga: publica mensagens de integracao pelo outbox do MassTransit/RabbitMQ (ver secao "Mensageria") e hospeda a saga orquestrada da OS (ver secao "Saga (F3b)").

O `README.md` foi reescrito para descrever o OS Service do ecossistema de microsservicos (visao geral, Saga e por que orquestrada, comunicacao, testes, observabilidade, CI/CD, execucao local, limitacoes e roteiro de publicacao); a evidencia de cobertura esta em `docs/relatorios/cobertura.md`.

## Estrutura do Projeto

```
src/
  OficinaMecanica.Domain/          # Entidades, Value Objects, Enums, Domain Events, Interfaces
  OficinaMecanica.Application/     # Commands, Queries, Handlers (MediatR), DTOs, Validators (FluentValidation), Event Handlers, Interfaces (ex: IEmailService)
  OficinaMecanica.Infrastructure/  # EF Core 9 (Azure SQL Database), Repositories, JWT Auth (so validacao via JWKS), SmtpEmailService (MailKit), Messaging (MassTransit + RabbitMQ + outbox/inbox), Saga (state machine, activities, sweeper de prazos, metricas), Execucao (cliente HTTP resiliente da consulta sincrona)
  OficinaMecanica.API/             # Controllers REST, ExceptionHandlingMiddleware, Swagger, Program.cs
tests/
  OficinaMecanica.Domain.Tests/       # Testes unitarios (entidades, VOs, regras de negocio)
  OficinaMecanica.Application.Tests/  # Testes dos handlers e validators (Moq)
  OficinaMecanica.Integration.Tests/  # Testes de integracao (WebApplicationFactory + InMemory, bus em memoria)
  OficinaMecanica.Infrastructure.Tests/ # Testes de infraestrutura: publisher/DI, health checks, a maquina de estados da saga e suas metricas, o cliente HTTP da Execucao (manipulador falso) sem Docker (harness + EF InMemory) + [Category=Infra] com Testcontainers (SQL Server e RabbitMQ reais)
  OficinaMecanica.Bdd.Tests/          # BDD (Reqnroll + xUnit, Gherkin em portugues): a API e a saga reais com Billing e Execucao programaveis, sem Docker - ver secao "F3c: BDD, consulta sincrona e metricas"
  OficinaMecanica.Contracts.Tests/    # Goldens JSON dos contratos de mensagem
  OficinaMecanica.Tests.Common/       # Builders de entidades com Bogus, compartilhados por Domain.Tests e Application.Tests
k8s/                                    # Manifests Kubernetes (Deployment, Service, Ingress, HPA, SecretProviderClass; Mailpit) - ver secao "Kubernetes"
scripts/                                # Scripts de CI/CD e qualidade (cobertura + gate, guardrail do MassTransit, OpenAPI, kubeconform) e os testes deles - ver secao "CI/CD"
.github/                                # workflows/ci.yml, rulesets/protect-main-release.json (versionado; entrada direta do `gh api`, atualizado a mao pelo usuario) e dependabot.yml
docs/                                   # Organizada por pasta (indice em docs/README.md): adr/ (ADRs), rfc/ (RFCs), c4/ (C4 niveis 1 a 3), diagramas/ (saga, clean architecture, event storming, sequencia), relatorios/ (cobertura, SonarQube, Trivy) e o contrato OpenAPI versionado na raiz (openapi.json)
local/                                  # Configs da stack de observabilidade do docker-compose (Prometheus, Loki, Alloy, Grafana) - ver secao "Docker"
e2e/                                    # Teste ponta a ponta local dos tres servicos: ambiente Docker isolado (projeto de Compose `oficina-e2e`) com OS, Billing e Execucao + SQL Server, MongoDB e RabbitMQ reais, JWKS de teste no lugar do Seguranca e gateway Fake; resultado e evidencias em e2e/evidencias; ver e2e/README.md
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

# BDD da saga (Reqnroll, sem Docker; paralelismo desligado no projeto)
dotnet test tests/OficinaMecanica.Bdd.Tests -c Release

# Tudo, inclusive Infra
dotnet test

# Ferramentas versionadas em dotnet-tools.json (dotnet-ef 9.0.22, reportgenerator 5.5.12 e swashbuckle.aspnetcore.cli 6.5.0); o dotnet-ef global do sistema e antigo demais para o EF Core 9
dotnet tool restore
dotnet ef migrations add <Nome> --project src/OficinaMecanica.Infrastructure --startup-project src/OficinaMecanica.API

# Os mesmos passos do job "Build & Test" do CI na maquina local (testes dos scripts, guardrail do MassTransit, kubeconform, OpenAPI,
# testes com cobertura e gate de 80%); precisa de Docker e leva ~9 min
bash scripts/ci-local.sh
# So testes + cobertura mesclada + gate (saida em TestResults/; HTML em TestResults/report-com-exclusoes/index.html); fases: testes-sem-infra | testes-infra | relatorio
bash scripts/cobertura.sh
# Regerar docs/openapi.json depois de mudar a API (o CI reprova se ele estiver defasado)
bash scripts/exportar-swagger.sh
# Testes dos scripts de CI (casos e mutantes); validacao dos manifests (kubeconform via Docker)
bash scripts/testar-scripts.sh
bash scripts/validar-k8s.sh
# BDD da saga (sem Docker); entra no merge de cobertura pelo scripts/cobertura.sh
dotnet test tests/OficinaMecanica.Bdd.Tests -c Release

# Teste ponta a ponta local dos tres servicos (ambiente Docker isolado, projeto de Compose "oficina-e2e": OS + Billing + Execucao com SQL Server, MongoDB
# e RabbitMQ reais, JWKS de teste no lugar do Seguranca e gateway Fake; resultado e evidencias em e2e/evidencias; ver e2e/README.md). Precisa dos
# repositorios Billing e Execucao clonados ao lado deste. Nunca derruba nada fora do projeto "oficina-e2e".
bash e2e/subir.sh                  # gera chaves e senhas de DEV, constroi as imagens, sobe e espera as tres APIs (--sem-build reaproveita as imagens)
bash e2e/executar-cenarios.sh      # cenarios com asserts + verificacoes finais (--so 01,03 escolhe cenarios; --sem-finais pula as finais)
bash e2e/verificar-contratos.sh    # confere que os goldens e os tipos de contrato sao identicos entre os tres repositorios
bash e2e/derrubar.sh               # remove so o projeto oficina-e2e (--imagens remove tambem as tres imagens)

# Executar API localmente
dotnet run --project src/OficinaMecanica.API

# Docker (sobe API, SQL Server, RabbitMQ, SonarQube e a stack de observabilidade; ver secao "Docker")
docker compose up -d

# Rebuild da imagem da API apos mudancas de codigo
docker compose build --no-cache api && docker compose up -d

# Analise SonarQube (requer SonarQube UP e token em SONAR_TOKEN)
# Token em: http://localhost:9000/account/security  |  login: admin / SONAR_ADMIN_PASSWORD (ver .env)
dotnet sonarscanner begin /k:"oficina-mecanica" /n:"OficinaMecanica" /v:"1.0.0" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="$SONAR_TOKEN" /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" /d:sonar.cs.vstest.reportsPaths="**/*.trx" /d:sonar.exclusions="stress/**,e2e/**"
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

### Decisoes registradas (`docs/adr/`, `docs/rfc/`)

Numeracao propria deste repositorio, sem lacunas: ADR 0001-0009 e RFC 0001-0005.

- **RFC 0001** Clean Architecture + DDD + CQRS via MediatR (vale dentro de cada servico). **RFC 0002** Domain Events in-process x broker externo: *parcialmente substituido* pelo
  RFC 0005 (os eventos seguem in-process via MediatR; a ausencia de broker deixou de valer entre servicos). **RFC 0003** divisao em repositorios (estendido pelo RFC 0005).
  **RFC 0004** .NET 8 / C#. **RFC 0005** decomposicao em microsservicos com Saga orquestrada.
- **ADR 0001** comunicacao sincrona via HTTP/JWKS: *parcialmente substituido* pelo RFC 0005 e pelo ADR 0006 (o JWKS segue sincrono; entre OS, Billing e Execucao ha RabbitMQ).
  **ADR 0002** migrations por step explicito no CI (aplicado no job `deploy-to-aks`, ADR 0009). **ADR 0003** saga orquestrada. **ADR 0004** MassTransit 8 fixado. **ADR 0005** contratos
  por copia + golden JSON. **ADR 0006** quebras de API e sincrono x assincrono. **ADR 0007** outbox/inbox e publicacao antes do commit. **ADR 0008** sweeper e estados da saga.
  **ADR 0009** CI/CD independente com gate de qualidade.

## Fluxo da Ordem de Servico

```
Recebida -> EmDiagnostico -> AguardandoAprovacao -> AguardandoPagamento -> EmExecucao -> Finalizada -> Entregue
                                    |                       |
                                    +-> OrcamentoRecusado   +-> Cancelada
```

`Cancelada` tambem sai de `Recebida`, `EmDiagnostico` e `AguardandoAprovacao`; de `EmExecucao` so como compensacao (execucao inviavel/estorno). Transicoes controladas pelo dominio (`OrdemDeServico.cs`, dicionario `TransicoesPermitidas`). Fluxo completo da saga: `docs/diagramas/saga-maquina-de-estados.puml`, decisoes em `docs/adr/0003|0006` e `docs/rfc/0005`.

- **Enum persistido como int, nunca renumerar**: `OrcamentoRecusado=0 ... Entregue=6`, `AguardandoPagamento=7`, `Cancelada=8`. Por isso a listagem nao ordena mais pelo numero (ver abaixo).
- **Historico de status em ordem cronologica**: `OrdemDeServico.HistoricoStatus` ordena por `DataAlteracao` e, em empate, pela posicao do status no fluxo (`OrdemDeServico.PosicaoNoFluxo`); quem le o agregado (DTO, metricas) nao depende da consulta. O banco entrega as linhas na ordem do indice da chave (`Guid` aleatorio, sem relacao com o tempo), e foi isso que fez a API devolver o historico embaralhado; `AsSplitQuery` nao interfere (a consulta do historico so ordena pela chave da OS). Nao ha coluna sequencial para desempatar (exigiria migration); o desempate pela posicao vale porque cada status entra uma unica vez e as transicoes so avancam. Os testes `Category=Infra` (`HistoricoDeStatusComSqlTests`) gravam as chaves em ordem inversa a do tempo.
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
- **Erros e abandono do cliente**: `ExceptionHandlingMiddleware` mapeia Domain/Validation 400, `ConflitoException` 409,
  KeyNotFound 404 e o resto 500 sem detalhe. Com `RequestAborted` cancelado e a resposta nao iniciada, `CancelamentoDaRequisicao`
  reconhece `OperationCanceledException` (inclusive dentro de `DbUpdateException`) e o cancelamento de comando SQL em voo, que o
  SqlClient devolve como `SqlException` com todos os erros em Number 0/Class 11/State 0 (criterio numerico, nunca o texto); o
  middleware responde 499 sem corpo e loga Information so com o template da rota (`RouteEndpoint.RoutePattern.RawText`, nunca o
  path, porque `/api/clientes/documento/{numero}` carrega CPF/CNPJ) e o trace id. Deadlock, timeout, chave duplicada e sessao
  derrubada seguem como erro. Com a resposta ja iniciada o middleware nao mexe em status nem cabecalhos: registra o erro e
  aborta a conexao (o cliente ve a falha em vez de uma resposta truncada); se foi o cliente que fechou a conexao, so loga Information.
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
- Resiliencia a falha transitoria: `AddDbContext` usa `EnableRetryOnFailure` (`DependencyInjection.cs`), porque o Azure SQL Database serverless "pausa" apos periodo ocioso (erro 40613) e a primeira conexao depois da pausa (cold start) pode falhar sem o retry. Fica ligado so fora de consumer; dentro dele a resiliencia vem do `UseMessageRetry` (ver "Mensageria")
- Testes de integracao com EF Core 9: o `AddDbContext` acumula `IDbContextOptionsConfiguration<T>`, entao o `CustomWebApplicationFactory` remove essa configuracao (alem de `DbContextOptions<T>`) antes de registrar o InMemory
- Migration aplicada automaticamente no startup (Program.cs, apenas em Development): roda contra o banco local do docker-compose, que mantem `ASPNETCORE_ENVIRONMENT=Development`. No AKS, `ASPNETCORE_ENVIRONMENT=Production` desliga esse gate (a API nunca migra sozinha em Production), e quem aplica o esquema, uma vez por deploy, e o step "Aplicar migrations" do job `deploy-to-aks` (`dotnet ef database update`, ver "CI/CD" e `docs/adr/0002-migrations-via-step-explicito-no-ci.md`)
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
- **Health check de verdade**: o check do bus do MassTransit (tag `ready`) continua saudavel com o RabbitMQ parado enquanto o servico nao tem receive endpoints (medido: 200 por 80 s). Por isso `RabbitMqHealthCheck` abre uma conexao AMQP real (timeout de 3 s, `RabbitMQ.Client` que ja vem com o `MassTransit.RabbitMQ`) com a tag `ready` e responde `Degraded` (nao `Unhealthy`) quando o broker nao responde: `/health/ready` continua 200 e so o SQL Server fora vira 503 (ver "Observabilidade"). So existe com `Mensageria__Transporte=RabbitMq`. O limite de 3 s vale tambem contra um broker que aceita a TCP e nao responde (o cliente AMQP sozinho leva ~8 s: `tentativa.WaitAsync(LimiteDaConexao, ...)`); a conexao que completar depois do limite e fechada (`FecharSeConectarDepoisAsync`) e a excecao dela e observada. Cancelamento do chamador (probe abortada) sobe como excecao, nunca vira `Degraded`.
- **Testes**: `tests/OficinaMecanica.Infrastructure.Tests`. Sem Docker (publisher + `MessageId`, registro no DI, retry do EF por escopo, validador de ambiente, despacho de domain events do `AppDbContext` sobre EF InMemory): `dotnet test --filter "Category!=Infra"`. Com Docker, `[Trait("Category","Infra")]`: Testcontainers com SQL Server (`mcr.microsoft.com/mssql/server:2022-latest` fixado por digest, `MSSQL_MEMORY_LIMIT_MB=1024`) e RabbitMQ (`rabbitmq:4.4.0-management`), containers `os-f3a-*`, um banco e um virtual host novos por teste; rodar com `dotnet test tests/OficinaMecanica.Infrastructure.Tests --filter "Category=Infra"` (cobrem: outbox atomico no caminho HTTP com rollback, publish depois do save como controle negativo, mensagem duplicada consumida uma vez, excecoes de negocio vao direto para `_error` e falha tecnica esgota o retry, ordem dos filtros, retry do EF fora de consumer com conexao derrubada, domain events pos-commit em escopo novo sem e-mail fantasma, `MessageId` do envelope, `/health/ready` com o RabbitMQ parado (200, rabbitmq `Degraded`) e com o banco offline (503) e a migration aplicada a partir da `Initial`). Os consumers desses testes (F3a) sao de teste; a saga tem os dela (secao "Saga (F3b)").
- **Fila ligada antes do trafego (resolvido na F3b)**: o exchange fanout de cada contrato so nasce no primeiro publish e, sem fila ligada, o RabbitMQ descarta a mensagem e o outbox esvazia (uma OS criada antes de a fila existir perde o `OrdemDeServicoAberta`). O endpoint `os-saga` (`Filas.Saga`) da saga e criado na subida do bus (`WaitUntilStarted`), com o binding de `OrdemDeServicoAberta`, antes da API aceitar a primeira OS; o compose prova a fila existindo ao subir. No k8s (secao "Kubernetes"): `/health` como liveness, `startupProbe` com 120 s (sem broker no boot a API cai apos ~60 s por `WaitUntilStarted`) e readiness em `/health/ready` (503 so com o SQL fora; broker fora fica `Degraded`/200). Os checks de prontidao (SQL e broker, em paralelo) levam ate 3 s e o `timeoutSeconds` padrao do Kubernetes e 1 s: a readiness precisa de `timeoutSeconds >= 5`. `periodSeconds` alto (30) so limita a carga das probes; elas nao seguram nem acordam o Azure SQL serverless, porque o outbox e o sweeper ja consultam o banco a todo momento.
- **Feito na F3b, F3c e F7**: saga, activities, endpoint `os-saga`, sweeper de prazos e migration (secao "Saga (F3b)"); BDD, consulta sincrona a Execucao e metricas da saga (secao "F3c: BDD, consulta sincrona e metricas"); as variaveis `RabbitMq__*`, `Mensageria__*`, `Saga__*` e `Execucao__BaseUrl` no `k8s/oficinamecanica-api/deployment.yaml`, o step de CI que aplica migrations, os testes Infra e o BDD no `scripts/cobertura.sh` (secoes "Kubernetes" e "CI/CD"); o RabbitMQ no AKS vem do `OficinaMecanica.Infra` (`helm/rabbitmq.tf`, `rabbitmq.messaging.svc.cluster.local`). Dashboard e alerta (NRQL) das metricas ficam para a documentacao final.

## Saga (F3b)

Orquestrador da OS em `Infrastructure/Saga`. A ESPECIFICACAO e `docs/diagramas/saga-maquina-de-estados.puml` (cada transicao existe na maquina e tem teste de comportamento; `SagaGrafoDaMaquinaTests` LE o puml e compara com o grafo da maquina dois recortes, que eventos movem cada estado e para onde cada evento leva, e o trio origem + evento -> destino, que o grafo nao expoe, fica com os testes de comportamento de cada transicao; o rotulo de cada seta e `Evento1 | Evento2 [condicao]`, com `Fault<Comando>` valendo o evento `FalhaAo<Comando>`); decisoes em `docs/adr/0003` e `docs/adr/0008`; aqui fica so o que e preciso para mexer no codigo.

- **Composicao**: o `Program.cs` chama `AddMensageriaComSaga(configuration)` = `AddServicosDaSaga` (opcoes `Saga__*` com `ValidateOnStart`, `IPrazoRepository`, sweeper, activities no escopo) + `AddMensageria` com a saga nos ganchos (`AddSagaDaOrdemDeServico` em `registrarConsumers`, `ReceiveEndpointDaSaga` em `configurarEndpoints`). Teste que precisa de mais consumers usa as tres pecas direto (ver `Support/AmbienteDaSagaInfra`). Com `Mensageria__Persistencia=Nenhuma` (so Development/Testing) o repositorio da saga e o em memoria.
- **Estado** (`OrdemDeServicoSagaState`, tabela `OrdemDeServicoSagas`, mesmo `AppDbContext`, mapa aplicado em `OnModelCreating`): `CorrelationId` = `OrdemDeServicoId`, `CurrentState`, `Numero`, `OrcamentoId?`, `PagamentoId?`, `PrazoAprovacaoId?`/`PrazoPagamentoId?` (prazo VIGENTE de cada tipo; nulo quando a etapa termina), `CancelamentoPorStaff`, `MotivoCancelamento?` (motivo do encerramento, guardado ate o passo que o aplica), `CriadaEm`, `AtualizadaEm`. Sem `SetCompletedWhenFinalized`: a linha fica nos finais.
- **Estados**: `AguardandoDiagnostico`, `EmDiagnostico`, `GerandoOrcamento`, `AguardandoAprovacao`, `AguardandoPagamento`, `CancelandoPagamento`, `EmExecucao`, `Estornando`, `EstornoPendenteManual` e finais `Concluida`, `Cancelada`, `OrcamentoRecusado`. Evento sem transicao no estado e IGNORADO (`DuringAny(Ignore)`; o teste `Maquina_DeveDeclararTodoEventoEmTodoEstado...` quebra se um evento novo ficar sem tratamento); as tres decisoes de uma pessoa (`ClienteAprovouOrcamento`, `ClienteRecusouOrcamento`, `CancelamentoSolicitado`) ignoradas onde nao tem mais efeito deixam um log `Warning` com o id da OS (`LogarDecisaoIgnoradaActivity`, declarado por estado em `DeclararDecisoesSemEfeito`); evento antes de a saga existir usa `OnMissingInstance(Fault)` + retry do endpoint; `PrazoExpirado` so age com o `PrazoId` e o `Tipo` vigentes.
- **Activities** (`Saga/Activities`, uma por passo, base `AtividadeDaSaga`): chamam UM command idempotente da Application no mesmo escopo/DbContext/transacao do consumer (`IMediator`), enviam o comando com `Send` para `queue:{Filas.X}` e agendam prazos por `IPrazoRepository` (sem EF na activity). Excecao dentro dela desfaz tudo (nada de rollback manual) e o retry reprocessa. A leitura da OS para montar mensagens e `ObterSnapshotDaOrdemQuery`.
- **Ids dos comandos**: `IdentificadorDeMensagem.Para(os, nomeDoComando, discriminador?)` (SHA-256), copiado para o `MessageId` do envelope; discriminador = janela de pagamento em `SolicitarPagamento`/`CancelarPagamento`, `PagamentoId` em `EstornarPagamento`. `OcorridoEm` = `TimeProvider`.
- **`Fault<T>`** (falha tecnica; log de erro estruturado sempre): `SolicitarDiagnostico`/`GerarOrcamento` cancelam a OS; `SolicitarPagamento` age como `PagamentoNaoSolicitado`; `IniciarExecucao` age como `ExecucaoInviavel` (estorna); `EstornarPagamento` -> `EstornoPendenteManual`; `CancelarPagamento`/`CancelarOrcamento`/`CancelarProducao` so logam: SEM reenvio automatico (loop com mensagem venenosa) e sem transicao, a saga fica onde estava e o comando falho espera na `<fila>_error` do destino ate o redrive manual (idempotente: id deterministico). O log de erro tem template estavel (`Falha técnica no comando {Comando} da ordem de serviço {OrdemDeServicoId} (saga em {Estado}), mensagem {MessageIdDoComando} ...`; alerta por texto). Detalhes e runbook de redrive no ADR 0008. A producao so e cancelada no cancelamento do staff.
- **Sweeper de prazos** (`SweeperDePrazosBackgroundService` -> `SweeperDePrazos` -> `IPrazoRepository.ProcessarVencidosAsync`): a cada `Saga__SweeperIntervaloSegundos` reivindica ate `Saga__SweeperLote` linhas vencidas de `SagaPrazos` com `UPDLOCK, READPAST, ROWLOCK, INDEX(IX_SagaPrazos_Processado_ExpiraEm)` (SQL cru, `TimeProvider` como parametro), marca `Processado` e publica `PrazoExpirado` pelo outbox na MESMA transacao; nunca escreve na linha da saga; varias replicas (HPA) sao seguras. Lote cheio repete na hora. As dicas travam o que o plano LE: sem o `INDEX(...)` uma instancia trava tudo e as outras ficam com zero. Fora de provider relacional (EF InMemory) o repositorio le sem lock.
- **Variaveis** (`appsettings.json`, secao `Saga`): `Saga__PrazoAprovacaoHoras` (72, tambem o `ValidoAte` do orcamento), `Saga__PrazoPagamentoHoras` (48), `Saga__SweeperIntervaloSegundos` (30), `Saga__SweeperLote` (50), `Saga__CompensacaoPresaMinutos` (30, limite do gauge de compensacao presa), todas > 0; `Saga__SweeperHabilitado` (true) so desliga o `BackgroundService` (testes disparam o sweeper a mao). Nao ha prazo de diagnostico nem de reparo (`TipoPrazo` so tem `Aprovacao` e `Pagamento`): uma OS parada em `AguardandoDiagnostico`, `EmDiagnostico` ou `EmExecucao` so aparece nas metricas de estado da saga e no `AtualizadaEm`; alerta por idade do estado e evolucao futura.
- **Divergencias do puml** (puml e ADR 0003 ja atualizados): `DiagnosticoConcluido` antes de `DiagnosticoIniciado` adianta o inicio; `CancelamentoSolicitado` em `CancelandoPagamento` marca o staff e desfaz a producao; transicoes de `Fault<T>`; `OnMissingInstance` e `Fault` (nao `Discard`).
- **Migration** `AdicionaSagaEPrazos` (`OrdemDeServicoSagas`, `SagaPrazos`, indice `IX_SagaPrazos_Processado_ExpiraEm`), validada em SQL Server real a partir de `AdicionaPagamentoEMensageria` (colunas, defaults, indices, `Down`).
- **Testes da saga**: sem Docker (`Category!=Infra`, em `Infrastructure.Tests/Saga`) a maquina REAL roda no `ITestHarness` com repositorio de saga em memoria, EF InMemory e handlers/commands reais (`Support/AmbienteDaSaga` + `CenarioDaSaga`; o teste faz o papel do Billing e da Execucao publicando os eventos e lendo `Harness.Sent`); o harness SUBSTITUI o bus do `AddMensageria`, por isso o endpoint `os-saga` e redeclarado no bloco do harness. Com Docker (`Category=Infra`, em `Infrastructure.Tests/Infra`: `SagaPonteAPonteTests`, `SagaConcorrenciaTests`, `SagaAtomicidadeTests`, `SweeperComSqlTests`, `MigrationTests`): SQL Server 2022 + RabbitMQ 4 (containers `os-f3b-*`), com consumers de teste (`ServicosSimulados`) nas filas reais do Billing e da Execucao; um consumer pode lancar excecao tecnica (`LancarExcecaoTecnicaEm<T>`), o que exercita o `Fault<T>` REAL (consumer lanca, `_error` do destino, `Fault<T>` na fila `os-saga`): `SolicitarPagamento` compensa (Cancelada, estoque devolvido) e `CancelarPagamento` so loga e deixa a saga em `CancelandoPagamento`, com um teste de redrive que devolve a mensagem da `_error` (`InfraFixture.RedriveDaFilaDeErroAsync`) e fecha a saga. `SagaConcorrenciaTests` captura o SQL (`CapturadorDeSql`, `DbCommandInterceptor`) e exige `UPDLOCK` na leitura da linha da saga: o bloqueio vem do padrao do MassTransit 8.5.12 (modo `Pessimistic` e SQL Server; o mesmo SQL sai sem as duas linhas explicitas de `SagaExtensions`) e o teste reprova se o modo virar `Optimistic`. Rodar: `dotnet test --filter "Category!=Infra"` (segundos) e `dotnet test tests/OficinaMecanica.Infrastructure.Tests --filter "Category=Infra"` (~5 min; precisa de Docker e ~1,5 GB livres).
- **Medicao historica desta entrega da saga (2026-11-08; registro daquela entrega, os numeros atuais estao na "Medicao integrada" da secao "CI/CD")**: testes Contracts 71, Domain 145, Application 298, Integration 108, Bdd 12, Infrastructure 392 (330 sem Docker + 62 `Category=Infra`), 1026 no total. Cobertura de linhas mesclada (`reportgenerator`, todos os projetos de teste incluindo Bdd e Infra): **bruta 96,1%** (6087/6328; Application 98,1%, Infrastructure 97,6%, Domain 91,2%, API 79,7%) e **94,8% com as exclusoes documentadas** (Migrations, `DependencyInjection*`, `*Configuration`, `Program`: 3199/3373). Mutacao rapida numa copia fora do repo: mutantes na ordem das transicoes, no `PrazoId` vigente, em `CancelamentoPorStaff`, na reserva, nas compensacoes ausentes, no `Ignore`, no `OnMissingInstance`, nos ids, nas dicas do claim (inclusive o `INDEX`), no `<=` do vencimento, no modo `Optimistic` do repositorio e na lista de estados de `DeclararDecisoesSemEfeito` (um mutante por estado), todos mortos pelos testes; no BDD, oito mutantes da maquina (estorno que nao libera o estoque, estoque liberado antes do estorno, recusa que vira `Cancelada`, `PagamentoCancelado` que nao libera, `EstornoFalhou` que cancela, finalizacao que cancela, pagamento tardio por prazo que estorna, observador de metricas desconectado) reprovam pelo menos um cenario, dois mutantes de atraso (`Thread.Sleep(300)` na reacao do Billing a `SolicitarPagamento` e a todo comando) que o BDD agora tolera (20 rodadas seguidas com coverlet, 12/12 em todas), e oito mutantes das metricas (prazo sem metrica, tag de origem trocada, duracao invertida, concluida que nao conta, compensacao que conta todo `Fault`, decisao ignorada sem estado, limite invertido e zeros do gauge) reprovam os testes de metricas. O `UPDLOCK` na leitura da linha da saga e o padrao do MassTransit (o SQL e o mesmo com e sem as duas linhas explicitas de `SagaExtensions`) e quem o garante e o teste que captura o SQL.

## F3c: BDD, consulta sincrona e metricas

### BDD (Reqnroll)

`tests/OficinaMecanica.Bdd.Tests`, Reqnroll 3.3.4 + xUnit (pacotes fixos; o Reqnroll exige `xunit` >= 2.8, por isso o projeto usa 2.9.3 e o resto da solucao segue no 2.4.2), Gherkin em portugues (`# language: pt`: Funcionalidade/Contexto/Cenario/Dado/Quando/Entao) e steps em pt-BR. Rodar: `dotnet test tests/OficinaMecanica.Bdd.Tests -c Release` (segundos, sem Docker). Entra no merge de cobertura (comando `TestResults/Bdd` em "Comandos Uteis").

- **Como sobe**: `Support/FabricaDaApi` e a API de verdade (`WebApplicationFactory<Program>`: controllers, MediatR, handlers, saga, activities, EF InMemory) com `Transporte=EmMemoria`, `Persistencia=Nenhuma`, `Saga:SweeperHabilitado=false`, retry curto, relogio `FakeTimeProvider`, e-mail capturado, chave RSA propria para o JWT e `AddMassTransitTestHarness(...)` DEPOIS do `AddMensageria` (o harness remove o bus e o repositorio de saga; por isso a saga e o endpoint `os-saga` sao redeclarados no bloco do harness: `bus.AddSagaDaOrdemDeServico(repositorioEmMemoria: true)` + `bus.UsingInMemory((c, e) => e.ReceiveEndpointDaSaga(c))`, mais os consumers do Billing e da Execucao nas filas reais `billing-comandos` e `execucao-comandos`). Um host novo (banco, relogio, harness) por cenario; `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
- **Billing e Execucao** (`Support/RoteiroDosServicos`): consumers sem regra de negocio que guardam o comando recebido e, se o cenario programou (`AoReceber<T>`), respondem com o evento que o cenario manda ou lancam falha tecnica (`FalharAoReceber<T>`, que faz o MassTransit publicar o `Fault<T>`). Sem programacao ficam em silencio e o passo do cenario publica o evento. Nao reimplementam nada dos servicos.
- **Passos**: as mensagens dos servicos simulados saem dos builders de `Tests.Common` (os de Billing aceitam `ComOcorridoEm` para o relogio falso); toda leitura de estado que depende de resposta assincrona (a OS, a saga, o estoque, o link de pagamento, o motivo, a reserva, o prazo, as mensagens recebidas e as metricas) espera com `Esperar` em vez de ler uma vez, e a suite passa com o consumer do Billing demorando 300 ms em qualquer comando. O que o atendente e o cliente fazem vai pela API real (abrir a OS com JWT, `aprovar`, `recusar`, `cancelar`); o sweeper e disparado a mao (`SweeperDePrazos` do DI) depois de `Relogio.Advance`; as verificacoes olham o estado da saga (repositorio em memoria do harness), a OS e o estoque (API e banco), as mensagens (o que cada servico recebeu e, para "nao enviou", o que a SAGA enviou, registrado no envio: olhar o consumer deixaria passar uma mensagem enviada por engano que ele ainda nao leu), o e-mail e as metricas (`MeterListener` por cenario).
- **Sem espera sem limite**: todo passo que depende de mensagem assincrona espera com teto de 10 s (`Support/Esperar`) e reprova dizendo o que esperava e o que viu por ultimo; o harness tem `TestTimeout` de 10 s.
- **Cenarios** (`Features/*.feature`): caminho feliz ate `Concluida`; orcamento recusado pelo cliente, prazo de aprovacao vencido (sweeper) e aprovacao tardia (409); pagamento nao concluido (prazo vence, `CancelarPagamento`, estoque devolvido so depois do `PagamentoCancelado`) e pagamento tardio (o Billing responde `PagamentoAprovado` ao cancelamento: segue `EmExecucao`); execucao inviavel com estorno (`EstornarPagamento`, estoque liberado so depois do `PagamentoEstornado`) e estorno que falha (`EstornoPendenteManual`, sem devolver o estoque); cancelamento do staff (antes e depois da aprovacao), estoque insuficiente na aprovacao e falha tecnica de `CancelarPagamento` (a saga fica em `CancelandoPagamento`, sem reenvio, e conta `saga_compensacao_falhou`).
- **Geracao**: o codigo dos `.feature` e gerado em `obj/` (`ReqnrollUseIntermediateOutputPathForCodeBehind`), nunca no repositorio.
- **Mutacao**: trocar a transicao de `Estornando` para nao liberar o estoque reprova o cenario de execucao inviavel; os demais mutantes estao na linha "Medicao" da secao "Saga (F3b)".

### Consulta sincrona a Execucao

`GET /api/ordens-de-servico/{id}/execucao` (JWT; 400 id vazio, 401, 404 OS inexistente). Decisoes e matriz sincrono x assincrono: ADR 0006.

- **Camadas**: porta `IExecucaoClient` e `ISagaStateReader` em `Application/Interfaces`; query + validator + handler em `Queries/ObterExecucaoDaOrdem`; DTO proprio (`ExecucaoDaOrdemDto`, `ProducaoDaOrdemDto`: modelo anticorrupcao, nenhum projeto da Execucao e referenciado); em `Infrastructure/Execucao`: `ExecucaoHttpClient` (HttpClient tipado), `OrdemDeProducaoExterna` (forma do JSON da Execucao, so os campos usados), `RepasseDeCredenciaisHandler` (copia o `Authorization` do chamador e poe `X-Correlation-Id`), `OpcoesDaExecucao` + `ValidadorDasOpcoesDaExecucao` (`ValidateOnStart`) e `ExecucaoExtensions.AddClienteDaExecucao` (chamado no `Program.cs`). `SagaStateReader` (`Infrastructure/Saga`) le `OrdemDeServicoSagas` do proprio banco.
- **Resposta**: 200 com `origem = "execucao"` (detalhe da producao) ou `origem = "saga"` (status da OS + ultimo estado conhecido da saga, `producao` nulo, `motivoDoFallback` = `ProducaoNaoEnfileirada` se a Execucao deu 404 ou `ExecucaoIndisponivel`). Nada da producao e inventado no fallback. A resposta precisa ser da OS pedida: `id` diferente (ou ausente) no JSON da Execucao e descartado como `ExecucaoIndisponivel`, com Warning so com os dois ids. 5xx, timeout, circuito aberto, falha de rede e qualquer 4xx da Execucao (inclusive 401/403) viram fallback; so o cancelamento do proprio chamador propaga.
- **Resiliencia** (`Microsoft.Extensions.Http.Resilience` 8.10.0, fixo): de fora para dentro, timeout total, retry (backoff exponencial + jitter, so GET, so 5xx/rede/timeout, nunca 4xx), circuit breaker (so falha transitoria) e timeout por tentativa. O pipeline usa o `TimeProvider` do container, inclusive na espera entre tentativas: com um `FakeTimeProvider` parado o retry nunca acorda (os testes avancam o relogio com teto de 10 s).
- **Variaveis** (`appsettings.json`, secao `Execucao`; em producao `Execucao__BaseUrl`): `BaseUrl` (`http://localhost:5002`; compose `http://host.docker.internal:5002`; AKS `http://oficinamecanica-execucao`), `TimeoutSegundos` (6), `TimeoutPorTentativaSegundos` (2), `TentativasDeRetry` (2, minimo do Polly e 1), `AtrasoDoRetryMs` (200), `CircuitoTaxaDeFalha` (0,5), `CircuitoMinimoDeChamadas` (5, minimo 2), `CircuitoJanelaSegundos` (30), `CircuitoPausaSegundos` (30).
- **Swagger**: `GenerateDocumentationFile` no projeto da API e `IncludeXmlComments` (so se o `OficinaMecanica.API.xml` existir) alimentam descricao e respostas em portugues; `CustomOperationIds` mantem `OrdensDeServico_ObterExecucao`.
- **Testes**: handler (Moq), validator, cliente (`ManipuladorHttpFalso` em `Tests.Common/Fakes`: 200, 404, 4xx sem retry, 5xx com retry e fallback, falha de rede, timeout de tentativa e total, circuito aberto e fechando depois da pausa, GET-only, repasse do token e da correlacao, token fora do log, opcoes) e a rota inteira na API (`FabricaComExecucaoSimulada`, sem Docker). Sem CPF/PII em log: so o id da OS.

### Metricas da saga

Meter `OficinaMecanica.Saga` (`Infrastructure/Saga/MetricasDaSaga.cs`). Descricao completa (instrumentos, tags, como medem e como alertar) no ADR 0008, secao "Metricas da saga". Resumo: contadores `saga_iniciada`, `saga_concluida`, `saga_cancelada{estado_origem}`, `saga_orcamento_recusado{causa}`, `saga_estorno_pendente_manual{causa}`, `saga_compensacao_falhou{comando}`, `saga_decisao_ignorada{decisao,estado}`, `saga_prazo_expirado{tipo}`; histograma `saga_duracao_estado_segundos{estado}`; gauges `saga_por_estado{estado}` e `saga_compensacao_presa{estado}` (sagas em compensacao sem atualizacao ha mais que `Saga__CompensacaoPresaMinutos`).

- **Emissao**: as transicoes saem do `ObservadorDeMetricasDaSaga` (`IEventObserver`, conectado no construtor da maquina; roda depois de todos os passos do evento, entao transicao que falha NUM PASSO e e refeita pelo retry nao conta; mas a emissao e anterior ao commit da transacao do consumer: commit que falha e retry que reprocessa contam a transicao de novo, entao a contagem pode ficar a MAIOR por causa do commit (nunca a menor por causa dele), e alerta deve ser por taxa ou limiar, nunca por contagem exata, com os gauges, que leem o banco, como fonte autoritativa; ver "Limitacoes conhecidas" no ADR 0008); `saga_compensacao_falhou` e `saga_decisao_ignorada` saem de `LogarFalhaTecnicaActivity` e `LogarDecisaoIgnoradaActivity`, junto do log. A maquina nao mudou de comportamento.
- **Gauges**: `ColetorDeMetricasDaSaga` (duas consultas agregadas ao banco do servico) e chamado pelo `MetricasDeNegocioBackgroundService` a cada 5 min (so existe com `Otel:Endpoint`); sem tarefa de fundo nova. Com varias replicas agregar os gauges com `max`.
- **Como alertar**: `saga_compensacao_presa > 0`, `saga_por_estado{estado="EstornoPendenteManual"} > 0` (o contador `saga_estorno_pendente_manual` serve so como aviso de taxa) e aumento de `saga_compensacao_falhou`. Dashboard e alerta (NRQL) ficam para a documentacao final; ate la valem tambem os logs de erro estruturados ("EstornoPendenteManual" e "Falha técnica no comando").
- **Testes**: `MetricasDaSagaTests` e `ColetorDeMetricasDaSagaTests` (maquina real no harness + `MeterListener`; o Meter e estatico, entao rodam na colecao `ColecaoDeMetricas` com `DisableParallelization`), cenarios do BDD, `Infra/ColetorDeMetricasEConsultaDaSagaComSqlTests` (as consultas contra SQL Server de verdade) e `Infra/SagaMetricasNoCommitComSqlTests` (documenta a limitacao conhecida: commit que falha e retry contam a transicao duas vezes). A colecao `Infra` tambem roda sem paralelismo com as demais, pelo mesmo motivo do Meter estatico.

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
- Metricas da saga: Meter proprio (`OficinaMecanica.Saga`, `MetricasDaSaga`), registrado no mesmo bloco por `AddMeter(MetricasDaSaga.MeterName)`; contadores, histograma de duracao por estado e gauges de sagas por estado e de compensacao presa, no mesmo ciclo de coleta do servico acima (secao "Metricas da saga" em "F3c: BDD, consulta sincrona e metricas"). Valem as mesmas regras de temporalidade delta para os contadores.
- **Gotcha confirmado na pratica**: `Counter<T>` (os 3 acima) apareciam
  sempre zerados na New Relic (`sum()`/valor bruto = 0, mesmo com
  incrementos reais acontecendo) - causa raiz e a temporalidade
  **cumulativa** default do exporter OTLP do .NET, que a New Relic nao
  converte pra delta de forma confiavel em metricas customizadas (so nas
  nativas do agente proprietario, que nao usamos). Corrigido via env var
  `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=delta`
  (`k8s/oficinamecanica-api/deployment.yaml`) - o `Histogram<double>` acima
  nao precisa disso, funciona corretamente com o default.
- `GET /health`: health check simples (`AddHealthChecks()`/`MapHealthChecks`, sem verificacao de dependencias como banco) — so confirma que o processo esta de pe. Sem autenticacao. Os checks de prontidao tem a tag `ready`: `sqlserver` (`SqlServerHealthCheck`, `CanConnectAsync` com limite de 3 s), `rabbitmq` (`RabbitMqHealthCheck`, conexao AMQP de verdade) e `masstransit-bus`. `/health` os exclui de proposito (uma queda de dependencia nao pode reiniciar o pod pela probe de liveness) e `GET /health/ready` (tambem anonimo, corpo JSON com o status de cada check) os inclui: **503 so quando o SQL Server cai** (sem ele a API nao aceita escrita); **broker fora = `Degraded`, HTTP 200**, porque o outbox segura as mensagens e tirar o pod de rotacao por causa do RabbitMQ transformaria a queda do broker em indisponibilidade total (o check do bus do MassTransit tambem tem a falha rebaixada para `Degraded`). Os checks `sqlserver` e `rabbitmq` rodam dentro de `HealthCheckComCache` (`Infrastructure/Health`, `AddCheckComCache<T>`): resultado em cache de 5 s (`Health__ProntidaoCacheSegundos`, 0 desliga; so os testes de infraestrutura usam) porque o endpoint e anonimo e a APIM repassa qualquer path, entao sem cache cada requisicao abriria uma conexao SQL e uma AMQP; requisicoes simultaneas esperam uma unica avaliacao e excecao/cancelamento nunca e guardado. Cada check desiste em 3 s e o cancelamento do chamador sobe (nao vira `Degraded`/`Unhealthy`). O corpo (`ProntidaoResponseWriter`) traz so status e descricao de cada check, nunca a excecao. O alerta operacional do broker fica por metrica (F9).
- `GET /metrics`: metricas no formato Prometheus (`prometheus-net.AspNetCore`, `UseHttpMetrics()`/`MapMetrics()`) — sem nenhum Prometheus no AKS pra fazer scrape disso hoje (ver historico do repositorio), mantido so porque o docker-compose local ainda usa. `UseHttpMetrics()` fica por fora do `ExceptionHandlingMiddleware` (`MetricasDeRequisicaoTests`), entao os contadores trazem o codigo real (400/404/409/499/500) e nao 200.
- CPU/memoria de pods/nodes no AKS: integracao de Kubernetes da New Relic (`nri-bundle`, `OficinaMecanica.Infra/helm/newrelic.tf`), independente do Collector acima.
- Alertas/dashboards: o MCP oficial da New Relic existe mas as ferramentas de criacao de dashboard/alerta sao premium-gated (nao funcionaram mesmo autenticado) - na pratica, o dashboard foi montado como JSON versionado (`E:\FIAP\Pos\dashboard.json`, raiz do workspace, importado manualmente via New Relic UI: Dashboards > Import dashboard) e o alerta foi criado direto via NerdGraph (`curl` + User API Key `NRAK-...`, mutations `alertsPolicyCreate`/`alertsNrqlConditionStaticCreate`) - nao Terraform, nao UI manual pro alerta.

**Dev local (docker-compose): stack self-hosted, sem relacao com o New Relic acima**

- Serilog com sink para Console.
- Stack completa de observabilidade no `docker-compose.yml` (Prometheus + Loki + Alloy + Grafana + Jaeger, servicos `prometheus`/`loki`/`alloy`/`grafana`/`jaeger`) — continua existindo so pra desenvolvimento local, decisao deliberada de nao remover (o New Relic acima so é usado no ambiente AKS). Grafana local ja sai com Prometheus, Loki e Jaeger provisionados como datasources automaticamente (`local/grafana-datasources.yml`) — UI web em `http://localhost:3000` (login `admin` / `GRAFANA_ADMIN_PASSWORD`)
- **Cuidado com a app "Traces Drilldown"** do Grafana (menu lateral, instalada automaticamente como plugin): so funciona com datasource Tempo, nao reconhece datasource Jaeger — pra ver traces, usar a aba **Explore** (icone de bussola) normal, selecionando o datasource Jaeger manualmente

## Docker

Servicos no docker compose (`docker compose up -d` sobe todos):
- **oficinamecanica-api**: API .NET 8, porta 5000 (mapeada para 8080 interno) — le SQL/JwksUri/Smtp/Otel/RabbitMq/Execucao, todos via `.env` (a Execucao vem de `Execucao__BaseUrl`, `http://host.docker.internal:5002` no compose)
- **oficinamecanica-rabbitmq**: RabbitMQ 4 (`rabbitmq:4.4.0-management`, tag pinada), AMQP na porta 5672 e UI de gerenciamento em `http://localhost:15672` (login `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS` do `.env`), volume nomeado `rabbitmq_data`, healthcheck `rabbitmq-diagnostics ping`; a `api` so sobe depois dele saudavel. **Este compose e o dono do broker compartilhado**: `OficinaMecanica.Billing` e `OficinaMecanica.Execucao` (outros projetos de Compose) o alcancam por `host.docker.internal:5672` (`extra_hosts: host-gateway`, mesmo precedente do `JwksUri`), com as mesmas credenciais
- **oficinamecanica-sqlserver**: SQL Server 2022 (edicao Developer), banco local — separado do Azure SQL Database usado em producao/AKS, que continua intacto (ver secao "Banco de Dados")
- **oficinamecanica-sonar**: SonarQube 10 Community, porta 9000
- **oficinamecanica-sonar-db**: PostgreSQL 15 (banco do SonarQube), interno
- **oficinamecanica-sonar-setup**: container de inicializacao unica — cria o projeto `oficina-mecanica` e configura a senha do admin no primeiro boot
- **oficinamecanica-trivy**: scanner de vulnerabilidades (profile `security`, nao sobe com `docker compose up -d`) — ver secao "Analise de Vulnerabilidades" no README
- **oficinamecanica-mailpit**: servidor SMTP de desenvolvimento (`axllent/mailpit`), captura os e-mails enviados pela API sem entregar de verdade — UI web na porta 8025 (ver secao "Notificacao por E-mail")
- **oficinamecanica-jaeger**: tracing distribuido de desenvolvimento (`jaegertracing/all-in-one`), recebe os spans exportados pela API via OTLP — UI web na porta 16686 (ver secao "Observabilidade")
- **oficinamecanica-prometheus**: Prometheus, coleta metricas via scrape de `api:8080/metrics` (config em `local/prometheus.yml`) — porta 9090
- **oficinamecanica-loki**: Loki (config em `local/loki-config.yaml`; os parametros vieram do Loki que o `OficinaMecanica.Infra` tinha no AKS antes de a stack self-hosted ser removida em favor do New Relic), porta 3100
- **oficinamecanica-alloy**: Grafana Alloy, versao local do Alloy que o `OficinaMecanica.Infra` tinha no AKS (removido do Infra junto com o resto da stack self-hosted, em favor do New Relic) — em vez de ler arquivo de log do node (impossivel fora do Kubernetes), le os logs de todos os containers direto da API do Docker (`discovery.docker`/`loki.source.docker`, config em `local/alloy-config.river`), via socket do Docker montado
- **oficinamecanica-grafana**: Grafana, ja com Prometheus/Loki/Jaeger provisionados como datasources automaticamente (`local/grafana-datasources.yml`, montado em `/etc/grafana/provisioning/datasources/`) — porta 3000, login `admin` / `GRAFANA_ADMIN_PASSWORD` (`.env`)

Credenciais SonarQube: `admin` / valor de `SONAR_ADMIN_PASSWORD` no `.env` (default sugerido no `.env.example`: `Admin@Sonar2024`)  
Dashboard do projeto: `http://localhost:9000/dashboard?id=oficina-mecanica`  
Relatorio de qualidade: `docs/relatorios/sonarqube-report.md`

Notas importantes:
- `InvariantGlobalization` deve ser `false` no `OficinaMecanica.API.csproj` — o SqlClient precisa da cultura `en-us`. Definir como `true` quebra a conexao com o banco mesmo que `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` esteja no docker-compose (a propriedade do csproj e compilada no binario e tem precedencia)
- `.dockerignore` deixa fora do contexto da imagem `tests/`, `docs/`, `k8s/`, `scripts/`, `.github/`, `artifacts/`, `.config/`, `.env*` (menos `.env.example`) e os `*.md`; o Dockerfile so copia `nuget.config`, a `.sln`, os `.csproj` e `src/` (nao usa o `dotnet-tools.json`)
- A porta 8080 vem do `ASPNETCORE_HTTP_PORTS=8080` da imagem base `aspnet:8.0`: o Dockerfile nao define `ASPNETCORE_URLS` (o `URLS` vence o `HTTP_PORTS` e gera o aviso `Overriding HTTP_PORTS` a cada boot), como no Billing e na Execucao. Rodei a imagem com o mesmo `securityContext` do manifest (UID 1654, root somente leitura com `/tmp` gravavel, `drop ALL`): sobe, `/health` e `/health/ready` respondem 200 na 8080 e o aviso some.
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
`main` ou na `release`; so o provisionamento de infra e que passou a ser manual num
repositorio separado (ja era manual antes tambem, so mudou de onde roda).

## Kubernetes

Pasta `k8s/` - manifests da aplicacao (a infra do cluster fica no repositorio `OficinaMecanica.Infra`, via Terraform). O job `deploy-to-aks`
aplica as duas subpastas a cada push em `main` ou `release` (ver "CI/CD"). `kubectl apply` direto serve para o `mailpit/` e para o SecretProviderClass, o Service, o Ingress e o HPA: o
`deployment.yaml` da API tem o placeholder `${IMAGE}` e precisa passar antes por `envsubst '${IMAGE}'` (sem isso o pod falha com
nome de imagem invalido, de proposito). `bash scripts/validar-k8s.sh` valida as duas pastas com o kubeconform (`-strict`, Docker).
Subpastas:

```
k8s/
  oficinamecanica-api/   # Deployment, Service, Ingress, HPA e SecretProviderClass do OS Service
  mailpit/               # SMTP de demonstracao (captura os e-mails da API dentro do cluster)
```

`k8s/monitoring/` (ServiceMonitor + dashboard-as-code do Grafana) e `k8s/jaeger/` (tracing all-in-one) existiram no passado e foram
removidos junto com o Prometheus/Grafana/Loki/Alloy/Jaeger do AKS, substituidos pelo New Relic via OpenTelemetry Collector (ver
"Observabilidade").

### `k8s/oficinamecanica-api/`

- **`deployment.yaml`**: `oficinamecanica-api` no namespace `default`, imagem `${IMAGE}` (o CI renderiza para
  `acrfiap.azurecr.io/oficinamecanica-api:<sha>`; nao existe mais `:latest` no manifest), sem `replicas` (a contagem e do HPA).
  `strategy` `RollingUpdate` com `maxSurge: 0` / `maxUnavailable: 1`: o no unico (D4as_v4) nao tem CPU de requests para um pod extra
  (ele ficaria `Pending` e o `rollout status` do CI nunca terminaria); com 1 replica ha uma janela sem pod a cada deploy.
  Endurecido: pod `runAsNonRoot` com UID/GID 1654 (usuario `app` da imagem aspnet:8.0; o Dockerfile faz `USER $APP_UID`),
  `seccompProfile: RuntimeDefault`, `automountServiceAccountToken: false`; container com `allowPrivilegeEscalation: false`,
  `readOnlyRootFilesystem: true` (`emptyDir` de 64Mi em `/tmp`) e `capabilities.drop: ["ALL"]`. Validado com a imagem real (root somente
  leitura, usuario 1654, sem capabilities) contra SQL Server e RabbitMQ reais: sobe em ~9 s, `/health/ready` 200 saudavel; broker parado
  -> 200 `Degraded` em ~3 s; banco offline -> 503; `/health` sempre 200. `terminationGracePeriodSeconds: 40` (o host espera ate 30 s).
- **Variaveis**: `ASPNETCORE_ENVIRONMENT=Production` (o validador da partida recusa transporte em memoria e ausencia de outbox fora de
  Development/Testing); `Mensageria__Transporte=RabbitMq` e `Mensageria__Persistencia=EntityFramework` explicitos; `RabbitMq__Host=rabbitmq.messaging.svc.cluster.local`,
  `RabbitMq__Port`, `RabbitMq__VirtualHost`; `Execucao__BaseUrl=http://oficinamecanica-execucao.default.svc.cluster.local:80` (consulta sincrona a
  Execucao, HTTP interno do cluster, sem passar pelo ingress); `Saga__PrazoAprovacaoHoras/PrazoPagamentoHoras/SweeperIntervaloSegundos/SweeperLote/SweeperHabilitado`
  explicitos (iguais ao appsettings, para uma mudanca aparecer no diff); JWT (emissor/audiencia/JWKS da APIM), `Smtp__*` (Service `mailpit`), `Otel__Endpoint` e
  `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE=delta` (ver "Observabilidade"), `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`. **Segredos so por
  `secretKeyRef`** no Secret `oficinamecanica-secrets` (`sql-connection-string`, `rabbitmq-username`, `rabbitmq-password`), sincronizado do Key Vault pelo CSI
  Secrets Store; nunca valor literal.
- **Probes**: `startupProbe` e `livenessProbe` em `/health` (liveness puro: 200 sem tocar em banco nem broker), `readinessProbe` em `/health/ready`.
  `startupProbe` com 24 x 5 s = 120 s (o MassTransit segura a partida ate o bus conectar, ate 60 s). Readiness e liveness com `periodSeconds: 30`,
  `timeoutSeconds: 5` (os checks levam ate 3 s; o padrao do Kubernetes e 1 s) e `failureThreshold: 3`. O cache de 5 s de `/health/ready` (menor que o periodo)
  limita o custo de um endpoint anonimo.
- **Recursos**: requests 100m / 256Mi / 256Mi de ephemeral-storage, limits 500m / 512Mi / 512Mi, os mesmos que o monolito ja tinha (nao reduzidos). Medido
  no container (512 MiB, 0,5 CPU, Production): ~95 MiB em repouso e ~125 MiB depois de 400 requisicoes; a CPU sob mensagens reais da saga nao foi medida, entao
  nao reduzi ao tamanho do Billing/Execucao (50m / 192Mi).
- **`service.yaml`**: ClusterIP, porta 80 -> 8080, porta nomeada `http` (as probes a referenciam).
- **`ingress.yaml`**: `ingressClassName: nginx`, catch-all `/` (`Prefix`), sem `host`; o Billing (`/billing`) e a Execucao (`/execucao`) entram no mesmo
  ingress-nginx por prefixo mais longo. Depende do `ingress-nginx` do `OficinaMecanica.Infra/helm/`.
- **`hpa.yaml`**: 1 a 2 replicas (era 2 a 5) por CPU (70%) e memoria (80%) sobre os requests; o no unico nao comporta mais. Mais de uma replica e segura (inbox
  deduplica; o sweeper reivindica com `UPDLOCK, READPAST`). Depende do metrics-server (padrao no AKS).
- **`secret-provider-class.yaml`**: le os 3 segredos do Key Vault (`kvfiap`: `sql-connection-string`, `rabbitmq-username`, `rabbitmq-password`) com a managed
  identity do addon `key_vault_secrets_provider` e os sincroniza (`secretObjects`) para o Secret `oficinamecanica-secrets`. O Deployment monta o volume
  `secrets-store` so para disparar essa sincronizacao; os valores chegam por `secretKeyRef`. O CI aplica este arquivo antes do resto (sem ele o pod ficaria em
  `ContainerCreating` na primeira implantacao).

### `k8s/mailpit/`

- **`deployment.yaml`**: `mailpit` (imagem `axllent/mailpit:v1.31.0`, versao fixada, a mesma do `docker-compose.yml`), 1 replica - SMTP de demonstracao, sem persistencia (mesmo papel do
  `docker-compose.yml`, agora tambem no AKS, para demonstrar o fluxo de e-mail sem SMTP externo). Endurecido como a API: `runAsNonRoot` (UID 65532, a imagem e
  `scratch`), `readOnlyRootFilesystem` com `emptyDir` em `/tmp` (o Mailpit guarda um SQLite ali), `drop: ["ALL"]`, `seccompProfile: RuntimeDefault`
  (validado em Docker: sobe e recebe e-mail por SMTP).
- **`service.yaml`**: ClusterIP, portas 1025 (SMTP, consumida pela API via `Smtp__Host: mailpit`) e 8025 (UI web).
- **`ingress.yaml`**: expoe so a 8025 (UI) via `ingress-nginx`, host nip.io (`mailpit.<ip>.nip.io`); o SMTP fica so dentro do cluster. Nao e canal de e-mail de producao.

## CI/CD

Detalhes e justificativas em `docs/adr/0009-ci-cd-independente-com-gate-de-qualidade.md`. `.github/workflows/ci.yml`, 5 jobs encadeados. Gatilhos: `push` e `pull_request`
para `main` e `release`, e `workflow_dispatch`. Deploy so em push/dispatch em `main` e `release`; **os dois fazem deploy no MESMO Deployment e banco** (o no unico
do AKS nao tem CPU para uma segunda instancia; a separacao e so de processo: branch protegida + PR obrigatorio). Os passos de build/teste vivem em `scripts/` e sao os
mesmos que `scripts/ci-local.sh` roda na maquina local.

1. **`build-and-test`** (check obrigatorio `Build & Test`): restore, build Release, `testar-scripts.sh`, guardrail do MassTransit, kubeconform, OpenAPI em dia
   (`exportar-swagger.sh --verificar`), testes `Category!=Infra`, testes `Category=Infra` (Testcontainers; o runner ubuntu tem Docker), cobertura mesclada e gate
   (`cobertura.sh relatorio`), resultados publicados (`dorny/test-reporter`, Job Summary, artifacts: .trx, HTML da cobertura, OpenCover, `openapi.json`).
2. **`quality`** (check obrigatorio `Quality`): SonarCloud (`sonar.qualitygate.wait=true`) com o OpenCover do job anterior; excecoes de cobertura iguais as do gate;
   `Migrations` e `stress/` fora da analise. Precisa do secret `SONAR_TOKEN` e das variaveis `SONAR_PROJECT_KEY`/`SONAR_ORGANIZATION`; sem eles o job falha com mensagem
   clara (em PR de fork ele e pulado).
3. **`scan-image`**: constroi a imagem sem push, escaneia com o Trivy (HIGH/CRITICAL com correcao, `ignore-unfixed`) e salva a imagem aprovada como artifact. So
   `contents: read`: sem `id-token` nem segredos, porque executa actions de terceiros.
4. **`build-and-push-image`**: `id-token: write` (OIDC); baixa a imagem escaneada e empurra a MESMA no `acrfiap` (`acrfiap.azurecr.io/oficinamecanica-api`), tag = sha do
   commit; `:latest` so na `main`. So executa `actions/*` e `azure/*` (guarda em `scripts/verificar-workflow.sh`).
5. **`deploy-to-aks`** (`concurrency: deploy-aks-oficinamecanica-api`, sem cancelar): login OIDC; migrations (`dotnet ef database update`, ferramenta de `dotnet-tools.json`,
   connection string do Key Vault `kvfiap`, segredo `sql-connection-string`, ate 5 tentativas por causa do auto-pause do Azure SQL; antes era `dotnet-ef 8.*`, incompativel
   com o EF Core 9); `azure/aks-set-context` (`admin: true`); renderiza `k8s/oficinamecanica-api/` com `envsubst '${IMAGE}'`; `kubectl apply` (SecretProviderClass primeiro, depois a
   pasta renderizada e `k8s/mailpit/`); `kubectl set image` + `rollout status` (300 s); diagnostico em caso de falha.

Autenticacao no Azure so por **OIDC** (`OficinaMecanica.Infra/github_oidc/`), sem secret de longa duracao; as 3 variables do repositorio (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID`) vem dos outputs do modulo. O unico secret e o `SONAR_TOKEN` (o SonarCloud nao tem OIDC).

**Scripts** (`scripts/`; fim de linha LF, `.gitattributes`):

| Script | O que faz |
|---|---|
| `cobertura.sh [fase]` | roda os 5 projetos de teste (coverlet/OpenCover + .trx, `--blame-hang-timeout 5m`), une com o ReportGenerator, escreve `report-bruto/` e `report-com-exclusoes/` e aplica o gate. Fases: `tudo`, `testes-sem-infra`, `testes-infra`, `relatorio`. As duas fases de teste percorrem os MESMOS 5 projetos (uma lista so, `PROJETOS_DE_TESTE`) com `Category!=Infra` e `Category=Infra` (resultados em `TestResults/<nome>` e `TestResults/infra-<nome>`); nos projetos sem teste de Infra o filtro acha 0 testes e o `dotnet test` sai com 0, assim um `Category=Infra` criado em qualquer projeto de teste tem fase |
| `verificar-cobertura.sh` | gate: >= `COBERTURA_MINIMA` (80) sobre linhas cobertas/cobertaveis (nao sobre o numero arredondado) |
| `verificar-masstransit.sh` | guardrail: qualquer `MassTransit*` direto ou transitivo fora do major 8 reprova (o 9.x e comercial) |
| `verificar-workflow.sh` | guarda: job com `id-token` so executa `actions/*` e `azure/*` |
| `exportar-swagger.sh [--verificar]` | gera `docs/openapi.json` (`swagger tofile`) ou confere que ele esta em dia |
| `validar-k8s.sh` | renderiza `${IMAGE}` e valida `k8s/oficinamecanica-api` e `k8s/mailpit` com o kubeconform (Docker) |
| `testar-scripts.sh` | casos + mutantes dos scripts acima, do `ci.yml`, do ruleset e do `ci-local.sh`; 76 casos e 38 mutantes detectados |
| `ci-local.sh [--sem-infra]` | os mesmos passos do job `Build & Test`, na ordem do workflow |

**Cobertura** (gate de 80% de linhas sobre o numero "com exclusoes"; o bruto e publicado ao lado). Exclusoes documentadas, as mesmas no `cobertura.sh`, no Sonar e aqui:
`Migrations`, `Program.cs`, `DependencyInjection*` e as EF Configurations (`Data/Configurations`). `[ExcludeFromCodeCoverage]` e proibido sem ADR (o coverlet o honra e o codigo some das duas
medicoes; o `cobertura.sh` lista as ocorrencias). `CompilerGeneratedAttribute` nao entra em `coverage.runsettings` de proposito (inflaria o numero).

**Medicao integrada (F3b + F3c + F7, a integracao do `origin/release` e a ordem cronologica do historico de status, 2026-11-08)**: `bash scripts/ci-local.sh` completo verde com o BDD nas fases de teste (rodado antes da correcao do historico; depois dela rodei o build, os testes de todos os projetos, o `cobertura.sh` completo e o `exportar-swagger.sh --verificar`, todos verdes); testes Contracts 71, Domain 153, Application 306, Integration 176, Infrastructure 429 (354 sem Docker + 75 `Category=Infra`), Bdd 12, 1147 no total (5 vieram do `release`: `MetricasNegocioEventHandlersTests`, `MetricasDeNegocioBackgroundServiceTests` e `JsonWebKeySetRetrieverTests`; 23 sao da ordem do historico de status; 65 sao do tratamento do abandono do cliente no middleware de excecoes e das metricas HTTP por codigo de resposta). Cobertura de linhas mesclada: **bruta 96,71%** (6284/6498) e **95,85% com as exclusoes documentadas** (3395/3542); `testar-scripts.sh`: 76 casos e 38 mutantes (remedido em 2026-11-09, depois de remover o `aplicar-ruleset.sh`, que tinha 2 casos); `docs/openapi.json` regenerado com a rota `GET /api/ordens-de-servico/{id}/execucao`. As medicoes por fase abaixo ficam como registro de cada uma.

**Medicao da F7 isolada (2026-11-08)**: testes Contracts 71, Domain 145, Application 287, Integration 97, Infrastructure 326 (267 sem Docker + 59 `Category=Infra`), 926 no total. Cobertura de linhas mesclada (todos os projetos de teste, inclusive Infra): **bruta 95,7%** (5840/6098; Application 96,4%, Infrastructure 97,7%, Domain 90,6%, API 74,4%, Contracts 100%) e **93,9% com as exclusoes documentadas** (2957/3149). `bash scripts/ci-local.sh` completo verde em 9 min 13 s; `testar-scripts.sh`: 78 casos e 38 mutantes detectados; `actionlint` e `shellcheck` limpos. Mutacoes provadas numa copia fora do repo: apagar a chamada do gate em `cobertura.sh` reprova o `testar-scripts`; uma action de terceiros no job com `id-token` reprova o `verificar-workflow.sh` e o `testar-scripts`; remover `Trava.WaitAsync` do `HealthCheckComCache`, `tentativa.WaitAsync` do `RabbitMqHealthCheck` ou o `when (!cancellationToken.IsCancellationRequested)` do `SqlServerHealthCheck` faz os testes FALHAREM (em 13 s, 53 s e 14 s no total; nenhum trava).

**Ruleset e dependabot**: `.github/rulesets/protect-main-release.json` (main e release: sem delete nem force-push, PR obrigatorio, checks `Build & Test` e `Quality`) e a entrada direta
da GitHub CLI: nao ha script (o `aplicar-ruleset.sh` deixou de existir) e quem executa e o usuario, a mao. O OS **ja tem** um ruleset
`protect-main-release` ATIVO no GitHub (id 21858959, conferido so em leitura) exigindo SO o `Build & Test`, entao o comando e de
ATUALIZACAO (`PUT`), nao de criacao: `gh api --method PUT repos/willianrmattos/OficinaMecanica/rulesets/$(gh api repos/willianrmattos/OficinaMecanica/rulesets --jq '.[] | select(.name == "protect-main-release") | .id') --input .github/rulesets/protect-main-release.json`. **So aplicar depois** de o SonarCloud estar
configurado (`SONAR_TOKEN` + variaveis `SONAR_*`) e de o job `Quality` ter rodado ao menos uma vez: o JSON passa a exigir o `Quality`, e sem isso todo PR
fica bloqueado esperando um check que nao existe ou falha. `.github/dependabot.yml` ignora o major de `MassTransit*` e de `Microsoft.EntityFrameworkCore*` (9 -> 10 so com a migracao para net10.0) e o major
das imagens `dotnet/aspnet` e `dotnet/sdk`.

**Actions fixadas por SHA (2026-11-09)**: todas as actions do `ci.yml` (de terceiros e do GitHub) usam `uses: <action>@<sha completo> # <versao>`; o SHA veio da tag que o workflow usava, resolvida no dia, e o conteudo do commit nao foi auditado (o pin impede mover a tag depois, nao prova que o commit e confiavel); o Dependabot (`github-actions`) propoe as atualizacoes no mesmo formato. O Trivy estava como `0.28.0`, tag que nao existe no repositorio da action; o pin aponta para a `v0.28.0`.
**Pendencias para publicar**:
(1) criar `SONAR_TOKEN` e as variaveis `SONAR_*` no repositorio; (2) atualizar o ruleset no GitHub (`PUT`, comando acima), so depois de (1) e de o `Quality` ter rodado ao menos uma vez; (3) o workflow so foi validado localmente (`actionlint`, `shellcheck`, `kubeconform`, `docker build`, scripts e
testes), a ordem dos jobs e as condicoes `if` so se provam na primeira execucao; (4) APIM: a policy que devolve 404 para `/health/ready` e `/metrics` na rota publica existe no OficinaMecanica.Infra (ADR 0007) e fica ativa quando o Terraform do Infra for aplicado (ainda nao foi); conferir logo apos o apply.

**API server do AKS sem restricao de IP**: nao criei nenhum mecanismo de `authorized_ip_ranges` pro cluster - o runner do GitHub Actions (hospedado, IP dinamico) nao teria como ser adicionado a
uma lista fixa sem um passo extra no workflow pra liberar/revogar IP a cada execucao (`az aks update`), adicionando minutos de espera por deploy pra uma protecao que ja e coberta pela autenticacao: sem
uma credencial Azure AD valida com a role certa (`Cluster Admin Role`), o IP sozinho nao abre o cluster pra ninguem. Restringir por IP so faria sentido com um runner self-hosted dentro da mesma rede
(VNet) do cluster - fora de escopo aqui.

**Limitacao conhecida**: o `deploy-to-aks` falha se o cluster estiver parado (`az aks stop`, usado pra nao gerar custo ocioso) - decisao consciente de manter automatico mesmo assim, em vez de gatilho manual.
