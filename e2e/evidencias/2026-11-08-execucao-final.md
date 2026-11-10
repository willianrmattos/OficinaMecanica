# Evidencia do e2e final - 2026-11-08 (terceira rodada)

Terceira rodada do teste ponta a ponta local do OS Service, do Billing e da Execucao, a partir do **checkout principal**
(`E:\FIAP\Pos\OficinaMecanica\e2e`), depois de uma avaliacao independente do `e2e/` (0 bloqueante, 0 alto, 5 medio, varios baixo) e da
correcao do tratamento de requisicao cancelada pelo cliente nos tres servicos (working trees ainda nao commitadas). As duas evidencias
anteriores (`2026-11-08-execucao.md` e `2026-11-08-execucao-integrada.md`) **nao foram alteradas**: sao o registro historico.
Tudo abaixo e **saida real** de `bash e2e/executar-cenarios.sh`, executado contra um ambiente recem-criado
(`derrubar.sh` + `subir.sh --regerar`). A saida bruta esta em `e2e/evidencias/2026-11-08-execucao-final-saida.txt`.

## Resultado

```text
01-caminho-feliz                         PASSOU   asserts=117|falhas=0|tempo=35s
02-recusa-do-orcamento                   PASSOU   asserts=43|falhas=0|tempo=12s
03-pagamento-expirado                    PASSOU   asserts=55|falhas=0|tempo=28s
04-execucao-inviavel-com-estorno         PASSOU   asserts=57|falhas=0|tempo=27s
05-resiliencia-billing-fora              PASSOU   asserts=121|falhas=0|tempo=95s
06-idempotencia                          PASSOU   asserts=71|falhas=0|tempo=32s
07-resiliencia-broker-fora               PASSOU   asserts=68|falhas=0|tempo=76s
99-verificacoes-finais                   PASSOU   asserts=54|falhas=0|tempo=18s
log completo: /e/FIAP/Pos/OficinaMecanica/e2e/.gerado/logs/execucao-20261108-174951.log
```

**8 de 8 passaram** (586 asserts, 0 falhas; a rodada anterior teve 574). O ERR `Erro não tratado: The operation was canceled.` que a
Execucao registrava quando o OS desistia de uma consulta **desapareceu do log**: a Execucao termina a execucao com 0 linhas ERR e com o Information
`Requisição cancelada pelo cliente antes da resposta` no lugar (ver "Defeitos"). Nenhum defeito novo nos servicos.

## Estado testado

```text
== OficinaMecanica: HEAD 938487a (feature/microsservicos) test: adiciona teste ponta a ponta local dos tres microsservicos
   alteracoes no working tree (git status --short, sem e2e/):
      M .dockerignore
      M CLAUDE.md
      M Dockerfile
      M README.md
      M docs/adr/0003-saga-orquestrada-no-os-service.md
      M docs/adr/0007-outbox-inbox-transacional-e-publicacao-antes-do-commit.md
      M docs/adr/0008-sweeper-de-prazos-e-estados-da-saga.md
      M docs/adr/0009-ci-cd-independente-com-gate-de-qualidade.md
      M docs/rfc/0005-decomposicao-em-microsservicos-com-saga-orquestrada.md
      M docs/saga-maquina-de-estados.puml
      M src/OficinaMecanica.API/Middleware/ExceptionHandlingMiddleware.cs
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
     ?? tests/OficinaMecanica.Integration.Tests/Middleware/
     ?? tests/OficinaMecanica.Integration.Tests/Repositories/TempoMedioPorStatusComMudancasSimultaneasTests.cs
     ?? tests/OficinaMecanica.Tests.Common/Builders/HistoricoEmbaralhado.cs
   arquivos de codigo/teste/Dockerfile alterados: Dockerfile src/OficinaMecanica.API/Middleware/ExceptionHandlingMiddleware.cs src/OficinaMecanica.Domain/Entities/OrdemDeServico.cs src/OficinaMecanica.Infrastructure/OficinaMecanica.Infrastructure.csproj src/OficinaMecanica.Infrastructure/Repositories/OrdemDeServicoRepository.cs tests/OficinaMecanica.Application.Tests/DTOs/OrdemDeServicoMapperTests.cs tests/OficinaMecanica.Integration.Tests/Controllers/ListarOrdensDeServicoTests.cs tests/OficinaMecanica.Integration.Tests/Controllers/OrdensDeServicoControllerTests.cs 
   sha256 do diff de src+tests+Dockerfile: 8992a1fde52e9a77
== OficinaMecanica.Billing: HEAD 1351440 (feature/microsservicos) test: protege o valor do gate de cobertura no workflow
   alteracoes no working tree (git status --short, sem e2e/):
      M CLAUDE.md
      M README.md
      M docker-compose.yml
      M docs/adr/0003-ci-cd-independente-com-gate-de-qualidade.md
      M src/OficinaMecanica.Billing.API/Middleware/ExceptionHandlingMiddleware.cs
      M src/OficinaMecanica.Billing.Infrastructure/Gateways/FakePagamentoGateway.cs
      M tests/OficinaMecanica.Billing.Integration.Tests/Api/ExceptionHandlingMiddlewareTests.cs
      M tests/OficinaMecanica.Billing.Integration.Tests/Gateways/FakePagamentoGatewayTests.cs
      M tests/OficinaMecanica.Billing.Integration.Tests/Infra/AmbienteInfra.cs
      M tests/OficinaMecanica.Billing.Integration.Tests/Infra/WebhookFimAFimTests.cs
     ?? docs/cobertura.md
   arquivos de codigo/teste/Dockerfile alterados: src/OficinaMecanica.Billing.API/Middleware/ExceptionHandlingMiddleware.cs src/OficinaMecanica.Billing.Infrastructure/Gateways/FakePagamentoGateway.cs tests/OficinaMecanica.Billing.Integration.Tests/Api/ExceptionHandlingMiddlewareTests.cs tests/OficinaMecanica.Billing.Integration.Tests/Gateways/FakePagamentoGatewayTests.cs tests/OficinaMecanica.Billing.Integration.Tests/Infra/AmbienteInfra.cs tests/OficinaMecanica.Billing.Integration.Tests/Infra/WebhookFimAFimTests.cs 
   sha256 do diff de src+tests+Dockerfile: 9544fd2caff18f8f
== OficinaMecanica.Execucao: HEAD 4883e84 (feature/microsservicos) test: protege o valor do gate de cobertura no workflow
   alteracoes no working tree (git status --short, sem e2e/):
      M CLAUDE.md
      M README.md
      M docs/adr/0002-outbox-mongodb-do-masstransit-e-unit-of-work.md
      M docs/adr/0003-ci-cd-independente-com-gate-de-qualidade.md
      M src/OficinaMecanica.Execucao.API/Middleware/ExceptionHandlingMiddleware.cs
      M tests/OficinaMecanica.Execucao.Integration.Tests/Api/ExceptionHandlingMiddlewareTests.cs
     ?? docs/cobertura.md
   arquivos de codigo/teste/Dockerfile alterados: src/OficinaMecanica.Execucao.API/Middleware/ExceptionHandlingMiddleware.cs tests/OficinaMecanica.Execucao.Integration.Tests/Api/ExceptionHandlingMiddlewareTests.cs 
   sha256 do diff de src+tests+Dockerfile: 0376f9ad1c0aee48
```

Leitura: o build usa o **working tree** de cada repositorio. O OS esta em `938487a` (commit do e2e) com as correcoes de historico em ordem, do Dockerfile e
do middleware de cancelamento, o pin de `System.Security.Cryptography.Pkcs` e o `e2e/` no `.dockerignore` (este ultimo e do e2e); o Billing em `1351440` com o
gateway Fake de ids pelo relogio e o middleware; a Execucao em `4883e84` com o middleware. O restante das alteracoes listadas e documentacao e testes de outros
agentes, que o `.dockerignore` de cada repositorio mantem fora da imagem (`docs/`, `*.md`, `tests/`).

```text
== e2e oficina-e2e | 2026-11-08 17:49:51 ==
  OS: 938487a (feature/microsservicos), alteracoes nao commitadas fora de e2e/: 24
  Billing: 1351440 (feature/microsservicos), alteracoes nao commitadas fora de e2e/: 11
  Execucao: 4883e84 (feature/microsservicos), alteracoes nao commitadas fora de e2e/: 7
SERVICE     IMAGE                                                                                                                STATUS
billing     oficina-e2e/billing:local                                                                                            Up 11 seconds (healthy)
execucao    oficina-e2e/execucao:local                                                                                           Up 17 seconds (healthy)
jwks        python:3.12.16-alpine                                                                                                Up 23 seconds (healthy)
mailpit     axllent/mailpit:v1.31.0                                                                                              Up 23 seconds (healthy)
mongo       mongo:7.0.45                                                                                                         Up 23 seconds (healthy)
os          oficina-e2e/os:local                                                                                                 Up 5 seconds (healthy)
rabbitmq    rabbitmq:4.4.0-management                                                                                            Up 23 seconds (healthy)
sqlserver   mcr.microsoft.com/mssql/server:2022-latest@sha256:d38c2a64812d775f844088e4e44ab33846eefc61157431bf9c8a3943a534c22e   Up 23 seconds (healthy)
```

## O que mudou no e2e por achado da avaliacao

