#!/usr/bin/env bash
# Biblioteca dos cenarios e2e: chamadas HTTP, esperas com teto, asserts, observadores (SQL, MongoDB, RabbitMQ,
# Mailpit) e o cadastro de massa de dados. So le e escreve no que e do projeto oficina-e2e.
#
# Convencoes:
#  - toda espera tem teto e, ao estourar, imprime o que esperava e o ultimo valor visto (nada trava o run);
#  - assert que falha so marca o cenario como FALHOU e segue: o objetivo e coletar o maximo de evidencia;
#  - funcoes que preenchem variaveis globais (HTTP_STATUS, HTTP_BODY, <PREFIXO>_*) nao rodam em $(...), que perderia
#    o estado; as que so devolvem um valor para esperar_valor rodam em subshell de proposito.

CEN_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib-ambiente.sh
source "$CEN_DIR/../lib-ambiente.sh"
carregar_env

FERR="$AQUI/scripts/ferramentas.py"
OS_URL="http://127.0.0.1:$E2E_PORTA_OS"
BILLING_URL="http://127.0.0.1:$E2E_PORTA_BILLING"
EXECUCAO_URL="http://127.0.0.1:$E2E_PORTA_EXECUCAO"
RABBIT_URL="http://127.0.0.1:$E2E_PORTA_RABBIT_UI"
MAILPIT_URL="http://127.0.0.1:$E2E_PORTA_MAILPIT_UI"
LEDGER="$GERADO/ledger-estoque.tsv"

# Valores de negocio usados em todas as OS: total esperado do orcamento = servico + pecas.
PRECO_SERVICO="150.00"
PRECO_PECA="50.00"
QTD_PECA=2
TOTAL_ESPERADO="250"

TMPD="$(mktemp -d)"
TMPD_WIN="$(win "$TMPD")"
trap 'rm -rf "$TMPD"' EXIT

ASSERTS=0
FALHAS=0
INICIO_CENARIO=$(date +%s)

# ---------------------------------------------------------------------------------------------- saida

agora_rel() { echo "+$(( $(date +%s) - INICIO_CENARIO ))s"; }
titulo() { echo; echo "### $*"; }
passo() { echo; echo "-- [$(agora_rel)] $*"; }
info() { echo "  $*"; }

aprovar() { ASSERTS=$((ASSERTS + 1)); echo "  [OK]    $*"; }
reprovar() { ASSERTS=$((ASSERTS + 1)); FALHAS=$((FALHAS + 1)); echo "  [FALHA] $*"; }

afirmar_igual() { # descricao esperado obtido
  if [ "$2" = "$3" ]; then aprovar "$1 = '$3'"; else reprovar "$1: esperado '$2', obtido '$3'"; fi
}
afirmar_diferente() { # descricao nao_esperado obtido
  if [ "$2" != "$3" ]; then aprovar "$1 ('$3' != '$2')"; else reprovar "$1: nao deveria ser '$2'"; fi
}
afirmar_contem() { # descricao texto trecho
  case "$2" in *"$3"*) aprovar "$1 contem '$3'" ;; *) reprovar "$1: '$2' nao contem '$3'" ;; esac
}
afirmar_vazio() { # descricao valor
  if [ -z "$2" ]; then aprovar "$1 vazio"; else reprovar "$1: esperado vazio, obtido '$2'"; fi
}
afirmar_nao_vazio() { # descricao valor
  if [ -n "$2" ]; then aprovar "$1 preenchido ('$2')"; else reprovar "$1: esperado preenchido, veio vazio"; fi
}
afirmar_numero() { # descricao operador esperado obtido  (operadores do test sem o hifen: eq ge le gt lt)
  local op="-${2#-}"
  if test "$4" "$op" "$3" 2>/dev/null; then aprovar "$1: $4 ${2#-} $3"; else reprovar "$1: obtido '$4', esperado ${2#-} $3"; fi
}

