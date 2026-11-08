#!/usr/bin/env bash
# Cenario 06 - IDEMPOTENCIA: reenviar a mesma decisao do cliente e entregar o mesmo webhook de pagamento mais de uma vez
# nao pode duplicar efeito (um estoque reservado, um pagamento, uma aprovacao, uma notificacao). Cobre tambem as
# respostas documentadas da rota de webhook (200 duplicado, 401 assinatura ruim, 503 pagamento desconhecido).
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

cenario() {
  titulo "Cenario 06 - idempotencia"
  pre_condicoes

  passo "cadastros e OS ate AguardandoAprovacao"
  criar_massa F 10
  levar_ate_aguardando_aprovacao F || return 1
  local os="$F_OS_ID"

  passo "decisao duplicada: duas aprovacoes disparadas ao mesmo tempo (rota anonima)"
  curl -s -o /dev/null -w '%{http_code}' -X POST "$OS_URL/api/ordens-de-servico/$os/aprovar" > "$TMPD/aprov1" &
  curl -s -o /dev/null -w '%{http_code}' -X POST "$OS_URL/api/ordens-de-servico/$os/aprovar" > "$TMPD/aprov2" &
  wait
  local c1 c2 aceitas=0
  c1="$(cat "$TMPD/aprov1")"; c2="$(cat "$TMPD/aprov2")"
  info "respostas das duas aprovacoes simultaneas: $c1 e $c2"
  for c in "$c1" "$c2"; do
    case "$c" in
      202) aceitas=$((aceitas + 1)); aprovar "aprovacao respondeu 202 (decisao publicada para a saga)" ;;
      409) aprovar "aprovacao respondeu 409 (a saga ja tinha avancado)" ;;
      *) reprovar "aprovacao simultanea respondeu '$c' (esperado 202 ou 409)" ;;
    esac
  done
  afirmar_numero "ao menos uma aprovacao aceita (202)" ge 1 "$aceitas"
  esperar_valor "status da OS" 60 AguardandoPagamento os_status "$os" || return 1
  manter_valor "estoque reservado UMA vez (10 - 2)" 6 8 peca_estoque "$F_PECA_ID"
  registrar_estoque "$F_PECA_ID" 8 "cenario 06: OS finalizada, estoque baixado uma vez"
  esperar_preenchido "link de pagamento" 60 os_link_pagamento "$os" || return 1
  afirmar_igual "pagamentos criados no Billing" "1" "$(billing_contar Pagamentos "$os")"
  afirmar_igual "orcamentos no Billing" "1" "$(billing_contar Orcamentos "$os")"
  info "[INFO]  avisos 'Decisao ... ignorada' da saga no log do OS para esta OS: $(docker_logs os | grep -c "Decisão ClienteAprovouOrcamento da ordem de serviço $os ignorada" || true) (so existem se a 2a aprovacao chegou depois de a saga avancar)"

  passo "reenvio da mesma decisao depois de a saga avancar: 409 e nada muda"
  aprovar_orcamento "$os"
  esperar_http "aprovar de novo (OS em AguardandoPagamento)" 409
  recusar_orcamento "$os" "Mudei de ideia"
  esperar_http "recusar depois de aprovar" 409
  afirmar_igual "OS segue AguardandoPagamento" "AguardandoPagamento" "$(os_status "$os")"
  afirmar_igual "estoque segue reservado uma vez" "8" "$(peca_estoque "$F_PECA_ID")"

  passo "webhook: primeira entrega (aprovacao via gateway Fake)"
  pagar_pelo_gateway_fake "$os"
  local gw="$PAG_GATEWAY_ID" req="$PAG_REQUEST_ID"
  info "pagamento do gateway $gw, x-request-id $req"
  esperar_valor "status da OS" 60 EmExecucao os_status "$os" || return 1
  esperar_valor "ordem liberada para reparo" 60 AguardandoExecucao exec_status "$os" || return 1

  passo "webhook DUPLICADO: a mesma entrega (mesmo x-request-id, assinatura valida) chega de novo na rota real"
  webhook_mercadopago "$gw" "$req" "$(assinar_webhook "$gw" "$req")"
  esperar_http "reentrega do mesmo webhook" 200
  afirmar_igual "registros do x-request-id no Billing (deduplicado)" "1" "$(sql_billing "SELECT COUNT(*) FROM WebhooksRecebidos WHERE RequestId = '$req'")"
  afirmar_igual "tentativas de pagamento (uma so, aprovada)" "1" \
    "$(sql_billing "SELECT COUNT(*) FROM TentativasPagamento t JOIN Pagamentos p ON p.Id = t.PagamentoId WHERE p.OrdemDeServicoId = '$os'")"

  passo "mesmo pagamento notificado de novo com OUTRO x-request-id (reentrega do Mercado Pago)"
  local req2="reentrega-$RANDOM-$RANDOM"
  webhook_mercadopago "$gw" "$req2" "$(assinar_webhook "$gw" "$req2")"
  esperar_http "novo x-request-id para o mesmo pagamento" 200
  afirmar_igual "tentativas de pagamento continuam 1" "1" \
    "$(sql_billing "SELECT COUNT(*) FROM TentativasPagamento t JOIN Pagamentos p ON p.Id = t.PagamentoId WHERE p.OrdemDeServicoId = '$os'")"
  afirmar_igual "pagamento continua Aprovado (uma aprovacao)" "Aprovado" "$(billing_pagamento_status "$os")"
  manter_valor "OS continua EmExecucao" 4 EmExecucao os_status "$os"
  http GET "$EXECUCAO_URL/api/fila/resumo" "$TOKEN_MEC1"
  afirmar_igual "uma unica ordem esperando reparo na Execucao" "1" "$(J porStatus.AguardandoExecucao)"

  passo "respostas documentadas da rota de webhook"
  webhook_mercadopago "$gw" "req-assinatura-ruim" "ts=$(date +%s)000,v1=$(printf '0%.0s' $(seq 1 64))"
  esperar_http "assinatura invalida" 401
  webhook_mercadopago "$gw" "req-sem-assinatura" "-"
  esperar_http "sem x-signature" 401
  local req3="req-vencida-$RANDOM"
  webhook_mercadopago "$gw" "$req3" "$(assinar_webhook "$gw" "$req3" "$(( ($(date +%s) - 900) * 1000 ))")"
  esperar_http "assinatura valida mas fora da tolerancia de tempo (15 min)" 401
  local req4="req-desconhecido-$RANDOM"
  webhook_mercadopago "999999999999" "$req4" "$(assinar_webhook 999999999999 "$req4")"
  esperar_http "pagamento que o gateway ainda nao conhece (assinatura valida)" 503
  afirmar_contem "cabecalho Retry-After do 503" "$(printf '%s' "$HTTP_HEADERS" | tr 'A-Z' 'a-z')" "retry-after"
  local req5="req-outro-topico-$RANDOM"
  webhook_mercadopago "$gw" "$req5" "$(assinar_webhook "$gw" "$req5")" "merchant_order"
  esperar_http "topico diferente de payment (confirmado e ignorado)" 200

  passo "reparo e fim: reentrega tardia do webhook nao reabre nada"
  reivindicar_reparo "$os"
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$os/reparo/finalizar" "$TOKEN_MEC1" '{"resumo":"Servico concluido."}'
  esperar_http "finalizacao do reparo" 200
  esperar_valor "status da OS" 60 Finalizada os_status "$os" || return 1
  esperar_valor "estado da saga" 30 Concluida saga_estado "$os" || return 1
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$os/reparo/finalizar" "$TOKEN_MEC1" '{"resumo":"Servico concluido."}'
  if [ "$HTTP_STATUS" -ge 400 ] && [ "$HTTP_STATUS" -lt 500 ]; then
    aprovar "segunda finalizacao do reparo recusada com 4xx ($HTTP_STATUS): $(J message | cut -c1-120)"
  else
    reprovar "segunda finalizacao do reparo respondeu $HTTP_STATUS (esperado 4xx, nunca 2xx duplicado nem 5xx)"
  fi
  local req6="req-tardia-$RANDOM"
  webhook_mercadopago "$gw" "$req6" "$(assinar_webhook "$gw" "$req6")"
  esperar_http "webhook reentregue depois da OS finalizada" 200
  manter_valor "OS continua Finalizada" 4 Finalizada os_status "$os"
  os_carregar "$os"
  afirmar_igual "historico de status (cada transicao uma vez)" \
    "Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada" "$(Jhistorico)"
  afirmar_igual "estoque final (baixa unica)" "8" "$(peca_estoque "$F_PECA_ID")"
  esperar_valor "e-mails do cliente (5 transicoes + 1 link)" 30 6 mail_total "$F_EMAIL" || true
  afirmar_igual "e-mails de 'Em execucao' (uma notificacao so)" "1" \
    "$(mail_trechos "$F_EMAIL" | grep -c 'status atualizado para: Em execução' || true)"

  passo "nada preso"
  verificar_outbox_vazio
  verificar_sem_erros
}

rodar_cenario "06-idempotencia" cenario