| Achado | O que mudou |
|---|---|
| A-1 (medio) listagem de filas vazia/erro passava por vacuidade | `listar-filas` (em `scripts/ferramentas.py`) recusa corpo vazio, JSON de erro, lista que nao e de filas, menos de 3 filas ou fila de trabalho ausente (`os-saga`, `billing-comandos`, `execucao-comandos`); `listar_filas` do `lib.sh` devolve exit 1 e a causa; `somar_filas` devolve `INDISPONIVEL: <causa>` e o assert reprova com ela (`verificar_sem_erros` de cada cenario e as secoes 1 do 99). **Prova abaixo** |
| A-2 (medio) porta da UI do RabbitMQ colidia com a dos brokers `standalone` | `E2E_PORTA_RABBIT_UI` passou de 15673 para **15683** (compose, `lib-ambiente.sh`, README); novo `verificar-portas.sh` (le as portas dos composes de OS, Billing, Execucao e Seguranca e compara). **Prova abaixo** |
| A-3 (medio) comentario errado do `Execucao__BaseUrl` | reescrito: consulta sincrona do OS a Execucao pela rede do compose; no AKS o valor e o Service da Execucao |
| A-4 (medio) ids de defeito e narrativa em codigo/README | removidos de `lib.sh`, `99-verificacoes-finais.sh`, compose e README (descrevem o mecanismo atual). O 99 passou a exigir **0 ERR na Execucao**, 0 `Erro não tratado` nos tres servicos e **>= 1** Information `Requisição cancelada pelo cliente` (o cenario 01 congela a Execucao e o OS desiste por timeout) |
| A-5 (medio) versao do RabbitMQ | README, secao Limitacoes: o e2e roda `rabbitmq:4.4.0-management` (a dos testes e dos composes); conferir a do AKS em `OficinaMecanica.Infra/helm/variables.tf` |
| A-6 (baixo) SC2034 | `# shellcheck disable=SC2034` no topo do `lib.sh` com o motivo (variaveis lidas pelos cenarios via `source` dinamico); `shellcheck -S warning` limpo (saida abaixo) |
| A-7 (baixo) asserts fracos | 04: `EstornarPagamento` na fila `eq 1` (era `ge 1`); 06: o numero de avisos `Decisao ... ignorada` tem que ser **igual** a (aprovacoes aceitas - 1), com espera, e a metrica sobe >= isso; 05: "ainda sem link" usa `os_link_estado` (so passa com a consulta 200 e sem link); 99: cada log tem que ter linhas e a partida do bus antes dos asserts "0 ocorrencias" |
| A-8 (baixo) README | tetos reais de espera (10/15/20/30/60/90/120 s e as janelas), nota de CRLF/LF (hoje os tipos estao em LF nos 3 repositorios), tempo medido (~6 min), o conjunto das tres evidencias e o ciclo `derrubar` / `subir --regerar` / `executar-cenarios` / `verificar-contratos` |
| A-9 (baixo) | `e2e/` no `.dockerignore` do OS (o `Dockerfile` so copia `nuget.config`, a `.sln`, os `.csproj` e `src/`; arquivo com CRLF no working tree, como ja estava); `__pycache__/` no `e2e/.gitignore` |
| A-10 (baixo) | `curl` em background do 06 com `--max-time 30`; `executar-cenarios.sh --so` sem argumento (ou invalido) mostra uso e sai com 2 em vez de abortar com variavel nao definida; `python:3.12.16-alpine` (mesmo digest da imagem local, verificado com `docker buildx imagetools inspect`) e `mssql/server:2022-latest@sha256:d38c2a64...` (o digest dos testes de infraestrutura do OS; nenhum tag de patch CU22 a CU27 tem esse digest, entao fixei por digest) |

### Prova do A-1: com a API de management indisponivel o assert agora reprova

Executado com o ambiente derrubado (nada escutando na porta da UI do RabbitMQ), usando as funcoes reais do `lib.sh`:

```text
### 1. comportamento ANTERIOR (logica antiga: json.load direto + awk) com a API de management fora do ar
soma das mensagens *_error lida pela logica antiga = '0'  <- passaria o assert de fila vazia sem ter visto fila nenhuma

### 2. comportamento NOVO com a API de management fora do ar (ambiente derrubado, porta 15683 sem ninguem)
  [FALHA] mensagens em filas *_error (listagem completa da API de management): esperado '0', obtido 'INDISPONIVEL: curl: (7) Failed to connect to 127.0.0.1:15683 after 2037 ms: Could not connect to server
 API de management sem JSON valido (corpo: '')'
-> asserts=1 falhas=1 (esperado: 1 assert, 1 falha)
filas de trabalho drenadas (somar_filas trabalho) = 'INDISPONIVEL: curl: (7) Failed to connect to 127.0.0.1:15683 after 2043 ms: Could not connect to server
 API de management sem JSON valido (corpo: '')'

### 3. validador listar-filas contra respostas defeituosas da API de management
corpo vazio                        -> exit=1 API de management sem JSON valido (corpo: '') 
JSON de erro (credencial ruim)     -> exit=1 API de management nao devolveu uma lista de filas ('{"error":"not_authorised","reason":"Login failed"}') 
lista vazia                        -> exit=1 listagem incompleta: 0 fila(s), faltando os-saga, billing-comandos, execucao-comandos 
2 filas de 3 (falta execucao)      -> exit=1 listagem incompleta: 2 fila(s), faltando execucao-comandos 
3 filas com nomes errados          -> exit=1 listagem incompleta: 3 fila(s), faltando os-saga, billing-comandos, execucao-comandos 
listagem completa (3 + 1 error)    -> exit=0 billing-comandos|0|1 billing-comandos_error|2|0 execucao-comandos|0|1 os-saga|0|1 
```

### Prova do A-2: portas sem colisao

```text
$ bash e2e/verificar-portas.sh
portas publicadas pelos composes dos repositorios: 21 (1433, 1434, 1435, 3000, 3100, 5000, 5001, 5002, 5672, 5673, 7071, 8025, 9000, 9090, 10000, 10001, 10002, 15672, 15673, 16686, 27018)
portas do e2e: SQL=14330, AMQP=56720, UI_RABBIT=15683, MONGO=27118, OS=18080, BILLING=18081, EXECUCAO=18082, JWKS=18090, MAILPIT_UI=18025, SMTP=11025
OK: nenhuma porta do e2e colide com os composes dos repositorios nem entre si.
exit=0

$ E2E_PORTA_RABBIT_UI=15673 bash e2e/verificar-portas.sh   # teste negativo: a porta antiga da UI colide com os brokers standalone
portas publicadas pelos composes dos repositorios: 21 (1433, 1434, 1435, 3000, 3100, 5000, 5001, 5002, 5672, 5673, 7071, 8025, 9000, 9090, 10000, 10001, 10002, 15672, 15673, 16686, 27018)
portas do e2e: SQL=14330, AMQP=56720, UI_RABBIT=15673, MONGO=27118, OS=18080, BILLING=18081, EXECUCAO=18082, JWKS=18090, MAILPIT_UI=18025, SMTP=11025
COLISAO: UI_RABBIT=15673 e publicada por OficinaMecanica.Billing, OficinaMecanica.Execucao
exit=1
```

### shellcheck

```text
$ shellcheck -x -S warning (imagem koalaman/shellcheck:stable) em todos os .sh de e2e/ e e2e/cenarios/
exit=0 (0 = nenhum aviso em nivel warning)
```

## Asserts novos e alterados desta rodada

Total: de 574 para 586.

| Cenario | Rodada anterior | Agora | O que mudou |
|---|---|---|---|
| 01 | 117 | 117 | sem mudanca de contagem (comentarios e nomes sem ids de defeito) |
| 02, 03, 07 | 43, 55, 68 | 43, 55, 68 | sem mudanca de contagem; `verificar_sem_erros` agora le uma listagem completa |
| 04 | 57 | 57 | `EstornarPagamento` esperando na fila: `eq 1` |
| 05 | 121 | 121 | "ainda sem link": consulta validada (200 e sem link) |
| 06 | 70 | 71 (+1) | avisos `Decisao ... ignorada` **iguais** a (aprovacoes aceitas - 1), com espera; metrica `>=` isso |
| 99 | 43 | 54 (+11) | listagem de filas com >= 3 filas; por servico: log lido (linhas > 0) e `Bus started` (6 asserts); Execucao: 0 ERR, 0 `Erro não tratado` e >= 1 Information `Requisição cancelada pelo cliente`; OS e Billing: 0 `Erro não tratado`; (a tolerancia ao ERR da Execucao saiu) |

## Como o ambiente subiu

