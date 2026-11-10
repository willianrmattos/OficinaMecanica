#!/usr/bin/env bash
# Cenario 04 - EXECUCAO INVIAVEL COM ESTORNO: o pagamento ja foi aprovado e o mecanico declara o reparo inviavel.
# A saga manda EstornarPagamento ao Billing e so devolve o estoque e cancela a OS DEPOIS do PagamentoEstornado.
# Para provar essa ordem eu congelo o Billing (docker pause) no meio da compensacao: enquanto ele nao responde, a saga
# fica em Estornando, o estoque continua reservado e a OS continua EmExecucao. Optei por pause (e nao stop) porque o
# gateway Fake vive na memoria do Billing e um restart esqueceria o pagamento a estornar.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

BILLING_PAUSADO=0
retomar_billing() { [ "$BILLING_PAUSADO" -eq 1 ] && docker unpause "$(cid_de billing)" >/dev/null 2>&1 && BILLING_PAUSADO=0; return 0; }
trap 'retomar_billing; rm -rf "$TMPD"' EXIT

cenario() {
  titulo "Cenario 04 - execucao inviavel com estorno"
  pre_condicoes

  passo "cadastros e OS ate EmExecucao (pagamento aprovado pelo gateway Fake, reparo reivindicado)"
  criar_massa D 10
  levar_ate_aguardando_aprovacao D || return 1
  levar_ate_aguardando_pagamento D || return 1
  local os="$D_OS_ID"
  pagar_pelo_gateway_fake "$os"
  esperar_valor "status da OS" 60 EmExecucao os_status "$os" || return 1
  esperar_valor "ordem liberada para reparo" 60 AguardandoExecucao exec_status "$os" || return 1
  reivindicar_reparo "$os"
  afirmar_igual "estoque reservado durante a execucao" "8" "$(peca_estoque "$D_PECA_ID")"

  # Base lida antes e incremento conferido logo depois: as series do Meter da saga somem do /metrics 5 min depois da ultima atualizacao.
  local cancel0
  cancel0="$(os_metrica saga_cancelada estado_origem=Estornando)"

  passo "congela o Billing (docker pause) e o mecanico declara a execucao inviavel"
  docker pause "$(cid_de billing)" >/dev/null && BILLING_PAUSADO=1
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$os/reparo/inviabilizar" "$TOKEN_MEC1" \
    '{"motivo":"Bloco do motor trincado: reparo economicamente inviavel."}'
  esperar_http "inviabilizacao da execucao" 200
  afirmar_igual "status na Execucao" "Inviavel" "$(J status)"
  esperar_valor "estado da saga (ExecucaoInviavel -> EstornarPagamento enviado)" 60 Estornando saga_estado "$os" || return 1

  passo "com o Billing congelado: a compensacao NAO pode avancar (estoque preso, OS ainda EmExecucao)"
  manter_valor "estado da saga" 10 Estornando saga_estado "$os"
  manter_valor "status da OS" 1 EmExecucao os_status "$os"
  afirmar_igual "estoque ainda reservado (so volta depois do PagamentoEstornado)" "8" "$(peca_estoque "$D_PECA_ID")"
  afirmar_numero "EstornarPagamento (unica mensagem) esperando o Billing na fila billing-comandos" eq 1 "$(fila_total billing-comandos)"
  info "billing-comandos: total=$(fila_total billing-comandos) prontas=$(fila_prontas billing-comandos)"

  passo "descongela o Billing: estorno no gateway Fake -> PagamentoEstornado -> estoque liberado -> OS Cancelada"
  docker unpause "$(cid_de billing)" >/dev/null && BILLING_PAUSADO=0
  esperar_valor "estado da saga (final)" 90 Cancelada saga_estado "$os" || return 1
  esperar_valor "status da OS" 30 Cancelada os_status "$os" || return 1
  esperar_valor "estoque liberado (10)" 30 10 peca_estoque "$D_PECA_ID" || return 1
  afirmar_numero "metrica saga_cancelada{estado_origem=Estornando} subiu >= 1" ge "$((cancel0 + 1))" "$(os_metrica saga_cancelada estado_origem=Estornando)"
  registrar_estoque "$D_PECA_ID" 10 "cenario 04: execucao inviavel, estoque liberado apos o estorno"

  passo "efeitos no Billing e nos e-mails"
  esperar_valor "pagamento no Billing" 30 Estornado billing_pagamento_status "$os" || return 1
  http GET "$BILLING_URL/api/pagamentos/ordem-de-servico/$os" "$TOKEN_ADMIN"
  afirmar_igual "estorno concluido" "Concluido" "$(J estorno.status)"
  afirmar_contem "id do estorno no gateway Fake" "$(J estorno.gatewayRefundId)" "fake-refund-"
  afirmar_igual "um unico estorno para o pagamento" "1" "$(sql_billing "SELECT COUNT(*) FROM Estornos e JOIN Pagamentos p ON p.Id = e.PagamentoId WHERE p.OrdemDeServicoId = '$os'")"
  os_carregar "$os"
  afirmar_igual "historico de status" \
    "Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Cancelada" "$(Jhistorico)"
  afirmar_contem "e-mails do cliente" "$(mail_trechos "$D_EMAIL")" "Cancelada"
  esperar_valor "ordem de producao na Execucao" 10 Inviavel exec_status "$os" || true

  passo "nada preso"
  verificar_outbox_vazio
  verificar_sem_erros
}

rodar_cenario "04-execucao-inviavel-com-estorno" cenario
