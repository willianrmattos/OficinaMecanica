# Evidencia do e2e integrado - 2026-11-08 (segunda rodada)

Segunda rodada do teste ponta a ponta local do OS Service, do Billing e da Execucao, agora a partir do **checkout principal**
(`E:\FIAP\Pos\OficinaMecanica\e2e`), depois de o OS Service ser integrado ao `origin/release` (BDD, rota sincrona
`GET /api/ordens-de-servico/{id}/execucao`, metricas da saga e CI/CD) e de os tres defeitos da primeira rodada serem corrigidos em
working trees ainda nao commitadas. Esta evidencia **nao altera** a anterior (`2026-11-08-execucao.md`, registro historico).
Tudo abaixo e **saida real** de `bash e2e/executar-cenarios.sh`, executado contra um ambiente recem-criado
(`derrubar.sh` + `subir.sh --regerar`). A saida bruta esta em `e2e/evidencias/2026-11-08-execucao-integrada-saida.txt`.

## Resultado

```text
01-caminho-feliz                         PASSOU   asserts=117|falhas=0|tempo=33s
02-recusa-do-orcamento                   PASSOU   asserts=43|falhas=0|tempo=12s
03-pagamento-expirado                    PASSOU   asserts=55|falhas=0|tempo=20s
04-execucao-inviavel-com-estorno         PASSOU   asserts=57|falhas=0|tempo=28s
05-resiliencia-billing-fora              PASSOU   asserts=121|falhas=0|tempo=107s
06-idempotencia                          PASSOU   asserts=70|falhas=0|tempo=31s
07-resiliencia-broker-fora               PASSOU   asserts=68|falhas=0|tempo=96s
99-verificacoes-finais                   PASSOU   asserts=43|falhas=0|tempo=19s
log completo: /e/FIAP/Pos/OficinaMecanica/e2e/.gerado/logs/execucao-20261108-171003.log
```

**8 de 8 passaram** (574 asserts, 0 falhas; a primeira rodada teve 514). Os tres defeitos da primeira rodada estao
**confirmados como corrigidos**, com evidencia, e um **defeito novo** (DEF-4, baixa gravidade) apareceu: ver "Defeitos".

### Historico desta rodada (transparencia)

- Os asserts novos foram depurados contra o ambiente real em tres rodadas completas antes desta. Os problemas eram do **proprio roteiro**,
  corrigidos no roteiro e nao nos servicos: (1) o 99 conferia as metricas da saga e todas vinham zeradas, porque o adapter do
  prometheus-net so observa os Meters a partir da **primeira** leitura do `/metrics` (incrementos anteriores nao aparecem; ja registrado no
  CLAUDE.md do Billing) e a primeira leitura so acontecia no proprio 99 — agora `subir.sh` e `executar-cenarios.sh` fazem essa leitura antes
  de qualquer fluxo; (1b) com isso as series apareceram, mas as com tag de causa/estado (`cancelada`, `orcamento_recusado`) tinham **sumido do `/metrics`** quando o 99
  rodava, pouco mais de 5 minutos depois do cenario 02: o mesmo adapter remove a serie cerca de 5 minutos depois da ultima atualizacao (`MetricsExpireAfter`, simbolo presente na
  `Prometheus.NetStandard.dll` da imagem) — passei a conferir cada metrica no proprio cenario que a incrementa (base antes, incremento depois) e o 99 ficou so com as series
  que o ultimo cenario atualiza; o trecho real dessa rodada intermediaria (cenario 02 as 17:01, 99 as 17:06, mais de 5 minutos depois):

  ```text
  banco: sagas=8 concluidas=5 canceladas=2 orcamento-recusado=1
  metricas: saga_iniciada=8 saga_concluida=5 saga_cancelada=2 saga_orcamento_recusado=0 saga_prazo_expirado=1 saga_decisao_ignorada=1
  [FALHA] saga_orcamento_recusado >= sagas OrcamentoRecusado (banco): obtido '0', esperado ge 1
  ```

  (2) o fallback `ExecucaoIndisponivel` que o cenario 01 provoca de proposito (docker pause da Execucao) gera avisos e
  ERR esperados no log, que a lista do 99 ainda nao previa (e uma regex minha nao casava com o texto real).
- O cabecalho abaixo mostra as alteracoes nao commitadas dos tres working trees, que sao o que o build usou.

## Estado testado

```text
== OficinaMecanica: HEAD 938487a (feature/microsservicos) test: adiciona teste ponta a ponta local dos tres microsservicos
   alteracoes no working tree (git status --short, sem e2e/):
      M CLAUDE.md
      M Dockerfile
      M README.md
      M docs/adr/0003-saga-orquestrada-no-os-service.md
      M docs/adr/0009-ci-cd-independente-com-gate-de-qualidade.md
      M docs/saga-maquina-de-estados.puml
      M src/OficinaMecanica.Domain/Entities/OrdemDeServico.cs
      M src/OficinaMecanica.Infrastructure/OficinaMecanica.Infrastructure.csproj
      M src/OficinaMecanica.Infrastructure/Repositories/OrdemDeServicoRepository.cs
      M tests/OficinaMecanica.Application.Tests/DTOs/OrdemDeServicoMapperTests.cs
      M tests/OficinaMecanica.Integration.Tests/Controllers/ListarOrdensDeServicoTests.cs
      M tests/OficinaMecanica.Integration.Tests/Controllers/OrdensDeServicoControllerTests.cs
     ?? docs/cobertura.md
     ?? tests/OficinaMecanica.Application.Tests/Queries/ObterOrdemDeServicoQueryHandlerTests.cs
     ?? tests/OficinaMecanica.Domain.Tests/Entities/OrdemDeServicoHistoricoEmOrdemTests.cs
     ?? tests/OficinaMecanica.Infrastructure.Tests/Infra/HistoricoDeStatusComSqlTests.cs
     ?? tests/OficinaMecanica.Integration.Tests/Repositories/TempoMedioPorStatusComMudancasSimultaneasTests.cs
     ?? tests/OficinaMecanica.Tests.Common/Builders/HistoricoEmbaralhado.cs
   arquivos de codigo/teste/Dockerfile alterados: Dockerfile src/OficinaMecanica.Domain/Entities/OrdemDeServico.cs src/OficinaMecanica.Infrastructure/OficinaMecanica.Infrastructure.csproj src/OficinaMecanica.Infrastructure/Repositories/OrdemDeServicoRepository.cs tests/OficinaMecanica.Application.Tests/DTOs/OrdemDeServicoMapperTests.cs tests/OficinaMecanica.Integration.Tests/Controllers/ListarOrdensDeServicoTests.cs tests/OficinaMecanica.Integration.Tests/Controllers/OrdensDeServicoControllerTests.cs 
   sha256 do diff de src+tests+Dockerfile: 6482e6c349789a12
== OficinaMecanica.Billing: HEAD 1351440 (feature/microsservicos) test: protege o valor do gate de cobertura no workflow
   alteracoes no working tree (git status --short, sem e2e/):
      M CLAUDE.md
      M README.md
      M docs/adr/0003-ci-cd-independente-com-gate-de-qualidade.md
      M src/OficinaMecanica.Billing.Infrastructure/Gateways/FakePagamentoGateway.cs
      M tests/OficinaMecanica.Billing.Integration.Tests/Gateways/FakePagamentoGatewayTests.cs
      M tests/OficinaMecanica.Billing.Integration.Tests/Infra/AmbienteInfra.cs
      M tests/OficinaMecanica.Billing.Integration.Tests/Infra/WebhookFimAFimTests.cs
     ?? docs/cobertura.md
   arquivos de codigo/teste/Dockerfile alterados: src/OficinaMecanica.Billing.Infrastructure/Gateways/FakePagamentoGateway.cs tests/OficinaMecanica.Billing.Integration.Tests/Gateways/FakePagamentoGatewayTests.cs tests/OficinaMecanica.Billing.Integration.Tests/Infra/AmbienteInfra.cs tests/OficinaMecanica.Billing.Integration.Tests/Infra/WebhookFimAFimTests.cs 
   sha256 do diff de src+tests+Dockerfile: 91643178168ecc55
== OficinaMecanica.Execucao: HEAD 4883e84 (feature/microsservicos) test: protege o valor do gate de cobertura no workflow
   alteracoes no working tree (git status --short, sem e2e/):
      M CLAUDE.md
      M README.md
      M docs/adr/0003-ci-cd-independente-com-gate-de-qualidade.md
     ?? docs/cobertura.md
   arquivos de codigo/teste/Dockerfile alterados: nenhum
   sha256 do diff de src+tests+Dockerfile: e3b0c44298fc1c14
```

Leitura: o OS esta no checkout principal, branch `feature/microsservicos`, HEAD `938487a` (o commit do e2e), com as correcoes DEF-2 (dominio e
repositorio) e DEF-3 (Dockerfile) em working tree, mais alteracoes de outros agentes em testes, documentacao e o pin de `System.Security.Cryptography.Pkcs` 8.0.2 no csproj da Infrastructure (CVE do MailKit); o Billing esta em `1351440` com a
correcao DEF-1 (`FakePagamentoGateway.cs`) em working tree; a Execucao esta em `4883e84` so com documentacao alterada. O build usa o **working tree**
de cada repositorio, entao as correcoes entram nas imagens; `.dockerignore` de cada um exclui `docs/` e `*.md`. O Mailpit do compose agora e a `v1.31.0`.

```text
== e2e oficina-e2e | 2026-11-08 17:10:03 ==
  OS: 938487a (feature/microsservicos), alteracoes nao commitadas fora de e2e/: 18
  Billing: 1351440 (feature/microsservicos), alteracoes nao commitadas fora de e2e/: 8
  Execucao: 4883e84 (feature/microsservicos), alteracoes nao commitadas fora de e2e/: 4
SERVICE     IMAGE                                        STATUS
billing     oficina-e2e/billing:local                    Up 11 seconds (healthy)
execucao    oficina-e2e/execucao:local                   Up 13 seconds (healthy)
jwks        python:3.12-alpine                           Up 19 seconds (healthy)
mailpit     axllent/mailpit:v1.31.0                      Up 19 seconds (healthy)
mongo       mongo:7.0.45                                 Up 19 seconds (healthy)
os          oficina-e2e/os:local                         Up 5 seconds (healthy)
rabbitmq    rabbitmq:4.4.0-management                    Up 19 seconds (healthy)
sqlserver   mcr.microsoft.com/mssql/server:2022-latest   Up 19 seconds (healthy)
```

## Asserts novos desta rodada

Total: de 514 para 574 asserts (+60), nos mesmos 8 blocos.

