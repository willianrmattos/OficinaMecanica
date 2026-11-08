#!/usr/bin/env bash
# Verificacoes de FIM DE EXECUCAO (rodam depois de todos os cenarios): filas *_error vazias, nenhum erro inesperado nos
# logs dos tres servicos, ninguem preso (sagas, producao, pagamentos, outbox), containers de pe sem restart inesperado e
# o estoque de cada peca conferido contra o que cada cenario registrou em .gerado/ledger-estoque.tsv.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

# Padroes de aviso (WRN) que os proprios cenarios provocam de proposito (409 e 400 pedidos, assinatura ruim, aprovacao
# tardia, quedas do RabbitMQ do cenario 07, copia duplicada injetada no 05) ou que sao ruido de infraestrutura conhecido.
# Qualquer outro aviso reprova a verificacao: e preciso olhar.
AVISOS_ESPERADOS='Conflito de estado|Decisão .* ignorada|Erro de domínio|DataProtection|XML encryptor|Overriding HTTP_PORTS|QuerySplittingBehavior|Usage Telemetry|Webhook rejeitado: assinatura inválida|ainda não está disponível no gateway|aprovado no gateway depois de (Expirado|Cancelado)|Conflito de concorrência na persistência|Health check (masstransit-bus|rabbitmq) with status Degraded|Connection Failed: rabbitmq://|R-RETRY rabbitmq://rabbitmq/billing-comandos .*GerarOrcamento'

