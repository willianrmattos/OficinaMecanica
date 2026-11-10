#!/usr/bin/env bash
# Cenario 07 - RESILIENCIA DO BROKER: paro o RabbitMQ no meio do fluxo. Com o outbox transacional os servicos continuam
# ACEITANDO escrita (a mensagem espera no banco) e /health/ready fica 200 com o broker Degraded; quando o broker volta
# os tres servicos reconectam sozinhos e o fluxo retoma sem perder mensagem.
#  Queda 1: antes de abrir a OS (a OrdemDeServicoAberta fica no outbox do OS Service).
#  Queda 2: com a OS em AguardandoAprovacao (a decisao do cliente fica no outbox do OS Service).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

garantir_rabbit_de_pe() { docker start "$(cid_de rabbitmq)" >/dev/null 2>&1; return 0; }
trap 'garantir_rabbit_de_pe; rm -rf "$TMPD"' EXIT

rabbit_subir() {
  local t0
  t0=$(date +%s)
  docker start "$(cid_de rabbitmq)" >/dev/null
  esperar_valor "management do RabbitMQ no ar" 120 200 \
    bash -c "curl -s -o /dev/null -w '%{http_code}' --max-time 3 -u '$E2E_RABBIT_USER:$E2E_RABBIT_PASSWORD' '$RABBIT_URL/api/overview'" || return 1
  local fila
  for fila in os-saga billing-comandos execucao-comandos; do
    esperar_valor "consumidor de $fila reconectado" 120 1 fila_consumidores "$fila" || return 1
  done
  info "tempo ate os tres servicos reconectarem: $(( $(date +%s) - t0 )) s"
}