```text
senhas de DEV geradas em /e/FIAP/Pos/OficinaMecanica/e2e/.gerado/e2e.env
chaves: E:/FIAP/Pos/OficinaMecanica/e2e/.gerado\jwt-privada.pem (so local) | jwks: E:/FIAP/Pos/OficinaMecanica/e2e/.gerado\jwks\jwks.json
== build das imagens (OS, Billing, Execucao) ==
 Image oficina-e2e/execucao:local Building 
 Image oficina-e2e/os:local Building 
 Image oficina-e2e/billing:local Building 
 Image oficina-e2e/execucao:local Built 
 Image oficina-e2e/os:local Built 
 Image oficina-e2e/billing:local Built 
== subindo ==
 Image python:3.12.16-alpine Pulling 
 Image python:3.12.16-alpine Pulled 
 Network oficina-e2e-net Creating 
 Network oficina-e2e-net Created 
 Volume oficina-e2e_rabbitmq_data Creating 
 Volume oficina-e2e_rabbitmq_data Created 
 Volume oficina-e2e_sqlserver_data Creating 
 Volume oficina-e2e_sqlserver_data Created 
 Volume oficina-e2e_mongo_data Creating 
 Volume oficina-e2e_mongo_data Created 
 Container oficina-e2e-sqlserver-1 Creating 
 Container oficina-e2e-rabbitmq-1 Creating 
 Container oficina-e2e-jwks-1 Creating 
 Container oficina-e2e-mailpit-1 Creating 
 Container oficina-e2e-mongo-1 Creating 
 Container oficina-e2e-sqlserver-1 Created 
 Container oficina-e2e-sql-init-1 Creating 
 Container oficina-e2e-mailpit-1 Created 
 Container oficina-e2e-mongo-1 Created 
 Container oficina-e2e-mongo-init-1 Creating 
 Container oficina-e2e-jwks-1 Created 
 Container oficina-e2e-rabbitmq-1 Created 
 Container oficina-e2e-sql-init-1 Created 
 Container oficina-e2e-billing-1 Creating 
 Container oficina-e2e-mongo-init-1 Created 
 Container oficina-e2e-execucao-1 Creating 
 Container oficina-e2e-billing-1 Created 
 Container oficina-e2e-execucao-1 Created 
 Container oficina-e2e-os-1 Creating 
 Container oficina-e2e-os-1 Created 
 Container oficina-e2e-rabbitmq-1 Starting 
 Container oficina-e2e-mailpit-1 Starting 
 Container oficina-e2e-jwks-1 Starting 
 Container oficina-e2e-sqlserver-1 Starting 
 Container oficina-e2e-mongo-1 Starting 
 Container oficina-e2e-mongo-1 Started 
 Container oficina-e2e-mongo-init-1 Starting 
 Container oficina-e2e-jwks-1 Started 
 Container oficina-e2e-mailpit-1 Started 
 Container oficina-e2e-sqlserver-1 Started 
 Container oficina-e2e-sqlserver-1 Waiting 
 Container oficina-e2e-rabbitmq-1 Started 
 Container oficina-e2e-mongo-init-1 Started 
 Container oficina-e2e-rabbitmq-1 Waiting 
 Container oficina-e2e-jwks-1 Waiting 
 Container oficina-e2e-mongo-1 Waiting 
 Container oficina-e2e-mongo-init-1 Waiting 
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
 Container oficina-e2e-rabbitmq-1 Healthy 
 Container oficina-e2e-jwks-1 Healthy 
 Container oficina-e2e-sql-init-1 Exited 
 Container oficina-e2e-billing-1 Starting 
 Container oficina-e2e-billing-1 Started 
 Container oficina-e2e-billing-1 Waiting 
 Container oficina-e2e-execucao-1 Waiting 
 Container oficina-e2e-sql-init-1 Waiting 
 Container oficina-e2e-rabbitmq-1 Waiting 
 Container oficina-e2e-jwks-1 Waiting 
 Container oficina-e2e-jwks-1 Healthy 
 Container oficina-e2e-execucao-1 Healthy 
 Container oficina-e2e-rabbitmq-1 Healthy 
 Container oficina-e2e-sql-init-1 Exited 
 Container oficina-e2e-billing-1 Healthy 
 Container oficina-e2e-os-1 Starting 
 Container oficina-e2e-os-1 Started 
== esperando as tres APIs ficarem prontas (/health/ready) ==
  OS pronto em 3 s
  Billing pronto em 3 s
  Execucao pronto em 4 s
NAME                      IMAGE                                                                                                                COMMAND                  SERVICE     CREATED          STATUS                            PORTS
oficina-e2e-billing-1     oficina-e2e/billing:local                                                                                            "dotnet OficinaMecan…"   billing     22 seconds ago   Up 9 seconds (healthy)            127.0.0.1:18081->8080/tcp
oficina-e2e-execucao-1    oficina-e2e/execucao:local                                                                                           "dotnet OficinaMecan…"   execucao    22 seconds ago   Up 15 seconds (healthy)           127.0.0.1:18082->8080/tcp
oficina-e2e-jwks-1        python:3.12.16-alpine                                                                                                "python -m http.serv…"   jwks        23 seconds ago   Up 21 seconds (healthy)           127.0.0.1:18090->80/tcp
oficina-e2e-mailpit-1     axllent/mailpit:v1.31.0                                                                                              "/mailpit"               mailpit     23 seconds ago   Up 21 seconds (healthy)           127.0.0.1:11025->1025/tcp, 127.0.0.1:18025->8025/tcp
oficina-e2e-mongo-1       mongo:7.0.45                                                                                                         "docker-entrypoint.s…"   mongo       23 seconds ago   Up 21 seconds (healthy)           127.0.0.1:27118->27017/tcp
oficina-e2e-os-1          oficina-e2e/os:local                                                                                                 "dotnet OficinaMecan…"   os          22 seconds ago   Up 4 seconds (health: starting)   127.0.0.1:18080->8080/tcp
oficina-e2e-rabbitmq-1    rabbitmq:4.4.0-management                                                                                            "docker-entrypoint.s…"   rabbitmq    23 seconds ago   Up 21 seconds (healthy)           127.0.0.1:56720->5672/tcp, 127.0.0.1:15683->15672/tcp
oficina-e2e-sqlserver-1   mcr.microsoft.com/mssql/server:2022-latest@sha256:d38c2a64812d775f844088e4e44ab33846eefc61157431bf9c8a3943a534c22e   "/opt/mssql/bin/laun…"   sqlserver   23 seconds ago   Up 21 seconds (healthy)           127.0.0.1:14330->1433/tcp
Ambiente pronto. Portas (127.0.0.1):
  OS         http://127.0.0.1:18080        Billing  http://127.0.0.1:18081        Execucao http://127.0.0.1:18082
  JWKS stub  http://127.0.0.1:18090/.well-known/jwks.json
  RabbitMQ   amqp 56720 | UI http://127.0.0.1:15683 (usuario/senha em /e/FIAP/Pos/OficinaMecanica/e2e/.gerado/e2e.env)
  SQL Server 14330 | MongoDB 27118 | Mailpit http://127.0.0.1:18025
Cenarios: bash e2e/executar-cenarios.sh

tempo de parede do subir.sh --regerar (build + subida + espera dos 3 /health/ready + leitura inicial do /metrics):
real	0m55.357s
user	0m0.559s
sys	0m0.593s
```

## Configuracao efetiva (sem segredos)

