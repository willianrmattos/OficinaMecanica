#!/usr/bin/env bash
# Verificacoes de FIM DE EXECUCAO (rodam depois de todos os cenarios): filas *_error vazias, nenhum erro inesperado nos
# logs dos tres servicos, ninguem preso (sagas, producao, pagamentos, outbox), containers de pe sem restart inesperado e
# o estoque de cada peca conferido contra o que cada cenario registrou em .gerado/ledger-estoque.tsv.
set -uo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

# Padroes de aviso (WRN) que os proprios cenarios provocam de proposito (409 e 400 pedidos, assinatura ruim, aprovacao
# tardia, quedas do RabbitMQ do cenario 07, Execucao congelada no 01, copia duplicada injetada no 05) ou que sao ruido de infraestrutura conhecido.
# Qualquer outro aviso reprova a verificacao: e preciso olhar.
AVISOS_ESPERADOS='Conflito de estado|Decisão .* ignorada|Erro de domínio|DataProtection|XML encryptor|QuerySplittingBehavior|Usage Telemetry|Webhook rejeitado: assinatura inválida|ainda não está disponível no gateway|aprovado no gateway depois de (Expirado|Cancelado)|Health check (masstransit-bus|rabbitmq) with status Degraded|Connection Failed: rabbitmq://|Consulta de execução da ordem de serviço .*ExecucaoIndisponivel|Execution attempt\. Source: .IExecucaoClient-execucao|Resilience event occurred\. EventName: .OnRetry., Source: .IExecucaoClient-execucao|A Execução não atendeu a consulta da ordem de serviço .*: timeout|R-RETRY rabbitmq://rabbitmq/billing-comandos .*GerarOrcamento'