| Cenario | Antes | Agora | Novos asserts |
|---|---|---|---|
| 01 caminho feliz | 79 | 117 (+38) | rota sincrona `GET /api/ordens-de-servico/{id}/execucao`: 200 com `origem: execucao` em tres momentos (ordem na fila, reparo em andamento, OS finalizada com laudo, resumo e mecanico), 401 sem token, 404 de OS inexistente, e fallback com a Execucao congelada (`docker pause`): 200, `origem: saga`, `motivoDoFallback: ExecucaoIndisponivel`, `producao` nulo, resposta dentro do teto do cliente, e `origem` volta a `execucao` depois; `historicoStatus` na ordem em que a API devolve, primeira `Recebida`, ultima `Finalizada`, datas nao decrescentes (DEF-2); metricas `saga_iniciada` e `saga_concluida` sobem >= 1 |
| 02 recusa | 42 | 43 (+1) | `saga_orcamento_recusado{causa=ClienteRecusouOrcamento}` sobe >= 1 |
| 03 expirado | 53 | 55 (+2) | `saga_prazo_expirado{tipo=Pagamento}` e `saga_cancelada{estado_origem=CancelandoPagamento}` sobem >= 1 |
| 04 inviavel | 56 | 57 (+1) | `saga_cancelada{estado_origem=Estornando}` sobe >= 1 |
| 05 resiliencia Billing | 118 | 121 (+3) | a primeira aprovacao do gateway Fake depois dos restarts (stop e kill) responde **200** (sem a tolerancia/retry da rodada anterior), com id maior que todos os ja gravados e com 16 ou mais digitos, e exatamente uma tentativa nova gravada (DEF-1) |
| 06 idempotencia | 69 | 70 (+1) | `saga_decisao_ignorada` sobe pelo menos o numero de avisos `Decisao ... ignorada` do log desta OS |
| 07 resiliencia broker | 62 | 68 (+6) | com o RabbitMQ fora e a OS ainda nao enfileirada, `/execucao` responde 200 com `origem: saga`, `motivoDoFallback: ProducaoNaoEnfileirada`, `estadoDaSaga` vazio e `producao` nulo |
| 99 verificacoes finais | 35 | 43 (+8) | 0 colisoes de id do Fake (DEF-1), 0 aviso `Overriding HTTP_PORTS` (DEF-3), 0 409 de persistencia no Billing; ERR/WRN esperados do fallback da `/execucao` separados dos inesperados; metricas da saga coerentes com o banco (`saga_iniciada`, `saga_concluida`, contagem do histograma `>=`, e `compensacao_falhou`/`estorno_pendente_manual` em 0) |

Os asserts de historico dos cenarios 02 a 07 ja existiam, mas agora leem a ordem **da API** (o roteiro deixou de reordenar por data): DEF-2 vigiado em todo cenario.

## Como o ambiente subiu

```text
senhas de DEV geradas em /e/FIAP/Pos/OficinaMecanica/e2e/.gerado/e2e.env
chaves: E:/FIAP/Pos/OficinaMecanica/e2e/.gerado\jwt-privada.pem (so local) | jwks: E:/FIAP/Pos/OficinaMecanica/e2e/.gerado\jwks\jwks.json
== build das imagens (OS, Billing, Execucao) ==
 Image oficina-e2e/billing:local Building 
 Image oficina-e2e/execucao:local Building 
 Image oficina-e2e/os:local Building 
 Image oficina-e2e/billing:local Built 
 Image oficina-e2e/os:local Built 
 Image oficina-e2e/execucao:local Built 
== subindo ==
 Network oficina-e2e-net Creating 
 Network oficina-e2e-net Created 
 Volume oficina-e2e_sqlserver_data Creating 
 Volume oficina-e2e_sqlserver_data Created 
 Volume oficina-e2e_rabbitmq_data Creating 
 Volume oficina-e2e_rabbitmq_data Created 
 Volume oficina-e2e_mongo_data Creating 
 Volume oficina-e2e_mongo_data Created 
 Container oficina-e2e-rabbitmq-1 Creating 
 Container oficina-e2e-mongo-1 Creating 
 Container oficina-e2e-jwks-1 Creating 
 Container oficina-e2e-sqlserver-1 Creating 
 Container oficina-e2e-mailpit-1 Creating 
 Container oficina-e2e-sqlserver-1 Created 
 Container oficina-e2e-sql-init-1 Creating 
 Container oficina-e2e-mongo-1 Created 
 Container oficina-e2e-mongo-init-1 Creating 
 Container oficina-e2e-jwks-1 Created 
 Container oficina-e2e-rabbitmq-1 Created 
 Container oficina-e2e-mailpit-1 Created 
 Container oficina-e2e-sql-init-1 Created 
 Container oficina-e2e-billing-1 Creating 
 Container oficina-e2e-mongo-init-1 Created 
 Container oficina-e2e-execucao-1 Creating 
 Container oficina-e2e-billing-1 Created 
 Container oficina-e2e-execucao-1 Created 
 Container oficina-e2e-os-1 Creating 
 Container oficina-e2e-os-1 Created 
 Container oficina-e2e-rabbitmq-1 Starting 
 Container oficina-e2e-jwks-1 Starting 
 Container oficina-e2e-mailpit-1 Starting 
 Container oficina-e2e-mongo-1 Starting 
 Container oficina-e2e-sqlserver-1 Starting 
 Container oficina-e2e-jwks-1 Started 
 Container oficina-e2e-mongo-1 Started 
 Container oficina-e2e-mongo-init-1 Starting 
 Container oficina-e2e-mailpit-1 Started 
 Container oficina-e2e-rabbitmq-1 Started 
 Container oficina-e2e-sqlserver-1 Started 
 Container oficina-e2e-sqlserver-1 Waiting 
 Container oficina-e2e-mongo-init-1 Started 
 Container oficina-e2e-mongo-1 Waiting 
 Container oficina-e2e-mongo-init-1 Waiting 
 Container oficina-e2e-rabbitmq-1 Waiting 
 Container oficina-e2e-jwks-1 Waiting 
 Container oficina-e2e-mongo-init-1 Exited 
 Container oficina-e2e-jwks-1 Healthy 
 Container oficina-e2e-mongo-1 Healthy 
 Container oficina-e2e-rabbitmq-1 Healthy 
 Container oficina-e2e-execucao-1 Starting 
 Container oficina-e2e-execucao-1 Started 
 Container oficina-e2e-sqlserver-1 Healthy 
 Container oficina-e2e-sql-init-1 Starting 
 Container oficina-e2e-sql-init-1 Started 
 Container oficina-e2e-sql-init-1 Waiting 
 Container oficina-e2e-rabbitmq-1 Waiting 
 Container oficina-e2e-jwks-1 Waiting 
 Container oficina-e2e-jwks-1 Healthy 
 Container oficina-e2e-rabbitmq-1 Healthy 
 Container oficina-e2e-sql-init-1 Exited 
 Container oficina-e2e-billing-1 Starting 
 Container oficina-e2e-billing-1 Started 
 Container oficina-e2e-rabbitmq-1 Waiting 
 Container oficina-e2e-jwks-1 Waiting 
 Container oficina-e2e-billing-1 Waiting 
 Container oficina-e2e-execucao-1 Waiting 
 Container oficina-e2e-sql-init-1 Waiting 
 Container oficina-e2e-sql-init-1 Exited 
 Container oficina-e2e-jwks-1 Healthy 
 Container oficina-e2e-rabbitmq-1 Healthy 
 Container oficina-e2e-execucao-1 Healthy 
 Container oficina-e2e-billing-1 Healthy 
 Container oficina-e2e-os-1 Starting 
 Container oficina-e2e-os-1 Started 
== esperando as tres APIs ficarem prontas (/health/ready) ==
  OS pronto em 4 s
  Billing pronto em 4 s
  Execucao pronto em 4 s
NAME                      IMAGE                                        COMMAND                  SERVICE     CREATED          STATUS                            PORTS
oficina-e2e-billing-1     oficina-e2e/billing:local                    "dotnet OficinaMecan…"   billing     18 seconds ago   Up 9 seconds (healthy)            127.0.0.1:18081->8080/tcp
oficina-e2e-execucao-1    oficina-e2e/execucao:local                   "dotnet OficinaMecan…"   execucao    18 seconds ago   Up 11 seconds (healthy)           127.0.0.1:18082->8080/tcp
oficina-e2e-jwks-1        python:3.12-alpine                           "python -m http.serv…"   jwks        19 seconds ago   Up 18 seconds (healthy)           127.0.0.1:18090->80/tcp
oficina-e2e-mailpit-1     axllent/mailpit:v1.31.0                      "/mailpit"               mailpit     19 seconds ago   Up 18 seconds (healthy)           127.0.0.1:11025->1025/tcp, 127.0.0.1:18025->8025/tcp
oficina-e2e-mongo-1       mongo:7.0.45                                 "docker-entrypoint.s…"   mongo       19 seconds ago   Up 18 seconds (healthy)           127.0.0.1:27118->27017/tcp
oficina-e2e-os-1          oficina-e2e/os:local                         "dotnet OficinaMecan…"   os          18 seconds ago   Up 4 seconds (health: starting)   127.0.0.1:18080->8080/tcp
oficina-e2e-rabbitmq-1    rabbitmq:4.4.0-management                    "docker-entrypoint.s…"   rabbitmq    19 seconds ago   Up 18 seconds (healthy)           127.0.0.1:56720->5672/tcp, 127.0.0.1:15673->15672/tcp
oficina-e2e-sqlserver-1   mcr.microsoft.com/mssql/server:2022-latest   "/opt/mssql/bin/laun…"   sqlserver   19 seconds ago   Up 18 seconds (healthy)           127.0.0.1:14330->1433/tcp
Ambiente pronto. Portas (127.0.0.1):
  OS         http://127.0.0.1:18080        Billing  http://127.0.0.1:18081        Execucao http://127.0.0.1:18082
  JWKS stub  http://127.0.0.1:18090/.well-known/jwks.json
  RabbitMQ   amqp 56720 | UI http://127.0.0.1:15673 (usuario/senha em /e/FIAP/Pos/OficinaMecanica/e2e/.gerado/e2e.env)
  SQL Server 14330 | MongoDB 27118 | Mailpit http://127.0.0.1:18025
Cenarios: bash e2e/executar-cenarios.sh

tempo de parede do subir.sh --regerar (build com cache + subida + espera dos 3 /health/ready + leitura inicial do /metrics):
real	0m27.521s
user	0m0.591s
sys	0m0.668s
```

## Configuracao efetiva (sem segredos)

