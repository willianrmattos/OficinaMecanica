#!/usr/bin/env bash
# Cenario 01 - CAMINHO FELIZ: cadastros -> OS aberta -> diagnostico (Execucao) -> orcamento (Billing) -> aprovacao
# anonima -> estoque reservado -> pagamento (link do Checkout fake) -> webhook do gateway Fake -> reparo -> OS Finalizada.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

cenario() {
  titulo "Cenario 01 - caminho feliz"
  pre_condicoes

  passo "cadastros no OS Service (cliente com e-mail, veiculo, servico e peca com estoque 10)"
  criar_massa A 10
  afirmar_igual "estoque inicial da peca" "10" "$(peca_estoque "$A_PECA_ID")"

  passo "abre a OS (servico 150,00 + 2 pecas de 50,00 = 250,00)"
  abrir_os A
  local os="$A_OS_ID"
  info "OS $A_OS_NUMERO id=$os"

  passo "saga nasce em AguardandoDiagnostico e a Execucao enfileira a ordem (SolicitarDiagnostico)"
  esperar_valor "ordem de producao na Execucao" 60 Fila exec_status "$os" || return 1
  esperar_valor "estado da saga" 30 AguardandoDiagnostico saga_estado "$os" || return 1

  passo "mecanico reivindica o diagnostico (POST /api/diagnosticos/proximo)"
  reivindicar_diagnostico "$os"
  esperar_valor "status da OS (DiagnosticoIniciado)" 60 EmDiagnostico os_status "$os" || return 1
  esperar_valor "estado da saga" 30 EmDiagnostico saga_estado "$os" || return 1

  passo "mecanico conclui o diagnostico (publica DiagnosticoConcluido) e o Billing gera o orcamento"
  concluir_diagnostico "$os"
  esperar_valor "status da OS (orcamento gerado)" 60 AguardandoAprovacao os_status "$os" || return 1
  esperar_valor "estado da saga" 30 AguardandoAprovacao saga_estado "$os" || return 1

  passo "confere o orcamento no Billing"
  http GET "$BILLING_URL/api/orcamentos/ordem-de-servico/$os" "$TOKEN_ADMIN"
  esperar_http "consulta do orcamento no Billing" 200
  afirmar_igual "status do orcamento" "Gerado" "$(J status)"
  afirmar_igual "valor total do orcamento" "$TOTAL_ESPERADO" "$(J valorTotal | sed 's/\.0*$//')"
  afirmar_igual "itens do orcamento (1 servico + 1 peca)" "2" "$(Jlen itens)"
  afirmar_contem "laudo do mecanico no orcamento" "$(J laudo)" "correia dentada"
  afirmar_igual "numero do orcamento = numero da OS" "$A_OS_NUMERO" "$(J numero)"
  afirmar_igual "estoque ainda intacto antes da aprovacao" "10" "$(peca_estoque "$A_PECA_ID")"

  passo "cliente aprova o orcamento (rota anonima, 202)"
  aprovar_orcamento "$os"
  esperar_http "aprovacao do orcamento" 202
  afirmar_igual "corpo do 202: ordemDeServicoId" "$os" "$(J ordemDeServicoId)"
  afirmar_igual "corpo do 202: status ainda AguardandoAprovacao (decisao so publicada)" "AguardandoAprovacao" "$(J status)"
  esperar_valor "status da OS (saga aplicou a aprovacao)" 60 AguardandoPagamento os_status "$os" || return 1
  esperar_valor "estoque reservado de verdade (10 - 2)" 30 8 peca_estoque "$A_PECA_ID" || return 1
  registrar_estoque "$A_PECA_ID" 8 "cenario 01: OS finalizada, estoque baixado"

  passo "Billing cria o pagamento (Checkout Pro fake) e o link volta para a OS"
  esperar_valor "pagamento no Billing" 60 Pendente billing_pagamento_status "$os" || return 1
  esperar_valor "estado da saga" 30 AguardandoPagamento saga_estado "$os" || return 1
  esperar_preenchido "link de pagamento gravado na OS" 60 os_link_pagamento "$os" || return 1
  local link="$ULTIMO_VALOR"
  afirmar_contem "link de pagamento da OS" "$link" "https://fake.mercadopago.local/checkout/fake-pref-"
  http GET "$BILLING_URL/api/pagamentos/ordem-de-servico/$os" "$TOKEN_ADMIN"
  afirmar_igual "link da OS = link do Billing" "$link" "$(J linkPagamento)"
  afirmar_igual "valor do pagamento" "$TOTAL_ESPERADO" "$(J valor | sed 's/\.0*$//')"
  afirmar_igual "orcamento aprovado pelo cliente" "Aprovado" "$(billing_orcamento_status "$os")"
  os_carregar "$os"
  local expira_em horas
  expira_em="$(J pagamentoExpiraEm)"
  horas="$("$PY" -c "
import sys, datetime as d
alvo = d.datetime.fromisoformat(sys.argv[1].replace('Z', '+00:00'))
print(round((alvo - d.datetime.now(d.timezone.utc)).total_seconds() / 3600))" "$expira_em")"
  afirmar_igual "prazo de pagamento da OS (horas a partir de agora, Saga__PrazoPagamentoHoras=48)" "48" "$horas"

  passo "cliente paga: o gateway Fake aprova e dispara o webhook assinado (POST /dev/pagamentos/{pref}/aprovar)"
  pagar_pelo_gateway_fake "$os"
  info "pagamento do gateway $PAG_GATEWAY_ID, x-request-id $PAG_REQUEST_ID"
  esperar_valor "status do pagamento no Billing" 30 Aprovado billing_pagamento_status "$os" || return 1
  esperar_valor "status da OS (PagamentoAprovado -> IniciarExecucao)" 60 EmExecucao os_status "$os" || return 1
  esperar_valor "estado da saga" 30 EmExecucao saga_estado "$os" || return 1
  esperar_valor "ordem liberada para reparo na Execucao" 60 AguardandoExecucao exec_status "$os" || return 1
  http GET "$BILLING_URL/api/pagamentos/ordem-de-servico/$os" "$TOKEN_ADMIN"
  afirmar_igual "gateway payment id aprovado gravado" "$PAG_GATEWAY_ID" "$(J gatewayPaymentIdAprovado)"
  afirmar_igual "orcamento depois do pagamento" "Pago" "$(billing_orcamento_status "$os")"

  passo "mecanico reivindica o reparo (POST /api/reparos/proximo) e finaliza"
  reivindicar_reparo "$os"
  afirmar_igual "pecas do snapshot aprovado na Execucao" "1" "$(Jlen pecas)"
  afirmar_igual "servicos do snapshot aprovado na Execucao" "1" "$(Jlen servicos)"
  http POST "$EXECUCAO_URL/api/ordens-de-producao/$os/reparo/finalizar" "$TOKEN_MEC1" \
    '{"resumo":"Trocados filtro de oleo e correia dentada; teste de rodagem sem ruidos."}'
  esperar_http "finalizacao do reparo" 200
  afirmar_igual "status na Execucao" "Finalizada" "$(J status)"

  passo "OS Finalizada: estado final, historico, estoque, e-mails"
  esperar_valor "status da OS" 60 Finalizada os_status "$os" || return 1
  esperar_valor "estado da saga" 30 Concluida saga_estado "$os" || return 1
  os_carregar "$os"
  afirmar_nao_vazio "dataConclusao da OS" "$(J dataConclusao)"
  afirmar_igual "historico de status em ordem cronologica" \
    "Recebida,EmDiagnostico,AguardandoAprovacao,AguardandoPagamento,EmExecucao,Finalizada" "$(Jhistorico)"
  afirmar_igual "estoque definitivamente baixado (nao devolvido)" "8" "$(peca_estoque "$A_PECA_ID")"
  afirmar_igual "saga sem prazo VIGENTE de aprovacao nem de pagamento" "NULL|NULL"     "$(sql_os "SELECT CONCAT(ISNULL(CAST(PrazoAprovacaoId AS varchar(36)), 'NULL'), '|', ISNULL(CAST(PrazoPagamentoId AS varchar(36)), 'NULL')) FROM OrdemDeServicoSagas WHERE CorrelationId='$os'")"
  info "[INFO]  linhas de SagaPrazos desta OS (Processado fica 0 ate o vencimento; a saga ignora o PrazoExpirado que nao e o vigente): $(sql_os "SELECT STRING_AGG(CONCAT(Tipo, ':', Processado), ',') FROM SagaPrazos WHERE CorrelationId='$os'")"
  afirmar_igual "um unico orcamento no Billing" "1" "$(billing_contar Orcamentos "$os")"
  afirmar_igual "um unico pagamento no Billing" "1" "$(billing_contar Pagamentos "$os")"

  passo "e-mails ao cliente no Mailpit (uma notificacao por transicao + o link de pagamento)"
  esperar_valor "quantidade de e-mails para $A_EMAIL (5 transicoes + 1 link)" 30 6 mail_total "$A_EMAIL" || true
  mail_trechos "$A_EMAIL" | while IFS= read -r t; do info "e-mail: ${t:0:200}"; done
  local trechos
  trechos="$(mail_trechos "$A_EMAIL")"
  for frase in "Em diagnóstico" "Aguardando aprovação do orçamento" "Aguardando pagamento" "Em execução" "Finalizada"; do
    afirmar_contem "e-mails do cliente" "$trechos" "$frase"
  done
  afirmar_contem "e-mail do link de pagamento" "$trechos" "fake.mercadopago.local"

  passo "rota sincrona GET /api/ordens-de-servico/{id}/execucao (da F3c; pode nao existir neste commit)"
  http GET "$OS_URL/api/ordens-de-servico/$os/execucao" "$TOKEN_ADMIN"
  if [ "$HTTP_STATUS" = "404" ]; then
    info "[INFO]  rota ausente neste commit (HTTP 404), como previsto: fica para a F3c"
  else
    afirmar_igual "rota sincrona de execucao (HTTP)" "200" "$HTTP_STATUS"
    info "corpo: ${HTTP_BODY:0:300}"
  fi

  passo "nada preso: outbox das tres bases vazio e nenhuma mensagem em *_error"
  verificar_outbox_vazio
  verificar_sem_erros
}

rodar_cenario "01-caminho-feliz" cenario
