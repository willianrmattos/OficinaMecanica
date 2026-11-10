# e2e - teste ponta a ponta local dos tres microsservicos

Sobe o **OS Service**, o **Billing** e a **Execucao** conversando de verdade na maquina local, com RabbitMQ, SQL Server
e MongoDB (replica set) reais e o gateway de pagamento **Fake** do Billing, e roda cenarios com asserts que percorrem a
Saga inteira: caminho feliz, recusa do orcamento, pagamento expirado, execucao inviavel com estorno, queda do Billing,
queda do RabbitMQ e idempotencia. O que fica aqui dentro (`e2e/`) nao altera o codigo de nenhum servico.

## O que o ambiente prova e o que ele NAO prova

Prova: o fluxo de negocio atravessando os tres servicos pelo broker real (comandos e eventos, outbox/inbox
transacionais, retomada depois de queda), a validacao de JWT RS256 via JWKS pelos tres servicos, o e-mail ao cliente, o
estoque baixado/devolvido de verdade e as compensacoes da saga.

**Nao prova** (ver "Limitacoes"): a emissao do token (o `OficinaMecanica.Seguranca` **nao** sobe: ele e uma Azure
Function com Azurite e Key Vault), o Mercado Pago real, o Azure SQL/MongoDB Atlas, o AKS, o APIM e o CI que aplica as
migrations em producao.

## Pre-requisitos

- Docker Desktop ligado, com `docker compose` v2 (testado com Docker 29 e Compose 5).
- Git Bash no Windows (ou qualquer bash), com `curl`, `openssl`, `sha256sum` e `timeout`; **Python 3.10+** no PATH
  (so biblioteca padrao). Nao precisa de `jq`, .NET SDK nem `kubectl`.
- Os tres repositorios lado a lado, no layout do ecossistema: `E:\FIAP\Pos\OficinaMecanica` (este checkout, onde fica o
  `e2e/`), `E:\FIAP\Pos\OficinaMecanica.Billing` e `E:\FIAP\Pos\OficinaMecanica.Execucao`. O `lib-ambiente.sh` procura os
  irmaos ao lado da raiz do OS e, se este `e2e/` estiver num worktree (`<Pos>/_worktrees/<nome>/e2e`), dois niveis acima.
  Outro caminho: `E2E_CONTEXTO_OS`, `E2E_CONTEXTO_BILLING`, `E2E_CONTEXTO_EXECUCAO`.
- ~4 GB de RAM livres para os containers (o SQL Server fica limitado a 1,5 GB) e portas livres (tabela abaixo).
- A primeira subida constroi as tres imagens a partir dos `Dockerfile` de cada repositorio (minutos); as seguintes usam
  o cache. **O build usa o working tree de cada repositorio** (o que esta no disco, commitado ou nao), nao um commit
  especifico: para saber o que foi testado, registre `git rev-parse HEAD` e `git status` dos tres antes de rodar.
  O `.dockerignore` do OS exclui `e2e/`, entao `e2e/.gerado/` (chave de DEV e logs) nao vai no contexto enviado ao
  builder local; alem disso o `Dockerfile` so copia `src/` e os `.csproj`, entao nada disso entra na imagem.

## Isolamento (nada do que ja existe na maquina e tocado)

- Compose proprio (`docker-compose.e2e.yml`) com **project name fixo `oficina-e2e`**, rede propria
  (`oficina-e2e-net`), volumes nomeados proprios (`oficina-e2e_sqlserver_data`, `_mongo_data`, `_rabbitmq_data`),
  **sem `container_name`** e imagens com tag propria (`oficina-e2e/os|billing|execucao:local`).
- Os composes dos repositorios nao sao usados (eles fixam `container_name` e portas padrao).
- Portas do host publicadas **so em 127.0.0.1** e fora das portas padrao.
- `derrubar.sh` usa sempre `-p oficina-e2e`: so recursos deste projeto sao removidos.
- Segredos de DEV (senhas do SQL, do RabbitMQ, segredo do webhook Fake) e o par RSA sao **gerados no primeiro `subir.sh`**
  em `e2e/.gerado/` (`.gitignore` da pasta); nada disso vai para o repositorio.