```text
### docker/compose
Docker Engine 29.6.1
Docker Compose version v5.3.0

### compose ps
SERVICE     IMAGE                                        STATUS                        PORTS
billing     oficina-e2e/billing:local                    Up 3 minutes (healthy)        127.0.0.1:18081->8080/tcp
execucao    oficina-e2e/execucao:local                   Up 6 minutes (healthy)        127.0.0.1:18082->8080/tcp
jwks        python:3.12-alpine                           Up 6 minutes (healthy)        127.0.0.1:18090->80/tcp
mailpit     axllent/mailpit:v1.31.0                      Up 6 minutes (healthy)        127.0.0.1:11025->1025/tcp, 127.0.0.1:18025->8025/tcp
mongo       mongo:7.0.45                                 Up 6 minutes (healthy)        127.0.0.1:27118->27017/tcp
os          oficina-e2e/os:local                         Up 6 minutes (healthy)        127.0.0.1:18080->8080/tcp
rabbitmq    rabbitmq:4.4.0-management                    Up About a minute (healthy)   127.0.0.1:56720->5672/tcp, 127.0.0.1:15673->15672/tcp
sqlserver   mcr.microsoft.com/mssql/server:2022-latest   Up 6 minutes (healthy)        127.0.0.1:14330->1433/tcp

### imagens construidas
oficina-e2e/os:local  67302c9a56d6  criada 39 minutes ago  379MB
oficina-e2e/billing:local  59bf22053fdb  criada 58 minutes ago  368MB
oficina-e2e/execucao:local  a0df492b882d  criada 2 hours ago  351MB

### env nao secreta de os
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_HTTP_PORTS=8080
Execucao__BaseUrl=http://execucao:8080
JwtSettings__Audience=OficinaMecanica.Client
JwtSettings__Issuer=OficinaMecanica.Seguranca
JwtSettings__JwksUri=http://jwks/.well-known/jwks.json
Mensageria__Persistencia=EntityFramework
Mensageria__Transporte=RabbitMq
RabbitMq__Host=rabbitmq
RabbitMq__Port=5672
RabbitMq__VirtualHost=/
Saga__PrazoAprovacaoHoras=72
Saga__PrazoPagamentoHoras=48
Saga__SweeperHabilitado=true
Saga__SweeperIntervaloSegundos=5
Saga__SweeperLote=50
Smtp__Host=mailpit
Smtp__Port=1025
Smtp__Remetente=no-reply@oficinamecanica.local

### env nao secreta de billing
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_HTTP_PORTS=8080
JwtSettings__Audience=OficinaMecanica.Client
JwtSettings__Issuer=OficinaMecanica.Seguranca
JwtSettings__JwksUri=http://jwks/.well-known/jwks.json
Mensageria__Persistencia=EntityFramework
Mensageria__Transporte=RabbitMq
Pagamento__Provider=Fake
RabbitMq__Host=rabbitmq
RabbitMq__Port=5672
RabbitMq__VirtualHost=/

### env nao secreta de execucao
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_HTTP_PORTS=8080
JwtSettings__Audience=OficinaMecanica.Client
JwtSettings__Issuer=OficinaMecanica.Seguranca
JwtSettings__JwksUri=http://jwks/.well-known/jwks.json
Mensageria__Outbox=MongoDb
Mensageria__Transporte=RabbitMq
MongoDb__Database=execucao
RabbitMq__Host=rabbitmq
RabbitMq__Port=5672
RabbitMq__VirtualHost=/

### replica set do MongoDB
mongo:27017 PRIMARY
```

## Verificacao dos contratos de mensagem

`bash e2e/verificar-contratos.sh` (saida real; o OS no checkout principal e a fonte da verdade):

```text
== Fonte da verdade (OS): /e/FIAP/Pos/OficinaMecanica ==
  33 goldens, 29 tipos
== Billing (/e/FIAP/Pos/OficinaMecanica.Billing) ==
  Billing tests/OficinaMecanica.Contracts.Tests/Golden: 18 arquivo(s) copiado(s), 0 divergente(s)
  Billing src/OficinaMecanica.Contracts: 17 arquivo(s) copiado(s), 0 divergente(s)
== Execucao (/e/FIAP/Pos/OficinaMecanica.Execucao) ==
  Execucao tests/OficinaMecanica.Contracts.Tests/Golden: 9 arquivo(s) copiado(s), 0 divergente(s)
  Execucao src/OficinaMecanica.Contracts: 11 arquivo(s) copiado(s), 0 divergente(s)
== Cobertura: cada servico tem todas as mensagens do seu lado ==
  Billing: 13 mensagem(ns) obrigatoria(s) do seu lado, 0 ausencia(s)
  Execucao: 7 mensagem(ns) obrigatoria(s) do seu lado, 0 ausencia(s)
== Billing x Execucao (interseccao dos dois conjuntos copiados) ==
  5 arquivo(s) presentes nos dois, comparados

RESULTADO: contratos IDENTICOS nos 3 repositorios (60 comparacao(oes) de hash ok).
exit=0
```

## Saida real de cada cenario

Legenda: `[OK]`/`[FALHA]` = assert; `[INFO]` = observacao; `-- [+Ns]` = tempo desde o inicio do cenario. Cada espera mostra "apos N s"
e tem teto; nada travou.

### Cenario 01 - Caminho feliz (+ rota sincrona /execucao e historico cronologico)

Inicio 17:10:04.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 1 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+1s] cadastros no OS Service (cliente com e-mail, veiculo, servico e peca com estoque 10)
  [OK]    cadastro do cliente A (HTTP) = '201'
  [OK]    cadastro do veiculo A (HTTP) = '201'
  [OK]    cadastro do servico A (HTTP) = '201'
  [OK]    cadastro da peca A (estoque 10) (HTTP) = '201'
  [OK]    estoque inicial da peca = '10'

-- [+3s] abre a OS (servico 150,00 + 2 pecas de 50,00 = 250,00)
  [OK]    abertura da OS A (HTTP) = '201'
  [OK]    status inicial da OS A = 'Recebida'
  [OK]    valor total da OS A = '250'
  OS OS-20261108-2AAB39 id=52b1073b-37c2-4577-88f1-2721ede94625

-- [+4s] saga nasce em AguardandoDiagnostico e a Execucao enfileira a ordem (SolicitarDiagnostico)
  [OK]    ordem de producao na Execucao = 'Fila' (apos 1 s)
  [OK]    estado da saga = 'AguardandoDiagnostico' (apos 0 s)

-- [+5s] rota sincrona GET /api/ordens-de-servico/{id}/execucao com a ordem esperando na fila da Execucao
  [OK]    consulta da execucao (OS repassa o token do chamador a Execucao) (HTTP) = '200'
  [OK]    origem da resposta = 'execucao'
  [OK]    numero da OS = 'OS-20261108-2AAB39'
  [OK]    statusDaOrdem (visao do OS) = 'Recebida'
  [OK]    estadoDaSaga (visao do OS) = 'AguardandoDiagnostico'
  [OK]    producao.status (visao da Execucao) = 'Fila'
  [OK]    motivoDoFallback (nao houve fallback) vazio
  [OK]    consulta da execucao sem token (HTTP) = '401'
  [OK]    consulta da execucao de OS inexistente (HTTP) = '404'

-- [+7s] mecanico reivindica o diagnostico (POST /api/diagnosticos/proximo)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '52b1073b-37c2-4577-88f1-2721ede94625'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS (DiagnosticoIniciado) = 'EmDiagnostico' (apos 1 s)
  [OK]    estado da saga = 'EmDiagnostico' (apos 0 s)

-- [+8s] mecanico conclui o diagnostico (publica DiagnosticoConcluido) e o Billing gera o orcamento
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS (orcamento gerado) = 'AguardandoAprovacao' (apos 1 s)
  [OK]    estado da saga = 'AguardandoAprovacao' (apos 0 s)

-- [+11s] confere o orcamento no Billing
  [OK]    consulta do orcamento no Billing (HTTP) = '200'
  [OK]    status do orcamento = 'Gerado'
  [OK]    valor total do orcamento = '250'
  [OK]    itens do orcamento (1 servico + 1 peca) = '2'
  [OK]    laudo do mecanico no orcamento contem 'correia dentada'
  [OK]    numero do orcamento = numero da OS = 'OS-20261108-2AAB39'
  [OK]    estoque ainda intacto antes da aprovacao = '10'

-- [+12s] cliente aprova o orcamento (rota anonima, 202)
  [OK]    aprovacao do orcamento (HTTP) = '202'
  [OK]    corpo do 202: ordemDeServicoId = '52b1073b-37c2-4577-88f1-2721ede94625'
  [OK]    corpo do 202: status ainda AguardandoAprovacao (decisao so publicada) = 'AguardandoAprovacao'
  [OK]    status da OS (saga aplicou a aprovacao) = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque reservado de verdade (10 - 2) = '8' (apos 0 s)

-- [+13s] Billing cria o pagamento (Checkout Pro fake) e o link volta para a OS
  [OK]    pagamento no Billing = 'Pendente' (apos 0 s)
  [OK]    estado da saga = 'AguardandoPagamento' (apos 1 s)
  [OK]    link de pagamento gravado na OS preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-40a4dc9a071f44c58207c4047caf34'
  [OK]    link de pagamento da OS contem 'https://fake.mercadopago.local/checkout/fake-pref-'
  [OK]    link da OS = link do Billing = 'https://fake.mercadopago.local/checkout/fake-pref-40a4dc9a071f44c58207c4047caf34c0'
  [OK]    valor do pagamento = '250'
  [OK]    orcamento aprovado pelo cliente = 'Aprovado'
  [OK]    prazo de pagamento da OS (horas a partir de agora, Saga__PrazoPagamentoHoras=48) = '48'

-- [+15s] cliente paga: o gateway Fake aprova e dispara o webhook assinado (POST /dev/pagamentos/{pref}/aprovar)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  pagamento do gateway 1794172217487000, x-request-id 22458f83-7bcf-4f6c-9650-29f7fdde4875
  [OK]    status do pagamento no Billing = 'Aprovado' (apos 0 s)
  [OK]    status da OS (PagamentoAprovado -> IniciarExecucao) = 'EmExecucao' (apos 0 s)
  [OK]    estado da saga = 'EmExecucao' (apos 1 s)
  [OK]    ordem liberada para reparo na Execucao = 'AguardandoExecucao' (apos 0 s)
  [OK]    gateway payment id aprovado gravado = '1794172217487000'
  [OK]    orcamento depois do pagamento = 'Pago'

-- [+18s] mecanico reivindica o reparo (POST /api/reparos/proximo) e finaliza
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '52b1073b-37c2-4577-88f1-2721ede94625'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    pecas do snapshot aprovado na Execucao = '1'
  [OK]    servicos do snapshot aprovado na Execucao = '1'
  [OK]    consulta da execucao com o reparo em andamento (HTTP) = '200'
  [OK]    origem da resposta = 'execucao'
  [OK]    producao.status = 'EmExecucao'
  [OK]    producao.mecanicoId = sub do token do mecanico = '00000000-0000-4000-8000-0000000000b1'
  [OK]    statusDaOrdem (OS) = 'EmExecucao'
  [OK]    finalizacao do reparo (HTTP) = '200'
  [OK]    status na Execucao = 'Finalizada'

-- [+19s] OS Finalizada: estado final, historico, estoque, e-mails
  [OK]    status da OS = 'Finalizada' (apos 1 s)
  [OK]    estado da saga = 'Concluida' (apos 0 s)
  [OK]    dataConclusao da OS preenchido ('2026-11-08T21:10:24.4853682')
  [OK]    historicoStatus na ordem EM QUE A API DEVOLVE (sem o roteiro reordenar) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    historicoStatus: primeira entrada = 'Recebida'
  [OK]    historicoStatus: ultima entrada = 'Finalizada'
  [OK]    historicoStatus: datas em ordem nao decrescente = 'sim'
  [OK]    metrica saga_iniciada subiu >= 1 (a contagem pode ficar a maior em retry de commit, ADR 0008): 1 ge 1
  [OK]    metrica saga_concluida subiu >= 1: 1 ge 1
  [OK]    estoque definitivamente baixado (nao devolvido) = '8'
  [OK]    saga sem prazo VIGENTE de aprovacao nem de pagamento = 'NULL|NULL'
  [INFO]  linhas de SagaPrazos desta OS (Processado fica 0 ate o vencimento; a saga ignora o PrazoExpirado que nao e o vigente): Pagamento:0,Aprovacao:0
  [OK]    um unico orcamento no Billing = '1'
  [OK]    um unico pagamento no Billing = '1'

