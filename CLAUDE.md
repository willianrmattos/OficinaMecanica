# OficinaMecanica - Sistema de Gestao para Oficina Mecanica

## Visao Geral

Backend .NET 8 com DDD, Clean Architecture e CQRS (MediatR) para gestao de oficina mecanica.

## Estrutura do Projeto

```
src/
  OficinaMecanica.Domain/          # Entidades, Value Objects, Enums, Domain Events, Interfaces
  OficinaMecanica.Application/     # Commands, Queries, Handlers (MediatR), DTOs, Validators (FluentValidation), Event Handlers, Interfaces (ex: IEmailService)
  OficinaMecanica.Infrastructure/  # EF Core (Azure SQL Database), Repositories, JWT Auth (so validacao via JWKS), SmtpEmailService (MailKit)
  OficinaMecanica.API/             # Controllers REST, ExceptionHandlingMiddleware, Swagger, Program.cs
tests/
  OficinaMecanica.Domain.Tests/       # Testes unitarios (entidades, VOs, regras de negocio)
  OficinaMecanica.Application.Tests/  # Testes dos handlers e validators (Moq)
  OficinaMecanica.Integration.Tests/  # Testes de integracao (WebApplicationFactory + InMemory)
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

# Testes
dotnet test

# Testes com cobertura (OpenCover)
dotnet test tests/OficinaMecanica.Domain.Tests --collect:"XPlat Code Coverage" --results-directory TestResults/Domain -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet test tests/OficinaMecanica.Application.Tests --collect:"XPlat Code Coverage" --results-directory TestResults/Application -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet test tests/OficinaMecanica.Integration.Tests --collect:"XPlat Code Coverage" --results-directory TestResults/Integration -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Executar API localmente
dotnet run --project src/OficinaMecanica.API

# Docker (sobe API + SonarQube; o banco e Azure SQL externo, ver secao "Banco de Dados")
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
- **ValidationBehavior**: Pipeline MediatR que roda FluentValidation antes dos handlers
- **Repository Pattern**: Interfaces no Domain, implementacoes na Infrastructure
- **Unit of Work**: AppDbContext implementa IUnitOfWork, despacha Domain Events apos SaveChanges
- **Concorrencia Otimista**: Propriedade `Version` como ConcurrencyToken
- **Testes**: Moq para mocks (Application.Tests), Bogus para dados fake, builders fluentes em `OficinaMecanica.Tests.Common/Builders/` (ClienteBuilder, VeiculoBuilder, PecaBuilder, ServicoBuilder, OrdemDeServicoBuilder)

## Fluxo da Ordem de Servico

```
Recebida -> EmDiagnostico -> AguardandoAprovacao -> EmExecucao -> Finalizada -> Entregue
                                    |
                                    +-> OrcamentoRecusado
```

Transicoes controladas pelo dominio (OrdemDeServico.cs). Ao aprovar orcamento, da baixa automatica no estoque das pecas. Ao recusar (`RecusarOrcamento(motivo)`, sem baixa de estoque), a OS vai para `OrcamentoRecusado` — valor `0` no enum `StatusOrdemDeServico` (nao o proximo numero livre) de proposito, pra ordenar abaixo de `Recebida` na listagem padrao sem precisar de mapeamento de prioridade customizado (ver secao "Listagem de Ordens de Servico").

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
- Endpoints publicos: consulta OS por numero (`/api/ordens-de-servico/numero/{numero}`), aprovacao (`/api/ordens-de-servico/{id}/aprovar`) e recusa de orcamento (`/api/ordens-de-servico/{id}/recusar`)
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
- Resiliente de proposito: qualquer falha (SMTP fora do ar, e-mail mal formado, timeout de 5s) e so logada como Warning, nunca lanca excecao — o handler roda de forma sincrona dentro do `SaveChangesAsync` (`AppDbContext.cs`), uma falha aqui nao pode derrubar a resposta HTTP do endpoint que mudou o status
- Sem `Smtp:Host` configurado (default em `appsettings.json`), o envio e so ignorado com um Warning — a API funciona normalmente sem SMTP configurado
- Dev local: servico `mailpit` no `docker-compose.yml` (UI web em `http://localhost:8025` pra ver os e-mails capturados, nao entrega nada de verdade pra fora)
- AKS: mesmo Mailpit rodando dentro do cluster (`k8s/mailpit/`, imagem `axllent/mailpit`), pra poder demonstrar o fluxo de notificacao por e-mail sem depender de SMTP externo — `k8s/oficinamecanica-api/deployment.yaml` aponta `Smtp__Host`/`Smtp__Port` pro Service `mailpit` (mesmo namespace), UI web exposta via `k8s/mailpit/ingress.yaml` (host nip.io, mesmo padrao do Grafana/Prometheus)

## Busca de Cliente por Documento