## Como subir e derrubar

Layout de uso: a partir da raiz do checkout principal do OS Service, com os repositorios irmaos ao lado.

```bash
cd /e/FIAP/Pos/OficinaMecanica   # (E:\FIAP\Pos\OficinaMecanica no Windows); Billing e Execucao em /e/FIAP/Pos/OficinaMecanica.Billing e .Execucao
git rev-parse --short HEAD; git status --short   # registre o que sera testado (o build usa o working tree), nos tres repositorios
```

```bash
bash e2e/subir.sh                # gera chaves/senhas, constroi as imagens, sobe e espera /health/ready das tres APIs
bash e2e/subir.sh --sem-build    # reaproveita as imagens ja construidas
bash e2e/executar-cenarios.sh    # roda todos os cenarios + verificacoes finais
bash e2e/derrubar.sh             # = docker compose -p oficina-e2e down -v --remove-orphans (so este projeto)
bash e2e/derrubar.sh --imagens   # idem, e remove as tres imagens oficina-e2e/*:local
```

Equivalente manual do derrubar: `docker compose -p oficina-e2e -f e2e/docker-compose.e2e.yml down -v`.

## Mapa de portas (127.0.0.1)

| Servico | Porta no host | Dentro da rede do compose | Variavel para trocar |
|---|---|---|---|
| OS Service | 18080 | `os:8080` | `E2E_PORTA_OS` |
| Billing | 18081 | `billing:8080` | `E2E_PORTA_BILLING` |
| Execucao | 18082 | `execucao:8080` | `E2E_PORTA_EXECUCAO` |
| JWKS stub | 18090 (`/.well-known/jwks.json`) | `jwks:80` | `E2E_PORTA_JWKS` |
| SQL Server | 14330 | `sqlserver:1433` | `E2E_PORTA_SQL` |
| MongoDB (rs0) | 27118 | `mongo:27017` | `E2E_PORTA_MONGO` |
| RabbitMQ AMQP | 56720 | `rabbitmq:5672` | `E2E_PORTA_AMQP` |
| RabbitMQ UI/management | 15683 | `rabbitmq:15672` | `E2E_PORTA_RABBIT_UI` |
| Mailpit UI/API | 18025 | `mailpit:8025` | `E2E_PORTA_MAILPIT_UI` |
| Mailpit SMTP | 11025 | `mailpit:1025` | `E2E_PORTA_SMTP` |

Nenhuma dessas portas colide com as publicadas pelos composes do OS, do Billing, da Execucao e do Seguranca (inclusive os brokers
`standalone`, que usam 5673 e 15673): `bash e2e/verificar-portas.sh` le as portas dos arquivos e reprova se houver colisao.

Usuarios/senhas (RabbitMQ, SQL `sa`, `app_os`, `app_billing`) e o segredo do webhook Fake estao em
`e2e/.gerado/e2e.env`, sorteados na primeira subida. Swagger de cada API: `http://127.0.0.1:<porta>/`.

## O que sobe

```
cenarios (curl) -> OS :18080 --------\                 /-> Billing :18081 -- SQL Server (BillingDb, usuario app_billing)
                   Billing :18081 ----+- RabbitMQ 4 ---+-> OS      :18080 -- SQL Server (OficinaMecanicaDb, usuario app_os)
                   Execucao :18082 --/   (os-saga,       \-> Execucao :18082 -- MongoDB 7 replica set rs0 (banco execucao)
                                          billing-comandos,
   JWKS stub :18090 <- os tres validam o JWT           execucao-comandos)           Mailpit <- e-mails do OS
```

- **sqlserver + sql-init**: um servidor, dois bancos (`OficinaMecanicaDb`, `BillingDb`) com **usuario contido por banco**
  (`app_os`, `app_billing`, `db_owner` so no proprio banco), o mesmo desenho do `OficinaMecanica.Banco/sql/criar-usuarios.sql`.