-- [+22s] e-mails ao cliente no Mailpit (uma notificacao por transicao + o link de pagamento)
  [OK]    quantidade de e-mails para cliente.17941722064428@e2e.local (5 transicoes + 1 link) = '6' (apos 0 s)
  e-mail: Olá, Cliente E2E 17941722064428! A sua ordem de serviço OS-20261108-2AAB39 teve o status atualizado para: Em diagnóstico. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 17941722064428! A sua ordem de serviço OS-20261108-2AAB39 teve o status atualizado para: Aguardando aprovação do orçamento. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 17941722064428! A sua ordem de serviço OS-20261108-2AAB39 teve o status atualizado para: Aguardando pagamento (o link de pagamento chegará em seguida). Atenciosamente, Oficina Mec.
  e-mail: Olá, Cliente E2E 17941722064428! O orçamento da sua ordem de serviço OS-20261108-2AAB39 foi aprovado. Para iniciarmos o serviço, faça o pagamento pelo link abaixo: https://fake.mercadopago.local/
  e-mail: Olá, Cliente E2E 17941722064428! A sua ordem de serviço OS-20261108-2AAB39 teve o status atualizado para: Em execução. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 17941722064428! A sua ordem de serviço OS-20261108-2AAB39 teve o status atualizado para: Finalizada. Atenciosamente, Oficina Mecânica
  [OK]    e-mails do cliente contem 'Em diagnóstico'
  [OK]    e-mails do cliente contem 'Aguardando aprovação do orçamento'
  [OK]    e-mails do cliente contem 'Aguardando pagamento'
  [OK]    e-mails do cliente contem 'Em execução'
  [OK]    e-mails do cliente contem 'Finalizada'
  [OK]    e-mail do link de pagamento contem 'fake.mercadopago.local'

-- [+22s] rota sincrona GET /api/ordens-de-servico/{id}/execucao com a OS Finalizada
  [OK]    consulta da execucao (HTTP) = '200'
  [OK]    origem da resposta = 'execucao'
  [OK]    statusDaOrdem = 'Finalizada'
  [OK]    estadoDaSaga = 'Concluida'
  [OK]    estadoDaSagaAtualizadoEm preenchido ('2026-11-08T21:10:24.4789139+00:00')
  [OK]    producao.status = 'Finalizada'
  [OK]    producao.laudo.texto contem 'correia dentada'
  [OK]    producao.laudo.horasEstimadas = '2.5'
  [OK]    producao.resumo contem 'teste de rodagem'
  [OK]    producao.mecanicoId = '00000000-0000-4000-8000-0000000000b1'
  [OK]    motivoDoFallback vazio

-- [+24s] rota sincrona com a Execucao INDISPONIVEL (docker pause): fallback 200 com origem saga, sem inventar producao
  [OK]    consulta da execucao com a Execucao congelada (HTTP) = '200'
  [OK]    origem da resposta (fallback) = 'saga'
  [OK]    motivoDoFallback = 'ExecucaoIndisponivel'
  [OK]    statusDaOrdem (do proprio OS) = 'Finalizada'
  [OK]    estadoDaSaga (do proprio OS) = 'Concluida'
  [OK]    producao (nada inventado no fallback) = ''
  [OK]    tempo da resposta com a Execucao parada (s, teto de 6 s do cliente): 6 le 9
  tempo da resposta de fallback: 6 s
  [OK]    Execucao de volta: origem volta a execucao = 'execucao' (apos 1 s)

-- [+32s] nada preso: outbox das tres bases vazio e nenhuma mensagem em *_error
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error = '0'

RESULTADO|01-caminho-feliz|PASSOU|asserts=117|falhas=0|tempo=33s
```

### Cenario 02 - Recusa do orcamento

Inicio 17:10:39.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+2s] cadastros e OS ate AguardandoAprovacao
  [OK]    cadastro do cliente B (HTTP) = '201'
  [OK]    cadastro do veiculo B (HTTP) = '201'
  [OK]    cadastro do servico B (HTTP) = '201'
  [OK]    cadastro da peca B (estoque 10) (HTTP) = '201'
  [OK]    abertura da OS B (HTTP) = '201'
  [OK]    status inicial da OS B = 'Recebida'
  [OK]    valor total da OS B = '250'
  [OK]    ordem de producao da OS B na Execucao = 'Fila' (apos 1 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '239e2fee-660f-4c69-a2d1-08995462c966'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS B (DiagnosticoIniciado) = 'EmDiagnostico' (apos 1 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS B (orcamento gerado) = 'AguardandoAprovacao' (apos 0 s)
  [OK]    orcamento da OS B no Billing = 'Gerado' (apos 1 s)
  [OK]    estoque antes da decisao = '10'

-- [+6s] cliente recusa o orcamento (rota anonima, 202)
  [OK]    recusa do orcamento (HTTP) = '202'
  [OK]    corpo do 202: status ainda AguardandoAprovacao = 'AguardandoAprovacao'
  [OK]    status da OS (saga aplicou a recusa) = 'OrcamentoRecusado' (apos 1 s)
  [OK]    estado da saga (final) = 'OrcamentoRecusado' (apos 0 s)
  [OK]    metrica saga_orcamento_recusado{causa=ClienteRecusouOrcamento} subiu >= 1: 1 ge 1

-- [+7s] efeitos da recusa: orcamento cancelado, sem pagamento, estoque intacto, e-mail
  [OK]    orcamento no Billing (CancelarOrcamento) = 'Cancelado' (apos 1 s)
  [OK]    motivo da recusa propagado ate o orcamento do Billing = 'Valor acima do esperado'
  [OK]    nenhum pagamento criado no Billing = '404'
  [OK]    estoque nunca foi reservado = '10'
  [OK]    historico de status = 'Recebida,EmDiagnostico,AguardandoAprovacao,OrcamentoRecusado'
  [OK]    e-mails ao cliente (EmDiagnostico, AguardandoAprovacao, OrcamentoRecusado) = '3' (apos 0 s)
  e-mail: Olá, Cliente E2E 179417224129119! A sua ordem de serviço OS-20261108-B74A6B teve o status atualizado para: Em diagnóstico. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417224129119! A sua ordem de serviço OS-20261108-B74A6B teve o status atualizado para: Aguardando aprovação do orçamento. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417224129119! A sua ordem de serviço OS-20261108-B74A6B teve o status atualizado para: Orçamento recusado. Atenciosamente, Oficina Mecânica
  [OK]    e-mails do cliente contem 'Orçamento recusado'
  [OK]    prazo de aprovacao vigente limpo na saga = 'NULL'
  [INFO]  Execucao apos a recusa: ordem de producao em 'DiagnosticoConcluido' (a saga so cancela a producao no cancelamento do staff)

-- [+10s] decisoes depois da OS encerrada: aprovar e recusar de novo respondem 409
  [OK]    aprovar depois da recusa (HTTP) = '409'
  [OK]    recusar de novo (HTTP) = '409'
  [OK]    OS segue OrcamentoRecusado = 'OrcamentoRecusado'

-- [+10s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error = '0'

RESULTADO|02-recusa-do-orcamento|PASSOU|asserts=43|falhas=0|tempo=12s
```

### Cenario 03 - Pagamento expirado (e aprovacao tardia)

