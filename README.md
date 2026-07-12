# OficinaMecanica - Sistema de Gestao para Oficina Mecanica

Sistema backend para gestao de oficina mecanica, desenvolvido em .NET 8 com DDD, Clean Architecture e CQRS.

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
- Fluxo de status controlado pelo dominio: Recebida -> EmDiagnostico -> AguardandoAprovacao -> EmExecucao -> Finalizada -> Entregue
- Historico completo de mudancas de status
- Consulta publica por numero da OS (acompanhamento pelo cliente)
- Aprovacao de orcamento com baixa automatica no estoque

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
para `.env` e preencha `SQL_CONNECTION_STRING` com o servidor e a senha do
seu Azure SQL Database (a infraestrutura correspondente e provisionada via
Terraform, ver secao [Infraestrutura](#infraestrutura)).

```bash
cp .env.example .env   # preencher SQL_CONNECTION_STRING antes de continuar
docker compose up -d
```

| Servico | URL | Credenciais |
|---------|-----|-------------|
| API + Swagger | http://localhost:5000 | — |
| SonarQube | http://localhost:9000 | admin / Admin@Sonar2024 |

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
| `infra/helm` | Helm Releases (ingress-nginx, kube-prometheus-stack) | Ingress Controller e observabilidade (Prometheus + Grafana) |
| `infra/sqldb` | Azure SQL Database | Banco de dados relacional gerenciado (tier serverless) |

O deploy segue o fluxo padrao do Terraform (`init` -> `plan` -> `apply`) a
partir do diretorio `infra/`, com variaveis sensiveis mantidas fora do
controle de versao (`terraform.tfvars`, ignorado pelo Git).

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

## Kubernetes

Os manifests da aplicacao ficam no diretorio [k8s/](k8s/), organizados por assunto:

### `k8s/oficinamecanica-api/`

| Arquivo | Recurso | Finalidade |
|---------|---------|------------|
| `deployment.yaml` | Deployment | Gerencia os pods da API (imagem publicada no Azure Container Registry) |
| `service.yaml` | Service (ClusterIP) | Expõe os pods internamente ao cluster |
| `ingress.yaml` | Ingress | Roteamento externo via ingress-nginx |
| `hpa.yaml` | HorizontalPodAutoscaler | Escala de 2 a 5 replicas por CPU/memoria |
| `secret.yaml.example` | Secret (template) | Modelo para as credenciais sensiveis da aplicacao |

Antes de aplicar, copie `secret.yaml.example` para `secret.yaml` (fora do Git)
e preencha com os valores reais:

```bash
kubectl apply -f k8s/oficinamecanica-api/secret.yaml
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

### Permissões: RBAC do Azure vs. RBAC do Kubernetes

O projeto usa dois sistemas de controle de acesso distintos, que não devem
ser confundidos:

- **Azure RBAC** (`azurerm_role_assignment`, definido em `infra/aks/main.tf`):
  concede permissões sobre **recursos do Azure** a identidades gerenciadas
  do cluster — por exemplo, `AcrPull` (permite ao AKS puxar imagens do
  Azure Container Registry) e `Key Vault Secrets User` (permite ao CSI
  Secrets Store driver ler segredos do Key Vault). Esses precisam ser
  criados explicitamente via Terraform.
- **RBAC do Kubernetes** (`ClusterRole`/`ClusterRoleBinding`, nativos do
  cluster): controlam o que cada `ServiceAccount` pode fazer **dentro da
  API do Kubernetes**. O Prometheus e o Grafana instalados via
  `infra/helm/monitoring.tf` já vêm com suas próprias permissões desse
  tipo, criadas automaticamente pelo chart `kube-prometheus-stack` — o
  Prometheus precisa listar/observar Pods e Services do cluster pra
  descobrir alvos de coleta, e o Grafana precisa observar `ConfigMap`s
  pra importar dashboards/datasources. Nenhum desses recursos precisa ser
  criado manualmente.

## Endpoints da API

### Observabilidade
| Metodo | Rota | Autenticado | Descricao |
|--------|------|:-----------:|-----------|
| GET | /health | Nao | Health check simples (confirma que o processo esta de pe) |
| GET | /metrics | Nao | Metricas no formato Prometheus (scraped via `k8s/monitoring/servicemonitor.yaml`) |

### Autenticacao
| Metodo | Rota | Descricao |
|--------|------|-----------|
| POST | /api/auth/login | Login (retorna JWT) |

Credenciais padrao: `admin` / `Admin@123`

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
| GET | /api/ordens-de-servico | Sim | Listar OS (paginado, filtro por status) |
| PATCH | /api/ordens-de-servico/{id}/status | Sim | Atualizar status |
| POST | /api/ordens-de-servico/{id}/aprovar | Nao | Aprovar orcamento |
| GET | /api/ordens-de-servico/tempo-medio | Sim | Tempo medio de execucao |

## Regras de Negocio

### Ordem de Servico

**Fluxo de status** — transicoes controladas exclusivamente pelo dominio (`OrdemDeServico.cs`):

```
Recebida → EmDiagnostico → AguardandoAprovacao → EmExecucao → Finalizada → Entregue
```

Nenhuma transicao pode ser pulada. Tentar ir de `Recebida` direto para `EmExecucao`, por exemplo, lanca `DomainException`.

| Transicao | Regra adicional |
|-----------|----------------|
| Recebida → EmDiagnostico | — |
| EmDiagnostico → AguardandoAprovacao | A OS deve ter pelo menos um servico adicionado |
| AguardandoAprovacao → EmExecucao | Aprovacao do orcamento — da baixa automatica no estoque de todas as pecas da OS |
| EmExecucao → Finalizada | Registra `DataConclusao` |
| Finalizada → Entregue | — |

**Baixa de estoque** ocorre no momento da aprovacao do orcamento (transicao para `EmExecucao`). Se o estoque de qualquer peca for insuficiente, a operacao inteira e abortada com `DomainException`.

**Historico de status** — toda transicao gera automaticamente um registro em `HistoricoStatus` com a observacao padrao do dominio. O historico e imutavel.

**Adicao de servicos e pecas** — so e permitida quando a OS esta em `Recebida` ou `EmDiagnostico`. Tentativas em outros status lancam `DomainException`.

**Numero da OS** — gerado automaticamente no formato `OS-YYYYMMDD-XXXXXX` (data + 6 caracteres do GUID). Unico no banco.

**Consulta publica** — o endpoint `GET /api/ordens-de-servico/numero/{numero}` e anonimo para que o cliente final acompanhe o status da OS sem precisar de conta.

**Aprovacao de orcamento** — o endpoint `POST /api/ordens-de-servico/{id}/aprovar` tambem e anonimo (link enviado ao cliente). Internamente chama `AprovarOrcamento()` no dominio.

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

> **Estado atual:** a infraestrutura de eventos esta completa (disparo, publicacao via MediatR), porem **nenhum `INotificationHandler` foi implementado ainda**. Os eventos sao publicados mas nao ha observadores consumindo-os. Casos de uso previstos para implementacao futura: envio de e-mail/SMS ao cliente na aprovacao do orcamento, notificacao de estoque critico apos baixa, integracao com sistemas externos.

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
| Orquestracao | Kubernetes (AKS) + Helm (ingress-nginx, kube-prometheus-stack) | — |
| Observabilidade (cluster) | Prometheus + Grafana | — |
| Qualidade | SonarQube 10 Community | — |
| Seguranca | Trivy (Aqua Security) | latest |