- **mongo + mongo-init**: replica set de 1 no (`rs0`, host `mongo:27017`), obrigatorio para as transacoes do outbox.
- **Servicos** em `Development`, com a mensageria **escrita por extenso** como em producao
  (`Mensageria__Transporte=RabbitMq`, `Mensageria__Persistencia=EntityFramework` no OS e no Billing,
  `Mensageria__Outbox=MongoDb` na Execucao) e `Saga__*` com os prazos reais (72 h aprovacao, 48 h pagamento); so o
  intervalo do sweeper cai de 30 s para 5 s. `Execucao__BaseUrl=http://execucao:8080` alimenta a consulta sincrona
  `GET /api/ordens-de-servico/{id}/execucao` do OS (que repassa o token do chamador a Execucao).
- **Por que Development**: o gateway Fake do Billing so existe em `Development` (em `Production` ele exige as credenciais
  do Mercado Pago). Como cada repositorio aplica o esquema do banco na partida **so em Development**, as migrations sao
  aplicadas pelo proprio servico ao subir (`dotnet ef database update`, que o CI faz em producao, **nao** e exercitado
  aqui; a Execucao cria os indices do MongoDB pelo `MongoInitializer`).
- **JWKS stub** (`python -m http.server` servindo `.gerado/jwks/jwks.json`) no lugar do Seguranca; issuer
  `OficinaMecanica.Seguranca` e audience `OficinaMecanica.Client`, os mesmos do Seguranca.
- **Mailpit** como SMTP do OS Service (o e-mail de cada transicao e o link de pagamento).

## Tokens (JWT RS256 de DEV)

```bash
TOKEN=$(bash e2e/emitir-token.sh admin)                 # admin | atendente | mecanico  [--sub <id>] [--minutos N]
curl -H "Authorization: Bearer $TOKEN" http://127.0.0.1:18080/api/clientes
curl -X POST -H "Authorization: Bearer $(bash e2e/emitir-token.sh mecanico)" http://127.0.0.1:18082/api/diagnosticos/proximo
```

O token imita o do Seguranca: `sub`, `name`, `role`, `jti`, `iat`, `exp`, `iss`, `aud`, header `alg=RS256`, `kid=e2e-dev-1`.
A chave privada fica em `.gerado/jwt-privada.pem` (nunca versionada) e o JWKS publico em `.gerado/jwks/jwks.json`.

**Sobre os papeis**: lendo os controllers dos tres servicos, **nenhum** exige papel: todos sao `[Authorize]` simples (e as
rotas de aprovar/recusar/consulta por numero, `AllowAnonymous`); o Seguranca real so emite o perfil `Admin`, e o
"mecanico" da Execucao e o claim `sub`. Por isso `atendente` e `mecanico` so mudam `role`/`sub` para identificar quem
chamou; se um dia surgirem politicas por papel, os nomes de claim/role a ajustar estao em `scripts/ferramentas.py`.

## Cenarios

`bash e2e/executar-cenarios.sh [--so 01,03] [--sem-finais]` roda tudo em ordem; cada cenario tambem roda sozinho
(`bash e2e/cenarios/03-pagamento-expirado.sh`). Cada um cria a propria massa (cliente, veiculo, servico, peca) e parte
de "fila de producao vazia".