Inicio 17:10:51.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 1 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+1s] cadastros e OS ate AguardandoPagamento (estoque reservado, link gerado)
  [OK]    cadastro do cliente C (HTTP) = '201'
  [OK]    cadastro do veiculo C (HTTP) = '201'
  [OK]    cadastro do servico C (HTTP) = '201'
  [OK]    cadastro da peca C (estoque 10) (HTTP) = '201'
  [OK]    abertura da OS C (HTTP) = '201'
  [OK]    status inicial da OS C = 'Recebida'
  [OK]    valor total da OS C = '250'
  [OK]    ordem de producao da OS C na Execucao = 'Fila' (apos 0 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '34bfda2b-f161-4bde-ae22-4e1603dbc5a8'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS C (DiagnosticoIniciado) = 'EmDiagnostico' (apos 0 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS C (orcamento gerado) = 'AguardandoAprovacao' (apos 1 s)
  [OK]    orcamento da OS C no Billing = 'Gerado' (apos 0 s)
  [OK]    aprovacao do orcamento da OS C (HTTP) = '202'
  [OK]    status da OS C = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque da peca C reservado (10 - 2) = '8' (apos 0 s)
  [OK]    link de pagamento da OS C preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-29468ee340774d7f8ca21e72f02459'
  [OK]    pagamento da OS C no Billing = 'Pendente' (apos 0 s)
  [OK]    estoque reservado = '8'

-- [+7s] forca o vencimento do prazo de pagamento (Pagamentos.ExpiraEm e SagaPrazos.ExpiraEm no passado)
  [OK]    linhas de Pagamentos vencidas (Billing) = '1'
  [OK]    linhas de SagaPrazos vencidas (OS) = '1'

-- [+8s] sweeper (a cada 5 s) publica PrazoExpirado -> saga cancela o pagamento no Billing -> OS Cancelada
  [OK]    estado da saga (final) = 'Cancelada' (apos 0 s)
  do vencimento forcado ate a saga Cancelada: 0 s
  [OK]    status da OS = 'Cancelada' (apos 1 s)
  [OK]    metrica saga_prazo_expirado{tipo=Pagamento} subiu >= 1 (so o prazo vigente conta): 1 ge 1
  [OK]    metrica saga_cancelada{estado_origem=CancelandoPagamento} subiu >= 1: 1 ge 1

-- [+9s] efeitos: pagamento Expirado, orcamento cancelado, estoque devolvido, prazo marcado, e-mail
  [OK]    pagamento no Billing = 'Expirado'
  [OK]    pagamento sem aprovacao tardia (ainda) = 'false'
  [OK]    orcamento no Billing = 'Cancelado'
  [OK]    estoque devolvido (10) = '10' (apos 0 s)
  [OK]    prazo de pagamento processado pelo sweeper = '1'
  [OK]    historico de status = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,Cancelada'
  [OK]    e-mails ao cliente (5 transicoes + 1 link) = '5' (apos 0 s)
  e-mail: Olá, Cliente E2E 179417225314851! A sua ordem de serviço OS-20261108-E92724 teve o status atualizado para: Em diagnóstico. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417225314851! A sua ordem de serviço OS-20261108-E92724 teve o status atualizado para: Aguardando aprovação do orçamento. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417225314851! A sua ordem de serviço OS-20261108-E92724 teve o status atualizado para: Aguardando pagamento (o link de pagamento chegará em seguida). Atenciosamente, Oficina Mec
  e-mail: Olá, Cliente E2E 179417225314851! O orçamento da sua ordem de serviço OS-20261108-E92724 foi aprovado. Para iniciarmos o serviço, faça o pagamento pelo link abaixo: https://fake.mercadopago.local
  e-mail: Olá, Cliente E2E 179417225314851! A sua ordem de serviço OS-20261108-E92724 teve o status atualizado para: Cancelada. Atenciosamente, Oficina Mecânica
  [OK]    e-mails do cliente contem 'Cancelada'
  [OK]    a Execucao nao recebeu cancelamento (so o staff cancela a producao) = 'DiagnosticoConcluido'

-- [+11s] aprovacao TARDIA: o cliente paga depois do prazo (gateway Fake, webhook assinado)
  [OK]    webhook da aprovacao tardia (HTTP) = '200'
  [OK]    pagamento estornado automaticamente pelo Billing = 'Estornado' (apos 0 s)
  [OK]    aprovacao tardia registrada = 'true'
  [OK]    estorno concluido = 'Concluido'
  [OK]    id do estorno no gateway preenchido ('fake-refund-1')
  pagamento tardio do gateway: 1794172217487001
  [OK]    saga continua Cancelada (ignora PagamentoEstornado fora de Estornando) continuou 'Cancelada' por 6s
  [OK]    OS continua Cancelada = 'Cancelada'
  [OK]    estoque NAO devolvido de novo = '10'

-- [+18s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error = '0'

RESULTADO|03-pagamento-expirado|PASSOU|asserts=55|falhas=0|tempo=20s
```

### Cenario 04 - Execucao inviavel com estorno

Inicio 17:11:13.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+1s] cadastros e OS ate EmExecucao (pagamento aprovado pelo gateway Fake, reparo reivindicado)
  [OK]    cadastro do cliente D (HTTP) = '201'
  [OK]    cadastro do veiculo D (HTTP) = '201'
  [OK]    cadastro do servico D (HTTP) = '201'
  [OK]    cadastro da peca D (estoque 10) (HTTP) = '201'
  [OK]    abertura da OS D (HTTP) = '201'
  [OK]    status inicial da OS D = 'Recebida'
  [OK]    valor total da OS D = '250'
  [OK]    ordem de producao da OS D na Execucao = 'Fila' (apos 0 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '5b2accfc-30bc-4d1b-99a9-c686fe1aa193'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS D (DiagnosticoIniciado) = 'EmDiagnostico' (apos 0 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS D (orcamento gerado) = 'AguardandoAprovacao' (apos 0 s)
  [OK]    orcamento da OS D no Billing = 'Gerado' (apos 0 s)
  [OK]    aprovacao do orcamento da OS D (HTTP) = '202'
  [OK]    status da OS D = 'AguardandoPagamento' (apos 1 s)
  [OK]    estoque da peca D reservado (10 - 2) = '8' (apos 0 s)
  [OK]    link de pagamento da OS D preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-e7840b29cdfb4f0998ebbc3c78648a'
  [OK]    pagamento da OS D no Billing = 'Pendente' (apos 1 s)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    status da OS = 'EmExecucao' (apos 1 s)
  [OK]    ordem liberada para reparo = 'AguardandoExecucao' (apos 0 s)
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '5b2accfc-30bc-4d1b-99a9-c686fe1aa193'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    estoque reservado durante a execucao = '8'

-- [+9s] congela o Billing (docker pause) e o mecanico declara a execucao inviavel
  [OK]    inviabilizacao da execucao (HTTP) = '200'
  [OK]    status na Execucao = 'Inviavel'
  [OK]    estado da saga (ExecucaoInviavel -> EstornarPagamento enviado) = 'Estornando' (apos 0 s)

-- [+10s] com o Billing congelado: a compensacao NAO pode avancar (estoque preso, OS ainda EmExecucao)
  [OK]    estado da saga continuou 'Estornando' por 10s
  [OK]    status da OS continuou 'EmExecucao' por 1s
  [OK]    estoque ainda reservado (so volta depois do PagamentoEstornado) = '8'
  [OK]    EstornarPagamento esperando o Billing na fila billing-comandos: 1 ge 1
  billing-comandos: total=1 prontas=0

-- [+23s] descongela o Billing: estorno no gateway Fake -> PagamentoEstornado -> estoque liberado -> OS Cancelada
  [OK]    estado da saga (final) = 'Cancelada' (apos 0 s)
  [OK]    status da OS = 'Cancelada' (apos 1 s)
  [OK]    estoque liberado (10) = '10' (apos 0 s)
  [OK]    metrica saga_cancelada{estado_origem=Estornando} subiu >= 1: 1 ge 1

-- [+24s] efeitos no Billing e nos e-mails
  [OK]    pagamento no Billing = 'Estornado' (apos 1 s)
  [OK]    estorno concluido = 'Concluido'
  [OK]    id do estorno no gateway Fake contem 'fake-refund-'
  [OK]    um unico estorno para o pagamento = '1'
  [OK]    historico de status = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Cancelada'
  [OK]    e-mails do cliente contem 'Cancelada'
  [OK]    ordem de producao na Execucao = 'Inviavel' (apos 0 s)

-- [+26s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 0 s)
  [OK]    mensagens em filas *_error = '0'

RESULTADO|04-execucao-inviavel-com-estorno|PASSOU|asserts=57|falhas=0|tempo=28s
```

### Cenario 05 - Resiliencia: Billing fora (stop e kill), mensagem duplicada e ids do gateway Fake

Inicio 17:11:41.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+2s] cadastros (duas OS independentes, A e B, cada uma com a sua peca)
  [OK]    cadastro do cliente A (HTTP) = '201'
  [OK]    cadastro do veiculo A (HTTP) = '201'
  [OK]    cadastro do servico A (HTTP) = '201'
  [OK]    cadastro da peca A (estoque 10) (HTTP) = '201'
  [OK]    cadastro do cliente B (HTTP) = '201'
  [OK]    cadastro do veiculo B (HTTP) = '201'
  [OK]    cadastro do servico B (HTTP) = '201'
  [OK]    cadastro da peca B (estoque 10) (HTTP) = '201'

-- [+4s] QUEDA 1: para o Billing (docker stop) antes de abrir as OS
  [OK]    consumidores do billing-comandos = '0' (apos 9 s)
  [OK]    Billing inacessivel (curl devolve 000) = '000' (apos 2 s)

-- [+16s] OS e Execucao seguem sozinhos: abrem as OS, o mecanico diagnostica as duas
  [OK]    abertura da OS A (HTTP) = '201'
  [OK]    status inicial da OS A = 'Recebida'
  [OK]    valor total da OS A = '250'
  [OK]    ordem A na Execucao = 'Fila' (apos 1 s)
  [OK]    abertura da OS B (HTTP) = '201'
  [OK]    status inicial da OS B = 'Recebida'
  [OK]    valor total da OS B = '250'
  [OK]    ordem B na Execucao = 'Fila' (apos 1 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '2fd34f85-0651-4036-b260-a77634f4f80d'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '1b8c875d-db68-4db0-b938-0f5b9271467d'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    saga A = 'GerandoOrcamento' (apos 1 s)
  [OK]    saga B = 'GerandoOrcamento' (apos 0 s)

-- [+20s] com o Billing fora: nada avanca, as mensagens esperam no broker e a API do OS continua servindo
  [OK]    GerarOrcamento x2 esperando na billing-comandos = '2' (apos 9 s)
  [OK]    status da OS A continuou 'EmDiagnostico' por 6s
  [OK]    status da OS B continuou 'EmDiagnostico' por 1s
  [OK]    OS Service consultando peca com o Billing fora (HTTP) = '200'
  [OK]    cadastro do cliente Z (HTTP) = '201'
  [OK]    cadastro do veiculo Z (HTTP) = '201'
  [OK]    cadastro do servico Z (HTTP) = '201'
  [OK]    cadastro da peca Z (estoque 5) (HTTP) = '201'
  [OK]    Execucao respondendo com o Billing fora (HTTP) = '200'
  [OK]    OS /health/ready (so o banco derruba; o Billing nao e dependencia) = '200'

-- [+38s] entrega DUPLICADA no broker: copio (mesmo messageId) um GerarOrcamento direto na fila, como um at-least-once faria
  [OK]    messageId da mensagem duplicada preenchido ('d1aef687-3ac1-c5de-4835-d528683d8ee0')
  [OK]    billing-comandos com 3 mensagens (2 originais + 1 copia) = '3' (apos 9 s)

-- [+48s] Billing volta: a fila drena e cada OS ganha UM orcamento (a copia e descartada pelo inbox)
  [OK]    Billing /health/ready depois de subir = '200' (apos 2 s)
  [OK]    consumidor do billing-comandos reconectado = '1' (apos 8 s)
  [OK]    billing-comandos drenada = '0' (apos 0 s)
  [OK]    InboxState do Billing: ReceiveCount do messageId copiado (copia + retentativas do inbox): 5 ge 2
  [OK]    InboxState do Billing: o messageId copiado foi CONSUMIDO uma vez (linhas com Consumed preenchido) = '1'
  [OK]    status da OS A = 'AguardandoAprovacao' (apos 0 s)
  [OK]    status da OS B = 'AguardandoAprovacao' (apos 0 s)
  [OK]    orcamentos da OS A no Billing = '1'
  [OK]    orcamentos da OS B no Billing = '1'
  [OK]    orcamento A = 'Gerado'
  [OK]    orcamento B = 'Gerado'

-- [+61s] QUEDA 2: mata o Billing (docker kill) e aprova as duas OS com ele fora
  [OK]    consumidores do billing-comandos = '0' (apos 6 s)
  [OK]    aprovacao da OS A com o Billing fora (HTTP) = '202'
  [OK]    aprovacao da OS B com o Billing fora (HTTP) = '202'
  [OK]    status da OS A = 'AguardandoPagamento' (apos 1 s)
  [OK]    status da OS B = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque da peca A reservado = '8' (apos 0 s)
  [OK]    estoque da peca B reservado = '8' (apos 0 s)
  [OK]    SolicitarPagamento x2 esperando na billing-comandos = '2' (apos 9 s)
  [OK]    link de pagamento da OS A ausente continuou '' por 5s

-- [+83s] Billing volta (processo que tinha morrido): cada OS ganha UM pagamento e um link
  [OK]    Billing /health/ready depois de subir = '200' (apos 2 s)
  [OK]    consumidor do billing-comandos reconectado = '1' (apos 8 s)
  [OK]    link de pagamento da OS A preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-29f5b0e3fe69461e8c614193059331'
  [OK]    link de pagamento da OS B preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-e9156fd82bd14a43a4ae73df2b6248'
  [OK]    billing-comandos drenada = '0' (apos 0 s)
  [OK]    pagamentos da OS A no Billing = '1'
  [OK]    pagamentos da OS B no Billing = '1'
  [OK]    estoque da peca A (uma unica baixa) = '8'
  [OK]    estoque da peca B (uma unica baixa) = '8'

-- [+95s] fluxo retomado: pagamento e reparo das duas OS ate Finalizada
  ids de gateway ja gravados no BillingDb antes da 1a aprovacao pos-restart: 3 (maior: 1794172217487002)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    id do gateway apos o restart do Billing e maior que todos os ja gravados: 1794172387394000 gt 1794172217487002
  [OK]    id do gateway Fake com 16 ou mais digitos (derivado do relogio, como o do Mercado Pago): 16 ge 16
  [OK]    exatamente uma tentativa nova gravada no BillingDb (nenhuma colisao, nenhuma repeticao): 4 eq 4
  [OK]    ordem A liberada para reparo = 'AguardandoExecucao' (apos 1 s)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    ordem B liberada para reparo = 'AguardandoExecucao' (apos 1 s)
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '2fd34f85-0651-4036-b260-a77634f4f80d'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo A (HTTP) = '200'
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '1b8c875d-db68-4db0-b938-0f5b9271467d'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo B (HTTP) = '200'
  [OK]    status da OS A = 'Finalizada' (apos 0 s)
  [OK]    status da OS B = 'Finalizada' (apos 0 s)
  [OK]    saga A = 'Concluida' (apos 1 s)
  [OK]    saga B = 'Concluida' (apos 0 s)

-- [+101s] sem perder e sem duplicar: contagens finais
  [OK]    historico da OS A (cada transicao uma vez) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    orcamentos da OS A = '1'
  [OK]    pagamentos da OS A = '1'
  [OK]    tentativas de pagamento da OS A = '1'
  [OK]    pagamento da OS A = 'Aprovado'
  [OK]    orcamento da OS A = 'Pago'
  [OK]    estoque da peca A (baixa unica, definitiva) = '8'
  [OK]    e-mails do cliente A (5 transicoes + 1 link, sem repeticao) = '6' (apos 0 s)
  [OK]    e-mails de Finalizada do cliente A = '1'
  [OK]    historico da OS B (cada transicao uma vez) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    orcamentos da OS B = '1'
  [OK]    pagamentos da OS B = '1'
  [OK]    tentativas de pagamento da OS B = '1'
  [OK]    pagamento da OS B = 'Aprovado'
  [OK]    orcamento da OS B = 'Pago'
  [OK]    estoque da peca B (baixa unica, definitiva) = '8'
  [OK]    e-mails do cliente B (5 transicoes + 1 link, sem repeticao) = '6' (apos 0 s)
  [OK]    e-mails de Finalizada do cliente B = '1'

-- [+106s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error = '0'
  fila billing-comandos: mensagens=0 consumidores=1
  fila execucao-comandos: mensagens=0 consumidores=1
  fila os-saga: mensagens=0 consumidores=1

RESULTADO|05-resiliencia-billing-fora|PASSOU|asserts=121|falhas=0|tempo=107s
```

### Cenario 06 - Idempotencia

Inicio 17:13:29.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+1s] cadastros e OS ate AguardandoAprovacao
  [OK]    cadastro do cliente F (HTTP) = '201'
  [OK]    cadastro do veiculo F (HTTP) = '201'
  [OK]    cadastro do servico F (HTTP) = '201'
  [OK]    cadastro da peca F (estoque 10) (HTTP) = '201'
  [OK]    abertura da OS F (HTTP) = '201'
  [OK]    status inicial da OS F = 'Recebida'
  [OK]    valor total da OS F = '250'
  [OK]    ordem de producao da OS F na Execucao = 'Fila' (apos 0 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '93e32c58-b8e3-42f5-8943-966046698cd9'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS F (DiagnosticoIniciado) = 'EmDiagnostico' (apos 0 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS F (orcamento gerado) = 'AguardandoAprovacao' (apos 0 s)
  [OK]    orcamento da OS F no Billing = 'Gerado' (apos 0 s)

-- [+5s] decisao duplicada: duas aprovacoes disparadas ao mesmo tempo (rota anonima)
  respostas das duas aprovacoes simultaneas: 202 e 202
  [OK]    aprovacao respondeu 202 (decisao publicada para a saga)
  [OK]    aprovacao respondeu 202 (decisao publicada para a saga)
  [OK]    ao menos uma aprovacao aceita (202): 2 ge 1
  [OK]    status da OS = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque reservado UMA vez (10 - 2) continuou '8' por 6s
  [OK]    link de pagamento preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-9ea942b4670444a1b128d4edfa9b7f'
  [OK]    pagamentos criados no Billing = '1'
  [OK]    orcamentos no Billing = '1'
  [INFO]  avisos 'Decisao ... ignorada' da saga no log do OS para esta OS: 1 (so existem se a 2a aprovacao chegou depois de a saga avancar)
  [OK]    metrica saga_decisao_ignorada subiu pelo menos o numero de avisos do log desta OS: 1 ge 1

-- [+13s] reenvio da mesma decisao depois de a saga avancar: 409 e nada muda
  [OK]    aprovar de novo (OS em AguardandoPagamento) (HTTP) = '409'
  [OK]    recusar depois de aprovar (HTTP) = '409'
  [OK]    OS segue AguardandoPagamento = 'AguardandoPagamento'
  [OK]    estoque segue reservado uma vez = '8'

-- [+14s] webhook: primeira entrega (aprovacao via gateway Fake)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  pagamento do gateway 1794172387394002, x-request-id 3219cc76-8f72-4b09-8a06-0ba6ee45dc6b
  [OK]    status da OS = 'EmExecucao' (apos 1 s)
  [OK]    ordem liberada para reparo = 'AguardandoExecucao' (apos 0 s)

-- [+15s] webhook DUPLICADO: a mesma entrega (mesmo x-request-id, assinatura valida) chega de novo na rota real
  [OK]    reentrega do mesmo webhook (HTTP) = '200'
  [OK]    registros do x-request-id no Billing (deduplicado) = '1'
  [OK]    tentativas de pagamento (uma so, aprovada) = '1'

-- [+16s] mesmo pagamento notificado de novo com OUTRO x-request-id (reentrega do Mercado Pago)
  [OK]    novo x-request-id para o mesmo pagamento (HTTP) = '200'
  [OK]    tentativas de pagamento continuam 1 = '1'
  [OK]    pagamento continua Aprovado (uma aprovacao) = 'Aprovado'
  [OK]    OS continua EmExecucao continuou 'EmExecucao' por 4s
  [OK]    uma unica ordem esperando reparo na Execucao = '1'

-- [+21s] respostas documentadas da rota de webhook
  [OK]    assinatura invalida (HTTP) = '401'
  [OK]    sem x-signature (HTTP) = '401'
  [OK]    assinatura valida mas fora da tolerancia de tempo (15 min) (HTTP) = '401'
  [OK]    pagamento que o gateway ainda nao conhece (assinatura valida) (HTTP) = '503'
  [OK]    cabecalho Retry-After do 503 contem 'retry-after'
  [OK]    topico diferente de payment (confirmado e ignorado) (HTTP) = '200'

-- [+22s] reparo e fim: reentrega tardia do webhook nao reabre nada
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '93e32c58-b8e3-42f5-8943-966046698cd9'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo (HTTP) = '200'
  [OK]    status da OS = 'Finalizada' (apos 0 s)
  [OK]    estado da saga = 'Concluida' (apos 0 s)
  [OK]    segunda finalizacao do reparo recusada com 4xx (400): 
  [OK]    webhook reentregue depois da OS finalizada (HTTP) = '200'
  [OK]    OS continua Finalizada continuou 'Finalizada' por 4s
  [OK]    historico de status (cada transicao uma vez) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    estoque final (baixa unica) = '8'
  [OK]    e-mails do cliente (5 transicoes + 1 link) = '6' (apos 0 s)
  [OK]    e-mails de 'Em execucao' (uma notificacao so) = '1'

-- [+29s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 0 s)
  [OK]    mensagens em filas *_error = '0'

RESULTADO|06-idempotencia|PASSOU|asserts=70|falhas=0|tempo=31s
```

### Cenario 07 - Resiliencia: RabbitMQ fora (+ fallback da rota /execucao)

Inicio 17:14:01.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+2s] cadastros
  [OK]    cadastro do cliente G (HTTP) = '201'
  [OK]    cadastro do veiculo G (HTTP) = '201'
  [OK]    cadastro do servico G (HTTP) = '201'
  [OK]    cadastro da peca G (estoque 10) (HTTP) = '201'

-- [+3s] QUEDA 1: para o RabbitMQ (docker stop) e abre a OS
  [OK]    RabbitMQ fora (management responde 000) = '000' (apos 2 s)
  [OK]    abertura da OS G (HTTP) = '201'
  [OK]    status inicial da OS G = 'Recebida'
  [OK]    valor total da OS G = '250'
  [OK]    OS /health/ready com o broker fora: 200 e corpo com Degraded = '200 degradado' (apos 3 s)
  [OK]    Billing /health/ready com o broker fora: 200 e corpo com Degraded = '200 degradado' (apos 2 s)
  [OK]    Execucao /health/ready com o broker fora: 200 e corpo com Degraded = '200 degradado' (apos 3 s)
  prontidao do OS: {"status":"Degraded","checks":{"sqlserver":{"status":"Healthy","descricao":"Conexão com o banco de dados estabelecida."},"rabbitmq":{"status":"Degraded","descricao":"Não foi possível conectar ao RabbitMQ; as mensagens ficam no outbox até o broker voltar."}
  [OK]    OrdemDeServicoAberta retida no outbox do OS (linhas de OutboxMessage) = '1' (apos 1 s)
  [OK]    ordem na Execucao (ainda inexistente) continuou '404' por 6s
  [OK]    status da OS (aceita e gravada) = 'Recebida'
  [OK]    rota sincrona /execucao com a OS ainda nao enfileirada (HTTP) = '200'
  [OK]    origem da resposta (fallback) = 'saga'
  [OK]    motivoDoFallback (Execucao respondeu 404) = 'ProducaoNaoEnfileirada'
  [OK]    statusDaOrdem = 'Recebida'
  [OK]    estadoDaSaga (a saga ainda nao existe) vazio
  [OK]    producao (nada inventado no fallback) = ''

-- [+26s] RabbitMQ volta: os servicos reconectam e a ordem chega na Execucao
  [OK]    management do RabbitMQ no ar = '200' (apos 5 s)
  [OK]    consumidor de os-saga reconectado = '1' (apos 20 s)
  [OK]    consumidor de billing-comandos reconectado = '1' (apos 0 s)
  [OK]    consumidor de execucao-comandos reconectado = '1' (apos 1 s)
  tempo ate os tres servicos reconectarem: 26 s
  [OK]    ordem de producao na Execucao = 'Fila' (apos 0 s)
  [OK]    estado da saga = 'AguardandoDiagnostico' (apos 0 s)

-- [+52s] fluxo normal ate AguardandoAprovacao
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = 'eddfccb6-436f-450e-8c18-52584d9cb946'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS = 'EmDiagnostico' (apos 0 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS = 'AguardandoAprovacao' (apos 0 s)

-- [+54s] QUEDA 2: para o RabbitMQ de novo e o cliente aprova o orcamento com o broker fora
  [OK]    RabbitMQ fora (management responde 000) = '000' (apos 2 s)
  [OK]    aprovacao aceita com o broker fora (decisao vai para o outbox) (HTTP) = '202'
  [OK]    status da OS (decisao ainda nao chegou na saga) continuou 'AguardandoAprovacao' por 6s
  [OK]    decisao retida no outbox do OS: 1 ge 1
  [OK]    estoque ainda intacto = '10'

-- [+64s] RabbitMQ volta: a decisao e entregue e o fluxo segue ate Finalizada
  [OK]    management do RabbitMQ no ar = '200' (apos 4 s)
  [OK]    consumidor de os-saga reconectado = '1' (apos 5 s)
  [OK]    consumidor de billing-comandos reconectado = '1' (apos 4 s)
  [OK]    consumidor de execucao-comandos reconectado = '1' (apos 10 s)
  tempo ate os tres servicos reconectarem: 25 s
  [OK]    status da OS = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque reservado = '8' (apos 0 s)
  [OK]    link de pagamento preenchido (apos 1 s): 'https://fake.mercadopago.local/checkout/fake-pref-2c0d8807eca0476296bd40a0a4712f'
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    ordem liberada para reparo = 'AguardandoExecucao' (apos 1 s)
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = 'eddfccb6-436f-450e-8c18-52584d9cb946'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo (HTTP) = '200'
  [OK]    status da OS = 'Finalizada' (apos 1 s)
  [OK]    estado da saga = 'Concluida' (apos 0 s)
  [OK]    historico de status (cada transicao uma vez) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    orcamentos e pagamentos no Billing = '1|1'
  [OK]    estoque final (baixa unica) = '8'
  [OK]    e-mails do cliente (5 transicoes + 1 link, sem repeticao) = '6' (apos 0 s)

-- [+94s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error = '0'

RESULTADO|07-resiliencia-broker-fora|PASSOU|asserts=68|falhas=0|tempo=96s
```

### Verificacoes finais (+ metricas da saga)

Inicio 17:15:38.

```text
-- [+1s] 1. filas do RabbitMQ (API de management): nenhuma *_error com mensagem
  fila billing-comandos: mensagens=0 consumidores=1
  fila execucao-comandos: mensagens=0 consumidores=1
  fila os-saga: mensagens=0 consumidores=1
  [OK]    soma das mensagens em filas *_error = '0'
  [OK]    filas de trabalho (os-saga, billing-comandos, execucao-comandos) drenadas = '0'

-- [+1s] 2. logs dos servicos: nenhum erro/Fault inesperado
  os: 19954 linhas de log; linhas ERR/FTL: 3
  billing: 609 linhas de log; linhas ERR/FTL: 1
  execucao: 550 linhas de log; linhas ERR/FTL: 1
  ERR esperados do cenario 01: OS OnTimeout do Polly=3, Execucao 'Erro não tratado: The operation was canceled' (DEF-4)=1
  [OK]    OS: linhas ERR/FTL fora do OnTimeout do cliente da Execucao = '0'
  [OK]    Execucao: linhas ERR/FTL fora do cancelamento por cliente que desistiu (DEF-4) = '0'
  [OK]    OS: 'Falha técnica no comando' (Fault<T> real da saga) = '0'
  [OK]    OS/Billing/Execucao: R-FAULT / Fault<> do MassTransit = '0'
  [OK]    Billing: ERR que nao seja o 'saving changes' do EF = '0'
  Billing: violacoes de indice unico: id do gateway Fake (IX_TentativasPagamento_GatewayPaymentId)=0, copia duplicada no inbox (AK_InboxState_MessageId_ConsumerId)=3
  [OK]    Billing: colisoes de id do gateway Fake apos restart (DEF-1 corrigido) = '0'
  [OK]    Billing: violacoes de unicidade fora do inbox da copia duplicada = '0'
  [OK]    Billing: 409 por conflito de persistencia no log = '0'
  [OK]    OS: aviso 'Overriding HTTP_PORTS' no log (DEF-3 corrigido) = '0'
  [OK]    os: nenhum aviso (WRN) fora da lista de esperados
  [OK]    billing: nenhum aviso (WRN) fora da lista de esperados
  [OK]    execucao: nenhum aviso (WRN) fora da lista de esperados

-- [+4s] 3. ninguem preso: sagas, producao, pagamentos
  sagas por estado: Cancelada=2, Concluida=5, OrcamentoRecusado=1
  [OK]    sagas fora de um estado final (Concluida, Cancelada, OrcamentoRecusado) = '0'
  ordens de producao por status: [{"_id":"DiagnosticoConcluido","n":2},{"_id":"Inviavel","n":1},{"_id":"Finalizada","n":5}]
  [OK]    ordens de producao esperando diagnostico/reparo ou em andamento = '0'
  pagamentos por status: Aprovado=5, Estornado=2
  [OK]    pagamentos em Pendente/Estornando/EstornoFalhou = '0'
  [OK]    estornos Pendente/Falhou = '0'
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)

-- [+9s] 4. containers: todos de pe, sem restart automatico nem OOM
  [OK]    container sqlserver = 'running restarts=0 oom=false'
  [OK]    container mongo = 'running restarts=0 oom=false'
  [OK]    container rabbitmq = 'running restarts=0 oom=false'
  [OK]    container mailpit = 'running restarts=0 oom=false'
  [OK]    container jwks = 'running restarts=0 oom=false'
  [OK]    container billing = 'running restarts=0 oom=false'
  [OK]    container execucao = 'running restarts=0 oom=false'
  [OK]    container os = 'running restarts=0 oom=false'

-- [+11s] 5. estoque conferido (cada linha do ledger e uma peca com o saldo esperado)
  [OK]    estoque da peca (cenario 01: OS finalizada, estoque baixado) = '8'
  [OK]    estoque da peca (cenario 02: orcamento recusado, estoque intacto) = '10'
  [OK]    estoque da peca (cenario 03: pagamento expirado, estoque devolvido) = '10'
  [OK]    estoque da peca (cenario 04: execucao inviavel, estoque liberado apos o estorno) = '10'
  [OK]    estoque da peca (cenario 05: OS A finalizada apos quedas do Billing) = '8'
  [OK]    estoque da peca (cenario 05: OS B finalizada apos quedas do Billing) = '8'
  [OK]    estoque da peca (cenario 05: massa extra criada com o Billing fora (sem OS)) = '5'
  [OK]    estoque da peca (cenario 06: OS finalizada, estoque baixado uma vez) = '8'
  [OK]    estoque da peca (cenario 07: OS finalizada apos duas quedas do RabbitMQ) = '8'

-- [+14s] 6. metricas da saga no /metrics do OS (Meter OficinaMecanica.Saga, ADR 0008) coerentes com o banco
  banco: sagas=8 concluidas=5 canceladas=2 orcamento-recusado=1
  metricas agora: saga_iniciada=8 saga_concluida=5 saga_cancelada=2 saga_orcamento_recusado=1 (as duas ultimas podem ter expirado do /metrics)
  [OK]    saga_iniciada >= sagas criadas (banco): 8 ge 8
  [OK]    saga_concluida >= sagas Concluida (banco): 5 ge 5
  [OK]    saga_duracao_estado_segundos_count{estado=AguardandoDiagnostico} >= sagas criadas (toda saga sai desse estado): 8 ge 8
  [OK]    saga_compensacao_falhou (nenhum Fault de compensacao) = '0'
  [OK]    saga_estorno_pendente_manual (nenhum estorno parado) = '0'

-- [+16s] 7. resumo dos dados gerados pela execucao
  ordens de servico: 8  | orcamentos: 8  | pagamentos: 7  | estornos: 2
  e-mails no Mailpit: 44
  inbox (mensagens consumidas com deduplicacao): OS=64 Billing=19 Execucao=14

RESULTADO|99-verificacoes-finais|PASSOU|asserts=43|falhas=0|tempo=19s
```


## Estado das filas, metricas e logs no fim (coletado antes de derrubar)

```text
### filas RabbitMQ no fim (nome | mensagens | consumidores)
billing-comandos | msgs=0 | consumidores=1
execucao-comandos | msgs=0 | consumidores=1
os-saga | msgs=0 | consumidores=1
filas *_error existentes: 0

### e-mails no Mailpit: 44


### metricas da saga no /metrics do OS (contadores e contagem do histograma; sem buckets)
oficinamecanica_saga_saga_iniciada 8
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="AguardandoDiagnostico"} 8
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="EmDiagnostico"} 8
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="GerandoOrcamento"} 8
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="AguardandoAprovacao"} 8
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="AguardandoPagamento"} 7
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="EmExecucao"} 6
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="CancelandoPagamento"} 1
oficinamecanica_saga_saga_duracao_estado_segundos_count{estado="Estornando"} 1
oficinamecanica_saga_saga_concluida 5
oficinamecanica_saga_saga_orcamento_recusado{causa="ClienteRecusouOrcamento"} 1
oficinamecanica_saga_saga_cancelada{estado_origem="CancelandoPagamento"} 1
oficinamecanica_saga_saga_cancelada{estado_origem="Estornando"} 1
oficinamecanica_saga_saga_decisao_ignorada{decisao="ClienteAprovouOrcamento",estado="AguardandoPagamento"} 1

