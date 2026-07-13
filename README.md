# OficinaMecanica - Sistema de Gestao para Oficina Mecanica

Sistema backend para gestao de oficina mecanica, desenvolvido em .NET 8 com DDD, Clean Architecture e CQRS. Cobre o ciclo completo de uma ordem de servico — cadastro de clientes/veiculos, orcamento (servicos + pecas com baixa de estoque), aprovacao/recusa pelo cliente e acompanhamento por notificacao de e-mail — com autenticacao JWT para as rotas administrativas e um conjunto de rotas publicas pensadas para o proprio cliente final.

## Objetivo desta Fase

Esta fase evolui a aplicacao de um container isolado (`docker compose`) para uma implantacao cloud-native completa no Azure, com tres frentes:

1. **Infraestrutura como codigo** (Terraform): todo recurso Azure — cluster Kubernetes, banco de dados, registro de imagens, cofre de segredos, autenticacao do pipeline — e provisionado e versionado como codigo, sem cliques manuais no portal (ver [Infraestrutura](#infraestrutura)).
2. **Orquestracao em Kubernetes** (AKS): a API roda em pods gerenciados, com autoscaling por CPU/memoria (HPA), segredos sincronizados do Key Vault (sem arquivo de credenciais versionado) e observabilidade via Prometheus/Grafana (ver [Kubernetes](#kubernetes)).
3. **Entrega continua** (GitHub Actions): todo push na `main` roda build + testes automaticamente e, se tudo passar, publica a imagem no registry e atualiza o Deployment no cluster sem intervencao manual (ver [CI/CD](#cicd)).

## Diagramas e Relatorios

| Documento | Arquivo |
|-----------|---------|
| Clean Architecture (camadas e dependencias) | [docs/clean-architecture.puml](docs/clean-architecture.puml) |
| C4 Nivel 1 - Contexto do sistema | [docs/c4-nivel1-contexto.puml](docs/c4-nivel1-contexto.puml) |
| C4 Nivel 2 - Containers | [docs/c4-nivel2-container.puml](docs/c4-nivel2-container.puml) |
| C4 Nivel 3 - Componentes | [docs/c4-nivel3-componente.puml](docs/c4-nivel3-componente.puml) |
| Event Storming | [docs/event-storming.pdf](docs/event-storming.pdf) |
| Relatorio SonarQube (qualidade) | [docs/sonarqube-report.md](docs/sonarqube-report.md) |
| Relatorio Trivy (vulnerabilidades) | [docs/trivy-report.md](docs/trivy-report.md) |

## Arquitetura

```
src/
  OficinaMecanica.Domain/          # Entidades, Value Objects, Enums, Events, Interfaces
  OficinaMecanica.Application/     # Commands, Queries, Handlers, DTOs, Validators
  OficinaMecanica.Infrastructure/  # EF Core, Repositories, JWT Auth
  OficinaMecanica.API/             # Controllers REST, Middleware, Swagger
tests/
  OficinaMecanica.Domain.Tests/       # Testes unitarios do dominio
  OficinaMecanica.Application.Tests/  # Testes dos handlers e validators (Moq)
  OficinaMecanica.Integration.Tests/  # Testes de integracao da API
  OficinaMecanica.Tests.Common/       # Builders de entidades (Bogus) compartilhados
```

### Decisoes Arquiteturais

- **Monolito Modular** com Clean Architecture (camadas bem definidas)
- **DDD**: Aggregates (Cliente, OrdemDeServico, Servico, Peca), Value Objects (Documento, Placa), Domain Events
- **CQRS com MediatR**: separacao de Commands e Queries com pipeline de validacao
- **Repository Pattern**: abstracoes no Domain, implementacoes na Infrastructure
- **FluentValidation**: validacao de dados na camada Application
- **EF Core + Azure SQL Database**: persistencia relacional com consistencia transacional (ver justificativa abaixo)
- **Domain Events**: publicados apos SaveChanges via MediatR (OrdemCriada, StatusAlterado, OrcamentoGerado, OrcamentoAprovado)
- **Concorrencia Otimista**: Version como ConcurrencyToken nas entidades principais
- **Serilog**: logs estruturados com observabilidade

### Por que Azure SQL Database

O dominio de oficina mecanica exige **consistencia transacional forte**: ao aprovar um orcamento, o sistema precisa em uma unica transacao ACID (1) alterar o status da OS, (2) dar baixa no estoque de cada peca utilizada e (3) publicar eventos de dominio. Um banco relacional com suporte a transacoes ACID garante que nenhum desses passos seja aplicado parcialmente em caso de falha. Alem disso, o modelo de dados possui relacionamentos expressivos (OS -> ItemPeca -> Peca, Cliente -> Veiculo) que se mapeiam naturalmente para tabelas relacionais com integridade referencial.

## Funcionalidades

### Gestao de Ordens de Servico (Core)
- Criacao de OS com cliente, veiculo, servicos e pecas
- Geracao automatica de orcamento
- Fluxo de status controlado pelo dominio: Recebida -> EmDiagnostico -> AguardandoAprovacao -> EmExecucao -> Finalizada -> Entregue (com o desvio AguardandoAprovacao -> OrcamentoRecusado)
- Historico completo de mudancas de status
- Consulta publica por numero da OS (acompanhamento pelo cliente)
- Aprovacao ou recusa de orcamento (endpoints publicos, pensados para notificacao externa do cliente) com baixa automatica no estoque na aprovacao
- Listagem priorizada por status (OS em execucao aparecem antes de recebidas) e mais antigas primeiro, ocultando por padrao as ja finalizadas/entregues
- Notificacao por e-mail ao cliente a cada mudanca de status da OS

### Gestao Administrativa
- CRUD de Clientes com busca por CPF/CNPJ
- CRUD de Veiculos (com validacao de placa antiga e Mercosul)
- CRUD de Servicos com ativacao/desativacao
- CRUD de Pecas com controle de estoque e alerta de estoque abaixo do minimo
- Tempo medio de execucao dos servicos

### Seguranca
- Autenticacao JWT para endpoints administrativos
- Validacao de CPF/CNPJ com algoritmo completo de digitos verificadores
- Validacao de placa (formatos antigo e Mercosul)
- Tratamento de excecoes padronizado (DomainException, ValidationException)

## Como Executar

### Pre-requisitos
- Docker e Docker Compose

### Com Docker (recomendado)

O banco de dados nao roda em container: a API se conecta a um Azure SQL
Database gerenciado na nuvem. Antes de subir o compose, copie `.env.example`
para `.env` e preencha as variaveis (nenhuma credencial fica hardcoded no
`docker-compose.yml`, que e versionado):

| Variavel | Uso |
|---|---|
| `SQL_CONNECTION_STRING` | Connection string do Azure SQL Database (servidor e senha, ver secao [Infraestrutura](#infraestrutura)) |
| `JWT_SECRET_KEY` | Chave usada para assinar os tokens JWT |
| `ADMIN_USUARIO` / `ADMIN_SENHA` | Credenciais de login da API (`POST /api/auth/login`) |
| `SONAR_DB_USER` / `SONAR_DB_PASSWORD` | Credenciais do Postgres interno do SonarQube |
| `SONAR_ADMIN_PASSWORD` | Senha definida pro admin do SonarQube no primeiro boot |

```bash
cp .env.example .env   # preencher as variaveis antes de continuar
docker compose up -d
```

| Servico | URL | Credenciais |
|---------|-----|-------------|
| API + Swagger | http://localhost:5000 | `ADMIN_USUARIO` / `ADMIN_SENHA` (`.env`) |
| SonarQube | http://localhost:9000 | admin / `SONAR_ADMIN_PASSWORD` (`.env`) |
| Mailpit (e-mails capturados) | http://localhost:8025 | — |

> A migration e aplicada automaticamente na primeira execucao (contra o Azure SQL configurado no `.env`). O SonarQube leva ~2 minutos para inicializar; o projeto `oficina-mecanica` e criado automaticamente pelo servico `sonar-setup`.

O servico `trivy` usa o profile `security` e nao e iniciado pelo `docker compose up -d`:

```bash
# Ignorado (comportamento padrao)
docker compose up -d

# Roda o scan uma vez e sai (recomendado)
docker compose run --rm trivy

# Sobe junto com os demais servicos (ex: pipeline CI)
docker compose --profile security up -d
```

O relatorio e salvo em `docs/trivy-report-raw.json`.

### Localmente

Pre-requisitos: .NET 8 SDK e acesso a um Azure SQL Database (ou outro SQL Server compativel).

```bash
# Restaurar dependencias
dotnet restore

# Configurar a connection string via User Secrets (recomendado) ou variavel de ambiente,
# sem editar appsettings.json diretamente:
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<sua-connection-string>" --project src/OficinaMecanica.API

# Executar
dotnet run --project src/OficinaMecanica.API
```

### Executar Testes

```bash
dotnet test
```

### Analise de Qualidade (SonarQube)

Com o stack no ar (`docker compose up -d`), instale o scanner e execute:

```bash
dotnet tool install --global dotnet-sonarscanner
```

```powershell
# Windows — obtenha o token em http://localhost:9000/account/security
dotnet sonarscanner begin /k:"oficina-mecanica" /n:"OficinaMecanica" /v:"1.0.0" `
  /d:sonar.host.url="http://localhost:9000" /d:sonar.token="SEU_TOKEN" `
  /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" `
  /d:sonar.cs.vstest.reportsPaths="**/*.trx"

dotnet build --no-incremental -c Release

dotnet test tests/OficinaMecanica.Domain.Tests --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults/Domain --logger "trx;LogFileName=domain.trx" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet test tests/OficinaMecanica.Application.Tests --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults/Application --logger "trx;LogFileName=application.trx" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
dotnet test tests/OficinaMecanica.Integration.Tests --no-build -c Release --collect:"XPlat Code Coverage" --results-directory TestResults/Integration --logger "trx;LogFileName=integration.trx" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

dotnet sonarscanner end /d:sonar.token="SEU_TOKEN"
```

Resultados em: http://localhost:9000/dashboard?id=oficina-mecanica  
Relatorio salvo em: [docs/sonarqube-report.md](docs/sonarqube-report.md)

### Analise de Vulnerabilidades (Trivy)

Com a imagem da API construida (`docker compose build api`), execute:

```bash
docker compose run --rm trivy
```

O Trivy escaneia a imagem `oficinamecanica-api:latest` em busca de CVEs nos pacotes do SO (Debian) e nas dependencias .NET, e grava o resultado em `docs/trivy-report-raw.json`.

Para um scan com saida em tabela no terminal:

```bash
docker run --rm \
  -v //var/run/docker.sock:/var/run/docker.sock \
  aquasec/trivy:latest image \
  --severity CRITICAL,HIGH \
  oficinamecanica-api:latest
```

Relatorio detalhado: [docs/trivy-report.md](docs/trivy-report.md)

## Infraestrutura

A infraestrutura de nuvem do projeto e provisionada como codigo com
Terraform, no diretorio [infra/](infra/), organizada em modulos:

| Modulo | Recurso Azure | Finalidade |
|--------|---------------|------------|
| `infra/rg` | Resource Group | Agrupa todos os recursos do projeto |
| `infra/storage` | Storage Account | Backend remoto do state do Terraform |
| `infra/acr` | Azure Container Registry | Registro das imagens Docker da API |
| `infra/aks` | Azure Kubernetes Service | Orquestracao dos containers em produção |
| `infra/keyvault` | Azure Key Vault | Armazenamento centralizado de segredos (rede restrita por IP) |
| `infra/helm` | Helm Releases (ingress-nginx, kube-prometheus-stack, Loki, Alloy) | Ingress Controller e observabilidade (metricas via Prometheus + Grafana, logs via Loki + Alloy) |
| `infra/sqldb` | Azure SQL Database | Banco de dados relacional gerenciado (tier serverless) |
| `infra/github_oidc` | Azure AD App Registration + Federated Identity Credential | Autenticacao do GitHub Actions no Azure via OIDC, sem secrets de longa duracao (ver secao [CI/CD](#cicd)) |

Dois arquivos na raiz de `infra/` (`keyvault_secrets.tf`, `aks_keyvault_access.tf`)
conectam modulos entre si sem criar dependencia circular entre eles - cada
um precisa enxergar dois modulos ao mesmo tempo, o que so e possivel na raiz.

### Provisionando a infraestrutura

Pre-requisitos: [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.5, Azure CLI autenticado (`az login`) com permissao na assinatura.

```bash
cd infra
cp terraform.tfvars.example terraform.tfvars   # preencher os valores (nomes de recursos, regiao, etc.)

# Variaveis sensiveis nao tem default em terraform.tfvars.example de proposito
# (nunca commitadas) - definir via variavel de ambiente antes do apply:
export TF_VAR_sql_administrator_login_password="<senha-forte>"
export TF_VAR_jwt_secret_key="<chave-forte>"
export TF_VAR_admin_senha="<senha-forte>"

terraform init
terraform plan    # revisar o que sera criado/alterado antes de aplicar
terraform apply
```

> **Nota sobre o backend remoto**: o state fica em uma Storage Account
> (`infra/storage`, ver `infra/backend.tf`) que e ela mesma criada pelo
> Terraform — na pratica isso significa que a primeira vez que o projeto foi
> provisionado precisou de um bootstrap (aplicar `module.storage` com state
> local antes de configurar o backend remoto). Como a storage account ja
> existe hoje, um `terraform init` normal e suficiente para quem for rodar a
> partir daqui.

O `terraform apply` cria todos os 8 modulos na ordem correta de dependencias
(o proprio Terraform monta esse grafo a partir das referencias entre
`module.*`, sem precisar de flags especiais) — do Resource Group ate o
cluster AKS, Key Vault e a federacao OIDC do GitHub Actions. Para aplicar so
uma parte especifica durante o desenvolvimento (ex: iterar num modulo sem
tocar nos demais), use `terraform apply -target="module.<nome>"`.

Depois do apply, alguns outputs sao necessarios pra configurar o resto do
projeto (`.env` da API, variaveis do GitHub Actions):

```bash
terraform output                          # lista todos os outputs
terraform output -raw sql_server_fqdn     # ex: valor especifico, sem aspas
```

Variaveis sensiveis mantidas fora do controle de versao (`terraform.tfvars`,
ignorado pelo Git).

> **Nota sobre o Azure SQL Database**: o tier sempre-gratis (`use-free-limit`)
> so pode ser definido no momento da criacao do banco, via Azure CLI — o
> provider Terraform (`azurerm`) ainda nao expoe esse campo. Por isso o
> banco foi criado com `az sql db create --use-free-limit true` e em
> seguida importado para o state do Terraform (`terraform import`), para
> que continue gerenciado como o restante da infraestrutura.

> **O que vem no pacote `kube-prometheus-stack`** para observabilidade em Kubernetes:
>
> | Componente | Finalidade |
> |------------|------------|
> | Prometheus | Coleta e armazena as metricas (banco de dados de series temporais) |
> | Prometheus Operator | Controller que gerencia o Prometheus via CRDs (`ServiceMonitor`, `PodMonitor`, `PrometheusRule`, etc.) |
> | Grafana | Dashboards e visualizacao das metricas |
> | kube-state-metrics | Metricas sobre o estado dos objetos do Kubernetes (Deployments, Pods, HPAs, etc.) |
> | node-exporter | Metricas de sistema operacional/hardware de cada node |
> | Alertmanager | Roteamento de alertas (Slack, e-mail, etc.) — **desabilitado** neste projeto, sem canal de alerta configurado ainda |
>
> Cada componente ja vem com seu proprio `Deployment`/`StatefulSet`, `Service`
> e permissoes de RBAC do Kubernetes — nada disso precisou ser escrito na
> mao, so configurado via `infra/helm/monitoring.yaml.tpl`.

> **Logs agregados via Loki + Grafana Alloy** (`infra/helm/loki.tf`):
>
> | Componente | Finalidade |
> |------------|------------|
> | Loki | Armazena e indexa os logs (modo `Monolithic` — um unico binario, sem os componentes read/write/backend separados do modo distribuido, que so fariam sentido em escala maior) |
> | Grafana Alloy | Le o log de cada container do node (DaemonSet, 1 pod ja que o cluster tem 1 node so) e envia pro Loki |
>
> O Loki roda com storage em filesystem (PVC de
> 10Gi na mesma StorageClass do Prometheus/Grafana, sem object storage tipo
> Azure Blob Storage) e retencao de 72h — mais longa que as 6h do Prometheus
> Caches de chunks/resultados do Loki (baseados em Memcached) e o canary de
> teste E2E ficam desabilitados de proposito: o cluster tem 1 node so
> (`Standard_D2s_v3`, 2 vCPU/8GiB), e cada um desses componentes adicionaria
> outro Pod competindo pelo mesmo recurso escasso, sem necessidade real no
> volume de log baixo deste projeto.

## Kubernetes

Os manifests da aplicacao ficam no diretorio [k8s/](k8s/), organizados por
assunto. Em condicoes normais eles sao aplicados automaticamente pelo
pipeline de CI/CD a cada push na `main` (ver [CI/CD](#cicd)) — os passos
abaixo servem pra rodar o mesmo deploy manualmente (primeira vez, ou fora do
pipeline).

### Deploy manual no cluster

Pre-requisitos: infraestrutura ja provisionada (secao [Infraestrutura](#infraestrutura)), [kubectl](https://kubernetes.io/docs/tasks/tools/) instalado, Azure CLI autenticado (`az login`).

```bash
# Autentica o kubectl local contra o cluster AKS (baixa o kubeconfig)
az aks get-credentials --resource-group rgfiap --name aksfiap

# Aplica os manifests da API (Deployment, Service, Ingress, HPA, SecretProviderClass)
kubectl apply -f k8s/oficinamecanica-api/

# Aplica os manifests de observabilidade (ServiceMonitor, dashboards, Ingress do Grafana/Prometheus)
kubectl apply -f k8s/monitoring/

# Aplica o Mailpit (SMTP de desenvolvimento, dentro do proprio cluster)
kubectl apply -f k8s/mailpit/

# Acompanha o rollout e confirma que os pods subiram
kubectl rollout status deployment/oficinamecanica-api
kubectl get pods
```

### `k8s/oficinamecanica-api/`

| Arquivo | Recurso | Finalidade |
|---------|---------|------------|
| `deployment.yaml` | Deployment | Gerencia os pods da API (imagem publicada no Azure Container Registry) |
| `service.yaml` | Service (ClusterIP) | Expõe os pods internamente ao cluster |
| `ingress.yaml` | Ingress | Roteamento externo via ingress-nginx |
| `hpa.yaml` | HorizontalPodAutoscaler | Escala de 2 a 5 replicas por CPU/memoria |
| `secret-provider-class.yaml` | SecretProviderClass | Lê os segredos da aplicação direto do Key Vault e sincroniza para um Secret nativo |

As credenciais sensíveis (connection string, chave JWT, senha do admin) ficam
no Azure Key Vault (`infra/keyvault_secrets.tf`), não mais em um arquivo
aplicado manualmente. O CSI Secrets Store driver sincroniza esses valores
para o Secret nativo `oficinamecanica-secrets` automaticamente, assim que o
Deployment monta o `SecretProviderClass` como volume:

```bash
kubectl apply -f k8s/oficinamecanica-api/
```

### `k8s/monitoring/`

| Arquivo | Recurso | Finalidade |
|---------|---------|------------|
| `servicemonitor.yaml` | ServiceMonitor | Configura o Prometheus (instalado via Terraform) pra coletar as metricas da API em `GET /metrics` |
| `ingress.yaml` | Ingress | Expõe o Grafana e o Prometheus externamente via ingress-nginx (duas regras, um host cada) |
| `dashboard-oficinamecanica-api.yaml` | ConfigMap | Dashboard do Grafana como código — importado automaticamente pelo sidecar |

O Ingress usa host baseado em [nip.io](https://nip.io)
(resolve `qualquer-coisa.<ip>.nip.io` para o próprio IP embutido no nome, sem
precisar de domínio próprio) — antes de aplicar, substitua `<IP-DO-INGRESS>`
pelo IP público do ingress-nginx:

```bash
kubectl get svc -n ingress-nginx ingress-nginx-controller   # copiar o EXTERNAL-IP
kubectl apply -f k8s/monitoring/
```

**Acessando o Grafana**: o chart gera uma senha aleatoria pro usuario
`admin` a cada instalacao (nao fica hardcoded em lugar nenhum, nem no
`.tf`/state) — pra recuperar o valor atual:

```bash
kubectl get secret monitoring-grafana -n monitoring -o jsonpath="{.data.admin-password}" | base64 -d
```

Login em `http://grafana.<IP-DO-INGRESS>.nip.io` (ou via `kubectl port-forward
-n monitoring svc/monitoring-grafana 3000:80`, depois `http://localhost:3000`),
usuario `admin` + a senha do comando acima.

### `k8s/mailpit/`

| Arquivo | Recurso | Finalidade |
|---------|---------|------------|
| `deployment.yaml` | Deployment | Servidor SMTP de desenvolvimento (`axllent/mailpit`), 1 réplica, sem persistência |
| `service.yaml` | Service (ClusterIP) | Porta 1025 (SMTP, usada pela API) e 8025 (UI web) |
| `ingress.yaml` | Ingress | Expõe só a UI web (porta 8025) via ingress-nginx, host nip.io |

Mesmo papel que o serviço `mailpit` do `docker-compose.yml`, só que rodando
dentro do próprio cluster — permite demonstrar o fluxo de notificação por
e-mail (ver [Notificação por E-mail](#notificacao-por-e-mail)) direto no AKS,
sem precisar rodar o Docker Compose em paralelo. `k8s/oficinamecanica-api/deployment.yaml`
já aponta `Smtp__Host: mailpit` (mesmo namespace, resolvido pelo DNS interno
do cluster).

```bash
kubectl apply -f k8s/mailpit/
```

### Permissões: RBAC do Azure vs. RBAC do Kubernetes

O projeto usa dois sistemas de controle de acesso distintos, que não devem
ser confundidos:

- **Azure RBAC** (`azurerm_role_assignment`): concede permissões sobre
  **recursos do Azure** a identidades gerenciadas do cluster — `AcrPull`
  (definido em `infra/aks/main.tf`, permite ao AKS puxar imagens do Azure
  Container Registry) e `Key Vault Secrets User` (definido em
  `infra/aks_keyvault_access.tf`, na raiz — permite ao CSI Secrets Store
  driver ler segredos do Key Vault). Esses precisam ser criados
  explicitamente via Terraform.
- **RBAC do Kubernetes** (`ClusterRole`/`ClusterRoleBinding`, nativos do
  cluster): controlam o que cada `ServiceAccount` pode fazer **dentro da
  API do Kubernetes**. O Prometheus e o Grafana instalados via
  `infra/helm/monitoring.tf` já vêm com suas próprias permissões desse
  tipo, criadas automaticamente pelo chart `kube-prometheus-stack` — o
  Prometheus precisa listar/observar Pods e Services do cluster pra
  descobrir alvos de coleta, e o Grafana precisa observar `ConfigMap`s
  pra importar dashboards/datasources. Nenhum desses recursos precisa ser
  criado manualmente.

## CI/CD

O fluxo de deploy e automatizado via GitHub Actions
([.github/workflows/ci.yml](.github/workflows/ci.yml)), em 3 jobs sequenciais.
Roda automaticamente em todo push/PR pra `main`, ou sob demanda a qualquer
momento pelo botao **Run workflow** na aba *Actions* do GitHub
(`workflow_dispatch`) — util, por exemplo, pra re-testar o deploy depois de
um `terraform apply` que nao mexeu em codigo da aplicacao:

```
push/PR na main OU disparo manual (Run workflow)
  -> 1. build-and-test        (restore + build Release + testes Domain/Application/Integration)
       |
       | (so segue daqui em push direto na main ou disparo manual, nao em PR)
       v
     2. build-and-push-image  (login no Azure via OIDC, build da imagem, push pro ACR
       |                       com as tags <sha-do-commit> e latest)
       v
     3. deploy-to-aks         (kubectl apply nos manifests + kubectl set image
                                pro <sha-do-commit> + kubectl rollout status)
```

| Job | Quando roda | O que faz |
|-----|-------------|-----------|
| `build-and-test` | Todo push, PR ou disparo manual | Restore, build, os 3 projetos de teste, publica resultados como Job Summary (`dorny/test-reporter`) e artifact |
| `build-and-push-image` | So em push direto na `main` ou disparo manual (nao em PR) | Autentica no Azure (OIDC), publica a imagem no `acrfiap.azurecr.io` com a tag do commit (`github.sha`) e `latest` |
| `deploy-to-aks` | Idem, apos o job anterior | Aplica os manifests de `k8s/` e atualiza o Deployment pra imagem recem publicada, aguardando o rollout terminar |

**Autenticacao sem secrets de longa duracao**: os jobs 2 e 3 autenticam no
Azure via **OIDC** (`infra/github_oidc/`) — o GitHub emite um token de
identidade de curta duracao a cada execucao do workflow, e o Azure confia
nele atraves de uma Federated Identity Credential restrita a
`repo:<owner>/<repo>:ref:refs/heads/main`. Nao ha nenhum secret de Service
Principal armazenado no repositorio; as 3 `variables` do repositorio
(`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, valores nao
sensiveis — vem dos outputs do Terraform) so identificam pra qual App
Registration o token deve ser trocado.

**API server do AKS sem restricao de IP**: nao criei nenhum mecanismo de
`authorized_ip_ranges` pro cluster — o runner do GitHub Actions e hospedado
pelo GitHub, com IP dinamico, e nao teria como ser adicionado a uma lista
fixa sem um passo extra no workflow pra liberar/revogar IP a cada execucao,
adicionando minutos de espera por deploy pra uma protecao que ja e coberta
pela autenticacao: sem uma credencial Azure AD valida com a role certa
(`Cluster Admin Role`, `infra/github_oidc/main.tf`), o IP sozinho nao abre
o cluster pra ninguem. Restringir por IP so faria sentido com um runner
self-hosted dentro da mesma rede (VNet) do cluster — fora de escopo aqui.

**Limitacao conhecida**: o job `deploy-to-aks` ainda falha se o cluster AKS
estiver parado (`az aks stop`, usado pra nao gerar custo ocioso quando o
projeto nao esta em uso) — decisao consciente de manter o gatilho automatico
mesmo assim, em vez de exigir uma etapa manual de "religar o cluster" antes
de todo deploy.

**Fora do pipeline por enquanto**: a analise de qualidade via SonarQube (ver
[Analise de Qualidade](#analise-de-qualidade-sonarqube)) continua rodando so
localmente — a instancia atual vive dentro do Docker Compose do
desenvolvedor, inalcancavel pelo runner do GitHub Actions. Migrar pra uma
instancia acessivel (ex: SonarCloud) integraria isso ao job
`build-and-test`. O provisionamento da infraestrutura (Terraform) tambem
continua manual (ver [Provisionando a infraestrutura](#provisionando-a-infraestrutura)) — so o deploy da *aplicacao* e automatizado hoje.

## Endpoints da API

A collection completa e interativa da API (todas as rotas, schemas de request/response, e um botao "Try it out" pra chamar cada endpoint direto do navegador) e gerada automaticamente pelo Swashbuckle a partir dos controllers:

| Ambiente | URL |
|----------|-----|
| Local (`dotnet run`) | `https://localhost:{porta}` (porta exibida no console ao subir a API) |
| Docker (`docker compose up -d`) | http://localhost:5000 |
| AKS (producao) | IP publico do `ingress-nginx` — `kubectl get svc -n ingress-nginx ingress-nginx-controller` (coluna `EXTERNAL-IP`) |

Nos tres casos a Swagger UI fica na raiz (`/`, `RoutePrefix` vazio) e o JSON OpenAPI cru em `/swagger/v1/swagger.json`.

As tabelas abaixo resumem as mesmas rotas, agrupadas por area, para referencia rapida sem abrir o Swagger:

### Observabilidade
| Metodo | Rota | Autenticado | Descricao |
|--------|------|:-----------:|-----------|
| GET | /health | Nao | Health check simples (confirma que o processo esta de pe) |
| GET | /metrics | Nao | Metricas no formato Prometheus (scraped via `k8s/monitoring/servicemonitor.yaml`) |

### Autenticacao
| Metodo | Rota | Descricao |
|--------|------|-----------|
| POST | /api/auth/login | Login (retorna JWT) |

Credenciais: valor de `ADMIN_USUARIO`/`ADMIN_SENHA` no `.env` (rodando via Docker) ou `AdminCredentials` no `appsettings.json` (rodando localmente) — default sugerido nos dois: `admin` / `Admin@123`

### Clientes
| Metodo | Rota | Autenticado | Descricao |
|--------|------|:-----------:|-----------|
| POST | /api/clientes | Sim | Criar cliente |
| GET | /api/clientes/{id} | Sim | Obter cliente por ID |
| GET | /api/clientes/documento/{numero} | Sim | Buscar cliente por CPF/CNPJ (sem formatacao) |
| GET | /api/clientes | Sim | Listar clientes (paginado, filtro por nome) |
| PUT | /api/clientes/{id} | Sim | Atualizar cliente |
| DELETE | /api/clientes/{id} | Sim | Remover cliente |

### Veiculos
| Metodo | Rota | Autenticado | Descricao |
|--------|------|:-----------:|-----------|
| POST | /api/veiculos | Sim | Criar veiculo |
| GET | /api/veiculos/{id} | Sim | Obter veiculo por ID |
| GET | /api/veiculos/cliente/{clienteId} | Sim | Listar veiculos do cliente |
| PUT | /api/veiculos/{id} | Sim | Atualizar veiculo |
| DELETE | /api/veiculos/{id} | Sim | Remover veiculo |

### Servicos
| Metodo | Rota | Autenticado | Descricao |
|--------|------|:-----------:|-----------|
| POST | /api/servicos | Sim | Criar servico |
| GET | /api/servicos/{id} | Sim | Obter servico por ID |
| GET | /api/servicos | Sim | Listar servicos (paginado) |
| PUT | /api/servicos/{id} | Sim | Atualizar servico |
| PATCH | /api/servicos/{id}/desativar | Sim | Desativar servico |
| PATCH | /api/servicos/{id}/ativar | Sim | Ativar servico |

### Pecas
| Metodo | Rota | Autenticado | Descricao |
|--------|------|:-----------:|-----------|
| POST | /api/pecas | Sim | Criar peca |
| GET | /api/pecas/{id} | Sim | Obter peca por ID |
| GET | /api/pecas | Sim | Listar pecas (paginado) |
| PUT | /api/pecas/{id} | Sim | Atualizar peca |
| PATCH | /api/pecas/{id}/estoque | Sim | Adicionar estoque |
| PATCH | /api/pecas/{id}/desativar | Sim | Desativar peca |
| PATCH | /api/pecas/{id}/ativar | Sim | Ativar peca |

### Ordens de Servico
| Metodo | Rota | Autenticado | Descricao |
|--------|------|:-----------:|-----------|
| POST | /api/ordens-de-servico | Sim | Criar OS |
| GET | /api/ordens-de-servico/{id} | Sim | Obter OS por ID |
| GET | /api/ordens-de-servico/numero/{numero} | Nao | Consulta publica por numero |
| GET | /api/ordens-de-servico | Sim | Listar OS (paginado, filtro por status; sem filtro, exclui Finalizada/Entregue e ordena por prioridade de status + mais antigas primeiro) |
| PATCH | /api/ordens-de-servico/{id}/status | Sim | Atualizar status (bloqueado para EmExecucao e OrcamentoRecusado, que tem endpoint proprio) |
| POST | /api/ordens-de-servico/{id}/aprovar | Nao | Aprovar orcamento |
| POST | /api/ordens-de-servico/{id}/recusar | Nao | Recusar orcamento (corpo opcional `{ "motivo": "..." }`) |
| GET | /api/ordens-de-servico/tempo-medio | Sim | Tempo medio de execucao |

## Regras de Negocio

### Ordem de Servico

**Fluxo de status** — transicoes controladas exclusivamente pelo dominio (`OrdemDeServico.cs`):

```
Recebida → EmDiagnostico → AguardandoAprovacao → EmExecucao → Finalizada → Entregue
                                    ↓
                            OrcamentoRecusado
```

Nenhuma transicao pode ser pulada. Tentar ir de `Recebida` direto para `EmExecucao`, por exemplo, lanca `DomainException`.

| Transicao | Regra adicional |
|-----------|----------------|
| Recebida → EmDiagnostico | — |
| EmDiagnostico → AguardandoAprovacao | A OS deve ter pelo menos um servico adicionado |
| AguardandoAprovacao → EmExecucao | Aprovacao do orcamento — da baixa automatica no estoque de todas as pecas da OS |
| AguardandoAprovacao → OrcamentoRecusado | Recusa do orcamento — sem baixa de estoque, motivo opcional registrado no historico |
| EmExecucao → Finalizada | Registra `DataConclusao` |
| Finalizada → Entregue | — |

**Baixa de estoque** ocorre no momento da aprovacao do orcamento (transicao para `EmExecucao`). Se o estoque de qualquer peca for insuficiente, a operacao inteira e abortada com `DomainException`.

**Historico de status** — toda transicao gera automaticamente um registro em `HistoricoStatus` com a observacao padrao do dominio. O historico e imutavel.

**Adicao de servicos e pecas** — so e permitida quando a OS esta em `Recebida` ou `EmDiagnostico`. Tentativas em outros status lancam `DomainException`.

**Numero da OS** — gerado automaticamente no formato `OS-YYYYMMDD-XXXXXX` (data + 6 caracteres do GUID). Unico no banco.

**Consulta publica** — o endpoint `GET /api/ordens-de-servico/numero/{numero}` e anonimo para que o cliente final acompanhe o status da OS sem precisar de conta.

**Aprovacao ou recusa de orcamento** — os endpoints `POST /api/ordens-de-servico/{id}/aprovar` e `POST /api/ordens-de-servico/{id}/recusar` sao anonimos (pensados para receber uma notificacao externa do cliente, ex: link enviado por e-mail). Internamente chamam `AprovarOrcamento()`/`RecusarOrcamento(motivo)` no dominio. `StatusOrdemDeServico.OrcamentoRecusado` vale `0` (nao o proximo numero livre) de proposito: a listagem ordena por `OrderByDescending(Status)`, entao um valor baixo faz OS recusadas ficarem no fim da fila de prioridade, sem exigir um mapeamento de prioridade customizado.

**Listagem** (`GET /api/ordens-de-servico`) — sem `filtroStatus` explicito, exclui logicamente (via filtro de query, sem soft-delete fisico) as OS em `Finalizada`/`Entregue` e ordena por prioridade de status (`EmExecucao > AguardandoAprovacao > EmDiagnostico > Recebida > OrcamentoRecusado`), mais antigas primeiro dentro do mesmo status. Passar `filtroStatus` explicitamente (ex: `?filtroStatus=Finalizada`) sobrepoe essa exclusao padrao.

### Clientes e Veiculos

- CPF e validado com os dois digitos verificadores; CNPJ com os dois digitos verificadores. Documentos invalidos retornam 400.
- Busca por documento (`GET /api/clientes/documento/{numero}`) aceita CPF (11 digitos) ou CNPJ (14 digitos) sem formatacao. Documento invalido ou nao encontrado retorna 404 (sem lancar excecao).
- Um cliente so pode ser removido se nao possuir veiculos cadastrados (integridade referencial via EF Core / FK).

### Pecas e Servicos

- `QuantidadeEstoque` nunca pode ser negativa. `RemoverEstoque` lanca `DomainException` se a quantidade solicitada exceder o disponivel.
- `EstoqueMinimo` e informativo; a propriedade `EstoqueAbaixoDoMinimo` sinaliza quando o estoque esta critico.
- Pecas e servicos desativados (`Ativo = false`) nao sao removidos do banco — o historico das OS que os utilizaram e preservado.

### Concorrencia

- Entidades `AggregateRoot` possuem a propriedade `Version` como `ConcurrencyToken` no EF Core. Atualizacoes concorrentes sobre o mesmo agregado serao detectadas e rejeitadas com `DbUpdateConcurrencyException`.

## Eventos de Dominio

Os eventos sao publicados pelo `AppDbContext.SaveChangesAsync` via `IMediator.Publish` **apos** a persistencia ser confirmada. Sao implementados como `INotification` do MediatR e podem ter zero ou mais handlers (`INotificationHandler<T>`).

| Evento | Disparado quando | Dados transportados |
|--------|-----------------|---------------------|
| `OrdemDeServicoCriadaEvent` | Nova OS e criada | `OrdemDeServicoId`, `Numero`, `ClienteId` |
| `StatusOrdemAlteradoEvent` | Qualquer transicao de status ocorre | `OrdemDeServicoId`, `Numero`, `NovoStatus` |
| `OrcamentoGeradoEvent` | OS avanca para `AguardandoAprovacao` | `OrdemDeServicoId`, `Numero`, `ValorTotal` |
| `OrcamentoAprovadoEvent` | Orcamento e aprovado (`EmExecucao`) | `OrdemDeServicoId`, `Numero` |
| `OrcamentoRecusadoEvent` | Orcamento e recusado (`OrcamentoRecusado`) | `OrdemDeServicoId`, `Numero`, `Motivo` |

> **Estado atual:** `StatusOrdemAlteradoEvent` tem um `INotificationHandler` implementado — `StatusOrdemAlteradoEventHandler` (`OficinaMecanica.Application.EventHandlers`), que envia e-mail ao cliente a cada mudanca de status (ver secao [Notificacao por E-mail](#notificacao-por-e-mail)). Um unico handler nesse evento cobre todas as transicoes (aprovacao, recusa, avanco de diagnostico etc.) sem precisar de um handler por evento especifico. Os demais eventos (`OrdemDeServicoCriadaEvent`, `OrcamentoGeradoEvent`, `OrcamentoAprovadoEvent`, `OrcamentoRecusadoEvent`) continuam publicados mas sem nenhum handler consumindo-os — ficam disponiveis para casos de uso futuros (ex: notificacao de estoque critico, integracao com sistemas externos) sem exigir mudanca no dominio.

## Notificacao por E-mail

- Toda mudanca de status de uma OS dispara um e-mail ao cliente (se ele tiver `Email` cadastrado), via `StatusOrdemAlteradoEventHandler`.
- `IEmailService` (`OficinaMecanica.Application.Interfaces`) e implementado por `SmtpEmailService` (`OficinaMecanica.Infrastructure.Services`, pacote `MailKit`) — sem provedor externo (SendGrid, Mailgun, etc.), fala SMTP direto com um servidor configurado via `Smtp:Host`/`Smtp:Port`/`Smtp:Remetente`.
- Resiliente de proposito: qualquer falha (SMTP fora do ar, e-mail mal formado, timeout de 5s) e apenas logada como Warning, nunca lanca excecao — o handler roda de forma sincrona dentro do `SaveChangesAsync` (`AppDbContext.cs`), entao uma falha aqui nao pode derrubar a resposta HTTP do endpoint que mudou o status.
- Sem `Smtp:Host` configurado (default em `appsettings.json`), o envio e apenas ignorado com um Warning — a API funciona normalmente sem SMTP configurado.
- Dev local: servico `mailpit` no `docker-compose.yml` (UI web em http://localhost:8025 para ver os e-mails capturados, nao entrega nada de verdade para fora).
- AKS: mesmo Mailpit rodando dentro do proprio cluster ([k8s/mailpit/](k8s/mailpit/)), pra demonstrar o fluxo de notificacao sem depender de SMTP externo nem rodar o Docker Compose em paralelo (ver secao [Kubernetes](#kubernetes)).

## Stack Tecnologica

| Categoria | Tecnologia | Versao |
|-----------|-----------|--------|
| Runtime | .NET | 8.0 |
| ORM | Entity Framework Core (Azure SQL Database) | 8.0.0 |
| CQRS | MediatR | 12.2.0 |
| Validacao | FluentValidation | 11.9.0 |
| Logs | Serilog | 8.0.0 |
| Metricas | prometheus-net.AspNetCore | 8.2.1 |
| API Docs | Swashbuckle (Swagger/OpenAPI) | 6.5.0 |
| Auth | JWT Bearer | 8.0.0 |
| Testes | xUnit + FluentAssertions + Moq + Bogus | 2.4.2 / 6.12.0 / 4.20.70 / 35.6.1 |
| Infra (app) | Docker + Docker Compose | — |
| Infra (cloud) | Terraform (Azure: AKS, ACR, Key Vault, Azure SQL Database) | — |
| Orquestracao | Kubernetes (AKS) + Helm (ingress-nginx, kube-prometheus-stack, Loki, Alloy) | — |
| Observabilidade (cluster) | Prometheus + Grafana (metricas) + Loki + Grafana Alloy (logs) | — |
| Qualidade | SonarQube 10 Community | — |
| Seguranca | Trivy (Aqua Security) | latest |