# esperar_valor <descricao> <teto_s> <esperado> <comando...>
# Repete o comando (1 s entre tentativas) ate imprimir exatamente o valor esperado; ao estourar o teto reprova com o
# ultimo valor visto e devolve 1, para o cenario poder abortar com "|| return 1".
esperar_valor() {
  local desc="$1" teto="$2" esperado="$3"; shift 3
  local ini ultimo=""
  ini=$(date +%s)
  while :; do
    ultimo="$("$@" 2>/dev/null | tr -d '\r' || true)"
    if [ "$ultimo" = "$esperado" ]; then
      aprovar "$desc = '$esperado' (apos $(( $(date +%s) - ini )) s)"
      return 0
    fi
    if [ $(( $(date +%s) - ini )) -ge "$teto" ]; then
      reprovar "TIMEOUT de ${teto}s esperando $desc = '$esperado' (ultimo valor visto: '$ultimo')"
      return 1
    fi
    sleep 1
  done
}

# esperar_preenchido <descricao> <teto_s> <comando...>: espera o comando imprimir qualquer valor nao vazio (fica em ULTIMO_VALOR).
esperar_preenchido() {
  local desc="$1" teto="$2"; shift 2
  local ini
  ini=$(date +%s)
  ULTIMO_VALOR=""
  while :; do
    ULTIMO_VALOR="$("$@" 2>/dev/null | tr -d '\r' || true)"
    if [ -n "$ULTIMO_VALOR" ]; then
      aprovar "$desc preenchido (apos $(( $(date +%s) - ini )) s): '${ULTIMO_VALOR:0:80}'"
      return 0
    fi
    if [ $(( $(date +%s) - ini )) -ge "$teto" ]; then
      reprovar "TIMEOUT de ${teto}s esperando $desc (continua vazio)"
      return 1
    fi
    sleep 1
  done
}

# Variante para "nao deve acontecer": observa por <janela_s> e reprova se o valor sair do esperado.
manter_valor() { # descricao janela_s esperado comando...
  local desc="$1" janela="$2" esperado="$3"; shift 3
  local ini visto
  ini=$(date +%s)
  while [ $(( $(date +%s) - ini )) -lt "$janela" ]; do
    visto="$("$@" 2>/dev/null | tr -d '\r' || true)"
    if [ "$visto" != "$esperado" ]; then
      reprovar "$desc mudou para '$visto' (esperado '$esperado' durante ${janela}s)"
      return 1
    fi
    sleep 1
  done
  aprovar "$desc continuou '$esperado' por ${janela}s"
}

# ---------------------------------------------------------------------------------------------- HTTP

# http <METODO> <url> [token|-] [corpo-json]  ->  HTTP_STATUS e HTTP_BODY
http() {
  local metodo="$1" url="$2" token="${3:--}" corpo="${4:-}"
  local args=(-sS -X "$metodo" --max-time 30 -o "$TMPD_WIN/resp" -D "$TMPD_WIN/hdr" -w '%{http_code}' -H 'Accept: application/json')
  [ "$token" != "-" ] && args+=(-H "Authorization: Bearer $token")
  if [ -n "$corpo" ]; then
    printf '%s' "$corpo" > "$TMPD/req"
    args+=(-H 'Content-Type: application/json' --data-binary "@$TMPD_WIN/req")
  fi
  HTTP_STATUS="$(curl "${args[@]}" "$url" 2>"$TMPD/curl-err")" || HTTP_STATUS="000"
  HTTP_BODY="$(tr -d '\r' < "$TMPD/resp" 2>/dev/null || true)"
  HTTP_HEADERS="$(tr -d '\r' < "$TMPD/hdr" 2>/dev/null || true)"
}