### contagens de log
OS 'Overriding HTTP_PORTS': 0
Billing colisoes de id do Fake (IX_TentativasPagamento_GatewayPaymentId): 0
Billing 'Conflito de concorrência na persistência': 0
Billing violacoes do inbox (AK_InboxState_MessageId_ConsumerId), da copia duplicada do cenario 05: 3

### trechos de log
-- OS: decisao duplicada ignorada pela saga (WRN)
[21:13:35 WRN] Decisão ClienteAprovouOrcamento da ordem de serviço 93e32c58-b8e3-42f5-8943-966046698cd9 ignorada: a saga está em AguardandoPagamento e a decisão não tem mais efeito, mensagem 412d64e2-a7d4-4a57-b2f2-b9a092d531fa
-- OS: consulta de execucao com a Execucao indisponivel (WRN, cenario 01)
[21:10:35 WRN] Consulta de execução da ordem de serviço 52b1073b-37c2-4577-88f1-2721ede94625: usando o último estado conhecido da saga (ExecucaoIndisponivel, timeout).
-- OS: queda do RabbitMQ (cenario 07)
[21:10:01 INF] Bus started: rabbitmq://rabbitmq/
[21:14:10 WRN] Connection Failed: rabbitmq://rabbitmq/
[21:14:20 WRN] Connection Failed: rabbitmq://rabbitmq/
[21:14:29 WRN] Connection Failed: rabbitmq://rabbitmq/
-- Billing: copia duplicada concorrente (cenario 05; ERR do EF seguido de R-RETRY)
 ---> Microsoft.Data.SqlClient.SqlException (0x80131904): Violation of UNIQUE KEY constraint 'AK_InboxState_MessageId_ConsumerId'. Cannot insert duplicate key in object 'dbo.InboxState'. The duplicate key value is (d1aef687-3ac1-c5de-4835-d528683d8ee0, 4f7d84c7-af6e-a308-cd43-f95b3ffeb8a2).
