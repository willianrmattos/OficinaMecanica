#!/usr/bin/env bash
# Cenario 05 - RESILIENCIA: derrubo o Billing no meio de dois fluxos (A e B) em dois momentos diferentes, deixo o OS
# Service e a Execucao seguirem sozinhos e subo o Billing de novo. O outbox/inbox transacional tem que entregar tudo
# depois, sem perder mensagem e sem duplicar efeito (um orcamento, um pagamento, uma baixa de estoque e um e-mail por
# transicao, por OS).
#  Queda 1 (docker stop, saida limpa): enquanto o Billing esta fora o diagnostico das duas OS e concluido, entao os dois
#           GerarOrcamento esperam na fila billing-comandos.
#  Queda 2 (docker kill, SIGKILL = processo que morre): as duas OS sao aprovadas com o Billing fora, entao os dois
#           SolicitarPagamento esperam na fila.
# O gateway Fake e em memoria: por isso o pagamento so e criado (e pago) depois da ultima subida do Billing.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

garantir_billing_de_pe() {
  docker unpause "$(cid_de billing)" >/dev/null 2>&1
  docker start "$(cid_de billing)" >/dev/null 2>&1
  return 0
}
trap 'garantir_billing_de_pe; rm -rf "$TMPD"' EXIT

billing_subir() {
  docker start "$(cid_de billing)" >/dev/null
  esperar_valor "Billing /health/ready depois de subir" 120 200 \
    bash -c "curl -s -o /dev/null -w '%{http_code}' --max-time 4 '$BILLING_URL/health/ready'" || return 1
  esperar_valor "consumidor do billing-comandos reconectado" 60 1 fila_consumidores billing-comandos
}

