#!/usr/bin/env bash
# Emite um JWT RS256 de DEV assinado com a chave privada de e2e/.gerado/ (a mesma cujo JWKS o stub serve).
# Os claims imitam os do Seguranca: sub, name, role, jti, iat, exp, iss, aud.
#
# Uso: bash e2e/emitir-token.sh [admin|atendente|mecanico] [--sub <id>] [--minutos N]
#   Ex.: TOKEN=$(bash e2e/emitir-token.sh mecanico); curl -H "Authorization: Bearer $TOKEN" http://127.0.0.1:18082/api/fila/resumo
set -euo pipefail
AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib-ambiente.sh
source "$AQUI/lib-ambiente.sh"

[ -f "$GERADO/jwt-privada.pem" ] || { echo "sem chave em $GERADO: rode bash e2e/subir.sh antes." >&2; exit 1; }
perfil="${1:-admin}"; shift || true
"$PY" "$AQUI/scripts/ferramentas.py" emitir-token "$GERADO_WIN" "$perfil" "$@"