[21:12:32 WRN] R-RETRY rabbitmq://rabbitmq/billing-comandos d1aef687-3ac1-c5de-4835-d528683d8ee0 MassTransit.RetryPolicies.RetryConsumeContext<OficinaMecanica.Contracts.Commands.GerarOrcamento>
-- Billing: aprovacao tardia (cenario 03)
[21:11:03 WRN] Pagamento 29468ee3-4077-4d7f-8ca2-1e72f024590f aprovado no gateway depois de Expirado; estorno automático solicitado.
-- Execucao: 2a finalizacao do reparo (cenario 06)
[21:13:54 WRN] Erro de domínio: Não é possível finalizar o reparo com a ordem no status 'Finalizada' (permitido: 'EmExecucao').
```

## Defeitos

### Os tres defeitos da primeira rodada: confirmados como corrigidos

**DEF-1 (Billing, ids do gateway Fake colidiam apos restart): corrigido.**
Antes (primeira rodada, cenario 05): `[INFO] tentativa 1..3 da simulacao: 409` e log do Billing com
`Cannot insert duplicate key row ... 'IX_TentativasPagamento_GatewayPaymentId' ... (100000000001)`.
Agora: no cenario 05, a primeira aprovacao depois dos dois restarts (stop e kill) responde 200 sem repeticao
(`simulacao de aprovacao no gateway Fake (primeira tentativa) = 200`, com o assert de id maior que os ja gravados), o roteiro nao tem mais retry, e as
contagens de log do Billing fecham em 0 colisoes e 0 `Conflito de concorrência na persistência`. Os ids gravados agora derivam do relogio
(16 digitos, crescentes entre os restarts):

```text
### DEF-1: ids do gateway Fake gravados no BillingDb (derivados do relogio, unix_ms x 1000, crescentes entre restarts)
1794172217487000|approved
1794172217487001|approved
1794172217487002|approved
1794172387394000|approved
1794172387394001|approved
1794172387394002|approved
1794172387394003|approved
```

**DEF-2 (OS, `historicoStatus` fora de ordem): corrigido.**
Antes (primeira rodada, evidencia `2026-11-08-execucao.md`): a API devolvia `AguardandoPagamento, Recebida, EmDiagnostico, AguardandoAprovacao, Finalizada, EmExecucao`. Agora a
resposta crua da API, sem o roteiro reordenar, para a OS do cenario 01 (e os asserts de historico dos cenarios 01 a 07 leem a ordem da API):

```text
### DEF-2: historicoStatus NA ORDEM EM QUE A API DEVOLVE (OS do cenario 01, sem ordenar)
2026-11-08T21:10:08.9853243 Recebida
2026-11-08T21:10:12.6009408 EmDiagnostico
2026-11-08T21:10:14.6156256 AguardandoAprovacao
2026-11-08T21:10:17.406933 AguardandoPagamento
2026-11-08T21:10:20.876472 EmExecucao
2026-11-08T21:10:24.4853979 Finalizada
datas em ordem nao decrescente: sim
```

**DEF-3 (OS, aviso `Overriding HTTP_PORTS` a cada boot): corrigido.**
Antes: `[WRN] Overriding HTTP_PORTS '8080' and HTTPS_PORTS ''...` em todo start do OS. Agora o log do OS tem 0 ocorrencias (secao "contagens de log"
acima e o assert do 99), `ASPNETCORE_URLS` nao esta mais no ambiente da imagem e a porta continua 8080 (`ASPNETCORE_HTTP_PORTS=8080`, da imagem base),
com as tres APIs respondendo nas portas 18080/18081/18082 do host.

### Defeito novo

#### DEF-4 - Execucao: cancelamento pelo cliente (OS desiste por timeout) vira `ERR "Erro nao tratado"` e tenta responder 500

- **Sintoma**: quando o OS estoura o timeout da consulta sincrona e fecha a conexao, a Execucao registra
  `[ERR] Erro não tratado: The operation was canceled.` e segue o ramo de erro interno (500) para uma conexao que ja nao existe. Aconteceu no
  cenario 01 (fallback `ExecucaoIndisponivel`) e e provocado toda vez que um chamador desiste de uma requisicao em andamento.
- **Evidencia**: `ERR esperados do cenario 01: OS OnTimeout do Polly=3, Execucao 'Erro não tratado: The operation was canceled' (DEF-4)=1` na
  secao 2 do 99 (saida acima), e o trecho do log da Execucao nas "contagens de log"; o resto do log da Execucao tem 0 ERR.
- **Reproducao**: `bash e2e/subir.sh`; `bash e2e/executar-cenarios.sh --so 01`, ou na mao:
  `docker pause $(docker compose -p oficina-e2e -f e2e/docker-compose.e2e.yml ps -q execucao)`;
  `curl -H "Authorization: Bearer $(bash e2e/emitir-token.sh admin)" http://127.0.0.1:18080/api/ordens-de-servico/<id>/execucao` (volta 200 com `origem: saga` apos ~6-7 s);
  `docker unpause ...` e `docker logs` da Execucao: `ERR Erro não tratado: The operation was canceled.`