cenario() {
  titulo "Verificacoes finais"

  passo "1. filas do RabbitMQ (API de management): listagem completa, nenhuma *_error com mensagem, filas de trabalho drenadas"
  # A listagem so vale se trouxer as tres filas de trabalho (os-saga, billing-comandos, execucao-comandos): API fora do ar,
  # JSON de erro ou lista incompleta aparecem como "INDISPONIVEL: <causa>" e reprovam, em vez de virar "0 mensagens".
  local nome msgs cons filas_lidas
  filas_lidas="$(listar_filas 2>&1)" && while IFS='|' read -r nome msgs cons; do
    info "fila $nome: mensagens=$msgs consumidores=$cons"
  done <<< "$filas_lidas" || reprovar "listagem de filas indisponivel ou incompleta: $filas_lidas"
  afirmar_numero "filas listadas pela API de management (as 3 de trabalho, mais as *_error que existirem)" ge 3 "$(printf '%s\n' "$filas_lidas" | grep -c '|')"
  afirmar_igual "soma das mensagens em filas *_error" "0" "$(somar_filas erro)"
  afirmar_igual "filas de trabalho (os-saga, billing-comandos, execucao-comandos) drenadas" "0" "$(somar_filas trabalho)"

  passo "2. logs dos servicos: nenhum erro/Fault inesperado"
  local svc erros inesperados avisos linhas_log
  for svc in os billing execucao; do
    docker_logs "$svc" > "$GERADO/logs/final-$svc.log"
    linhas_log="$(wc -l < "$GERADO/logs/final-$svc.log")"
    erros="$(grep -cE '^\[[0-9:]+ (ERR|FTL)\]' "$GERADO/logs/final-$svc.log" || true)"
    info "$svc: $linhas_log linhas de log; linhas ERR/FTL: $erros"
    # Os asserts "0 ocorrencias" abaixo so significam algo se o log foi de fato lido: exijo linhas e a partida do bus.
    afirmar_numero "log de $svc lido (linhas > 0)" gt 0 "$linhas_log"
    afirmar_numero "log de $svc traz a partida do bus (Bus started)" ge 1 "$(grep -c 'Bus started' "$GERADO/logs/final-$svc.log" || true)"
  done
  # Execucao: nenhum ERR. A requisicao que o OS abandona por timeout (cenario 01, Execucao congelada) vira so um Information
  # "Requisicao cancelada pelo cliente", e deve aparecer pelo menos uma vez.
  afirmar_igual "Execucao: linhas ERR/FTL" "0" "$(grep -cE '^\[[0-9:]+ (ERR|FTL)\]' "$GERADO/logs/final-execucao.log" || true)"
  afirmar_igual "Execucao: 'Erro nao tratado' no log" "0" "$(grep -c 'Erro não tratado' "$GERADO/logs/final-execucao.log" || true)"
  afirmar_numero "Execucao: Information 'Requisicao cancelada pelo cliente' (o OS desistiu da consulta com a Execucao congelada)" ge 1 \
    "$(grep -E '^\[[0-9:]+ INF\] Requisição cancelada pelo cliente' "$GERADO/logs/final-execucao.log" | wc -l)"
  # OS: o unico ERR esperado e o da telemetria padrao de resiliencia do cliente HTTP da Execucao, que registra cada tentativa
  # estourada (OnTimeout) como ERR; o cenario 01 provoca esse caminho ao congelar a Execucao.
  info "OS: ERR OnTimeout do cliente da Execucao (esperado, 3 tentativas de 2 s por consulta estourada)=$(grep -E '^\[[0-9:]+ ERR\]' "$GERADO/logs/final-os.log" | grep -c "EventName: 'OnTimeout', Source: 'IExecucaoClient-execucao" || true)"
  afirmar_igual "OS: linhas ERR/FTL fora do OnTimeout do cliente da Execucao" "0" \
    "$(grep -E '^\[[0-9:]+ (ERR|FTL)\]' "$GERADO/logs/final-os.log" | grep -vc "EventName: 'OnTimeout', Source: 'IExecucaoClient-execucao" || true)"
  afirmar_igual "OS: 'Erro nao tratado' no log" "0" "$(grep -c 'Erro não tratado' "$GERADO/logs/final-os.log" || true)"
  afirmar_igual "OS: 'Falha técnica no comando' (Fault<T> real da saga)" "0" "$(grep -c 'Falha técnica no comando' "$GERADO/logs/final-os.log" || true)"
  afirmar_igual "OS/Billing/Execucao: R-FAULT / Fault<> do MassTransit" "0" \
    "$(cat "$GERADO"/logs/final-*.log | grep -E 'R-FAULT|Fault<|FaultException' | grep -vc 'FaultAddress' || true)"
  # Billing: o unico ERR esperado e o do EF quando duas copias do mesmo messageId (a copia duplicada do cenario 05) disputam
  # a linha do inbox; a perdedora e retentada e descartada.
  inesperados="$(grep -E '^\[[0-9:]+ ERR\]' "$GERADO/logs/final-billing.log" | grep -vc 'An exception occurred in the database while saving changes' || true)"
  afirmar_igual "Billing: ERR que nao seja o 'saving changes' do EF" "0" "$inesperados"
  afirmar_igual "Billing: 'Erro nao tratado' no log" "0" "$(grep -c 'Erro não tratado' "$GERADO/logs/final-billing.log" || true)"
  local fake_ids inbox_dups outras_unicas
  fake_ids="$(grep -c 'Cannot insert duplicate key row.*IX_TentativasPagamento_GatewayPaymentId' "$GERADO/logs/final-billing.log" || true)"
  inbox_dups="$(grep -c 'AK_InboxState_MessageId_ConsumerId' "$GERADO/logs/final-billing.log" || true)"
  outras_unicas="$(grep -E 'Cannot insert duplicate key|Violation of UNIQUE KEY' "$GERADO/logs/final-billing.log" \
    | grep -vcE 'IX_TentativasPagamento_GatewayPaymentId|AK_InboxState_MessageId_ConsumerId' || true)"
  info "Billing: violacoes de indice unico: id do gateway Fake (IX_TentativasPagamento_GatewayPaymentId)=$fake_ids, copia duplicada no inbox (AK_InboxState_MessageId_ConsumerId)=$inbox_dups"
  afirmar_igual "Billing: ids do gateway Fake repetidos entre reinicios (IX_TentativasPagamento_GatewayPaymentId)" "0" "$fake_ids"
  afirmar_igual "Billing: violacoes de unicidade fora do inbox da copia duplicada" "0" "$outras_unicas"
  afirmar_igual "Billing: 409 por conflito de persistencia no log" "0" "$(grep -c 'Conflito de concorrência na persistência' "$GERADO/logs/final-billing.log" || true)"
  afirmar_igual "OS: aviso 'Overriding HTTP_PORTS' no log (a porta vem so do ASPNETCORE_HTTP_PORTS da imagem)" "0" "$(grep -c 'Overriding HTTP_PORTS' "$GERADO/logs/final-os.log" || true)"
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

  passo "6. metricas da saga no /metrics do OS (Meter OficinaMecanica.Saga, ADR 0008) coerentes com o banco"
  # Os contadores contam no processo do OS (que nao reiniciou durante a execucao) e o ADR 0008 admite contagem a MAIOR quando um
  # commit falha e o retry reprocessa a mensagem: por isso comparo com ">=" contra a verdade do banco e nao exijo igualdade.
  # Aqui ficam so as series atualizadas pelos ultimos fluxos (iniciada e concluida, no cenario 07): o adapter do prometheus-net
  # remove uma serie 5 min depois da ultima atualizacao (MetricsExpireAfter), entao as series com tag de causa/estado
  # (cancelada, orcamento_recusado, prazo_expirado, decisao_ignorada) ja sumiram a esta altura; elas sao conferidas nos
  # cenarios 01 a 04 e 06, logo depois do fluxo que as incrementa. Rode o 99 em ate 5 min do ultimo fluxo.
  local n_total n_conc n_canc n_rec
  n_total="$(sql_os "SELECT COUNT(*) FROM OrdemDeServicoSagas")"
  n_conc="$(sql_os "SELECT COUNT(*) FROM OrdemDeServicoSagas WHERE CurrentState = 'Concluida'")"
  n_canc="$(sql_os "SELECT COUNT(*) FROM OrdemDeServicoSagas WHERE CurrentState = 'Cancelada'")"
  n_rec="$(sql_os "SELECT COUNT(*) FROM OrdemDeServicoSagas WHERE CurrentState = 'OrcamentoRecusado'")"
  info "banco: sagas=$n_total concluidas=$n_conc canceladas=$n_canc orcamento-recusado=$n_rec"
  info "metricas agora: saga_iniciada=$(os_metrica saga_iniciada) saga_concluida=$(os_metrica saga_concluida) saga_cancelada=$(os_metrica saga_cancelada) saga_orcamento_recusado=$(os_metrica saga_orcamento_recusado) (as duas ultimas podem ter expirado do /metrics)"
  afirmar_numero "saga_iniciada >= sagas criadas (banco)" ge "$n_total" "$(os_metrica saga_iniciada)"
  afirmar_numero "saga_concluida >= sagas Concluida (banco)" ge "$n_conc" "$(os_metrica saga_concluida)"
  afirmar_numero "saga_duracao_estado_segundos_count{estado=AguardandoDiagnostico} >= sagas criadas (toda saga sai desse estado)" ge "$n_total"     "$(os_metrica saga_duracao_estado_segundos_count estado=AguardandoDiagnostico)"
  afirmar_igual "saga_compensacao_falhou (nenhum Fault de compensacao)" "0" "$(os_metrica saga_compensacao_falhou)"
  afirmar_igual "saga_estorno_pendente_manual (nenhum estorno parado)" "0" "$(os_metrica saga_estorno_pendente_manual)"

  passo "7. resumo dos dados gerados pela execucao"
  info "ordens de servico: $(sql_os 'SELECT COUNT(*) FROM OrdensDeServico')  | orcamentos: $(sql_billing 'SELECT COUNT(*) FROM Orcamentos')  | pagamentos: $(sql_billing 'SELECT COUNT(*) FROM Pagamentos')  | estornos: $(sql_billing 'SELECT COUNT(*) FROM Estornos')"
  info "e-mails no Mailpit: $(curl -s "$MAILPIT_URL/api/v1/messages?limit=1" | "$PY" "$FERR" jget total)"
  info "inbox (mensagens consumidas com deduplicacao): OS=$(sql_os 'SELECT COUNT(*) FROM InboxState') Billing=$(sql_billing 'SELECT COUNT(*) FROM InboxState') Execucao=$(mongo_exec "db.getCollection('inbox.states').countDocuments({})")"
}

rodar_cenario "99-verificacoes-finais" cenario
