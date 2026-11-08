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
- Os tres repositorios lado a lado: `<Pos>/OficinaMecanica` (ou este worktree em `<Pos>/_worktrees/os-e2e`),
  `<Pos>/OficinaMecanica.Billing` e `<Pos>/OficinaMecanica.Execucao`. Outro caminho: `E2E_CONTEXTO_OS`,
  `E2E_CONTEXTO_BILLING`, `E2E_CONTEXTO_EXECUCAO`.
- ~4 GB de RAM livres para os containers (o SQL Server fica limitado a 1,5 GB) e portas livres (tabela abaixo).
- A primeira subida constroi as tres imagens a partir dos `Dockerfile` de cada repositorio (minutos); as seguintes usam
  o cache.

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
| RabbitMQ UI/management | 15673 | `rabbitmq:15672` | `E2E_PORTA_RABBIT_UI` |
| Mailpit UI/API | 18025 | `mailpit:8025` | `E2E_PORTA_MAILPIT_UI` |
| Mailpit SMTP | 11025 | `mailpit:1025` | `E2E_PORTA_SMTP` |

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
  intervalo do sweeper cai de 30 s para 5 s. `Execucao__BaseUrl` esta configurado como no manifest do AKS, mas ainda
  nao tem leitor neste commit (consulta sincrona e da F3c).
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
| `01-caminho-feliz` | cadastros -> OS -> diagnostico (Execucao) -> orcamento (Billing) -> aprovacao anonima (202) -> estoque reservado -> link de pagamento -> webhook Fake -> reparo -> OS Finalizada; saga, historico, estoque, e-mails, rota sincrona de execucao (se existir) |
| `02-recusa-do-orcamento` | recusa (202) -> saga `OrcamentoRecusado`, orcamento cancelado no Billing com o motivo, sem pagamento, estoque intacto; 409 nas decisoes seguintes |
| `03-pagamento-expirado` | vencimento forcado no banco -> `PrazoExpirado` -> `CancelarPagamento` -> estoque devolvido -> OS Cancelada; depois aprovacao **tardia** do gateway: estorno automatico e a saga ignora o `PagamentoEstornado` |
| `04-execucao-inviavel-com-estorno` | `reparo/inviabilizar` -> estorno no Billing -> estoque so volta depois do `PagamentoEstornado` (prova com o Billing congelado por `docker pause`) -> OS Cancelada |
| `05-resiliencia-billing-fora` | Billing parado (`docker stop`) e morto (`docker kill`) no meio de dois fluxos; OS e Execucao seguem; mensagem duplicada no broker; ao voltar: um orcamento, um pagamento, uma baixa de estoque e um e-mail por transicao |
| `06-idempotencia` | aprovacao dupla simultanea, reenvio de decisao (409), webhook duplicado (mesmo e outro `x-request-id`), 401/503/200 da rota de webhook, segunda finalizacao do reparo |
| `07-resiliencia-broker-fora` | RabbitMQ parado antes de abrir a OS e com a decisao do cliente em voo: o outbox segura, `/health/ready` fica 200 com broker `Degraded`, os servicos reconectam e o fluxo conclui |
| `99-verificacoes-finais` | filas `*_error` vazias (API de management), nenhum erro/`Fault` inesperado nos logs, sagas/producao/pagamentos sem nada preso, outbox das tres bases vazio, containers sem restart, estoque de cada peca conferido |

**Esperas**: toda espera tem teto (30 a 120 s) e, ao estourar, imprime o que esperava e o ultimo valor visto; cada cenario
tem ainda um teto rigido de 900 s no runner. Nada trava.

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
tipo `.cs` copiado seja igual ao do OS (os `.cs` do Billing e da Execucao estao com CRLF no working tree e os do OS com
LF: o script compara o hash dos bytes e, se so o fim de linha difere, aceita com aviso), (3) cada servico tenha todas as
mensagens do seu lado da conversa e (4) o que Billing e Execucao compartilham seja identico entre si.