cenario() {
  titulo "Cenario 05 - resiliencia com o Billing fora"
  pre_condicoes

  passo "cadastros (duas OS independentes, A e B, cada uma com a sua peca)"
  criar_massa A 10
  criar_massa B 10

  passo "QUEDA 1: para o Billing (docker stop) antes de abrir as OS"
  docker stop "$(cid_de billing)" >/dev/null
  esperar_valor "consumidores do billing-comandos" 30 0 fila_consumidores billing-comandos || return 1
  esperar_valor "Billing inacessivel (curl devolve 000)" 10 000 \
    bash -c "curl -s -o /dev/null -w '%{http_code}' --max-time 2 '$BILLING_URL/health' || true"

  passo "OS e Execucao seguem sozinhos: abrem as OS, o mecanico diagnostica as duas"
  abrir_os A
  esperar_valor "ordem A na Execucao" 60 Fila exec_status "$A_OS_ID" || return 1
  abrir_os B
  esperar_valor "ordem B na Execucao" 60 Fila exec_status "$B_OS_ID" || return 1
  reivindicar_diagnostico "$A_OS_ID"
  concluir_diagnostico "$A_OS_ID"
  reivindicar_diagnostico "$B_OS_ID"
  concluir_diagnostico "$B_OS_ID"
  esperar_valor "saga A" 60 GerandoOrcamento saga_estado "$A_OS_ID" || return 1
  esperar_valor "saga B" 60 GerandoOrcamento saga_estado "$B_OS_ID" || return 1

  passo "com o Billing fora: nada avanca, as mensagens esperam no broker e a API do OS continua servindo"
  esperar_valor "GerarOrcamento x2 esperando na billing-comandos" 30 2 fila_total billing-comandos || return 1
  manter_valor "status da OS A" 6 EmDiagnostico os_status "$A_OS_ID"
  manter_valor "status da OS B" 1 EmDiagnostico os_status "$B_OS_ID"
  http GET "$OS_URL/api/pecas/$A_PECA_ID" "$TOKEN_ADMIN"
  esperar_http "OS Service consultando peca com o Billing fora" 200
  criar_massa Z 5
  http GET "$EXECUCAO_URL/api/fila/resumo" "$TOKEN_MEC1"
  esperar_http "Execucao respondendo com o Billing fora" 200
  http GET "$OS_URL/health/ready" -
  afirmar_igual "OS /health/ready (so o banco derruba; o Billing nao e dependencia)" "200" "$HTTP_STATUS"

  passo "entrega DUPLICADA no broker: copio (mesmo messageId) um GerarOrcamento direto na fila, como um at-least-once faria"
  local dup_id
  dup_id="$("$PY" "$FERR" rabbit-duplicar "$RABBIT_URL" "$E2E_RABBIT_USER" "$E2E_RABBIT_PASSWORD" billing-comandos)"
  afirmar_nao_vazio "messageId da mensagem duplicada" "$dup_id"
  esperar_valor "billing-comandos com 3 mensagens (2 originais + 1 copia)" 15 3 fila_total billing-comandos || return 1

  passo "Billing volta: a fila drena e cada OS ganha UM orcamento (a copia e descartada pelo inbox)"
  billing_subir || return 1
  esperar_valor "billing-comandos drenada" 60 0 fila_total billing-comandos || return 1
  # ReceiveCount conta cada recebimento do mesmo messageId, inclusive as retentativas que o inbox faz quando as duas copias
  # disputam a mesma linha; o que prova a deduplicacao e a linha ter sido consumida uma unica vez.
  afirmar_numero "InboxState do Billing: ReceiveCount do messageId copiado (copia + retentativas do inbox)" ge 2 \
    "$(sql_billing "SELECT MAX(ReceiveCount) FROM InboxState WHERE MessageId = '$dup_id'")"
  afirmar_igual "InboxState do Billing: o messageId copiado foi CONSUMIDO uma vez (linhas com Consumed preenchido)" "1" \
    "$(sql_billing "SELECT COUNT(*) FROM InboxState WHERE MessageId = '$dup_id' AND Consumed IS NOT NULL")"
  esperar_valor "status da OS A" 60 AguardandoAprovacao os_status "$A_OS_ID" || return 1
  esperar_valor "status da OS B" 60 AguardandoAprovacao os_status "$B_OS_ID" || return 1
  afirmar_igual "orcamentos da OS A no Billing" "1" "$(billing_contar Orcamentos "$A_OS_ID")"
  afirmar_igual "orcamentos da OS B no Billing" "1" "$(billing_contar Orcamentos "$B_OS_ID")"
  afirmar_igual "orcamento A" "Gerado" "$(billing_orcamento_status "$A_OS_ID")"
  afirmar_igual "orcamento B" "Gerado" "$(billing_orcamento_status "$B_OS_ID")"

  passo "QUEDA 2: mata o Billing (docker kill) e aprova as duas OS com ele fora"
  docker kill "$(cid_de billing)" >/dev/null
  esperar_valor "consumidores do billing-comandos" 30 0 fila_consumidores billing-comandos || return 1
  aprovar_orcamento "$A_OS_ID"; esperar_http "aprovacao da OS A com o Billing fora" 202
  aprovar_orcamento "$B_OS_ID"; esperar_http "aprovacao da OS B com o Billing fora" 202
  esperar_valor "status da OS A" 60 AguardandoPagamento os_status "$A_OS_ID" || return 1
  esperar_valor "status da OS B" 60 AguardandoPagamento os_status "$B_OS_ID" || return 1
  esperar_valor "estoque da peca A reservado" 30 8 peca_estoque "$A_PECA_ID" || return 1
  esperar_valor "estoque da peca B reservado" 30 8 peca_estoque "$B_PECA_ID" || return 1
  esperar_valor "SolicitarPagamento x2 esperando na billing-comandos" 30 2 fila_total billing-comandos || return 1
  manter_valor "link de pagamento da OS A ausente" 5 "" os_link_pagamento "$A_OS_ID"

  passo "Billing volta (processo que tinha morrido): cada OS ganha UM pagamento e um link"
  billing_subir || return 1
  esperar_preenchido "link de pagamento da OS A" 90 os_link_pagamento "$A_OS_ID" || return 1
  esperar_preenchido "link de pagamento da OS B" 90 os_link_pagamento "$B_OS_ID" || return 1
  esperar_valor "billing-comandos drenada" 60 0 fila_total billing-comandos || return 1
  afirmar_igual "pagamentos da OS A no Billing" "1" "$(billing_contar Pagamentos "$A_OS_ID")"
  afirmar_igual "pagamentos da OS B no Billing" "1" "$(billing_contar Pagamentos "$B_OS_ID")"
  afirmar_igual "estoque da peca A (uma unica baixa)" "8" "$(peca_estoque "$A_PECA_ID")"
  afirmar_igual "estoque da peca B (uma unica baixa)" "8" "$(peca_estoque "$B_PECA_ID")"

  passo "fluxo retomado: pagamento e reparo das duas OS ate Finalizada"
  pagar_pelo_gateway_fake "$A_OS_ID"
  esperar_valor "ordem A liberada para reparo" 60 AguardandoExecucao exec_status "$A_OS_ID" || return 1
  pagar_pelo_gateway_fake "$B_OS_ID"
  esperar_valor "ordem B liberada para reparo" 60 AguardandoExecucao exec_status "$B_OS_ID" || return 1
  reivindicar_reparo "$A_OS_ID"
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$A_OS_ID/reparo/finalizar" "$TOKEN_MEC1" '{"resumo":"Servico A concluido."}'
  esperar_http "finalizacao do reparo A" 200
  reivindicar_reparo "$B_OS_ID"
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$B_OS_ID/reparo/finalizar" "$TOKEN_MEC1" '{"resumo":"Servico B concluido."}'
  esperar_http "finalizacao do reparo B" 200
  esperar_valor "status da OS A" 60 Finalizada os_status "$A_OS_ID" || return 1
  esperar_valor "status da OS B" 60 Finalizada os_status "$B_OS_ID" || return 1
  esperar_valor "saga A" 30 Concluida saga_estado "$A_OS_ID" || return 1
  esperar_valor "saga B" 30 Concluida saga_estado "$B_OS_ID" || return 1

  passo "sem perder e sem duplicar: contagens finais"
  local p os pc ec
  for p in A B; do
    os="${p}_OS_ID"; os="${!os}"
    pc="${p}_PECA_ID"; pc="${!pc}"
    ec="${p}_EMAIL"; ec="${!ec}"
    os_carregar "$os"
    afirmar_igual "historico da OS $p (cada transicao uma vez)" \
      "Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada" "$(Jhistorico)"
    afirmar_igual "orcamentos da OS $p" "1" "$(billing_contar Orcamentos "$os")"
    afirmar_igual "pagamentos da OS $p" "1" "$(billing_contar Pagamentos "$os")"
    afirmar_igual "tentativas de pagamento da OS $p" "1" \
      "$(sql_billing "SELECT COUNT(*) FROM TentativasPagamento t JOIN Pagamentos pg ON pg.Id = t.PagamentoId WHERE pg.OrdemDeServicoId = '$os'")"
    afirmar_igual "pagamento da OS $p" "Aprovado" "$(billing_pagamento_status "$os")"
    afirmar_igual "orcamento da OS $p" "Pago" "$(billing_orcamento_status "$os")"
    afirmar_igual "estoque da peca $p (baixa unica, definitiva)" "8" "$(peca_estoque "$pc")"
    registrar_estoque "$pc" 8 "cenario 05: OS $p finalizada apos quedas do Billing"
    esperar_valor "e-mails do cliente $p (5 transicoes + 1 link, sem repeticao)" 30 6 mail_total "$ec" || true
    afirmar_igual "e-mails de Finalizada do cliente $p" "1" \
      "$(mail_trechos "$ec" | grep -c 'status atualizado para: Finalizada' || true)"
  done
  registrar_estoque "$Z_PECA_ID" 5 "cenario 05: massa extra criada com o Billing fora (sem OS)"

  passo "nada preso"
  verificar_outbox_vazio
  verificar_sem_erros
  listar_filas | while IFS='|' read -r fila msgs cons; do info "fila $fila: mensagens=$msgs consumidores=$cons"; done
}

rodar_cenario "05-resiliencia-billing-fora" cenario