cenario() {
  titulo "Verificacoes finais"

  passo "1. filas do RabbitMQ (API de management): nenhuma *_error com mensagem"
  local nome msgs cons
  while IFS='|' read -r nome msgs cons; do
    info "fila $nome: mensagens=$msgs consumidores=$cons"
  done < <(listar_filas)
  afirmar_igual "soma das mensagens em filas *_error" "0" "$(erros_total)"
  afirmar_igual "filas de trabalho (os-saga, billing-comandos, execucao-comandos) drenadas" "0" \
    "$(listar_filas | awk -F'|' '$1 !~ /_error$/ { soma += $2 } END { print soma + 0 }')"

  passo "2. logs dos servicos: nenhum erro/Fault inesperado"
  local svc erros inesperados avisos
  for svc in os billing execucao; do
    docker_logs "$svc" > "$GERADO/logs/final-$svc.log"
    erros="$(grep -cE '^\[[0-9:]+ (ERR|FTL)\]' "$GERADO/logs/final-$svc.log" || true)"
    info "$svc: $(wc -l < "$GERADO/logs/final-$svc.log") linhas de log; linhas ERR/FTL: $erros"
  done
  # OS e Execucao: zero ERR. Billing: so os ERR do EF por violacao de indice unico provocados de proposito no cenario 05:
  # o contador de ids do gateway Fake que recomeca apos o restart e a copia duplicada de GerarOrcamento (duas copias do mesmo
  # messageId disputam a linha do inbox; a perdedora e retentada e descartada).
  afirmar_igual "OS: linhas ERR/FTL" "0" "$(grep -cE '^\[[0-9:]+ (ERR|FTL)\]' "$GERADO/logs/final-os.log" || true)"
  afirmar_igual "Execucao: linhas ERR/FTL" "0" "$(grep -cE '^\[[0-9:]+ (ERR|FTL)\]' "$GERADO/logs/final-execucao.log" || true)"
  afirmar_igual "OS: 'Falha técnica no comando' (Fault<T> real da saga)" "0" "$(grep -c 'Falha técnica no comando' "$GERADO/logs/final-os.log" || true)"
  afirmar_igual "OS/Billing/Execucao: R-FAULT / Fault<> do MassTransit" "0" \
    "$(cat "$GERADO"/logs/final-*.log | grep -E 'R-FAULT|Fault<|FaultException' | grep -vc 'FaultAddress' || true)"
  inesperados="$(grep -E '^\[[0-9:]+ ERR\]' "$GERADO/logs/final-billing.log" | grep -vc 'An exception occurred in the database while saving changes' || true)"
  afirmar_igual "Billing: ERR que nao seja o 'saving changes' do EF" "0" "$inesperados"
  local fake_ids inbox_dups outras_unicas
  fake_ids="$(grep -c 'IX_TentativasPagamento_GatewayPaymentId' "$GERADO/logs/final-billing.log" || true)"
  inbox_dups="$(grep -c 'AK_InboxState_MessageId_ConsumerId' "$GERADO/logs/final-billing.log" || true)"
  outras_unicas="$(grep -E 'Cannot insert duplicate key|Violation of UNIQUE KEY' "$GERADO/logs/final-billing.log" \
    | grep -vcE 'IX_TentativasPagamento_GatewayPaymentId|AK_InboxState_MessageId_ConsumerId' || true)"
  info "Billing: violacoes de indice unico: id do gateway Fake (IX_TentativasPagamento_GatewayPaymentId)=$fake_ids, copia duplicada no inbox (AK_InboxState_MessageId_ConsumerId)=$inbox_dups"
  afirmar_igual "Billing: violacoes de unicidade fora do id do gateway Fake e do inbox" "0" "$outras_unicas"
  for svc in os billing execucao; do
    avisos="$(grep -E '^\[[0-9:]+ WRN\]' "$GERADO/logs/final-$svc.log" | grep -vE "$AVISOS_ESPERADOS" || true)"
    if [ -z "$avisos" ]; then aprovar "$svc: nenhum aviso (WRN) fora da lista de esperados"; else
      reprovar "$svc: avisos inesperados:"; printf '%s\n' "$avisos" | sort | uniq -c | head -10 | cut -c1-240
    fi
  done

  passo "3. ninguem preso: sagas, producao, pagamentos"
  info "sagas por estado: $(sql_os "SELECT STRING_AGG(CONCAT(CurrentState, '=', n), ', ') FROM (SELECT CurrentState, COUNT(*) n FROM OrdemDeServicoSagas GROUP BY CurrentState) t")"
  afirmar_igual "sagas fora de um estado final (Concluida, Cancelada, OrcamentoRecusado)" "0" \
    "$(sql_os "SELECT COUNT(*) FROM OrdemDeServicoSagas WHERE CurrentState NOT IN ('Concluida','Cancelada','OrcamentoRecusado')")"
  info "ordens de producao por status: $(mongo_exec "JSON.stringify(db.ordens_producao.aggregate([{\$match:{_t:'ordem'}},{\$group:{_id:'\$status',n:{\$sum:1}}}]).toArray())")"
  afirmar_igual "ordens de producao esperando diagnostico/reparo ou em andamento" "0" \
    "$(mongo_exec "db.ordens_producao.countDocuments({_t:'ordem', status:{\$in:['Fila','EmDiagnostico','AguardandoExecucao','EmExecucao']}})")"
  # No BillingDb o status e inteiro: StatusPagamento 1=Pendente 2=Aprovado 3=Cancelado 4=Expirado 5=Estornando 6=Estornado 7=EstornoFalhou;
  # StatusEstorno 1=Pendente 2=Concluido 3=Falhou.
  info "pagamentos por status: $(sql_billing "SELECT STRING_AGG(CONCAT(CASE Status WHEN 1 THEN 'Pendente' WHEN 2 THEN 'Aprovado' WHEN 3 THEN 'Cancelado' WHEN 4 THEN 'Expirado' WHEN 5 THEN 'Estornando' WHEN 6 THEN 'Estornado' WHEN 7 THEN 'EstornoFalhou' END, '=', n), ', ') FROM (SELECT Status, COUNT(*) n FROM Pagamentos GROUP BY Status) t")"
  afirmar_igual "pagamentos em Pendente/Estornando/EstornoFalhou" "0"     "$(sql_billing "SELECT COUNT(*) FROM Pagamentos WHERE Status IN (1, 5, 7)")"
  afirmar_igual "estornos Pendente/Falhou" "0" "$(sql_billing "SELECT COUNT(*) FROM Estornos WHERE Status IN (1, 3)")"
  verificar_outbox_vazio

  passo "4. containers: todos de pe, sem restart automatico nem OOM"
  local c estado
  for svc in sqlserver mongo rabbitmq mailpit jwks billing execucao os; do
    c="$(cid_de "$svc")"
    estado="$(docker inspect -f '{{.State.Status}} restarts={{.RestartCount}} oom={{.State.OOMKilled}}' "$c" | tr -d '\r')"
    afirmar_igual "container $svc" "running restarts=0 oom=false" "$estado"
  done

  passo "5. estoque conferido (cada linha do ledger e uma peca com o saldo esperado)"
  if [ -s "$LEDGER" ]; then
    local peca esperado desc obtido
    while IFS=$'\t' read -r peca esperado desc; do
      obtido="$(peca_estoque "$peca")"
      afirmar_igual "estoque da peca ($desc)" "$esperado" "$obtido"
    done < "$LEDGER"
  else
    reprovar "ledger de estoque vazio: nenhum cenario registrou estoque (rode os cenarios 01-06 antes)"
  fi

  passo "6. resumo dos dados gerados pela execucao"
  info "ordens de servico: $(sql_os 'SELECT COUNT(*) FROM OrdensDeServico')  | orcamentos: $(sql_billing 'SELECT COUNT(*) FROM Orcamentos')  | pagamentos: $(sql_billing 'SELECT COUNT(*) FROM Pagamentos')  | estornos: $(sql_billing 'SELECT COUNT(*) FROM Estornos')"
  info "e-mails no Mailpit: $(curl -s "$MAILPIT_URL/api/v1/messages?limit=1" | "$PY" "$FERR" jget total)"
  info "inbox (mensagens consumidas com deduplicacao): OS=$(sql_os 'SELECT COUNT(*) FROM InboxState') Billing=$(sql_billing 'SELECT COUNT(*) FROM InboxState') Execucao=$(mongo_exec "db.getCollection('inbox.states').countDocuments({})")"
}

rodar_cenario "99-verificacoes-finais" cenario
