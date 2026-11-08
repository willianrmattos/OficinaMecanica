#!/usr/bin/env bash
# Roda os cenarios em ordem (01..NN e, por ultimo, 99-verificacoes-finais), com a saida na tela e em
# e2e/.gerado/logs/execucao-<data-hora>.log, e imprime um quadro-resumo. Exit 0 so se TODOS passarem.
#
# Uso: bash e2e/executar-cenarios.sh [--so 01,03] [--sem-finais]
#   --so 01,03     roda so os cenarios cujo prefixo numerico esta na lista (as verificacoes finais so se pedidas: --so 99)
#   --sem-finais   nao roda as verificacoes finais
# Cada cenario tem um teto rigido de 900 s (alem dos tetos de cada espera), para nada travar o run.
set -uo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib-ambiente.sh
source "$AQUI/lib-ambiente.sh"
carregar_env

uso() {
  echo "uso: bash e2e/executar-cenarios.sh [--so NN[,NN...]] [--sem-finais]" >&2
  echo "  --so 01,03     roda so os cenarios de prefixo 01 e 03 (99 = verificacoes finais)" >&2
  echo "  --sem-finais   nao roda as verificacoes finais" >&2
}

SO=""
FINAIS=1
while [ $# -gt 0 ]; do
  case "$1" in
    --so)
      # Sem argumento (ou seguido de outra opcao) nao ha lista para filtrar: aviso e saio em vez de abortar com variavel nao definida.
      if [ $# -lt 2 ] || [[ "$2" == --* ]] || ! [[ "$2" =~ ^[0-9]{2}(,[0-9]{2})*$ ]]; then
        echo "--so exige uma lista de prefixos numericos de dois digitos (ex.: --so 01,03)" >&2; uso; exit 2
      fi
      SO=",$2,"; shift ;;
    --sem-finais) FINAIS=0 ;;
    -h|--help) uso; exit 0 ;;
    *) echo "argumento desconhecido: $1" >&2; uso; exit 2 ;;
  esac
  shift
done

mkdir -p "$GERADO/logs"
# Mesma razao do subir.sh: a primeira leitura do /metrics e que liga a observacao dos Meters no prometheus-net; refaco aqui
# (e idempotente) para quem subiu o ambiente de outra forma. Os contadores da saga que o 99 confere partem dela.
for porta in "$E2E_PORTA_OS" "$E2E_PORTA_BILLING" "$E2E_PORTA_EXECUCAO"; do
  curl -s -o /dev/null --max-time 5 "http://127.0.0.1:$porta/metrics" || true
done
LOG="$GERADO/logs/execucao-$(date +%Y%m%d-%H%M%S).log"
# Cada execucao parte de um ledger de estoque novo (o ledger so vale para as pecas criadas nesta execucao).
[ -z "$SO" ] && rm -f "$GERADO/ledger-estoque.tsv"

{
  echo "== e2e oficina-e2e | $(date '+%Y-%m-%d %H:%M:%S') =="
  for repo in "OS:$E2E_CONTEXTO_OS_RESOLVIDO" "Billing:$E2E_CONTEXTO_BILLING_RESOLVIDO" "Execucao:$E2E_CONTEXTO_EXECUCAO_RESOLVIDO"; do
    dir="${repo#*:}"
    echo "  ${repo%%:*}: $(git -C "$dir" rev-parse --short HEAD 2>/dev/null) ($(git -C "$dir" branch --show-current 2>/dev/null)), alteracoes nao commitadas fora de e2e/: $(git -C "$dir" status --porcelain 2>/dev/null | grep -v ' e2e/' | wc -l)"
  done
  compose ps --format 'table {{.Service}}\t{{.Image}}\t{{.Status}}'
} | tee -a "$LOG"

declare -a NOMES RESULTADOS DETALHES
for arquivo in "$AQUI"/cenarios/[0-9][0-9]-*.sh; do
  nome="$(basename "$arquivo" .sh)"
  numero="${nome%%-*}"
  if [ "$numero" = "99" ]; then
    [ "$FINAIS" -eq 1 ] || continue
    [ -z "$SO" ] || case "$SO" in *",99,"*) ;; *) continue ;; esac
  else
    [ -z "$SO" ] || case "$SO" in *",$numero,"*) ;; *) continue ;; esac
  fi

  echo | tee -a "$LOG"
  echo "################################################################################" | tee -a "$LOG"
  echo "# $nome  ($(date '+%H:%M:%S'))" | tee -a "$LOG"
  echo "################################################################################" | tee -a "$LOG"
  saida="$(mktemp)"
  timeout 900 bash "$arquivo" 2>&1 | tee -a "$LOG" "$saida"
  rc=${PIPESTATUS[0]}
  resumo="$(grep -E '^RESULTADO\|' "$saida" | tail -1)"
  if [ -z "$resumo" ]; then resumo="RESULTADO|$nome|FALHOU|sem linha de resultado (codigo de saida $rc; 124 = teto de 900 s)"; fi
  rm -f "$saida"
  NOMES+=("$nome"); RESULTADOS+=("$(echo "$resumo" | cut -d'|' -f3)"); DETALHES+=("$(echo "$resumo" | cut -d'|' -f4-)")
done

echo | tee -a "$LOG"
echo "=========================== RESUMO ===========================" | tee -a "$LOG"
falhou=0
for i in "${!NOMES[@]}"; do
  printf '%-40s %-8s %s\n' "${NOMES[$i]}" "${RESULTADOS[$i]}" "${DETALHES[$i]}" | tee -a "$LOG"
  [ "${RESULTADOS[$i]}" = "PASSOU" ] || falhou=1
done
echo "log completo: $LOG" | tee -a "$LOG"
exit "$falhou"
