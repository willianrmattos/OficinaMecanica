# OficinaMecanica - Sistema de Gestao para Oficina Mecanica

## Visao Geral

Backend .NET 8 com DDD, Clean Architecture e CQRS (MediatR) para gestao de oficina mecanica.

## Estrutura do Projeto

```
src/
  OficinaMecanica.Domain/          # Entidades, Value Objects, Enums, Domain Events, Interfaces
  OficinaMecanica.Application/     # Commands, Queries, Handlers (MediatR), DTOs, Validators (FluentValidation), Event Handlers, Interfaces (ex: IEmailService)
  OficinaMecanica.Infrastructure/  # EF Core (Azure SQL Database), Repositories, JWT Auth, Token Service, SmtpEmailService (MailKit)
  OficinaMecanica.API/             # Controllers REST, ExceptionHandlingMiddleware, Swagger, Program.cs
tests/
  OficinaMecanica.Domain.Tests/       # Testes unitarios (entidades, VOs, regras de negocio)
  OficinaMecanica.Application.Tests/  # Testes dos handlers e validators (Moq)
  OficinaMecanica.Integration.Tests/  # Testes de integracao (WebApplicationFactory + InMemory)
  OficinaMecanica.Tests.Common/       # Builders de entidades com Bogus, compartilhados por Domain.Tests e Application.Tests
infra/                                  # Infraestrutura como codigo (Terraform / Azure) - ver secao "Infraestrutura (Terraform / Azure)"
k8s/                                    # Manifests Kubernetes (Deployment, Service, Ingress, HPA) - ver secao "Kubernetes"
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

- JWT Bearer com credenciais configuradas em `appsettings.json` (AdminCredentials)
- Endpoints publicos: consulta OS por numero (`/api/ordens-de-servico/numero/{numero}`), aprovacao (`/api/ordens-de-servico/{id}/aprovar`) e recusa de orcamento (`/api/ordens-de-servico/{id}/recusar`)
- `/health` e `/metrics` tambem sao anonimos (nao passam por `[Authorize]`, mapeados via `MapHealthChecks`/`MapMetrics` fora do `MapControllers()`) — ver secao "Observabilidade"
- Demais endpoints exigem token JWT

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

- SQL Server via EF Core — hoje aponta pra um **Azure SQL Database** real (`svsfiap.database.windows.net`, tier serverless sempre-gratis, provisionado via `infra/sqldb/`), nao mais um container local
- Configurations em `Infrastructure/Data/Configurations/`
- Migration aplicada automaticamente no startup (Program.cs, apenas em Development) — roda contra o Azure SQL tambem, ja que o docker-compose mantem `ASPNETCORE_ENVIRONMENT=Development`
- Connection string vem da variavel `SQL_CONNECTION_STRING` no `.env` (fora do Git — copiar de `.env.example` e preencher com o `sql_server_fqdn` do `terraform output` e a senha definida em `infra/terraform.tfvars`), que o `docker-compose.yml` repassa ao container como `ConnectionStrings__DefaultConnection`
- `appsettings.json`/`appsettings.Development.json` tem a senha vazia (`Password=;`) na connection string — nunca commitar a senha real ali, o `.env` sempre tem precedencia quando rodando via `docker compose`
- O `.env` tambem carrega as demais credenciais usadas pelo `docker-compose.yml` (nenhuma fica hardcoded no arquivo versionado): `JWT_SECRET_KEY`, `ADMIN_USUARIO`/`ADMIN_SENHA` (login da API), `SONAR_DB_USER`/`SONAR_DB_PASSWORD` (Postgres do SonarQube) e `SONAR_ADMIN_PASSWORD` (senha definida pro admin do SonarQube no primeiro boot, via `sonar-setup`)

## Observabilidade

- Serilog com sink para Console
- `GET /health`: health check simples (`AddHealthChecks()`/`MapHealthChecks`, sem verificacao de dependencias como banco) — so confirma que o processo esta de pe. Sem autenticacao.
- `GET /metrics`: metricas no formato Prometheus (`prometheus-net.AspNetCore`, `UseHttpMetrics()`/`MapMetrics()`) — contagem/duracao de requests HTTP por padrao. Sem autenticacao.
- Scrape configurado via `ServiceMonitor` em `k8s/monitoring/servicemonitor.yaml`, apontando pro Prometheus instalado em `infra/helm/monitoring.tf` (kube-prometheus-stack)
- Logs agregados via Loki + Grafana Alloy (`infra/helm/loki.tf`) — ver secao "Infraestrutura (Terraform / Azure)" pra detalhes
- Tracing distribuido via Jaeger (`k8s/jaeger/`, modo all-in-one) — API instrumentada com OpenTelemetry .NET (ASP.NET Core + SqlClient), exportando via OTLP. So ativa se `Otel:Endpoint` estiver configurado (mesmo padrao resiliente do `Smtp:Host`); Grafana ja sai com o Jaeger como fonte de dados adicional. Dev local: servico `jaeger` no `docker-compose.yml` (UI web em `http://localhost:16686`)