- `GET /api/clientes/documento/{numero}` — busca por CPF (11 digitos) ou CNPJ (14 digitos), sem formatacao
- Handler: `ObterClientePorDocumentoQueryHandler` (namespace `OficinaMecanica.Application.Queries.ObterCliente`)
- Retorna 404 quando o documento e invalido ou nao encontrado (nao lanca excecao para documentos invalidos)

## Banco de Dados

- SQL Server via EF Core — o banco em si difere entre ambientes, sem nenhuma mudanca no codigo da aplicacao (so a connection string muda):
  - **AKS/producao**: **Azure SQL Database** real (`svsfiap.database.windows.net`, tier serverless sempre-gratis, provisionado via `OficinaMecanica.Infra/sqldb/`), connection string sincronizada do Key Vault (ver secao "Kubernetes")
  - **Docker Compose/desenvolvimento local**: container `sqlserver` (`mcr.microsoft.com/mssql/server:2022-latest`, edicao Developer) no proprio `docker-compose.yml`, com volume nomeado (`sqlserver_data`) pra persistir entre `docker compose down`/`up` — optei por rodar local em vez de sempre depender do Azure SQL estar acessivel/acordado (tier serverless com auto-pause), so pra desenvolver
- Configurations em `Infrastructure/Data/Configurations/`
- Migration aplicada automaticamente no startup (Program.cs, apenas em Development) — roda contra qualquer um dos dois bancos acima, ja que o docker-compose mantem `ASPNETCORE_ENVIRONMENT=Development`
- No Docker Compose, a connection string do `sqlserver` local e montada direto no `docker-compose.yml` (usuario `sa`, senha vinda de `MSSQL_SA_PASSWORD` no `.env`) — nao ha mais dependencia do Azure SQL pra desenvolvimento local
- `appsettings.json`/`appsettings.Development.json` tem a senha vazia (`Password=;`) na connection string — nunca commitar a senha real ali, o `.env`/`docker-compose.yml` sempre tem precedencia quando rodando via `docker compose`
- O `.env` tambem carrega as demais credenciais usadas pelo `docker-compose.yml` (nenhuma fica hardcoded no arquivo versionado): `MSSQL_SA_PASSWORD` (SQL Server local), `GRAFANA_ADMIN_PASSWORD` (Grafana local), `SONAR_DB_USER`/`SONAR_DB_PASSWORD` (Postgres do SonarQube) e `SONAR_ADMIN_PASSWORD` (senha definida pro admin do SonarQube no primeiro boot, via `sonar-setup`)

## Observabilidade

- Serilog com sink para Console
- `GET /health`: health check simples (`AddHealthChecks()`/`MapHealthChecks`, sem verificacao de dependencias como banco) — so confirma que o processo esta de pe. Sem autenticacao.
- `GET /metrics`: metricas no formato Prometheus (`prometheus-net.AspNetCore`, `UseHttpMetrics()`/`MapMetrics()`) — contagem/duracao de requests HTTP por padrao. Sem autenticacao.
- Scrape configurado via `ServiceMonitor` em `k8s/monitoring/servicemonitor.yaml`, apontando pro Prometheus instalado em `OficinaMecanica.Infra/helm/monitoring.tf` (kube-prometheus-stack)
- Logs agregados via Loki + Grafana Alloy (`OficinaMecanica.Infra/helm/loki.tf`) — ver secao "Infraestrutura (Terraform / Azure)" pra detalhes
- Tracing distribuido via Jaeger (`k8s/jaeger/`, modo all-in-one) — API instrumentada com OpenTelemetry .NET (ASP.NET Core + SqlClient), exportando via OTLP. So ativa se `Otel:Endpoint` estiver configurado (mesmo padrao resiliente do `Smtp:Host`); Grafana ja sai com o Jaeger como fonte de dados adicional. Dev local: servico `jaeger` no `docker-compose.yml` (UI web em `http://localhost:16686`)
- Dev local: stack completa de observabilidade tambem no `docker-compose.yml` (Prometheus + Loki + Alloy + Grafana, servicos `prometheus`/`loki`/`alloy`/`grafana`), equivalente ao que roda no AKS via `OficinaMecanica.Infra/helm/`. Grafana local ja sai com Prometheus, Loki e Jaeger provisionados como datasources automaticamente (`local/grafana-datasources.yml`) — UI web em `http://localhost:3000` (login `admin` / `GRAFANA_ADMIN_PASSWORD`)
- **Cuidado com a app "Traces Drilldown"** do Grafana (menu lateral, instalada automaticamente como plugin): so funciona com datasource Tempo, nao reconhece datasource Jaeger — pra ver traces, usar a aba **Explore** (icone de bussola) normal, selecionando o datasource Jaeger manualmente

## Docker