cenario() {
  titulo "Cenario 07 - resiliencia com o RabbitMQ fora"
  pre_condicoes

  passo "cadastros"
  criar_massa G 10

  passo "QUEDA 1: para o RabbitMQ (docker stop) e abre a OS"
  docker stop "$(cid_de rabbitmq)" >/dev/null
  esperar_valor "RabbitMQ fora (management responde 000)" 20 000 \
    bash -c "curl -s -o /dev/null -w '%{http_code}' --max-time 2 '$RABBIT_URL/api/overview' || true"
  abrir_os G
  local os="$G_OS_ID"
  # O resultado da prontidao fica em cache por 5 s (Health__ProntidaoCacheSegundos), entao espero o rebaixamento aparecer.
  local svc url
  for svc in OS:$OS_URL Billing:$BILLING_URL Execucao:$EXECUCAO_URL; do
    url="${svc#*:}"
    esperar_valor "${svc%%:*} /health/ready com o broker fora: 200 e corpo com Degraded" 30 "200 degradado" \
      bash -c "r=\$(curl -s -w ' %{http_code}' --max-time 5 '$url/health/ready'); c=\${r##* }; case \"\$r\" in *Degraded*) echo \"\$c degradado\" ;; *) echo \"\$c saudavel\" ;; esac" || true
  done
  http GET "$OS_URL/health/ready" -
  info "prontidao do OS: ${HTTP_BODY:0:260}"
  esperar_valor "OrdemDeServicoAberta retida no outbox do OS (linhas de OutboxMessage)" 20 1 \
    sql_os "SELECT COUNT(*) FROM OutboxMessage" || true
  manter_valor "ordem na Execucao (ainda inexistente)" 6 404 exec_status "$os"
  afirmar_igual "status da OS (aceita e gravada)" "Recebida" "$(os_status "$os")"
  # A OS ainda nao foi enfileirada na Execucao (o OrdemDeServicoAberta esta no outbox): a rota sincrona cai no fallback
  # "saga" com o que o proprio OS sabe, sem inventar producao.
  http GET "$OS_URL/api/ordens-de-servico/$os/execucao" "$TOKEN_ADMIN"
  esperar_http "rota sincrona /execucao com a OS ainda nao enfileirada" 200
  afirmar_igual "origem da resposta (fallback)" "saga" "$(J origem)"
  afirmar_igual "motivoDoFallback (Execucao respondeu 404)" "ProducaoNaoEnfileirada" "$(J motivoDoFallback)"
  afirmar_igual "statusDaOrdem" "Recebida" "$(J statusDaOrdem)"
  afirmar_vazio "estadoDaSaga (a saga ainda nao existe)" "$(J estadoDaSaga)"
  afirmar_igual "producao (nada inventado no fallback)" "" "$(J producao)"

  passo "RabbitMQ volta: os servicos reconectam e a ordem chega na Execucao"
  rabbit_subir || return 1
  esperar_valor "ordem de producao na Execucao" 90 Fila exec_status "$os" || return 1
  esperar_valor "estado da saga" 30 AguardandoDiagnostico saga_estado "$os" || return 1

  passo "fluxo normal ate AguardandoAprovacao"
  reivindicar_diagnostico "$os"
  esperar_valor "status da OS" 60 EmDiagnostico os_status "$os" || return 1
  concluir_diagnostico "$os"
  esperar_valor "status da OS" 60 AguardandoAprovacao os_status "$os" || return 1

  passo "QUEDA 2: para o RabbitMQ de novo e o cliente aprova o orcamento com o broker fora"
  docker stop "$(cid_de rabbitmq)" >/dev/null
  esperar_valor "RabbitMQ fora (management responde 000)" 20 000 \
    bash -c "curl -s -o /dev/null -w '%{http_code}' --max-time 2 '$RABBIT_URL/api/overview' || true"
  aprovar_orcamento "$os"
  esperar_http "aprovacao aceita com o broker fora (decisao vai para o outbox)" 202
  manter_valor "status da OS (decisao ainda nao chegou na saga)" 6 AguardandoAprovacao os_status "$os"
  afirmar_numero "decisao retida no outbox do OS" ge 1 "$(sql_os "SELECT COUNT(*) FROM OutboxMessage")"
  afirmar_igual "estoque ainda intacto" "10" "$(peca_estoque "$G_PECA_ID")"

  passo "RabbitMQ volta: a decisao e entregue e o fluxo segue ate Finalizada"
  rabbit_subir || return 1
  esperar_valor "status da OS" 90 AguardandoPagamento os_status "$os" || return 1
  esperar_valor "estoque reservado" 30 8 peca_estoque "$G_PECA_ID" || return 1
  esperar_preenchido "link de pagamento" 60 os_link_pagamento "$os" || return 1
  pagar_pelo_gateway_fake "$os"
  esperar_valor "ordem liberada para reparo" 60 AguardandoExecucao exec_status "$os" || return 1
  reivindicar_reparo "$os"
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$os/reparo/finalizar" "$TOKEN_MEC1" '{"resumo":"Servico concluido apos quedas do broker."}'
  esperar_http "finalizacao do reparo" 200
  esperar_valor "status da OS" 60 Finalizada os_status "$os" || return 1
  esperar_valor "estado da saga" 30 Concluida saga_estado "$os" || return 1
  os_carregar "$os"
  afirmar_igual "historico de status (cada transicao uma vez)" \
    "Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada" "$(Jhistorico)"
  afirmar_igual "orcamentos e pagamentos no Billing" "1|1" "$(billing_contar Orcamentos "$os")|$(billing_contar Pagamentos "$os")"
  afirmar_igual "estoque final (baixa unica)" "8" "$(peca_estoque "$G_PECA_ID")"
  registrar_estoque "$G_PECA_ID" 8 "cenario 07: OS finalizada apos duas quedas do RabbitMQ"
  esperar_valor "e-mails do cliente (5 transicoes + 1 link, sem repeticao)" 30 6 mail_total "$G_EMAIL" || true

  passo "nada preso"
  verificar_outbox_vazio
  verificar_sem_erros
}

rodar_cenario "07-resiliencia-broker-fora" cenario