## Docker

Servicos no docker compose (`docker compose up -d` sobe todos):
- **oficinamecanica-api**: API .NET 8, porta 5000 (mapeada para 8080 interno) — conecta no Azure SQL Database, e le JWT/AdminCredentials/Smtp, todos via `.env` (nao tem mais banco local no compose)
- **oficinamecanica-sonar**: SonarQube 10 Community, porta 9000
- **oficinamecanica-sonar-db**: PostgreSQL 15 (banco do SonarQube), interno
- **oficinamecanica-sonar-setup**: container de inicializacao unica — cria o projeto `oficina-mecanica` e configura a senha do admin no primeiro boot
- **oficinamecanica-trivy**: scanner de vulnerabilidades (profile `security`, nao sobe com `docker compose up -d`) — ver secao "Analise de Vulnerabilidades" no README
- **oficinamecanica-mailpit**: servidor SMTP de desenvolvimento (`axllent/mailpit`), captura os e-mails enviados pela API sem entregar de verdade — UI web na porta 8025 (ver secao "Notificacao por E-mail")
- **oficinamecanica-jaeger**: tracing distribuido de desenvolvimento (`jaegertracing/all-in-one`), recebe os spans exportados pela API via OTLP — UI web na porta 16686 (ver secao "Observabilidade")

Credenciais SonarQube: `admin` / valor de `SONAR_ADMIN_PASSWORD` no `.env` (default sugerido no `.env.example`: `Admin@Sonar2024`)  
Dashboard do projeto: `http://localhost:9000/dashboard?id=oficina-mecanica`  
Relatorio de qualidade: `docs/sonarqube-report.md`

Notas importantes:
- `InvariantGlobalization` deve ser `false` no `OficinaMecanica.API.csproj` — o SqlClient precisa da cultura `en-us`. Definir como `true` quebra a conexao com o banco mesmo que `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` esteja no docker-compose (a propriedade do csproj e compilada no binario e tem precedencia)
- `SONAR_ES_BOOTSTRAP_CHECKS_DISABLE=true` e necessario para o SonarQube no Docker Desktop (Windows/macOS) onde `vm.max_map_count` nao e configuravel pelo usuario

## Infraestrutura (Terraform / Azure)

Pasta `infra/` — provisiona a infraestrutura real do projeto no Azure via
Terraform (`azurerm` ~> 4.0, `helm` ~> 2.0 e `azuread` ~> 3.0). Raiz
(`infra/*.tf`) + 8 modulos:

- **`infra/rg/`**: resource group (`rgfiap`, `northcentralus`)
- **`infra/storage/`**: storage account (`stfiap`) que guarda o tfstate remoto (backend `azurerm`, ver `infra/backend.tf`)
- **`infra/acr/`**: Container Registry (`acrfiap.azurecr.io`), SKU Standard (free tier)
- **`infra/aks/`**: cluster AKS (`aksfiap`), 1 node `Standard_D2s_v3` (nao-gratis, usar `az aks stop`/`start` pra nao gerar custo ocioso — mas isso NAO para o IP publico do Load Balancer nem o disco do node, que continuam cobrando mesmo com o cluster parado), Azure CNI Overlay, integrado ao ACR via role assignment `AcrPull`. Tambem tem `oidc_issuer_enabled`, `workload_identity_enabled` e o addon `key_vault_secrets_provider` (CSI Secrets Store driver) habilitados
- **`infra/keyvault/`**: Key Vault (`kvfiap`), RBAC-based (`rbac_authorization_enabled`), rede restrita por IP (`network_acls`, `default_action = Deny`) + bypass pra servicos Azure confiaveis — libera tanto o IP do cliente quanto o IP de saida do cluster AKS (esse ultimo descoberto automaticamente, ver `infra/aks_keyvault_access.tf` abaixo). Os 3 segredos da aplicacao (`infra/keyvault_secrets.tf`, na raiz) sao sincronizados pro Secret nativo do Kubernetes via CSI Secrets Store driver — ver `k8s/oficinamecanica-api/secret-provider-class.yaml`
- **`infra/helm/`**: quatro `helm_release` — `ingress-nginx` (chart oficial, namespace proprio, Service `LoadBalancer`, com a annotation `azure-load-balancer-health-probe-request-path: /healthz` — sem ela, o health probe do proprio Load Balancer do Azure bate em `GET /`, cai na regra catch-all da API e recebe `301` da Swagger UI em vez de `200`, fazendo o Azure bloquear **todo** trafego externo por considerar o `ingress-nginx` inteiro unhealthy), `monitoring` (`kube-prometheus-stack`: Prometheus + Grafana + kube-state-metrics + node-exporter, sem Alertmanager, PVC de 8Gi/4Gi na StorageClass `managed-csi-premium` que o proprio AKS ja cria, dashboards do Grafana como codigo via sidecar), `loki` (agregacao de logs, modo `Monolithic`, PVC de 10Gi na mesma StorageClass, retencao de 72h, chart do repositorio `grafana-community` — ver `infra/helm/loki.tf`) e `alloy` (coleta os logs de cada pod do node e envia pro Loki). Usa o provider `helm` configurado em `infra/providers.tf` apontando pro `infra/aks` via kube_config
- **`infra/sqldb/`**: Azure SQL Database (`svsfiap.database.windows.net` / `OficinaMecanicaDb`), serverless, tier sempre-gratis. O campo que ativa esse tier (`use_free_limit`) nao existe no provider `azurerm` e nao pode ser setado depois via `az sql db update` (so na criacao) — por isso o banco foi criado via `az sql db create --use-free-limit true --free-limit-exhaustion-behavior AutoPause ...` e depois trazido para o state do Terraform com `terraform import`
- **`infra/github_oidc/`**: App Registration + Service Principal + Federated Identity Credential (OIDC, restrita a `repo:<owner>/<repo>:ref:refs/heads/main`) usados pelo GitHub Actions pra autenticar no Azure sem nenhum secret de longa duracao. Role assignments `AcrPush` (no `acrfiap`) e `Azure Kubernetes Service Cluster Admin Role` (no `aksfiap`) — ver secao "CI/CD" abaixo

Dois arquivos na raiz (nao dentro de nenhum modulo) conectam modulos entre si
sem criar dependencia circular — cada um precisa ver outputs de dois modulos
ao mesmo tempo, o que so e possivel na raiz (modulos nunca "olham de volta"
pra quem os chama):
- **`infra/keyvault_secrets.tf`**: os 3 `azurerm_key_vault_secret` da aplicacao (`jwt-secret-key`, `admin-senha`, `sql-connection-string`, essa ultima montada a partir dos outputs do `infra/sqldb`)
- **`infra/aks_keyvault_access.tf`**: a role assignment `Key Vault Secrets User` pra identidade do addon CSI do `infra/aks` no `infra/keyvault`, mais um `data "azurerm_public_ip"` que descobre automaticamente o IP de saida do cluster (usado no `network_acls` do Key Vault) — nenhum dos dois modulos referencia o outro diretamente

A assinatura usada (Azure for Students) tem restricao de regiao
(`sys.regionrestriction`: so libera `chilecentral`, `canadacentral`,
`northcentralus`, `eastus`, `mexicocentral`). Alem dessa politica geral,
alguns servicos tem uma segunda trava de **capacidade propria por regiao**
(passar na politica de regiao nao garante que aquele servico especifico vai
deixar criar ali): o tamanho de VM do node pool do AKS (serie B bloqueada
pelo proprio AKS, `Standard_F2s_v2` apareceu como "Size not available" mesmo
com cota livre — fechado em `Standard_D2s_v3`) e o Azure SQL Database
(`centralus`/`eastus` bloqueados por `ProvisioningDisabled` apesar de
permitidos pela politica de regiao — fechado em `canadacentral`). Se o
provider Terraform nao expuser um campo que so existe via API/CLI (como o
`use_free_limit` do SQL Database), o caminho e criar o recurso via `az cli`
e trazer pro Terraform com `terraform import`.