- **Causa provavel**: `OficinaMecanica.Execucao.API/Middleware/ExceptionHandlingMiddleware.cs:90-94` (ramo `default` do `switch`): nao ha caso para
  `OperationCanceledException` quando `HttpContext.RequestAborted` foi acionado; cai em `LogError("Erro não tratado: ...")` e escreve 500.
- **Impacto**: ruido de nivel ERR (alerta por nivel teria falso positivo) a cada consulta do OS que cai no fallback, que e um caminho de degradacao
  previsto e frequente em incidente da Execucao. Sem perda de dado. **Sugestao**: `catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)`
  sem log de erro (ou em Debug) e sem escrever resposta.

### Observacoes (nao sao defeitos; vale uma decisao)

- **OBS-1 (continua)** - datas do OS sem offset: `dataAbertura`, `dataAlteracao` e `dataConclusao` saem como `DateTime` sem `Z`/offset (ver o historico acima, ex.
  `2026-11-08T20:53:33.9189711`, UTC pelo relogio do container), enquanto o Billing e a rota `/execucao` devolvem `DateTimeOffset` (`+00:00`).
- **OBS-2 (continua)** - linhas obsoletas de `SagaPrazos` ficam `Processado=0` ate vencer (cenario 01: `Aprovacao:0,Pagamento:0` com a OS ja `Finalizada`).
- **OBS-3 (continua)** - orcamento recusado ou pagamento expirado deixam a ordem da Execucao em `DiagnosticoConcluido` para sempre (2 ordens no fim).
- **OBS-4 (continua)** - copia duplicada concorrente no Billing gera ERR do EF (`AK_InboxState_MessageId_ConsumerId`) antes do retry descartar a copia.
- **OBS-5 (continua)** - linha `Usage Telemetry` do MassTransit nos logs; nao verifiquei se ha envio para fora.
- **OBS-6 (resolvida)** - a rota sincrona `GET /api/ordens-de-servico/{id}/execucao` existe agora e foi exercitada (cenarios 01 e 07).
- **OBS-8 (nova)** - o OS registra **3 linhas ERR** (`OnTimeout` do Polly, uma por tentativa de 2 s) e 6 avisos a cada `/execucao` que cai em
  `ExecucaoIndisponivel`: o fallback funciona (200 em ~6-7 s, dentro do teto de 6 s do cliente mais o jitter do retry), mas a telemetria padrao do Polly e ruidosa.
- **OBS-9 (nova)** - o adapter do prometheus-net (`MeterAdapter`, v8.2.1) so observa os Meters a partir da primeira leitura do `/metrics` e **remove cada serie cerca de 5 minutos
  depois da ultima atualizacao** (`MetricsExpireAfter`; o instante exato depende do ciclo de limpeza): sem uma leitura antes do primeiro fluxo, os contadores da saga nao aparecem (ou ficam a menor), e uma
  serie rara (`saga_orcamento_recusado`, `saga_cancelada{estado_origem=...}`) some do `/metrics` apos 5 min e reaparece do zero no proximo incremento. Um Prometheus real raspa continuamente, entao o efeito e so o das primeiras dezenas de segundos de
  cada pod; o CLAUDE.md do Billing ja registra o comportamento para o Meter dele, vale o mesmo aviso no do OS. Os gauges da saga so existem com `Otel__Endpoint`.

## Limitacoes desta execucao

- O Seguranca nao foi incluido (JWKS stub + token emitido por script): prova a **validacao** de JWT nos tres servicos, nao a emissao.
- Gateway Fake no lugar do Mercado Pago; servicos em `Development` (exigencia do Fake), migrations aplicadas pelo proprio servico na partida, nao pelo
  `dotnet ef database update` do CI; Mongo sem autenticacao; um no de cada dependencia e uma replica por servico (o sweeper multi-replica nao foi exercitado).
- Prazos de 48 h/72 h nao esperam o relogio: o vencimento foi forcado por `UPDATE` (cenario 03).
- Os gauges da saga (`saga_por_estado`, `saga_compensacao_presa`) nao foram conferidos: so existem com `Otel__Endpoint` configurado.
- O build usa o working tree de cada repositorio, com alteracoes ainda nao commitadas (listadas em "Estado testado"); a evidencia vale para esse estado exato.

## Docker antes e depois

```text
Comparacao feita com listas salvas ANTES de qualquer comando desta rodada (docker ps -a, volume ls, network ls, image ls)
e DEPOIS de 'bash e2e/derrubar.sh --imagens' (docker compose -p oficina-e2e down -v + remocao das 3 imagens oficina-e2e/*:local):

containers (docker ps -a): 16 antes, 16 depois; ids, nomes, imagens e status identicos
  (unica diferenca: o 'Exited (0) 11 days ago' do otel-collector virou '12 days ago', que avanca com o relogio)
volumes: 129 antes, 129 depois; lista identica (nenhum volume do usuario ou de outro agente foi tocado)
redes: 8 antes, 8 depois; identicas
imagens: identicas, exceto scan-os:local (6deac691047f -> bb534b48708f): NAO e minha; e a imagem do scan do Trivy de outro agente, reconstruida na janela.

Restos do projeto oficina-e2e: containers=0 volumes=0 redes=0 imagens oficina-e2e/*=0

docker ps -a agora (containers antigos do usuario continuam como estavam):
buildx_buildkit_desktop-linux  Up 7 hours
otel-collector  Exited (0) 12 days ago
oficinamecanica-seguranca  Exited (137) 2 months ago
oficinamecanica-seguranca-azurite  Exited (137) 2 months ago
oficinamecanica-seguranca-sqlserver  Exited (137) 2 months ago
oficinamecanica-api  Exited (0) 2 months ago
oficinamecanica-alloy  Exited (0) 2 months ago
oficinamecanica-grafana  Exited (0) 2 months ago
oficinamecanica-loki  Exited (0) 2 months ago
oficinamecanica-prometheus  Exited (0) 2 months ago
oficinamecanica-sonar-setup  Exited (0) 2 months ago
oficinamecanica-sonar  Exited (137) 2 months ago
oficinamecanica-sonar-db  Exited (0) 2 months ago
oficinamecanica-jaeger  Exited (0) 2 months ago
oficinamecanica-mailpit  Exited (0) 2 months ago
oficinamecanica-sqlserver  Exited (137) 2 months ago
```
