#!/usr/bin/env bash
# Cenario 02 - RECUSA DO ORCAMENTO: o cliente recusa na rota anonima; a saga encerra em OrcamentoRecusado, o Billing
# cancela o orcamento, nenhum pagamento e criado e o estoque nunca sai do lugar.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

cenario() {
  titulo "Cenario 02 - recusa do orcamento"
  pre_condicoes

  passo "cadastros e OS ate AguardandoAprovacao"
  criar_massa B 10
  levar_ate_aguardando_aprovacao B || return 1
  local os="$B_OS_ID"
  afirmar_igual "estoque antes da decisao" "10" "$(peca_estoque "$B_PECA_ID")"

  # Base lida antes e incremento conferido logo depois: as series do Meter da saga somem do /metrics 5 min depois da ultima atualizacao.
  local recusados0
  recusados0="$(os_metrica saga_orcamento_recusado causa=ClienteRecusouOrcamento)"

  passo "cliente recusa o orcamento (rota anonima, 202)"
  recusar_orcamento "$os" "Valor acima do esperado"
  esperar_http "recusa do orcamento" 202
  afirmar_igual "corpo do 202: status ainda AguardandoAprovacao" "AguardandoAprovacao" "$(J status)"
  esperar_valor "status da OS (saga aplicou a recusa)" 60 OrcamentoRecusado os_status "$os" || return 1
  esperar_valor "estado da saga (final)" 30 OrcamentoRecusado saga_estado "$os" || return 1
  afirmar_numero "metrica saga_orcamento_recusado{causa=ClienteRecusouOrcamento} subiu >= 1" ge "$((recusados0 + 1))" "$(os_metrica saga_orcamento_recusado causa=ClienteRecusouOrcamento)"

  passo "efeitos da recusa: orcamento cancelado, sem pagamento, estoque intacto, e-mail"
  esperar_valor "orcamento no Billing (CancelarOrcamento)" 60 Cancelado billing_orcamento_status "$os" || return 1
  http GET "$BILLING_URL/api/orcamentos/ordem-de-servico/$os" "$TOKEN_ADMIN"
  afirmar_igual "motivo da recusa propagado ate o orcamento do Billing" "Valor acima do esperado" "$(J motivoCancelamento)"
  afirmar_igual "nenhum pagamento criado no Billing" "404" "$(billing_pagamento_status "$os")"
  afirmar_igual "estoque nunca foi reservado" "10" "$(peca_estoque "$B_PECA_ID")"
  registrar_estoque "$B_PECA_ID" 10 "cenario 02: orcamento recusado, estoque intacto"
  os_carregar "$os"
  afirmar_igual "historico de status" "Recebida,EmDiagnostico,AguardandoAprovacao,OrcamentoRecusado" "$(Jhistorico)"
  esperar_valor "e-mails ao cliente (EmDiagnostico, AguardandoAprovacao, OrcamentoRecusado)" 30 3 mail_total "$B_EMAIL" || true
  local trechos
  trechos="$(mail_trechos "$B_EMAIL")"
  mail_trechos "$B_EMAIL" | while IFS= read -r t; do info "e-mail: ${t:0:200}"; done
  afirmar_contem "e-mails do cliente" "$trechos" "Orçamento recusado"
  afirmar_igual "prazo de aprovacao vigente limpo na saga" "NULL" "$(sql_os "SELECT ISNULL(CAST(PrazoAprovacaoId AS varchar(36)), 'NULL') FROM OrdemDeServicoSagas WHERE CorrelationId='$os'")"
  info "[INFO]  Execucao apos a recusa: ordem de producao em '$(exec_status "$os")' (a saga so cancela a producao no cancelamento do staff)"

  passo "decisoes depois da OS encerrada: aprovar e recusar de novo respondem 409"
  aprovar_orcamento "$os"
  esperar_http "aprovar depois da recusa" 409
  recusar_orcamento "$os" "Outra recusa"
  esperar_http "recusar de novo" 409
  afirmar_igual "OS segue OrcamentoRecusado" "OrcamentoRecusado" "$(os_status "$os")"

  passo "nada preso"
  verificar_outbox_vazio
  verificar_sem_erros
}

rodar_cenario "02-recusa-do-orcamento" cenario