```text
### docker/compose
Docker Engine 29.6.1
Docker Compose version v5.3.0

### compose ps
SERVICE     IMAGE                                                                                                                STATUS                    PORTS
billing     oficina-e2e/billing:local                                                                                            Up 2 minutes (healthy)    127.0.0.1:18081->8080/tcp
execucao    oficina-e2e/execucao:local                                                                                           Up 5 minutes (healthy)    127.0.0.1:18082->8080/tcp
jwks        python:3.12.16-alpine                                                                                                Up 6 minutes (healthy)    127.0.0.1:18090->80/tcp
mailpit     axllent/mailpit:v1.31.0                                                                                              Up 6 minutes (healthy)    127.0.0.1:11025->1025/tcp, 127.0.0.1:18025->8025/tcp
mongo       mongo:7.0.45                                                                                                         Up 6 minutes (healthy)    127.0.0.1:27118->27017/tcp
os          oficina-e2e/os:local                                                                                                 Up 5 minutes (healthy)    127.0.0.1:18080->8080/tcp
rabbitmq    rabbitmq:4.4.0-management                                                                                            Up 59 seconds (healthy)   127.0.0.1:56720->5672/tcp, 127.0.0.1:15683->15672/tcp
sqlserver   mcr.microsoft.com/mssql/server:2022-latest@sha256:d38c2a64812d775f844088e4e44ab33846eefc61157431bf9c8a3943a534c22e   Up 6 minutes (healthy)    127.0.0.1:14330->1433/tcp

### imagens construidas
oficina-e2e/os:local  f5fbfcb52d48  criada 6 minutes ago  379MB
oficina-e2e/billing:local  c0fbb830fae5  criada 6 minutes ago  368MB
oficina-e2e/execucao:local  2bc3465d1238  criada 6 minutes ago  351MB

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

`bash e2e/verificar-contratos.sh` (saida real; os tipos `.cs` dos tres repositorios estao em LF, entao nao ha aviso de fim de linha):

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

Inicio 17:49:52.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+2s] cadastros no OS Service (cliente com e-mail, veiculo, servico e peca com estoque 10)
  [OK]    cadastro do cliente A (HTTP) = '201'
  [OK]    cadastro do veiculo A (HTTP) = '201'
  [OK]    cadastro do servico A (HTTP) = '201'
  [OK]    cadastro da peca A (estoque 10) (HTTP) = '201'
  [OK]    estoque inicial da peca = '10'

-- [+4s] abre a OS (servico 150,00 + 2 pecas de 50,00 = 250,00)
  [OK]    abertura da OS A (HTTP) = '201'
  [OK]    status inicial da OS A = 'Recebida'
  [OK]    valor total da OS A = '250'
  OS OS-20261108-FB4A94 id=cf61d4da-6e21-4b01-886c-62402c610307

-- [+5s] saga nasce em AguardandoDiagnostico e a Execucao enfileira a ordem (SolicitarDiagnostico)
  [OK]    ordem de producao na Execucao = 'Fila' (apos 1 s)
  [OK]    estado da saga = 'AguardandoDiagnostico' (apos 0 s)

-- [+6s] rota sincrona GET /api/ordens-de-servico/{id}/execucao com a ordem esperando na fila da Execucao
  [OK]    consulta da execucao (OS repassa o token do chamador a Execucao) (HTTP) = '200'
  [OK]    origem da resposta = 'execucao'
  [OK]    numero da OS = 'OS-20261108-FB4A94'
  [OK]    statusDaOrdem (visao do OS) = 'Recebida'
  [OK]    estadoDaSaga (visao do OS) = 'AguardandoDiagnostico'
  [OK]    producao.status (visao da Execucao) = 'Fila'
  [OK]    motivoDoFallback (nao houve fallback) vazio
  [OK]    consulta da execucao sem token (HTTP) = '401'
  [OK]    consulta da execucao de OS inexistente (HTTP) = '404'

-- [+8s] mecanico reivindica o diagnostico (POST /api/diagnosticos/proximo)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = 'cf61d4da-6e21-4b01-886c-62402c610307'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS (DiagnosticoIniciado) = 'EmDiagnostico' (apos 1 s)
  [OK]    estado da saga = 'EmDiagnostico' (apos 0 s)

-- [+9s] mecanico conclui o diagnostico (publica DiagnosticoConcluido) e o Billing gera o orcamento
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS (orcamento gerado) = 'AguardandoAprovacao' (apos 1 s)
  [OK]    estado da saga = 'AguardandoAprovacao' (apos 1 s)

-- [+12s] confere o orcamento no Billing
  [OK]    consulta do orcamento no Billing (HTTP) = '200'
  [OK]    status do orcamento = 'Gerado'
  [OK]    valor total do orcamento = '250'
  [OK]    itens do orcamento (1 servico + 1 peca) = '2'
  [OK]    laudo do mecanico no orcamento contem 'correia dentada'
  [OK]    numero do orcamento = numero da OS = 'OS-20261108-FB4A94'
  [OK]    estoque ainda intacto antes da aprovacao = '10'

-- [+13s] cliente aprova o orcamento (rota anonima, 202)
  [OK]    aprovacao do orcamento (HTTP) = '202'
  [OK]    corpo do 202: ordemDeServicoId = 'cf61d4da-6e21-4b01-886c-62402c610307'
  [OK]    corpo do 202: status ainda AguardandoAprovacao (decisao so publicada) = 'AguardandoAprovacao'
  [OK]    status da OS (saga aplicou a aprovacao) = 'AguardandoPagamento' (apos 1 s)
  [OK]    estoque reservado de verdade (10 - 2) = '8' (apos 0 s)

-- [+14s] Billing cria o pagamento (Checkout Pro fake) e o link volta para a OS
  [OK]    pagamento no Billing = 'Pendente' (apos 0 s)
  [OK]    estado da saga = 'AguardandoPagamento' (apos 1 s)
  [OK]    link de pagamento gravado na OS preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-e7976997caac4c1389f71421836de0'
  [OK]    link de pagamento da OS contem 'https://fake.mercadopago.local/checkout/fake-pref-'
  [OK]    link da OS = link do Billing = 'https://fake.mercadopago.local/checkout/fake-pref-e7976997caac4c1389f71421836de00e'
  [OK]    valor do pagamento = '250'
  [OK]    orcamento aprovado pelo cliente = 'Aprovado'
  [OK]    prazo de pagamento da OS (horas a partir de agora, Saga__PrazoPagamentoHoras=48) = '48'

-- [+16s] cliente paga: o gateway Fake aprova e dispara o webhook assinado (POST /dev/pagamentos/{pref}/aprovar)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  pagamento do gateway 1794174605364000, x-request-id fa365255-1f68-4784-b8f3-d07d5287bf8c
  [OK]    status do pagamento no Billing = 'Aprovado' (apos 0 s)
  [OK]    status da OS (PagamentoAprovado -> IniciarExecucao) = 'EmExecucao' (apos 1 s)
  [OK]    estado da saga = 'EmExecucao' (apos 0 s)
  [OK]    ordem liberada para reparo na Execucao = 'AguardandoExecucao' (apos 0 s)
  [OK]    gateway payment id aprovado gravado = '1794174605364000'
  [OK]    orcamento depois do pagamento = 'Pago'

-- [+19s] mecanico reivindica o reparo (POST /api/reparos/proximo) e finaliza
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = 'cf61d4da-6e21-4b01-886c-62402c610307'
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

-- [+20s] OS Finalizada: estado final, historico, estoque, e-mails
  [OK]    status da OS = 'Finalizada' (apos 1 s)
  [OK]    estado da saga = 'Concluida' (apos 0 s)
  [OK]    dataConclusao da OS preenchido ('2026-11-08T21:50:12.6084503')
  [OK]    historicoStatus na ordem EM QUE A API DEVOLVE (sem o roteiro reordenar) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    historicoStatus: primeira entrada = 'Recebida'
  [OK]    historicoStatus: ultima entrada = 'Finalizada'
  [OK]    historicoStatus: datas em ordem nao decrescente = 'sim'
  [OK]    metrica saga_iniciada subiu >= 1 (a contagem pode ficar a maior em retry de commit, ADR 0008): 1 ge 1
  [OK]    metrica saga_concluida subiu >= 1: 1 ge 1
  [OK]    estoque definitivamente baixado (nao devolvido) = '8'
  [OK]    saga sem prazo VIGENTE de aprovacao nem de pagamento = 'NULL|NULL'
  [INFO]  linhas de SagaPrazos desta OS (Processado fica 0 ate o vencimento; a saga ignora o PrazoExpirado que nao e o vigente): Aprovacao:0,Pagamento:0
  [OK]    um unico orcamento no Billing = '1'
  [OK]    um unico pagamento no Billing = '1'

-- [+23s] e-mails ao cliente no Mailpit (uma notificacao por transicao + o link de pagamento)
  [OK]    quantidade de e-mails para cliente.1794174594312@e2e.local (5 transicoes + 1 link) = '6' (apos 0 s)
  e-mail: Olá, Cliente E2E 1794174594312! A sua ordem de serviço OS-20261108-FB4A94 teve o status atualizado para: Em diagnóstico. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 1794174594312! A sua ordem de serviço OS-20261108-FB4A94 teve o status atualizado para: Aguardando aprovação do orçamento. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 1794174594312! A sua ordem de serviço OS-20261108-FB4A94 teve o status atualizado para: Aguardando pagamento (o link de pagamento chegará em seguida). Atenciosamente, Oficina Mec..
  e-mail: Olá, Cliente E2E 1794174594312! O orçamento da sua ordem de serviço OS-20261108-FB4A94 foi aprovado. Para iniciarmos o serviço, faça o pagamento pelo link abaixo: https://fake.mercadopago.local/c
  e-mail: Olá, Cliente E2E 1794174594312! A sua ordem de serviço OS-20261108-FB4A94 teve o status atualizado para: Em execução. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 1794174594312! A sua ordem de serviço OS-20261108-FB4A94 teve o status atualizado para: Finalizada. Atenciosamente, Oficina Mecânica
  [OK]    e-mails do cliente contem 'Em diagnóstico'
  [OK]    e-mails do cliente contem 'Aguardando aprovação do orçamento'
  [OK]    e-mails do cliente contem 'Aguardando pagamento'
  [OK]    e-mails do cliente contem 'Em execução'
  [OK]    e-mails do cliente contem 'Finalizada'
  [OK]    e-mail do link de pagamento contem 'fake.mercadopago.local'

-- [+24s] rota sincrona GET /api/ordens-de-servico/{id}/execucao com a OS Finalizada
  [OK]    consulta da execucao (HTTP) = '200'
  [OK]    origem da resposta = 'execucao'
  [OK]    statusDaOrdem = 'Finalizada'
  [OK]    estadoDaSaga = 'Concluida'
  [OK]    estadoDaSagaAtualizadoEm preenchido ('2026-11-08T21:50:12.6016395+00:00')
  [OK]    producao.status = 'Finalizada'
  [OK]    producao.laudo.texto contem 'correia dentada'
  [OK]    producao.laudo.horasEstimadas = '2.5'
  [OK]    producao.resumo contem 'teste de rodagem'
  [OK]    producao.mecanicoId = '00000000-0000-4000-8000-0000000000b1'
  [OK]    motivoDoFallback vazio

-- [+25s] rota sincrona com a Execucao INDISPONIVEL (docker pause): fallback 200 com origem saga, sem inventar producao
  [OK]    consulta da execucao com a Execucao congelada (HTTP) = '200'
  [OK]    origem da resposta (fallback) = 'saga'
  [OK]    motivoDoFallback = 'ExecucaoIndisponivel'
  [OK]    statusDaOrdem (do proprio OS) = 'Finalizada'
  [OK]    estadoDaSaga (do proprio OS) = 'Concluida'
  [OK]    producao (nada inventado no fallback) = ''
  [OK]    tempo da resposta com a Execucao parada (s, teto de 6 s do cliente): 7 le 9
  tempo da resposta de fallback: 7 s
  [OK]    Execucao de volta: origem volta a execucao = 'execucao' (apos 1 s)

-- [+33s] nada preso: outbox das tres bases vazio e nenhuma mensagem em *_error
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 0 s)
  [OK]    mensagens em filas *_error (listagem completa da API de management) = '0'

RESULTADO|01-caminho-feliz|PASSOU|asserts=117|falhas=0|tempo=35s
```

### Cenario 02 - Recusa do orcamento