| Cenario | O que valida |
|---|---|
| `01-caminho-feliz` | cadastros -> OS -> diagnostico (Execucao) -> orcamento (Billing) -> aprovacao anonima (202) -> estoque reservado -> link de pagamento -> webhook Fake -> reparo -> OS Finalizada; saga, estoque, e-mails; `historicoStatus` na ordem cronologica em que a API o devolve; rota sincrona `GET /api/ordens-de-servico/{id}/execucao` (`origem: execucao` em tres momentos do fluxo, 401 sem token, 404 de OS inexistente e fallback `origem: saga` / `ExecucaoIndisponivel` com a Execucao congelada) |
| `02-recusa-do-orcamento` | recusa (202) -> saga `OrcamentoRecusado`, orcamento cancelado no Billing com o motivo, sem pagamento, estoque intacto; 409 nas decisoes seguintes |
| `03-pagamento-expirado` | vencimento forcado no banco -> `PrazoExpirado` -> `CancelarPagamento` -> estoque devolvido -> OS Cancelada; depois aprovacao **tardia** do gateway: estorno automatico e a saga ignora o `PagamentoEstornado` |
| `04-execucao-inviavel-com-estorno` | `reparo/inviabilizar` -> estorno no Billing -> estoque so volta depois do `PagamentoEstornado` (prova com o Billing congelado por `docker pause`) -> OS Cancelada |
| `05-resiliencia-billing-fora` | Billing parado (`docker stop`) e morto (`docker kill`) no meio de dois fluxos; OS e Execucao seguem; mensagem duplicada no broker; ao voltar: um orcamento, um pagamento, uma baixa de estoque e um e-mail por transicao; a primeira aprovacao do gateway Fake depois do restart responde 200 (sem colisao de id) |
| `06-idempotencia` | aprovacao dupla simultanea, reenvio de decisao (409), webhook duplicado (mesmo e outro `x-request-id`), 401/503/200 da rota de webhook, segunda finalizacao do reparo |
| `07-resiliencia-broker-fora` | RabbitMQ parado antes de abrir a OS e com a decisao do cliente em voo: o outbox segura, `/health/ready` fica 200 com broker `Degraded`, a rota sincrona `/execucao` cai no fallback `saga` / `ProducaoNaoEnfileirada`, os servicos reconectam e o fluxo conclui |
| `99-verificacoes-finais` | filas `*_error` e filas de trabalho vazias, lidas de uma listagem **completa** da API de management (API fora do ar, JSON de erro ou menos de tres filas reprovam, nao viram "0"); logs lidos de fato (linhas e `Bus started`) sem `Fault` nem ERR inesperado: a Execucao sem nenhum ERR e com o Information `Requisicao cancelada pelo cliente` do cenario 01; sagas/producao/pagamentos sem nada preso, outbox das tres bases vazio, containers sem restart, estoque de cada peca conferido, metricas da saga no `/metrics` do OS (`oficinamecanica_saga_saga_*`) coerentes com o banco, sem id do gateway Fake repetido nem aviso `Overriding HTTP_PORTS` |

**Esperas**: toda espera tem teto e, ao estourar, imprime o que esperava e o ultimo valor visto. Os tetos usados sao 10, 15, 20,
30, 60, 90 e 120 s (a maioria, 30 ou 60 s; 90 e 120 s so para subida de servico e reconexao do broker). Alem deles: janelas de
observacao de 1 a 10 s ("nao pode mudar"), `--max-time` de 30 s nas chamadas HTTP e de 10 s na API de management, e um teto
rigido de 900 s por cenario no runner. Nada trava.

**Como ler o resultado**: linhas `[OK]`/`[FALHA]` sao asserts; `[INFO]` e observacao; cada cenario termina com
`RESULTADO|<nome>|PASSOU|FALHOU|asserts=N|falhas=M|tempo=Ts`; o runner fecha com um quadro-resumo e sai com codigo 0 so
se todos passaram. O log completo de cada execucao fica em `e2e/.gerado/logs/execucao-<data-hora>.log`. Uma
espera estourada reprova o cenario e o interrompe (os passos seguintes dependem dela); um assert que falha so reprova e
segue, para coletar o maximo de evidencia.

Forcar o vencimento do pagamento (cenario 03): o prazo real e de 48 h, entao o cenario recua `SagaPrazos.ExpiraEm` (OS)
e `Pagamentos.ExpiraEm` (Billing) por SQL e o sweeper (a cada 5 s) faz o resto.

## Verificar os contratos de mensagem

```bash
bash e2e/verificar-contratos.sh      # exit 0 = identicos; exit 1 = divergencia ou contrato faltando
```

