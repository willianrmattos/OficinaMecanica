#!/usr/bin/env bash
# Derruba o ambiente e2e e apaga os volumes dele. O "-p oficina-e2e" faz o compose enxergar so os recursos deste projeto:
# containers, volumes e rede do usuario (monolito antigo, sonar, grafana...) nao sao tocados.
#
# Uso: bash e2e/derrubar.sh [--imagens]   (--imagens tambem remove as tres imagens oficina-e2e/*:local construidas aqui)
set -euo pipefail
AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib-ambiente.sh
source "$AQUI/lib-ambiente.sh"
carregar_env

compose down -v --remove-orphans

if [ "${1:-}" = "--imagens" ]; then
  for imagem in oficina-e2e/os:local oficina-e2e/billing:local oficina-e2e/execucao:local; do
    docker image rm "$imagem" >/dev/null 2>&1 && echo "imagem removida: $imagem" || true
  done
fi

restos="$(docker ps -a --filter label=com.docker.compose.project=oficina-e2e -q | wc -l)"
volumes="$(docker volume ls -q --filter label=com.docker.compose.project=oficina-e2e | wc -l)"
echo "restos do projeto oficina-e2e: containers=$restos volumes=$volumes"