# webhook_mercadopago <data.id> <x-request-id> <x-signature|-> [tipo]: entrega crua na rota real do Billing.
webhook_mercadopago() {
  local data_id="$1" req_id="$2" assinatura="$3" tipo="${4:-payment}"
  local args=(-sS -X POST --max-time 30 -o "$TMPD_WIN/resp" -D "$TMPD_WIN/hdr" -w '%{http_code}' -H 'Content-Type: application/json' -H "x-request-id: $req_id")
  [ "$assinatura" != "-" ] && args+=(-H "x-signature: $assinatura")
  printf '{"type":"%s","action":"payment.updated","data":{"id":"%s"}}' "$tipo" "$data_id" > "$TMPD/req"
  args+=(--data-binary "@$TMPD_WIN/req")
  HTTP_STATUS="$(curl "${args[@]}" "$BILLING_URL/webhooks/mercadopago?data.id=$data_id&type=$tipo" 2>"$TMPD/curl-err")" || HTTP_STATUS="000"
  HTTP_BODY="$(tr -d '\r' < "$TMPD/resp" 2>/dev/null || true)"
  HTTP_HEADERS="$(tr -d '\r' < "$TMPD/hdr" 2>/dev/null || true)"
}
assinar_webhook() { "$PY" "$FERR" assinar-webhook "$E2E_FAKE_WEBHOOK_SECRET" "$@"; }

J() { printf '%s' "$HTTP_BODY" | "$PY" "$FERR" jget "$1"; }          # campo do HTTP_BODY
Jlen() { printf '%s' "$HTTP_BODY" | "$PY" "$FERR" jlen "$1"; }
Jfind() { printf '%s' "$HTTP_BODY" | "$PY" "$FERR" jfind "$@"; }
Jhistorico() { printf '%s' "$HTTP_BODY" | "$PY" "$FERR" jhistorico; }
jval() { printf '%s' "$1" | "$PY" "$FERR" jget "$2"; }               # campo de um JSON qualquer

esperar_http() { # descricao esperado_status
  afirmar_igual "$1 (HTTP)" "$2" "$HTTP_STATUS"
}

# ---------------------------------------------------------------------------------------------- tokens

gerar_tokens() {
  TOKEN_ADMIN="$("$PY" "$FERR" emitir-token "$GERADO_WIN" admin --minutos 240)"
  TOKEN_MEC1="$("$PY" "$FERR" emitir-token "$GERADO_WIN" mecanico --minutos 240)"
  MEC1_SUB="$(printf '%s' "$TOKEN_MEC1" | cut -d. -f2 | "$PY" -c "
import sys,base64,json
s=sys.stdin.read().strip(); s+='='*(-len(s)%4); print(json.loads(base64.urlsafe_b64decode(s))['sub'])")"
}

# ---------------------------------------------------------------------------------------------- docker / observadores

cid_de() { # servico -> id do container (cache por execucao)
  local arq="$TMPD/cid-$1"
  [ -s "$arq" ] || compose ps -q "$1" | head -1 | tr -d '\r' > "$arq"
  cat "$arq"
}

docker_exec() { MSYS_NO_PATHCONV=1 docker exec "$@"; }

sql_em() { # banco usuario senha consulta
  docker_exec -e "SQLCMDPASSWORD=$3" "$(cid_de sqlserver)" /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$2" -d "$1" \
    -C -h -1 -W -s '|' -Q "SET NOCOUNT ON; $4" | tr -d '\r'
}
sql_os() { sql_em OficinaMecanicaDb app_os "$E2E_OS_DB_PASSWORD" "$1"; }
sql_billing() { sql_em BillingDb app_billing "$E2E_BILLING_DB_PASSWORD" "$1"; }

mongo_exec() { docker_exec "$(cid_de mongo)" mongosh --quiet execucao --eval "$1" | tr -d '\r'; }

