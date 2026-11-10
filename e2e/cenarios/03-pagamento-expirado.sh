#!/usr/bin/env bash
# Cenario 03 - PAGAMENTO EXPIRADO: ninguem paga dentro do prazo. Forco o vencimento no banco (o prazo real e de 48 h) e
# confiro o caminho CancelarPagamento -> PagamentoCancelado -> estoque liberado -> OS Cancelada. Depois simulo uma
# aprovacao TARDIA do gateway: o Billing estorna sozinho e a saga, ja encerrada, ignora o PagamentoEstornado.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

cenario() {
  titulo "Cenario 03 - pagamento expirado"
  pre_condicoes

  passo "cadastros e OS ate AguardandoPagamento (estoque reservado, link gerado)"
  criar_massa C 10
  levar_ate_aguardando_aprovacao C || return 1
  levar_ate_aguardando_pagamento C || return 1
  local os="$C_OS_ID" link pref
  link="$(os_link_pagamento "$os")"; pref="${link##*/}"
  afirmar_igual "estoque reservado" "8" "$(peca_estoque "$C_PECA_ID")"

  # Base lida antes e incremento conferido logo depois: as series do Meter da saga somem do /metrics 5 min depois da ultima atualizacao.
  local cancel0 prazo0
  cancel0="$(os_metrica saga_cancelada estado_origem=CancelandoPagamento)"
  prazo0="$(os_metrica saga_prazo_expirado tipo=Pagamento)"

  passo "forca o vencimento do prazo de pagamento (Pagamentos.ExpiraEm e SagaPrazos.ExpiraEm no passado)"
  # Optei por recuar o Billing ANTES do OS: o sweeper do OS (a cada 5 s) dispara assim que enxerga a linha vencida, e se o
  # CancelarPagamento chegasse ao Billing antes de Pagamentos.ExpiraEm passar, o pagamento seria registrado como Cancelado
  # e nao como Expirado.
  afirmar_igual "linhas de Pagamentos vencidas (Billing)" "1" \
    "$(sql_billing "UPDATE Pagamentos SET ExpiraEm = DATEADD(MINUTE, -5, SYSDATETIMEOFFSET()) WHERE OrdemDeServicoId='$os'; SELECT @@ROWCOUNT")"
  afirmar_igual "linhas de SagaPrazos vencidas (OS)" "1" \
    "$(sql_os "UPDATE SagaPrazos SET ExpiraEm = DATEADD(MINUTE, -5, SYSDATETIMEOFFSET()) WHERE CorrelationId='$os' AND Tipo='Pagamento' AND Processado=0; SELECT @@ROWCOUNT")"
  local t0
  t0=$(date +%s)

  passo "sweeper (a cada 5 s) publica PrazoExpirado -> saga cancela o pagamento no Billing -> OS Cancelada"
  esperar_valor "estado da saga (final)" 90 Cancelada saga_estado "$os" || return 1
  info "do vencimento forcado ate a saga Cancelada: $(( $(date +%s) - t0 )) s"
  esperar_valor "status da OS" 30 Cancelada os_status "$os" || return 1
  afirmar_numero "metrica saga_prazo_expirado{tipo=Pagamento} subiu >= 1 (so o prazo vigente conta)" ge "$((prazo0 + 1))" "$(os_metrica saga_prazo_expirado tipo=Pagamento)"
  afirmar_numero "metrica saga_cancelada{estado_origem=CancelandoPagamento} subiu >= 1" ge "$((cancel0 + 1))" "$(os_metrica saga_cancelada estado_origem=CancelandoPagamento)"

  passo "efeitos: pagamento Expirado, orcamento cancelado, estoque devolvido, prazo marcado, e-mail"
  afirmar_igual "pagamento no Billing" "Expirado" "$(billing_pagamento_status "$os")"
  http GET "$BILLING_URL/api/pagamentos/ordem-de-servico/$os" "$TOKEN_ADMIN"
  afirmar_igual "pagamento sem aprovacao tardia (ainda)" "false" "$(J aprovacaoTardia)"
  afirmar_igual "orcamento no Billing" "Cancelado" "$(billing_orcamento_status "$os")"
  esperar_valor "estoque devolvido (10)" 30 10 peca_estoque "$C_PECA_ID" || return 1
  registrar_estoque "$C_PECA_ID" 10 "cenario 03: pagamento expirado, estoque devolvido"
  afirmar_igual "prazo de pagamento processado pelo sweeper" "1" "$(sql_os "SELECT CAST(Processado AS int) FROM SagaPrazos WHERE CorrelationId='$os' AND Tipo='Pagamento'")"
  os_carregar "$os"
  afirmar_igual "historico de status" "Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,Cancelada" "$(Jhistorico)"
  esperar_valor "e-mails ao cliente (5 transicoes + 1 link)" 30 5 mail_total "$C_EMAIL" || true
  mail_trechos "$C_EMAIL" | while IFS= read -r t; do info "e-mail: ${t:0:200}"; done
  afirmar_contem "e-mails do cliente" "$(mail_trechos "$C_EMAIL")" "Cancelada"
  afirmar_igual "a Execucao nao recebeu cancelamento (so o staff cancela a producao)" "DiagnosticoConcluido" "$(exec_status "$os")"

  passo "aprovacao TARDIA: o cliente paga depois do prazo (gateway Fake, webhook assinado)"
  http POST "$BILLING_URL/dev/pagamentos/$pref/aprovar" -
  esperar_http "webhook da aprovacao tardia" 200
  local gw
  gw="$(J gatewayPaymentId)"
  esperar_valor "pagamento estornado automaticamente pelo Billing" 60 Estornado billing_pagamento_status "$os" || return 1
  http GET "$BILLING_URL/api/pagamentos/ordem-de-servico/$os" "$TOKEN_ADMIN"
  afirmar_igual "aprovacao tardia registrada" "true" "$(J aprovacaoTardia)"
  afirmar_igual "estorno concluido" "Concluido" "$(J estorno.status)"
  afirmar_nao_vazio "id do estorno no gateway" "$(J estorno.gatewayRefundId)"
  info "pagamento tardio do gateway: $gw"
  manter_valor "saga continua Cancelada (ignora PagamentoEstornado fora de Estornando)" 6 Cancelada saga_estado "$os"
  afirmar_igual "OS continua Cancelada" "Cancelada" "$(os_status "$os")"
  afirmar_igual "estoque NAO devolvido de novo" "10" "$(peca_estoque "$C_PECA_ID")"

  passo "nada preso"
  verificar_outbox_vazio
  verificar_sem_erros
}

rodar_cenario "03-pagamento-expirado" cenario