Inicio 17:50:27.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 1 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+1s] cadastros e OS ate AguardandoAprovacao
  [OK]    cadastro do cliente B (HTTP) = '201'
  [OK]    cadastro do veiculo B (HTTP) = '201'
  [OK]    cadastro do servico B (HTTP) = '201'
  [OK]    cadastro da peca B (estoque 10) (HTTP) = '201'
  [OK]    abertura da OS B (HTTP) = '201'
  [OK]    status inicial da OS B = 'Recebida'
  [OK]    valor total da OS B = '250'
  [OK]    ordem de producao da OS B na Execucao = 'Fila' (apos 0 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = 'a1b9b4a4-4f2a-4a82-b0ef-857e6f615222'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS B (DiagnosticoIniciado) = 'EmDiagnostico' (apos 0 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS B (orcamento gerado) = 'AguardandoAprovacao' (apos 1 s)
  [OK]    orcamento da OS B no Billing = 'Gerado' (apos 0 s)
  [OK]    estoque antes da decisao = '10'

-- [+5s] cliente recusa o orcamento (rota anonima, 202)
  [OK]    recusa do orcamento (HTTP) = '202'
  [OK]    corpo do 202: status ainda AguardandoAprovacao = 'AguardandoAprovacao'
  [OK]    status da OS (saga aplicou a recusa) = 'OrcamentoRecusado' (apos 0 s)
  [OK]    estado da saga (final) = 'OrcamentoRecusado' (apos 1 s)
  [OK]    metrica saga_orcamento_recusado{causa=ClienteRecusouOrcamento} subiu >= 1: 1 ge 1

-- [+7s] efeitos da recusa: orcamento cancelado, sem pagamento, estoque intacto, e-mail
  [OK]    orcamento no Billing (CancelarOrcamento) = 'Cancelado' (apos 0 s)
  [OK]    motivo da recusa propagado ate o orcamento do Billing = 'Valor acima do esperado'
  [OK]    nenhum pagamento criado no Billing = '404'
  [OK]    estoque nunca foi reservado = '10'
  [OK]    historico de status = 'Recebida,EmDiagnostico,AguardandoAprovacao,OrcamentoRecusado'
  [OK]    e-mails ao cliente (EmDiagnostico, AguardandoAprovacao, OrcamentoRecusado) = '3' (apos 0 s)
  e-mail: Olá, Cliente E2E 179417462931336! A sua ordem de serviço OS-20261108-C5D988 teve o status atualizado para: Em diagnóstico. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417462931336! A sua ordem de serviço OS-20261108-C5D988 teve o status atualizado para: Aguardando aprovação do orçamento. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417462931336! A sua ordem de serviço OS-20261108-C5D988 teve o status atualizado para: Orçamento recusado. Atenciosamente, Oficina Mecânica
  [OK]    e-mails do cliente contem 'Orçamento recusado'
  [OK]    prazo de aprovacao vigente limpo na saga = 'NULL'
  [INFO]  Execucao apos a recusa: ordem de producao em 'DiagnosticoConcluido' (a saga so cancela a producao no cancelamento do staff)

-- [+9s] decisoes depois da OS encerrada: aprovar e recusar de novo respondem 409
  [OK]    aprovar depois da recusa (HTTP) = '409'
  [OK]    recusar de novo (HTTP) = '409'
  [OK]    OS segue OrcamentoRecusado = 'OrcamentoRecusado'

-- [+10s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error (listagem completa da API de management) = '0'

RESULTADO|02-recusa-do-orcamento|PASSOU|asserts=43|falhas=0|tempo=12s
```

### Cenario 03 - Pagamento expirado (e aprovacao tardia)

Inicio 17:50:40.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+2s] cadastros e OS ate AguardandoPagamento (estoque reservado, link gerado)
  [OK]    cadastro do cliente C (HTTP) = '201'
  [OK]    cadastro do veiculo C (HTTP) = '201'
  [OK]    cadastro do servico C (HTTP) = '201'
  [OK]    cadastro da peca C (estoque 10) (HTTP) = '201'
  [OK]    abertura da OS C (HTTP) = '201'
  [OK]    status inicial da OS C = 'Recebida'
  [OK]    valor total da OS C = '250'
  [OK]    ordem de producao da OS C na Execucao = 'Fila' (apos 1 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = 'bc24d12a-c14a-4e6c-af5f-3a4194e2828c'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS C (DiagnosticoIniciado) = 'EmDiagnostico' (apos 1 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS C (orcamento gerado) = 'AguardandoAprovacao' (apos 0 s)
  [OK]    orcamento da OS C no Billing = 'Gerado' (apos 1 s)
  [OK]    aprovacao do orcamento da OS C (HTTP) = '202'
  [OK]    status da OS C = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque da peca C reservado (10 - 2) = '8' (apos 0 s)
  [OK]    link de pagamento da OS C preenchido (apos 1 s): 'https://fake.mercadopago.local/checkout/fake-pref-b185551fee55492492a8c48743a9a7'
  [OK]    pagamento da OS C no Billing = 'Pendente' (apos 0 s)
  [OK]    estoque reservado = '8'

-- [+8s] forca o vencimento do prazo de pagamento (Pagamentos.ExpiraEm e SagaPrazos.ExpiraEm no passado)
  [OK]    linhas de Pagamentos vencidas (Billing) = '1'
  [OK]    linhas de SagaPrazos vencidas (OS) = '1'

-- [+9s] sweeper (a cada 5 s) publica PrazoExpirado -> saga cancela o pagamento no Billing -> OS Cancelada
  [OK]    estado da saga (final) = 'Cancelada' (apos 5 s)
  do vencimento forcado ate a saga Cancelada: 5 s
  [OK]    status da OS = 'Cancelada' (apos 1 s)
  [OK]    metrica saga_prazo_expirado{tipo=Pagamento} subiu >= 1 (so o prazo vigente conta): 1 ge 1
  [OK]    metrica saga_cancelada{estado_origem=CancelandoPagamento} subiu >= 1: 1 ge 1

-- [+15s] efeitos: pagamento Expirado, orcamento cancelado, estoque devolvido, prazo marcado, e-mail
  [OK]    pagamento no Billing = 'Expirado'
  [OK]    pagamento sem aprovacao tardia (ainda) = 'false'
  [OK]    orcamento no Billing = 'Cancelado'
  [OK]    estoque devolvido (10) = '10' (apos 0 s)
  [OK]    prazo de pagamento processado pelo sweeper = '1'
  [OK]    historico de status = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,Cancelada'
  [OK]    e-mails ao cliente (5 transicoes + 1 link) = '5' (apos 0 s)
  e-mail: Olá, Cliente E2E 179417464213069! A sua ordem de serviço OS-20261108-E6CC26 teve o status atualizado para: Em diagnóstico. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417464213069! A sua ordem de serviço OS-20261108-E6CC26 teve o status atualizado para: Aguardando aprovação do orçamento. Atenciosamente, Oficina Mecânica
  e-mail: Olá, Cliente E2E 179417464213069! A sua ordem de serviço OS-20261108-E6CC26 teve o status atualizado para: Aguardando pagamento (o link de pagamento chegará em seguida). Atenciosamente, Oficina Mec
  e-mail: Olá, Cliente E2E 179417464213069! O orçamento da sua ordem de serviço OS-20261108-E6CC26 foi aprovado. Para iniciarmos o serviço, faça o pagamento pelo link abaixo: https://fake.mercadopago.local
  e-mail: Olá, Cliente E2E 179417464213069! A sua ordem de serviço OS-20261108-E6CC26 teve o status atualizado para: Cancelada. Atenciosamente, Oficina Mecânica
  [OK]    e-mails do cliente contem 'Cancelada'
  [OK]    a Execucao nao recebeu cancelamento (so o staff cancela a producao) = 'DiagnosticoConcluido'

-- [+17s] aprovacao TARDIA: o cliente paga depois do prazo (gateway Fake, webhook assinado)
  [OK]    webhook da aprovacao tardia (HTTP) = '200'
  [OK]    pagamento estornado automaticamente pelo Billing = 'Estornado' (apos 0 s)
  [OK]    aprovacao tardia registrada = 'true'
  [OK]    estorno concluido = 'Concluido'
  [OK]    id do estorno no gateway preenchido ('fake-refund-1')
  pagamento tardio do gateway: 1794174605364001
  [OK]    saga continua Cancelada (ignora PagamentoEstornado fora de Estornando) continuou 'Cancelada' por 6s
  [OK]    OS continua Cancelada = 'Cancelada'
  [OK]    estoque NAO devolvido de novo = '10'

-- [+26s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error (listagem completa da API de management) = '0'

RESULTADO|03-pagamento-expirado|PASSOU|asserts=55|falhas=0|tempo=28s
```

### Cenario 04 - Execucao inviavel com estorno

Inicio 17:51:08.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 1 s)
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
  [OK]    ordem reivindicada (diagnostico) = 'a7443691-39e7-4ddc-b8e9-f1530609cb16'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS D (DiagnosticoIniciado) = 'EmDiagnostico' (apos 0 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS D (orcamento gerado) = 'AguardandoAprovacao' (apos 1 s)
  [OK]    orcamento da OS D no Billing = 'Gerado' (apos 0 s)
  [OK]    aprovacao do orcamento da OS D (HTTP) = '202'
  [OK]    status da OS D = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque da peca D reservado (10 - 2) = '8' (apos 1 s)
  [OK]    link de pagamento da OS D preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-e6938de55dd243878e40524cd51459'
  [OK]    pagamento da OS D no Billing = 'Pendente' (apos 1 s)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    status da OS = 'EmExecucao' (apos 1 s)
  [OK]    ordem liberada para reparo = 'AguardandoExecucao' (apos 0 s)
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = 'a7443691-39e7-4ddc-b8e9-f1530609cb16'
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
  [OK]    EstornarPagamento (unica mensagem) esperando o Billing na fila billing-comandos: 1 eq 1
  billing-comandos: total=1 prontas=0

-- [+22s] descongela o Billing: estorno no gateway Fake -> PagamentoEstornado -> estoque liberado -> OS Cancelada
  [OK]    estado da saga (final) = 'Cancelada' (apos 0 s)
  [OK]    status da OS = 'Cancelada' (apos 1 s)
  [OK]    estoque liberado (10) = '10' (apos 0 s)
  [OK]    metrica saga_cancelada{estado_origem=Estornando} subiu >= 1: 1 ge 1

-- [+23s] efeitos no Billing e nos e-mails
  [OK]    pagamento no Billing = 'Estornado' (apos 1 s)
  [OK]    estorno concluido = 'Concluido'
  [OK]    id do estorno no gateway Fake contem 'fake-refund-'
  [OK]    um unico estorno para o pagamento = '1'
  [OK]    historico de status = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Cancelada'
  [OK]    e-mails do cliente contem 'Cancelada'
  [OK]    ordem de producao na Execucao = 'Inviavel' (apos 0 s)

-- [+25s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error (listagem completa da API de management) = '0'

RESULTADO|04-execucao-inviavel-com-estorno|PASSOU|asserts=57|falhas=0|tempo=27s
```

### Cenario 05 - Resiliencia: Billing fora (stop e kill), mensagem duplicada e ids do gateway Fake

Inicio 17:51:36.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 1 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+1s] cadastros (duas OS independentes, A e B, cada uma com a sua peca)
  [OK]    cadastro do cliente A (HTTP) = '201'
  [OK]    cadastro do veiculo A (HTTP) = '201'
  [OK]    cadastro do servico A (HTTP) = '201'
  [OK]    cadastro da peca A (estoque 10) (HTTP) = '201'
  [OK]    cadastro do cliente B (HTTP) = '201'
  [OK]    cadastro do veiculo B (HTTP) = '201'
  [OK]    cadastro do servico B (HTTP) = '201'
  [OK]    cadastro da peca B (estoque 10) (HTTP) = '201'

-- [+4s] QUEDA 1: para o Billing (docker stop) antes de abrir as OS
  [OK]    consumidores do billing-comandos = '0' (apos 6 s)
  [OK]    Billing inacessivel (curl devolve 000) = '000' (apos 2 s)

-- [+13s] OS e Execucao seguem sozinhos: abrem as OS, o mecanico diagnostica as duas
  [OK]    abertura da OS A (HTTP) = '201'
  [OK]    status inicial da OS A = 'Recebida'
  [OK]    valor total da OS A = '250'
  [OK]    ordem A na Execucao = 'Fila' (apos 0 s)
  [OK]    abertura da OS B (HTTP) = '201'
  [OK]    status inicial da OS B = 'Recebida'
  [OK]    valor total da OS B = '250'
  [OK]    ordem B na Execucao = 'Fila' (apos 0 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '01e91db6-4836-4a46-91e1-1d09b4c59cda'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '6e78eaa4-7066-4dae-ba48-1f9d5e3b23ce'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    saga A = 'GerandoOrcamento' (apos 0 s)
  [OK]    saga B = 'GerandoOrcamento' (apos 1 s)

-- [+18s] com o Billing fora: nada avanca, as mensagens esperam no broker e a API do OS continua servindo
  [OK]    GerarOrcamento x2 esperando na billing-comandos = '2' (apos 3 s)
  [OK]    status da OS A continuou 'EmDiagnostico' por 6s
  [OK]    status da OS B continuou 'EmDiagnostico' por 1s
  [OK]    OS Service consultando peca com o Billing fora (HTTP) = '200'
  [OK]    cadastro do cliente Z (HTTP) = '201'
  [OK]    cadastro do veiculo Z (HTTP) = '201'
  [OK]    cadastro do servico Z (HTTP) = '201'
  [OK]    cadastro da peca Z (estoque 5) (HTTP) = '201'
  [OK]    Execucao respondendo com o Billing fora (HTTP) = '200'
  [OK]    OS /health/ready (so o banco derruba; o Billing nao e dependencia) = '200'

-- [+30s] entrega DUPLICADA no broker: copio (mesmo messageId) um GerarOrcamento direto na fila, como um at-least-once faria
  [OK]    messageId da mensagem duplicada preenchido ('a0cece08-962f-d1f3-1c76-fb208cfd877f')
  [OK]    billing-comandos com 3 mensagens (2 originais + 1 copia) = '3' (apos 6 s)

-- [+36s] Billing volta: a fila drena e cada OS ganha UM orcamento (a copia e descartada pelo inbox)
  [OK]    Billing /health/ready depois de subir = '200' (apos 2 s)
  [OK]    consumidor do billing-comandos reconectado = '1' (apos 6 s)
  [OK]    billing-comandos drenada = '0' (apos 1 s)
  [OK]    InboxState do Billing: ReceiveCount do messageId copiado (copia + retentativas do inbox): 5 ge 2
  [OK]    InboxState do Billing: o messageId copiado foi CONSUMIDO uma vez (linhas com Consumed preenchido) = '1'
  [OK]    status da OS A = 'AguardandoAprovacao' (apos 1 s)
  [OK]    status da OS B = 'AguardandoAprovacao' (apos 0 s)
  [OK]    orcamentos da OS A no Billing = '1'
  [OK]    orcamentos da OS B no Billing = '1'
  [OK]    orcamento A = 'Gerado'
  [OK]    orcamento B = 'Gerado'

-- [+48s] QUEDA 2: mata o Billing (docker kill) e aprova as duas OS com ele fora
  [OK]    consumidores do billing-comandos = '0' (apos 7 s)
  [OK]    aprovacao da OS A com o Billing fora (HTTP) = '202'
  [OK]    aprovacao da OS B com o Billing fora (HTTP) = '202'
  [OK]    status da OS A = 'AguardandoPagamento' (apos 0 s)
  [OK]    status da OS B = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque da peca A reservado = '8' (apos 0 s)
  [OK]    estoque da peca B reservado = '8' (apos 1 s)
  [OK]    SolicitarPagamento x2 esperando na billing-comandos = '2' (apos 8 s)
  [OK]    link de pagamento da OS A (consulta 200 e sem link) continuou 'ausente' por 5s

-- [+71s] Billing volta (processo que tinha morrido): cada OS ganha UM pagamento e um link
  [OK]    Billing /health/ready depois de subir = '200' (apos 2 s)
  [OK]    consumidor do billing-comandos reconectado = '1' (apos 7 s)
  [OK]    link de pagamento da OS A preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-994e5ef5e97a4a0893a841f821f21d'
  [OK]    link de pagamento da OS B preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-e7cb456d54ef4a3bb17b81665051e2'
  [OK]    billing-comandos drenada = '0' (apos 0 s)
  [OK]    pagamentos da OS A no Billing = '1'
  [OK]    pagamentos da OS B no Billing = '1'
  [OK]    estoque da peca A (uma unica baixa) = '8'
  [OK]    estoque da peca B (uma unica baixa) = '8'

-- [+82s] fluxo retomado: pagamento e reparo das duas OS ate Finalizada
  ids de gateway ja gravados no BillingDb antes da 1a aprovacao pos-restart: 3 (maior: 1794174605364002)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    id do gateway apos o restart do Billing e maior que todos os ja gravados: 1794174771441000 gt 1794174605364002
  [OK]    id do gateway Fake com 16 ou mais digitos (derivado do relogio, como o do Mercado Pago): 16 ge 16
  [OK]    exatamente uma tentativa nova gravada no BillingDb (nenhuma colisao, nenhuma repeticao): 4 eq 4
  [OK]    ordem A liberada para reparo = 'AguardandoExecucao' (apos 1 s)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    ordem B liberada para reparo = 'AguardandoExecucao' (apos 1 s)
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '01e91db6-4836-4a46-91e1-1d09b4c59cda'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo A (HTTP) = '200'
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '6e78eaa4-7066-4dae-ba48-1f9d5e3b23ce'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo B (HTTP) = '200'
  [OK]    status da OS A = 'Finalizada' (apos 0 s)
  [OK]    status da OS B = 'Finalizada' (apos 1 s)
  [OK]    saga A = 'Concluida' (apos 0 s)
  [OK]    saga B = 'Concluida' (apos 0 s)

-- [+88s] sem perder e sem duplicar: contagens finais
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

-- [+93s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error (listagem completa da API de management) = '0'
  fila billing-comandos: mensagens=0 consumidores=1
  fila execucao-comandos: mensagens=0 consumidores=1
  fila os-saga: mensagens=0 consumidores=1

RESULTADO|05-resiliencia-billing-fora|PASSOU|asserts=121|falhas=0|tempo=95s
```

### Cenario 06 - Idempotencia

Inicio 17:53:13.

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
  [OK]    ordem de producao da OS F na Execucao = 'Fila' (apos 1 s)
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '1e177512-5446-4039-b1a2-bb52840033df'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS F (DiagnosticoIniciado) = 'EmDiagnostico' (apos 1 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS F (orcamento gerado) = 'AguardandoAprovacao' (apos 0 s)
  [OK]    orcamento da OS F no Billing = 'Gerado' (apos 1 s)

-- [+6s] decisao duplicada: duas aprovacoes disparadas ao mesmo tempo (rota anonima)
  respostas das duas aprovacoes simultaneas: 202 e 202
  [OK]    aprovacao respondeu 202 (decisao publicada para a saga)
  [OK]    aprovacao respondeu 202 (decisao publicada para a saga)
  [OK]    ao menos uma aprovacao aceita (202): 2 ge 1
  [OK]    status da OS = 'AguardandoPagamento' (apos 0 s)
  [OK]    estoque reservado UMA vez (10 - 2) continuou '8' por 6s
  [OK]    link de pagamento preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-08511366426f43779e614c3f5608be'
  [OK]    pagamentos criados no Billing = '1'
  [OK]    orcamentos no Billing = '1'
  [OK]    avisos 'Decisao ... ignorada' da saga no log do OS para esta OS (aprovacoes aceitas - 1) = '1' (apos 1 s)
  [OK]    metrica saga_decisao_ignorada subiu pelo menos o numero de decisoes ignoradas desta OS: 1 ge 1

-- [+14s] reenvio da mesma decisao depois de a saga avancar: 409 e nada muda
  [OK]    aprovar de novo (OS em AguardandoPagamento) (HTTP) = '409'
  [OK]    recusar depois de aprovar (HTTP) = '409'
  [OK]    OS segue AguardandoPagamento = 'AguardandoPagamento'
  [OK]    estoque segue reservado uma vez = '8'

-- [+15s] webhook: primeira entrega (aprovacao via gateway Fake)
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  pagamento do gateway 1794174771441002, x-request-id 415bd584-a591-4771-98db-fda0bc4af1f0
  [OK]    status da OS = 'EmExecucao' (apos 0 s)
  [OK]    ordem liberada para reparo = 'AguardandoExecucao' (apos 0 s)

-- [+16s] webhook DUPLICADO: a mesma entrega (mesmo x-request-id, assinatura valida) chega de novo na rota real
  [OK]    reentrega do mesmo webhook (HTTP) = '200'
  [OK]    registros do x-request-id no Billing (deduplicado) = '1'
  [OK]    tentativas de pagamento (uma so, aprovada) = '1'

-- [+17s] mesmo pagamento notificado de novo com OUTRO x-request-id (reentrega do Mercado Pago)
  [OK]    novo x-request-id para o mesmo pagamento (HTTP) = '200'
  [OK]    tentativas de pagamento continuam 1 = '1'
  [OK]    pagamento continua Aprovado (uma aprovacao) = 'Aprovado'
  [OK]    OS continua EmExecucao continuou 'EmExecucao' por 4s
  [OK]    uma unica ordem esperando reparo na Execucao = '1'

-- [+22s] respostas documentadas da rota de webhook
  [OK]    assinatura invalida (HTTP) = '401'
  [OK]    sem x-signature (HTTP) = '401'
  [OK]    assinatura valida mas fora da tolerancia de tempo (15 min) (HTTP) = '401'
  [OK]    pagamento que o gateway ainda nao conhece (assinatura valida) (HTTP) = '503'
  [OK]    cabecalho Retry-After do 503 contem 'retry-after'
  [OK]    topico diferente de payment (confirmado e ignorado) (HTTP) = '200'

-- [+24s] reparo e fim: reentrega tardia do webhook nao reabre nada
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '1e177512-5446-4039-b1a2-bb52840033df'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo (HTTP) = '200'
  [OK]    status da OS = 'Finalizada' (apos 1 s)
  [OK]    estado da saga = 'Concluida' (apos 0 s)
  [OK]    segunda finalizacao do reparo recusada com 4xx (400): 
  [OK]    webhook reentregue depois da OS finalizada (HTTP) = '200'
  [OK]    OS continua Finalizada continuou 'Finalizada' por 4s
  [OK]    historico de status (cada transicao uma vez) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    estoque final (baixa unica) = '8'
  [OK]    e-mails do cliente (5 transicoes + 1 link) = '6' (apos 0 s)
  [OK]    e-mails de 'Em execucao' (uma notificacao so) = '1'

-- [+31s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error (listagem completa da API de management) = '0'

RESULTADO|06-idempotencia|PASSOU|asserts=71|falhas=0|tempo=32s
```

### Cenario 07 - Resiliencia: RabbitMQ fora (+ fallback da rota /execucao)

Inicio 17:53:47.

```text
  [OK]    OS /health/ready = '200' (apos 0 s)
  [OK]    Billing /health/ready = '200' (apos 0 s)
  [OK]    Execucao /health/ready = '200' (apos 0 s)
  [OK]    fila de diagnostico da Execucao vazia (pre-condicao) = '0'
  [OK]    fila de reparo da Execucao vazia (pre-condicao) = '0'

-- [+1s] cadastros
  [OK]    cadastro do cliente G (HTTP) = '201'
  [OK]    cadastro do veiculo G (HTTP) = '201'
  [OK]    cadastro do servico G (HTTP) = '201'
  [OK]    cadastro da peca G (estoque 10) (HTTP) = '201'

-- [+2s] QUEDA 1: para o RabbitMQ (docker stop) e abre a OS
  [OK]    RabbitMQ fora (management responde 000) = '000' (apos 2 s)
  [OK]    abertura da OS G (HTTP) = '201'
  [OK]    status inicial da OS G = 'Recebida'
  [OK]    valor total da OS G = '250'
  [OK]    OS /health/ready com o broker fora: 200 e corpo com Degraded = '200 degradado' (apos 3 s)
  [OK]    Billing /health/ready com o broker fora: 200 e corpo com Degraded = '200 degradado' (apos 0 s)
  [OK]    Execucao /health/ready com o broker fora: 200 e corpo com Degraded = '200 degradado' (apos 1 s)
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

-- [+18s] RabbitMQ volta: os servicos reconectam e a ordem chega na Execucao
  [OK]    management do RabbitMQ no ar = '200' (apos 4 s)
  [OK]    consumidor de os-saga reconectado = '1' (apos 10 s)
  [OK]    consumidor de billing-comandos reconectado = '1' (apos 0 s)
  [OK]    consumidor de execucao-comandos reconectado = '1' (apos 0 s)
  tempo ate os tres servicos reconectarem: 15 s
  [OK]    ordem de producao na Execucao = 'Fila' (apos 1 s)
  [OK]    estado da saga = 'AguardandoDiagnostico' (apos 0 s)

-- [+34s] fluxo normal ate AguardandoAprovacao
  [OK]    reivindicacao do diagnostico (HTTP) = '200'
  [OK]    ordem reivindicada (diagnostico) = '0d3fdacf-a849-4579-b589-09bd2284ec53'
  [OK]    status na Execucao apos reivindicar = 'EmDiagnostico'
  [OK]    mecanico responsavel = sub do token = '00000000-0000-4000-8000-0000000000b1'
  [OK]    status da OS = 'EmDiagnostico' (apos 0 s)
  [OK]    conclusao do diagnostico (HTTP) = '200'
  [OK]    status na Execucao apos concluir = 'DiagnosticoConcluido'
  [OK]    status da OS = 'AguardandoAprovacao' (apos 1 s)

-- [+36s] QUEDA 2: para o RabbitMQ de novo e o cliente aprova o orcamento com o broker fora
  [OK]    RabbitMQ fora (management responde 000) = '000' (apos 2 s)
  [OK]    aprovacao aceita com o broker fora (decisao vai para o outbox) (HTTP) = '202'
  [OK]    status da OS (decisao ainda nao chegou na saga) continuou 'AguardandoAprovacao' por 6s
  [OK]    decisao retida no outbox do OS: 1 ge 1
  [OK]    estoque ainda intacto = '10'

-- [+45s] RabbitMQ volta: a decisao e entregue e o fluxo segue ate Finalizada
  [OK]    management do RabbitMQ no ar = '200' (apos 5 s)
  [OK]    consumidor de os-saga reconectado = '1' (apos 19 s)
  [OK]    consumidor de billing-comandos reconectado = '1' (apos 0 s)
  [OK]    consumidor de execucao-comandos reconectado = '1' (apos 0 s)
  tempo ate os tres servicos reconectarem: 24 s
  [OK]    status da OS = 'AguardandoPagamento' (apos 1 s)
  [OK]    estoque reservado = '8' (apos 0 s)
  [OK]    link de pagamento preenchido (apos 0 s): 'https://fake.mercadopago.local/checkout/fake-pref-156b87b8c3794ef7868107ac6b975e'
  [OK]    simulacao de aprovacao no gateway Fake (primeira tentativa) (HTTP) = '200'
  [OK]    status do pagamento no gateway = 'approved'
  [OK]    ordem liberada para reparo = 'AguardandoExecucao' (apos 0 s)
  [OK]    reivindicacao do reparo (HTTP) = '200'
  [OK]    ordem reivindicada (reparo) = '0d3fdacf-a849-4579-b589-09bd2284ec53'
  [OK]    status na Execucao apos reivindicar o reparo = 'EmExecucao'
  [OK]    finalizacao do reparo (HTTP) = '200'
  [OK]    status da OS = 'Finalizada' (apos 0 s)
  [OK]    estado da saga = 'Concluida' (apos 1 s)
  [OK]    historico de status (cada transicao uma vez) = 'Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada'
  [OK]    orcamentos e pagamentos no Billing = '1|1'
  [OK]    estoque final (baixa unica) = '8'
  [OK]    e-mails do cliente (5 transicoes + 1 link, sem repeticao) = '6' (apos 0 s)

-- [+74s] nada preso
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 1 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)
  [OK]    mensagens em filas *_error (listagem completa da API de management) = '0'

RESULTADO|07-resiliencia-broker-fora|PASSOU|asserts=68|falhas=0|tempo=76s
```

### Verificacoes finais (+ metricas da saga)

Inicio 17:55:04.

```text
-- [+0s] 1. filas do RabbitMQ (API de management): listagem completa, nenhuma *_error com mensagem, filas de trabalho drenadas
  fila billing-comandos: mensagens=0 consumidores=1
  fila execucao-comandos: mensagens=0 consumidores=1
  fila os-saga: mensagens=0 consumidores=1
  [OK]    filas listadas pela API de management (as 3 de trabalho, mais as *_error que existirem): 3 ge 3
  [OK]    soma das mensagens em filas *_error = '0'
  [OK]    filas de trabalho (os-saga, billing-comandos, execucao-comandos) drenadas = '0'

-- [+1s] 2. logs dos servicos: nenhum erro/Fault inesperado
  os: 19645 linhas de log; linhas ERR/FTL: 3
  [OK]    log de os lido (linhas > 0): 19645 gt 0
  [OK]    log de os traz a partida do bus (Bus started): 1 ge 1
  billing: 557 linhas de log; linhas ERR/FTL: 1
  [OK]    log de billing lido (linhas > 0): 557 gt 0
  [OK]    log de billing traz a partida do bus (Bus started): 3 ge 1
  execucao: 483 linhas de log; linhas ERR/FTL: 0
  [OK]    log de execucao lido (linhas > 0): 483 gt 0
  [OK]    log de execucao traz a partida do bus (Bus started): 1 ge 1
  [OK]    Execucao: linhas ERR/FTL = '0'
  [OK]    Execucao: 'Erro nao tratado' no log = '0'
  [OK]    Execucao: Information 'Requisicao cancelada pelo cliente' (o OS desistiu da consulta com a Execucao congelada): 2 ge 1
  OS: ERR OnTimeout do cliente da Execucao (esperado, 3 tentativas de 2 s por consulta estourada)=3
  [OK]    OS: linhas ERR/FTL fora do OnTimeout do cliente da Execucao = '0'
  [OK]    OS: 'Erro nao tratado' no log = '0'
  [OK]    OS: 'Falha técnica no comando' (Fault<T> real da saga) = '0'
  [OK]    OS/Billing/Execucao: R-FAULT / Fault<> do MassTransit = '0'
  [OK]    Billing: ERR que nao seja o 'saving changes' do EF = '0'
  [OK]    Billing: 'Erro nao tratado' no log = '0'
  Billing: violacoes de indice unico: id do gateway Fake (IX_TentativasPagamento_GatewayPaymentId)=0, copia duplicada no inbox (AK_InboxState_MessageId_ConsumerId)=3
  [OK]    Billing: ids do gateway Fake repetidos entre reinicios (IX_TentativasPagamento_GatewayPaymentId) = '0'
  [OK]    Billing: violacoes de unicidade fora do inbox da copia duplicada = '0'
  [OK]    Billing: 409 por conflito de persistencia no log = '0'
  [OK]    OS: aviso 'Overriding HTTP_PORTS' no log (a porta vem so do ASPNETCORE_HTTP_PORTS da imagem) = '0'
  [OK]    os: nenhum aviso (WRN) fora da lista de esperados
  [OK]    billing: nenhum aviso (WRN) fora da lista de esperados
  [OK]    execucao: nenhum aviso (WRN) fora da lista de esperados

-- [+3s] 3. ninguem preso: sagas, producao, pagamentos
  sagas por estado: Cancelada=2, Concluida=5, OrcamentoRecusado=1
  [OK]    sagas fora de um estado final (Concluida, Cancelada, OrcamentoRecusado) = '0'
  ordens de producao por status: [{"_id":"Finalizada","n":5},{"_id":"DiagnosticoConcluido","n":2},{"_id":"Inviavel","n":1}]
  [OK]    ordens de producao esperando diagnostico/reparo ou em andamento = '0'
  pagamentos por status: Aprovado=5, Estornado=2
  [OK]    pagamentos em Pendente/Estornando/EstornoFalhou = '0'
  [OK]    estornos Pendente/Falhou = '0'
  [OK]    outbox do OS (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox do Billing (OutboxMessage) vazio = '0' (apos 0 s)
  [OK]    outbox da Execucao (outbox.messages) vazio = '0' (apos 1 s)

-- [+8s] 4. containers: todos de pe, sem restart automatico nem OOM
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

-- [+13s] 6. metricas da saga no /metrics do OS (Meter OficinaMecanica.Saga, ADR 0008) coerentes com o banco
  banco: sagas=8 concluidas=5 canceladas=2 orcamento-recusado=1
  metricas agora: saga_iniciada=8 saga_concluida=5 saga_cancelada=2 saga_orcamento_recusado=1 (as duas ultimas podem ter expirado do /metrics)
  [OK]    saga_iniciada >= sagas criadas (banco): 8 ge 8
  [OK]    saga_concluida >= sagas Concluida (banco): 5 ge 5
  [OK]    saga_duracao_estado_segundos_count{estado=AguardandoDiagnostico} >= sagas criadas (toda saga sai desse estado): 8 ge 8
  [OK]    saga_compensacao_falhou (nenhum Fault de compensacao) = '0'
  [OK]    saga_estorno_pendente_manual (nenhum estorno parado) = '0'

-- [+15s] 7. resumo dos dados gerados pela execucao
  ordens de servico: 8  | orcamentos: 8  | pagamentos: 7  | estornos: 2
  e-mails no Mailpit: 44
  inbox (mensagens consumidas com deduplicacao): OS=64 Billing=19 Execucao=14

RESULTADO|99-verificacoes-finais|PASSOU|asserts=54|falhas=0|tempo=18s
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
oficinamecanica_saga_saga_prazo_expirado{tipo="Pagamento"} 1
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
[21:53:20 WRN] Decisão ClienteAprovouOrcamento da ordem de serviço 1e177512-5446-4039-b1a2-bb52840033df ignorada: a saga está em AguardandoPagamento e a decisão não tem mais efeito, mensagem ab678b8a-9dcf-47c7-bf8e-f5358030b02a
-- OS: consulta de execucao com a Execucao indisponivel (WRN, cenario 01)
[21:50:24 WRN] Consulta de execução da ordem de serviço cf61d4da-6e21-4b01-886c-62402c610307: usando o último estado conhecido da saga (ExecucaoIndisponivel, timeout).
-- OS: queda do RabbitMQ (cenario 07)
[21:49:48 INF] Bus started: rabbitmq://rabbitmq/
[21:53:56 WRN] Connection Failed: rabbitmq://rabbitmq/
[21:54:06 WRN] Connection Failed: rabbitmq://rabbitmq/
[21:54:34 WRN] Connection Failed: rabbitmq://rabbitmq/
-- Billing: copia duplicada concorrente (cenario 05; ERR do EF seguido de R-RETRY)
 ---> Microsoft.Data.SqlClient.SqlException (0x80131904): Violation of UNIQUE KEY constraint 'AK_InboxState_MessageId_ConsumerId'. Cannot insert duplicate key in object 'dbo.InboxState'. The duplicate key value is (a0cece08-962f-d1f3-1c76-fb208cfd877f, 4f7d84c7-af6e-a308-cd43-f95b3ffeb8a2).
[21:52:16 WRN] R-RETRY rabbitmq://rabbitmq/billing-comandos a0cece08-962f-d1f3-1c76-fb208cfd877f MassTransit.RetryPolicies.RetryConsumeContext<OficinaMecanica.Contracts.Commands.GerarOrcamento>
-- Billing: aprovacao tardia (cenario 03)
[21:50:57 WRN] Pagamento b185551f-ee55-4924-92a8-c48743a9a722 aprovado no gateway depois de Expirado; estorno automático solicitado.
-- Execucao: 2a finalizacao do reparo (cenario 06)
[21:53:39 WRN] Erro de domínio: Não é possível finalizar o reparo com a ordem no status 'Finalizada' (permitido: 'EmExecucao').
```

## Defeitos

### Confirmacao: o ERR da Execucao por requisicao abandonada desapareceu (DEF-4 corrigido)

Antes (evidencia integrada, cenario 01 com a Execucao congelada): o unico ERR do log da Execucao era
`[ERR] Erro não tratado: The operation was canceled.`, seguido do ramo de erro interno (500) para uma conexao que ja nao existia.
Agora, no mesmo cenario (o OS estoura as 3 tentativas de 2 s da consulta `/execucao` e fecha a conexao) o log da Execucao tem **0 linhas ERR**,
0 `Erro não tratado` e o Information do middleware, com so o caminho e o trace id (sem query string nem cabecalhos):

```text
### DEF-4: log da Execucao apos o fallback ExecucaoIndisponivel (cenario 01)
-- Execucao: linhas ERR/FTL no log inteiro = 0
-- Execucao: 'Erro nao tratado' no log inteiro = 0
-- Execucao: Information da requisicao abandonada pelo OS (so caminho e trace id, sem corpo):
[21:50:24 INF] Requisição cancelada pelo cliente antes da resposta: /api/ordens-de-producao/cf61d4da-6e21-4b01-886c-62402c610307 (trace 71e2cd0134148e920f494a8206893055).
[21:50:24 INF] Requisição cancelada pelo cliente antes da resposta: /api/ordens-de-producao/cf61d4da-6e21-4b01-886c-62402c610307 (trace 71e2cd0134148e920f494a8206893055).
-- OS: Information/ERR do mesmo episodio (o ERR OnTimeout vem da telemetria do cliente HTTP do proprio OS, nao do middleware)
[21:50:20 ERR] Resilience event occurred. EventName: 'OnTimeout', Source: 'IExecucaoClient-execucao//Timeout', Operation Key: 'null', Result: 'null'
[21:50:22 ERR] Resilience event occurred. EventName: 'OnTimeout', Source: 'IExecucaoClient-execucao//Timeout', Operation Key: 'null', Result: 'null'
[21:50:24 ERR] Resilience event occurred. EventName: 'OnTimeout', Source: 'IExecucaoClient-execucao//Timeout', Operation Key: 'null', Result: 'null'
[21:50:24 WRN] A Execução não atendeu a consulta da ordem de serviço cf61d4da-6e21-4b01-886c-62402c610307: timeout.
[21:50:24 WRN] Consulta de execução da ordem de serviço cf61d4da-6e21-4b01-886c-62402c610307: usando o último estado conhecido da saga (ExecucaoIndisponivel, timeout).
-- total de linhas ERR por servico: OS=3 Billing=1 Execucao=0
```

O 99 confere isso por assert (Execucao: 0 ERR/FTL, 0 `Erro não tratado`, >= 1 Information `Requisição cancelada pelo cliente`). So o caminho da Execucao
foi exercitado de ponta a ponta; o mesmo tratamento do OS e do Billing existe no codigo dos dois, mas este e2e nao abandona requisicoes contra eles, entao ali
a verificacao fica nos testes de integracao de cada repositorio (`ExceptionHandlingMiddlewareTests`).

### Defeitos novos

Nenhum defeito novo nos servicos apareceu nesta execucao.

### Observacoes (nao sao defeitos; continuam valendo)

- **OBS-1** - datas do OS sem offset (`DateTime` sem `Z`), enquanto Billing e `/execucao` devolvem `DateTimeOffset`.
- **OBS-2** - linhas obsoletas de `SagaPrazos` ficam `Processado=0` ate vencer.
- **OBS-3** - orcamento recusado ou pagamento expirado deixam a ordem da Execucao em `DiagnosticoConcluido` para sempre (2 ordens no fim).
- **OBS-4** - a copia duplicada concorrente do cenario 05 gera 1 ERR do EF no Billing (`AK_InboxState_MessageId_ConsumerId`) antes do retry descartar a copia.
- **OBS-5** - linha `Usage Telemetry` do MassTransit nos logs; nao verifiquei se ha envio para fora.
- **OBS-8** - o OS registra 3 linhas ERR (`OnTimeout` do Polly, uma por tentativa de 2 s) e 6 avisos a cada `/execucao` que cai em `ExecucaoIndisponivel`; vem da
  telemetria padrao do cliente HTTP do proprio OS, nao do middleware, e continua aparecendo (o 99 a separa dos ERR inesperados).
- **OBS-9** - o adapter do prometheus-net so observa os Meters a partir da primeira leitura do `/metrics` e remove cada serie cerca de 5 minutos depois da ultima
  atualizacao; por isso `subir.sh` e o runner leem o `/metrics` antes do primeiro fluxo e os cenarios conferem cada contador logo depois do fluxo.

## Limitacoes desta execucao

- O Seguranca nao foi incluido (JWKS stub + token emitido por script): prova a **validacao** de JWT nos tres servicos, nao a emissao.
- Gateway Fake no lugar do Mercado Pago; servicos em `Development` (exigencia do Fake), migrations aplicadas pelo proprio servico na partida, nao pelo
  `dotnet ef database update` do CI; Mongo sem autenticacao; um no de cada dependencia e uma replica por servico (o sweeper multi-replica nao foi exercitado).
- Prazos de 48 h/72 h nao esperam o relogio: o vencimento foi forcado por `UPDATE` (cenario 03).
- Os gauges da saga (`saga_por_estado`, `saga_compensacao_presa`) nao foram conferidos: so existem com `Otel__Endpoint` configurado.
- A versao do RabbitMQ do e2e (`4.4.0-management`) e a dos testes e dos composes; a do AKS vem de `OficinaMecanica.Infra/helm/variables.tf` e nao e conferida aqui.
- O build usa o working tree de cada repositorio, com alteracoes ainda nao commitadas (listadas em "Estado testado"); a evidencia vale para esse estado exato.

## Docker antes e depois

```text
Comparacao feita com listas salvas ANTES de qualquer comando desta rodada (docker ps -a, volume ls, network ls, image ls)
e DEPOIS de 'bash e2e/derrubar.sh --imagens' (docker compose -p oficina-e2e down -v + remocao das 3 imagens oficina-e2e/*:local):

containers (docker ps -a): 16 antes, 16 depois; ids, nomes, imagens e status identicos
volumes: 131 antes, 131 depois; lista identica
redes: 8 antes, 8 depois; identicas
imagens: 41 antes, 41 depois; identicas.
  (o compose puxou o tag python:3.12.16-alpine, que aponta para a mesma imagem e0646... ja existente como python:3.12-alpine; removi so esse tag extra,
   que era meu: 'docker image rm python:3.12.16-alpine' fez Untagged e a imagem continua com o tag original)

Restos do projeto oficina-e2e: containers=0 volumes=0 redes=0 imagens oficina-e2e/*=0

docker ps -a agora (containers antigos do usuario continuam como estavam):
buildx_buildkit_desktop-linux  Up 8 hours
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