O OS e a fonte da verdade (ADR 0005). Billing e Execucao copiam so o subconjunto que usam, entao a lista de arquivos nao
e igual nos tres; o script exige que (1) todo golden JSON copiado seja **byte a byte** igual ao do OS (sha256), (2) todo
tipo `.cs` copiado seja igual ao do OS (hoje os 29 tipos do OS, os 17 do Billing e os 11 da Execucao estao em LF, conforme `git ls-files --eol`; se um
repositorio voltar a ter CRLF no working tree, o script compara o hash dos bytes e, se so o fim de linha difere, aceita com aviso), (3) cada servico tenha todas as
mensagens do seu lado da conversa e (4) o que Billing e Execucao compartilham seja identico entre si.

## Arquivos e evidencia

| Arquivo | Para que serve |
|---|---|
| `docker-compose.e2e.yml`, `sql/init.sql` | o ambiente (project `oficina-e2e`) e a criacao dos dois bancos com usuario por banco |
| `subir.sh`, `derrubar.sh`, `lib-ambiente.sh` | subir (gera chaves/senhas, constroi, espera), derrubar so este projeto, e o atalho `compose` |
| `emitir-token.sh`, `scripts/ferramentas.py` | JWT RS256 de DEV, JWKS, assinatura do webhook, CPF/placa validos e leitura de JSON (so biblioteca padrao + `openssl`) |
| `executar-cenarios.sh`, `cenarios/` | o runner, a biblioteca (`lib.sh`) e os cenarios `01` a `07` mais `99-verificacoes-finais` |
| `verificar-contratos.sh` | confere os goldens e os tipos de contrato entre os tres repositorios |
| `verificar-portas.sh` | confere que as portas do e2e nao colidem com as dos composes dos repositorios nem entre si |
| `evidencias/2026-11-08-execucao.md` e `...-execucao-integrada.md` | registro historico das duas primeiras rodadas (saida real, defeitos achados e estado do Docker antes/depois; `...-saida.txt` e o log bruto) |
| `evidencias/2026-11-08-execucao-final.md` | evidencia da execucao completa mais recente, a partir do checkout principal (`...-final-saida.txt` e o log bruto) |
| `.gerado/` (ignorado pelo git) | par RSA, JWKS, `e2e.env` com as senhas de DEV sorteadas e os logs de cada execucao |

Uma execucao completa (7 cenarios + verificacoes finais) leva cerca de 6 minutos (medido: 5 min 20 s a 5 min 50 s); o `subir.sh` com
cache leva ~30 s. Para um ciclo do zero, na ordem: `bash e2e/derrubar.sh`, `bash e2e/subir.sh --regerar` (sorteia chaves e senhas novas
e reconstroi as imagens), `bash e2e/executar-cenarios.sh` e `bash e2e/verificar-contratos.sh`.

## Inspecao manual

```bash
source e2e/lib-ambiente.sh && carregar_env && source e2e/.gerado/e2e.env
# SQL (dentro do container; senha pelo ambiente do sqlcmd)
docker exec -e SQLCMDPASSWORD="$E2E_OS_DB_PASSWORD" $(compose ps -q sqlserver) /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U app_os -d OficinaMecanicaDb -C -Q "SELECT CorrelationId, CurrentState FROM OrdemDeServicoSagas"
# MongoDB
docker exec $(compose ps -q mongo) mongosh --quiet execucao --eval "db.ordens_producao.find({_t:'ordem'},{status:1}).toArray()"
# Filas e e-mails
curl -s -u "$E2E_RABBIT_USER:$E2E_RABBIT_PASSWORD" http://127.0.0.1:15683/api/queues/%2F | head -c 600
xdg-open http://127.0.0.1:18025   # Mailpit (no Windows: start http://127.0.0.1:18025)
```

## Limitacoes

- **Seguranca nao incluido**: o JWKS e um stub estatico e o token vem de `emitir-token.sh`. O e2e prova a **validacao**
  de JWT via JWKS nos tres servicos, nao a **emissao** (login/Key Vault) nem a rotacao de chave.