Comandos do Terraform ficam por conta de quem estiver rodando (nao ha
automacao de CI/CD pra provisionar infraestrutura ainda) — sempre a partir
de `infra/` como working directory. O deploy da *aplicacao* (nao da infra)
ja e automatizado, ver secao "CI/CD" abaixo.

## Kubernetes

Pasta `k8s/` — manifests da aplicacao (nao infra de cluster, essa fica em
`infra/` via Terraform). Aplicados automaticamente pelo job `deploy-to-aks`
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
  Sem `imagePullSecrets` (kubelet do AKS ja tem `AcrPull` via Terraform). Env vars
  sensiveis (`ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`,
  `AdminCredentials__Senha`) vem de `secretKeyRef` apontando pro Secret
  `oficinamecanica-secrets` — esse Secret e sincronizado automaticamente pelo
  CSI Secrets Store driver (ver `secret-provider-class.yaml` abaixo), nao mais
  aplicado a mao. Por isso o Deployment tambem monta um volume `secrets-store`
  (nao lido diretamente pelo container - so existe pra disparar essa
  sincronizacao). Readiness/liveness probe em `GET /health`.
- **`service.yaml`**: ClusterIP, porta 80 -> 8080 (so alcancavel via Ingress),
  porta nomeada `http` (necessario pro `ServiceMonitor` referenciar por nome).
- **`ingress.yaml`**: `ingressClassName: nginx`, roteia tudo pro Service. Depende
  do `ingress-nginx` instalado via `infra/helm/`.
- **`hpa.yaml`**: HorizontalPodAutoscaler, 2 a 5 replicas por CPU (70%) e memoria
  (80%). Depende do metrics-server (vem habilitado por padrao no AKS).
- **`secret-provider-class.yaml`**: `SecretProviderClass` que le os 3 campos
  sensiveis direto do Key Vault (`kvfiap`, via `infra/keyvault_secrets.tf`),
  usando a managed identity do proprio addon `key_vault_secrets_provider`
  (sem Workload Identity dedicada, escopo simples). O campo `secretObjects`
  sincroniza esses valores pro Secret nativo `oficinamecanica-secrets` — o
  Deployment continua lendo esse Secret normalmente, sem saber que a origem
  mudou. Substitui o antigo `secret.yaml`/`secret.yaml.example` aplicado a
  mao (ver secao "Banco de Dados"/"Infraestrutura" — o Key Vault ja estava
  provisionado, essa era a pendencia de conectar ele ao Deployment).

### `k8s/monitoring/`

- **`servicemonitor.yaml`**: diz pro Prometheus (instalado via
  `infra/helm/monitoring.tf`) pra fazer scrape do `GET /metrics` da API a
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
  sidecar do Grafana (`infra/helm/monitoring.yaml.tpl`) detecta sozinho e
  importa, sem precisar clicar em nada na UI. Paineis usam as metricas reais
  do `prometheus-net` (`http_requests_received_total`,
  `http_request_duration_seconds`, `http_requests_in_progress`) mais
  CPU/memoria/replicas via `kube-state-metrics`/cAdvisor.

O Deployment le seus segredos do Key Vault (`infra/keyvault/`) via CSI Secrets
Store driver — ver `secret-provider-class.yaml` acima e `infra/keyvault_secrets.tf`
na raiz (ver `## Infraestrutura`).

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
  por manifest puro (nao um `helm_release` em `infra/helm/`) porque o modo
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
   (OIDC — ver `infra/github_oidc/`), `az acr login`, e publica a imagem no
   `acrfiap` com 2 tags: `${{ github.sha }}` (hash do commit) e `latest`,
   via `docker/build-push-action` (cache de camadas `type=gha`).
3. **`deploy-to-aks`**: idem (push ou `workflow_dispatch` na `main`). Autentica via
   `azure/aks-set-context@v4` (`admin: true`, usa contas locais do cluster,
   nao Azure RBAC de autorizacao dentro do Kubernetes), aplica
   `k8s/oficinamecanica-api/`, `k8s/monitoring/`, `k8s/mailpit/` e `k8s/jaeger/`, e usa
   `kubectl set image` apontando pro hash do commit (nao o `:latest` fixo
   do YAML) + `kubectl rollout status` pra confirmar.

Autenticacao via **OIDC** (`infra/github_oidc/`) — o GitHub emite um token de
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