## Arquivos e evidencia

| Arquivo | Para que serve |
|---|---|
| `docker-compose.e2e.yml`, `sql/init.sql` | o ambiente (project `oficina-e2e`) e a criacao dos dois bancos com usuario por banco |
| `subir.sh`, `derrubar.sh`, `lib-ambiente.sh` | subir (gera chaves/senhas, constroi, espera), derrubar so este projeto, e o atalho `compose` |
| `emitir-token.sh`, `scripts/ferramentas.py` | JWT RS256 de DEV, JWKS, assinatura do webhook, CPF/placa validos e leitura de JSON (so biblioteca padrao + `openssl`) |
| `executar-cenarios.sh`, `cenarios/` | o runner, a biblioteca (`lib.sh`) e os cenarios `01` a `07` mais `99-verificacoes-finais` |
| `verificar-contratos.sh` | confere os goldens e os tipos de contrato entre os tres repositorios |
| `evidencias/AAAA-MM-DD-execucao.md` | saida real de uma execucao completa, defeitos encontrados e estado do Docker antes/depois (`...-saida.txt` e o log bruto) |
| `.gerado/` (ignorado pelo git) | par RSA, JWKS, `e2e.env` com as senhas de DEV sorteadas e os logs de cada execucao |

Uma execucao completa (7 cenarios + verificacoes finais) leva cerca de 7 minutos depois do build; o `subir.sh` com cache leva ~30 s.

## Inspecao manual

```bash
source e2e/lib-ambiente.sh && carregar_env && source e2e/.gerado/e2e.env
# SQL (dentro do container; senha pelo ambiente do sqlcmd)
docker exec -e SQLCMDPASSWORD="$E2E_OS_DB_PASSWORD" $(compose ps -q sqlserver) /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U app_os -d OficinaMecanicaDb -C -Q "SELECT CorrelationId, CurrentState FROM OrdemDeServicoSagas"
# MongoDB
docker exec $(compose ps -q mongo) mongosh --quiet execucao --eval "db.ordens_producao.find({_t:'ordem'},{status:1}).toArray()"
# Filas e e-mails
curl -s -u "$E2E_RABBIT_USER:$E2E_RABBIT_PASSWORD" http://127.0.0.1:15673/api/queues/%2F | head -c 600
xdg-open http://127.0.0.1:18025   # Mailpit (no Windows: start http://127.0.0.1:18025)
```

## Limitacoes

- **Seguranca nao incluido**: o JWKS e um stub estatico e o token vem de `emitir-token.sh`. O e2e prova a **validacao**
  de JWT via JWKS nos tres servicos, nao a **emissao** (login/Key Vault) nem a rotacao de chave.
- **Gateway Fake**: o estado dos pagamentos vive na memoria do Billing e o contador de ids recomeca em
  `100000000001` a cada restart, enquanto o `BillingDb` guarda os ids anteriores sob indice unico: depois de um restart a
  simulacao de aprovacao pode responder 409 ate o contador passar dos ids ja gravados. Os cenarios repetem a chamada e
  registram cada colisao como `[INFO]` (ver o relatorio de evidencia). Nao ha Mercado Pago real nem assinatura de
  producao.
- **Development, nao Production**: obrigatorio para o gateway Fake; as migrations sao aplicadas pelo proprio servico na
  partida, nao pelo passo `dotnet ef database update` do CI. Mongo sem autenticacao e SQL com `TrustServerCertificate`.
- **Um unico no de cada dependencia**, sem HPA, sem replicas do OS (o sweeper multi-replica nao e exercitado) e sem
  APIM/ingress: as APIs sao chamadas direto.
- **Prazos de negocio nao esperam o relogio**: o vencimento de 48 h/72 h e forcado no banco.
- A rota sincrona `GET /api/ordens-de-servico/{id}/execucao` (F3c) nao existe neste commit; o cenario 01 registra o 404.
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