Servicos no docker compose (`docker compose up -d` sobe todos):
- **oficinamecanica-api**: API .NET 8, porta 5000 (mapeada para 8080 interno) — le SQL/JwksUri/Smtp/Otel, todos via `.env`
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
  monitoring/             # "uso" do Prometheus/Grafana (o que monitorar)
  mailpit/                # SMTP de desenvolvimento (captura os e-mails da API dentro do cluster)
  jaeger/                 # tracing distribuido, modo all-in-one
```

### `k8s/oficinamecanica-api/`

- **`deployment.yaml`**: `oficinamecanica-api`, 2 replicas (valor inicial — quem
  controla depois e o HPA), imagem `acrfiap.azurecr.io/oficinamecanica-api:latest`.
  Sem `imagePullSecrets` (kubelet do AKS ja tem `AcrPull` via Terraform). Env var
  sensivel (`ConnectionStrings__DefaultConnection`) vem de `secretKeyRef` apontando pro Secret
  `oficinamecanica-secrets` — esse Secret e sincronizado automaticamente pelo
  CSI Secrets Store driver (ver `secret-provider-class.yaml` abaixo), nao mais
  aplicado a mao. Por isso o Deployment tambem monta um volume `secrets-store`
  (nao lido diretamente pelo container - so existe pra disparar essa
  sincronizacao). Readiness/liveness probe em `GET /health`.
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

### `k8s/monitoring/`

- **`servicemonitor.yaml`**: diz pro Prometheus (instalado via
  `OficinaMecanica.Infra/helm/monitoring.tf`) pra fazer scrape do `GET /metrics` da API a
  cada 30s. Tem o label `release: monitoring` (obrigatorio — e o nome do
  helm release do Prometheus, sem isso o `ServiceMonitor` e ignorado) e
  `namespaceSelector` apontando pro namespace `default` (onde o Service da
  API roda).
- **`ingress.yaml`**: expoe o Grafana e o Prometheus via `ingress-nginx`
  (duas regras no mesmo Ingress, ja que os dois tem o mesmo "dono" —
  observabilidade — diferente do Ingress da API, que fica separado em
  `k8s/oficinamecanica-api/`), usando host baseado em **nip.io**
  (`grafana.<ip>.nip.io`/`prometheus.<ip>.nip.io` — resolve sozinho pro IP
  embutido no nome, sem precisar de dominio real). O IP fica hardcoded nos
  hosts do arquivo (nao e um placeholder) — precisa ser atualizado pro IP
  atual do `ingress-nginx-controller` (`kubectl get svc -n ingress-nginx
  ingress-nginx-controller`) sempre que o Service for recriado. Alternativa
  mais simples pra teste rapido: `kubectl port-forward`.
- **`dashboard-oficinamecanica-api.yaml`**: `ConfigMap` com o label
  `grafana_dashboard: "1"` e o JSON do dashboard embutido em `data` — o
  sidecar do Grafana (`OficinaMecanica.Infra/helm/monitoring.yaml.tpl`) detecta sozinho e
  importa, sem precisar clicar em nada na UI. Paineis usam as metricas reais
  do `prometheus-net` (`http_requests_received_total`,
  `http_request_duration_seconds`, `http_requests_in_progress`) mais
  CPU/memoria/replicas via `kube-state-metrics`/cAdvisor.

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
  host nip.io (`mailpit.<ip>.nip.io`), mesmo padrao de
  `k8s/monitoring/ingress.yaml` — a porta 1025 (SMTP) fica so acessivel de
  dentro do cluster, sem sentido expor SMTP num Ingress HTTP.

### `k8s/jaeger/`

- **`deployment.yaml`**: `jaeger` (imagem `jaegertracing/all-in-one`), 1
  replica, sem persistencia (traces em memoria, aceitavel pro volume baixo
  de um projeto de estudo) — namespace `monitoring` (agrupado com o resto
  da observabilidade, diferente do Mailpit, que fica junto da API). Optei
  por manifest puro (nao um `helm_release` em `OficinaMecanica.Infra/helm/`) porque o modo
  all-in-one e um unico container sem configuracao complexa — o chart
  oficial do Jaeger e pensado pra instalacoes maiores (Cassandra/
  Elasticsearch, operator), desproporcional pro que precisamos aqui.
- **`service.yaml`**: ClusterIP, expoe as portas 4317/4318 (OTLP gRPC/HTTP,
  usadas pela API pra enviar spans) e 16686 (Jaeger Query UI).
- **`ingress.yaml`**: expoe so a porta 16686 (UI) via `ingress-nginx`, host
  nip.io (`jaeger.<ip>.nip.io`) — as portas OTLP ficam so acessiveis de
  dentro do cluster.

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
   `k8s/oficinamecanica-api/`, `k8s/monitoring/`, `k8s/mailpit/` e `k8s/jaeger/`, e usa
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