rabbit_api() { curl -sS --max-time 10 -u "$E2E_RABBIT_USER:$E2E_RABBIT_PASSWORD" "$RABBIT_URL/api/$1"; }
fila_total() { rabbit_api "queues/%2F/$1" | "$PY" "$FERR" jget messages; }
fila_prontas() { rabbit_api "queues/%2F/$1" | "$PY" "$FERR" jget messages_ready; }
fila_consumidores() { rabbit_api "queues/%2F/$1" | "$PY" "$FERR" jget consumers; }
listar_filas() { # nome|mensagens|consumidores, uma por linha
  rabbit_api "queues/%2F" | "$PY" -c "
import sys, json
for q in sorted(json.load(sys.stdin), key=lambda q: q['name']):
    print(f\"{q['name']}|{q['messages']}|{q['consumers']}\")"
}

# Mailpit: resumo do texto de cada e-mail recebido por um destinatario (um por linha, do mais antigo ao mais novo).
# Todos os e-mails de status tem o mesmo assunto, entao o que os distingue e o trecho do corpo (Snippet).
mail_trechos() {
  curl -sS --max-time 10 "$MAILPIT_URL/api/v1/search?query=to:$1&limit=200" | "$PY" -c "
import sys, json
d = json.load(sys.stdin)
for m in sorted(d.get('messages', []), key=lambda m: m['Created']):
    print(m['Snippet'].replace('\n', ' ')[:230])"
}
mail_total() { mail_trechos "$1" | grep -c . || true; }

docker_logs() { docker logs "$(cid_de "$1")" 2>&1 | tr -d '\r'; }

# ---------------------------------------------------------------------------------------------- observadores de dominio

os_status() { http GET "$OS_URL/api/ordens-de-servico/$1" "$TOKEN_ADMIN"; jval "$HTTP_BODY" status; }
os_carregar() { http GET "$OS_URL/api/ordens-de-servico/$1" "$TOKEN_ADMIN"; }
saga_estado() { sql_os "SELECT CurrentState FROM OrdemDeServicoSagas WHERE CorrelationId = '$1'"; }
peca_estoque() { http GET "$OS_URL/api/pecas/$1" "$TOKEN_ADMIN"; jval "$HTTP_BODY" quantidadeEstoque; }
exec_status() { # status da ordem de producao ou "404"
  http GET "$EXECUCAO_URL/api/ordens-de-producao/$1" "$TOKEN_MEC1"
  if [ "$HTTP_STATUS" = "200" ]; then jval "$HTTP_BODY" status; else echo "$HTTP_STATUS"; fi
}
billing_orcamento_status() {
  http GET "$BILLING_URL/api/orcamentos/ordem-de-servico/$1" "$TOKEN_ADMIN"
  if [ "$HTTP_STATUS" = "200" ]; then jval "$HTTP_BODY" status; else echo "$HTTP_STATUS"; fi
}
billing_pagamento_status() {
  http GET "$BILLING_URL/api/pagamentos/ordem-de-servico/$1" "$TOKEN_ADMIN"
  if [ "$HTTP_STATUS" = "200" ]; then jval "$HTTP_BODY" status; else echo "$HTTP_STATUS"; fi
}
os_link_pagamento() { http GET "$OS_URL/api/ordens-de-servico/$1" "$TOKEN_ADMIN"; jval "$HTTP_BODY" linkPagamento; }
# Total de linhas de uma tabela do Billing/OS para uma OS (prova "sem duplicar").
billing_contar() { sql_billing "SELECT COUNT(*) FROM $1 WHERE OrdemDeServicoId = '$2'"; }

# ---------------------------------------------------------------------------------------------- massa de dados
# criar_massa <PREFIXO> <estoque_inicial>: cliente (com e-mail, para o Mailpit), veiculo, servico e peca.
# Preenche <PREFIXO>_CLIENTE_ID, _EMAIL, _VEICULO_ID, _SERVICO_ID, _PECA_ID, _ESTOQUE_INICIAL.
criar_massa() {
  local p="$1" estoque="$2" sufixo email cpf placa
  sufixo="$(date +%s)$RANDOM"
  email="cliente.$sufixo@e2e.local"
  cpf="$("$PY" "$FERR" cpf)"
  placa="$("$PY" "$FERR" placa)"

  http POST "$OS_URL/api/clientes" "$TOKEN_ADMIN" \
    "{\"nome\":\"Cliente E2E $sufixo\",\"documento\":\"$cpf\",\"email\":\"$email\",\"telefone\":\"11999990000\"}"
  esperar_http "cadastro do cliente $p" 201
  printf -v "${p}_CLIENTE_ID" '%s' "$(J id)"
  printf -v "${p}_EMAIL" '%s' "$email"

  local cid="${p}_CLIENTE_ID"
  http POST "$OS_URL/api/veiculos" "$TOKEN_ADMIN" \
    "{\"clienteId\":\"${!cid}\",\"placa\":\"$placa\",\"marca\":\"Fiat\",\"modelo\":\"Uno E2E\",\"ano\":2020}"
  esperar_http "cadastro do veiculo $p" 201
  printf -v "${p}_VEICULO_ID" '%s' "$(J id)"

  http POST "$OS_URL/api/servicos" "$TOKEN_ADMIN" \
    "{\"nome\":\"Revisao completa E2E $sufixo\",\"descricao\":\"Servico do teste ponta a ponta\",\"preco\":$PRECO_SERVICO,\"tempoEstimadoMinutos\":60}"
  esperar_http "cadastro do servico $p" 201
  printf -v "${p}_SERVICO_ID" '%s' "$(J id)"

  http POST "$OS_URL/api/pecas" "$TOKEN_ADMIN" \
    "{\"nome\":\"Filtro E2E $sufixo\",\"descricao\":\"Peca do teste ponta a ponta\",\"codigoReferencia\":\"E2E-$sufixo\",\"precoUnitario\":$PRECO_PECA,\"quantidadeEstoque\":$estoque,\"estoqueMinimo\":1}"
  esperar_http "cadastro da peca $p (estoque $estoque)" 201
  printf -v "${p}_PECA_ID" '%s' "$(J id)"
  printf -v "${p}_ESTOQUE_INICIAL" '%s' "$estoque"
}

# abrir_os <PREFIXO>: abre a OS com 1 servico e QTD_PECA pecas. Preenche <PREFIXO>_OS_ID e _OS_NUMERO.
abrir_os() {
  local p="$1" c v s pe
  c="${p}_CLIENTE_ID"; v="${p}_VEICULO_ID"; s="${p}_SERVICO_ID"; pe="${p}_PECA_ID"
  http POST "$OS_URL/api/ordens-de-servico" "$TOKEN_ADMIN" \
    "{\"clienteId\":\"${!c}\",\"veiculoId\":\"${!v}\",\"observacoes\":\"Barulho no motor e troca de filtro\",\"servicos\":[{\"servicoId\":\"${!s}\",\"quantidade\":1}],\"pecas\":[{\"pecaId\":\"${!pe}\",\"quantidade\":$QTD_PECA}]}"
  esperar_http "abertura da OS $p" 201
  printf -v "${p}_OS_ID" '%s' "$(J id)"
  printf -v "${p}_OS_NUMERO" '%s' "$(J numero)"
  afirmar_igual "status inicial da OS $p" "Recebida" "$(J status)"
  afirmar_igual "valor total da OS $p" "$TOTAL_ESPERADO" "$(J valorTotal | sed 's/\.0*$//')"
}

registrar_estoque() { # pecaId esperado descricao -> conferido no cenario final
  mkdir -p "$GERADO"
  printf '%s\t%s\t%s\n' "$1" "$2" "$3" >> "$LEDGER"
}

# ---------------------------------------------------------------------------------------------- passos da conversa

# Execucao: o mecanico reivindica o proximo da fila de diagnostico e confere que e a OS esperada.
reivindicar_diagnostico() { # os_id
  http POST "$EXECUCAO_URL/api/diagnosticos/proximo" "$TOKEN_MEC1"
  esperar_http "reivindicacao do diagnostico" 200
  afirmar_igual "ordem reivindicada (diagnostico)" "$1" "$(J id)"
  afirmar_igual "status na Execucao apos reivindicar" "EmDiagnostico" "$(J status)"
  afirmar_igual "mecanico responsavel = sub do token" "$MEC1_SUB" "$(J mecanicoId)"
}
concluir_diagnostico() { # os_id
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$1/diagnostico/concluir" "$TOKEN_MEC1" \
    '{"laudo":"Filtro de oleo saturado e correia dentada com folga; recomendo troca de ambos.","horasEstimadas":2.5}'
  esperar_http "conclusao do diagnostico" 200
  afirmar_igual "status na Execucao apos concluir" "DiagnosticoConcluido" "$(J status)"
}
reivindicar_reparo() { # os_id
  http POST "$EXECUCAO_URL/api/reparos/proximo" "$TOKEN_MEC1"
  esperar_http "reivindicacao do reparo" 200
  afirmar_igual "ordem reivindicada (reparo)" "$1" "$(J id)"
  afirmar_igual "status na Execucao apos reivindicar o reparo" "EmExecucao" "$(J status)"
}
aprovar_orcamento() { http POST "$OS_URL/api/ordens-de-servico/$1/aprovar" -; }
recusar_orcamento() { http POST "$OS_URL/api/ordens-de-servico/$1/recusar" - "{\"motivo\":\"$2\"}"; }

# pagar_pelo_gateway_fake <os_id>: simula o cliente pagando no Checkout fake. O Billing cria o pagamento do gateway
# e dispara o webhook, com assinatura valida, pelo mesmo command da rota real. Preenche PAG_PREF, PAG_GATEWAY_ID e
# PAG_REQUEST_ID.
pagar_pelo_gateway_fake() {
  local link tentativa
  link="$(os_link_pagamento "$1")"
  PAG_PREF="${link##*/}"
  # O contador de ids do gateway Fake vive na memoria do Billing e recomeca em 100000000001 a cada restart, enquanto o
  # BillingDb guarda os ids anteriores sob indice unico: depois de um restart a simulacao colide (409) ate o contador
  # passar dos ids ja usados. Repito a chamada (cada uma consome um id) em vez de esconder o fato: fica registrado.
  for tentativa in 1 2 3 4 5 6 7 8 9 10 11 12; do
    http POST "$BILLING_URL/dev/pagamentos/$PAG_PREF/aprovar" -
    [ "$HTTP_STATUS" = "409" ] || break
    info "[INFO]  tentativa $tentativa da simulacao: 409 (id do gateway Fake ja gravado no BillingDb; contador em memoria reiniciado pelo restart do Billing)"
  done
  esperar_http "simulacao de aprovacao no gateway Fake" 200
  PAG_GATEWAY_ID="$(J gatewayPaymentId)"
  PAG_REQUEST_ID="$(J requestId)"
  afirmar_igual "status do pagamento no gateway" "approved" "$(J status)"
}

# levar_ate_aguardando_aprovacao <PREFIXO>: abre a OS, o mecanico reivindica e conclui o diagnostico e o Billing gera o orcamento.
levar_ate_aguardando_aprovacao() {
  local p="$1" os
  abrir_os "$p"
  os="${p}_OS_ID"; os="${!os}"
  esperar_valor "ordem de producao da OS $p na Execucao" 60 Fila exec_status "$os" || return 1
  reivindicar_diagnostico "$os"
  esperar_valor "status da OS $p (DiagnosticoIniciado)" 60 EmDiagnostico os_status "$os" || return 1
  concluir_diagnostico "$os"
  esperar_valor "status da OS $p (orcamento gerado)" 60 AguardandoAprovacao os_status "$os" || return 1
  esperar_valor "orcamento da OS $p no Billing" 30 Gerado billing_orcamento_status "$os" || return 1
}

# levar_ate_aguardando_pagamento <PREFIXO>: aprova o orcamento e espera o estoque reservado e o link de pagamento na OS.
levar_ate_aguardando_pagamento() {
  local p="$1" os peca estoque
  os="${p}_OS_ID"; os="${!os}"
  peca="${p}_PECA_ID"; peca="${!peca}"
  estoque="${p}_ESTOQUE_INICIAL"; estoque="${!estoque}"
  aprovar_orcamento "$os"
  esperar_http "aprovacao do orcamento da OS $p" 202
  esperar_valor "status da OS $p" 60 AguardandoPagamento os_status "$os" || return 1
  esperar_valor "estoque da peca $p reservado ($estoque - $QTD_PECA)" 30 $(( estoque - QTD_PECA )) peca_estoque "$peca" || return 1
  esperar_preenchido "link de pagamento da OS $p" 60 os_link_pagamento "$os" || return 1
  esperar_valor "pagamento da OS $p no Billing" 30 Pendente billing_pagamento_status "$os" || return 1
}

# Pre-condicao: filas de producao vazias e as tres APIs de pe, para a reivindicacao FIFO pegar a OS do cenario.
pre_condicoes() {
  local api
  for api in "OS:$OS_URL" "Billing:$BILLING_URL" "Execucao:$EXECUCAO_URL"; do
    esperar_valor "${api%%:*} /health/ready" 120 200 \
      bash -c "curl -s -o /dev/null -w '%{http_code}' --max-time 4 '${api#*:}/health/ready'"
  done
  http GET "$EXECUCAO_URL/api/fila/resumo" "$TOKEN_MEC1"
  afirmar_igual "fila de diagnostico da Execucao vazia (pre-condicao)" "0" "$(J porStatus.Fila)"
  afirmar_igual "fila de reparo da Execucao vazia (pre-condicao)" "0" "$(J porStatus.AguardandoExecucao)"
}

# Soma das mensagens paradas nas filas *_error (deve ser 0 depois de qualquer cenario sem falha injetada).
erros_total() {
  listar_filas | awk -F'|' '$1 ~ /_error$/ { soma += $2 } END { print soma + 0 }'
}
verificar_sem_erros() {
  afirmar_igual "mensagens em filas *_error" "0" "$(erros_total)"
}
# Espera o outbox transacional de um dos bancos SQL esvaziar (nada preso esperando entrega ao broker).
verificar_outbox_vazio() {
  esperar_valor "outbox do OS (OutboxMessage) vazio" 30 0 sql_os "SELECT COUNT(*) FROM OutboxMessage"
  esperar_valor "outbox do Billing (OutboxMessage) vazio" 30 0 sql_billing "SELECT COUNT(*) FROM OutboxMessage"
  esperar_valor "outbox da Execucao (outbox.messages) vazio" 30 0 mongo_exec "db.getCollection('outbox.messages').countDocuments({})"
}

# ---------------------------------------------------------------------------------------------- encerramento

# rodar_cenario <nome> <funcao>: roda a funcao do cenario; se ela abortar (return 1 depois de uma espera estourada) o
# cenario e FALHOU mesmo que nenhum assert tenha sido contado depois do aborto.
rodar_cenario() {
  "$2"
  local rc=$?
  if [ "$rc" -ne 0 ]; then
    if [ "$FALHAS" -eq 0 ]; then reprovar "cenario interrompido (codigo $rc) sem assert que explique"; else info "[INFO]  cenario interrompido no ponto da primeira falha"; fi
  fi
  finalizar_cenario "$1"
}

# finalizar_cenario <nome>: imprime a linha que o runner le e sai com 0 (passou) ou 1 (falhou).
finalizar_cenario() {
  local dur=$(( $(date +%s) - INICIO_CENARIO )) resultado=PASSOU
  [ "$FALHAS" -eq 0 ] || resultado=FALHOU
  echo
  echo "RESULTADO|$1|$resultado|asserts=$ASSERTS|falhas=$FALHAS|tempo=${dur}s"
  [ "$FALHAS" -eq 0 ]
}

gerar_tokens