- **Gateway Fake**: preferencias e pagamentos vivem na memoria do Billing e somem num restart (por isso os cenarios de
  queda so criam e pagam o pagamento depois da ultima subida). O id de cada pagamento do gateway parte do relogio a cada
  partida (16 digitos, crescente entre partidas), porque o `BillingDb` guarda o id com indice unico e sobrevive ao processo.
  Nao ha Mercado Pago real nem assinatura de producao.
- **Development, nao Production**: obrigatorio para o gateway Fake; as migrations sao aplicadas pelo proprio servico na
  partida, nao pelo passo `dotnet ef database update` do CI. Mongo sem autenticacao e SQL com `TrustServerCertificate`.
- **Versao do RabbitMQ**: o e2e roda `rabbitmq:4.4.0-management`, a mesma dos testes de infraestrutura e dos composes dos
  repositorios; confira a versao do broker do AKS em `OficinaMecanica.Infra/helm/variables.tf` (o e2e nao a le nem a garante).
- **Imagens fixadas**: `mssql/server:2022-latest@sha256:d38c2a64...` (o digest que os testes de infraestrutura do OS usam; o tag
  e movel e nenhum tag de patch tem esse digest), `python:3.12.16-alpine` (patch de mesmo digest da imagem local), `mongo:7.0.45`,
  `rabbitmq:4.4.0-management` e `mailpit:v1.31.0`. As imagens dos servicos usam as bases dos `Dockerfile` de cada repositorio.
- **Um unico no de cada dependencia**, sem HPA, sem replicas do OS (o sweeper multi-replica nao e exercitado) e sem
  APIM/ingress: as APIs sao chamadas direto.
- **Prazos de negocio nao esperam o relogio**: o vencimento de 48 h/72 h e forcado no banco.
- **Metricas da saga**: os contadores do Meter `OficinaMecanica.Saga` aparecem no `/metrics` do OS (prometheus-net); os
  gauges (`saga_por_estado`, `saga_compensacao_presa`) so existem com `Otel__Endpoint` configurado (o e2e deixa vazio),
  entao nao sao conferidos aqui. Os contadores podem ficar a maior em retry de commit (ADR 0008): os asserts usam `>=`.
  O adapter do prometheus-net so observa os Meters a partir da **primeira** leitura do `/metrics` (incrementos anteriores
  nao aparecem, como ja registra o CLAUDE.md do Billing): `subir.sh` e `executar-cenarios.sh` fazem essa leitura antes de
  qualquer fluxo. Se subir o ambiente na mao, leia `GET /metrics` do OS uma vez antes de abrir a primeira OS. O mesmo adapter
  remove uma serie **5 minutos depois da ultima atualizacao** (`MetricsExpireAfter`): por isso os cenarios 01 a 04 e 06 leem a
  base de cada contador antes do fluxo e conferem o incremento logo depois, e o 99 so confere (contra o banco) as series que o
  ultimo cenario acabou de atualizar. Rode o 99 em ate 5 minutos do ultimo fluxo.
- Reivindicacao de fila e FIFO global: os cenarios assumem a fila de producao vazia no inicio (checado como
  pre-condicao) e nao devem rodar em paralelo entre si.

## Solucao de problemas

- `subir.sh` reclama de porta em uso: outro processo usa uma das portas da tabela; defina `E2E_PORTA_*` e suba de novo
  (as portas devem ser as mesmas em `subir.sh` e nos cenarios, entao exporte a variavel nos dois).
- Cenario travado em pre-condicao ("fila ... vazia"): sobrou uma ordem em `Fila` ou `AguardandoExecucao` de uma
  execucao interrompida; `bash e2e/derrubar.sh && bash e2e/subir.sh --sem-build` recomeca do zero.
- Um cenario interrompido no meio (Ctrl+C) pode deixar o Billing ou o RabbitMQ parado: `docker start` neles (ou
  derrube e suba de novo). Os cenarios 04, 05 e 07 restauram o que pararam ao sair normalmente.
- Mudou o codigo de um servico: `bash e2e/subir.sh` (sem `--sem-build`) reconstroi so o que mudou.
