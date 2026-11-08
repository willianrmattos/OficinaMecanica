#!/usr/bin/env bash
# Pedacos compartilhados pelos scripts do e2e: caminhos, escolha do Python, portas padrao e o atalho "compose" que
# fixa o project name (oficina-e2e). Nada aqui toca em container que nao seja deste projeto.

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GERADO="$AQUI/.gerado"

# Caminho no formato que o docker.exe entende (E:/...) mesmo com MSYS_NO_PATHCONV ligado; fora do Windows nao muda.
win() { cygpath -m "$1" 2>/dev/null || echo "$1"; }
AQUI_WIN="$(win "$AQUI")"
GERADO_WIN="$(win "$GERADO")"

# O python3 do Windows pode ser so o atalho da Microsoft Store: confiro se roda de verdade.
PY=""
for candidato in python python3 py; do
  if "$candidato" -c 'import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)' >/dev/null 2>&1; then
    PY="$candidato"; break
  fi
done
[ -n "$PY" ] || { echo "Python 3.10+ nao encontrado no PATH." >&2; return 1 2>/dev/null || exit 1; }

# A raiz do OS Service e a pasta acima de e2e/. Os repositorios irmaos ficam ao lado dela no checkout principal
# (<Pos>/OficinaMecanica/e2e -> <Pos>/OficinaMecanica.Billing) ou dois niveis acima num worktree
# (<Pos>/_worktrees/<nome>/e2e): optei por procurar nos dois lugares em vez de fixar um layout.
E2E_CONTEXTO_OS_RESOLVIDO="$(cd "${E2E_CONTEXTO_OS:-$AQUI/..}" && pwd)"
achar_repo_irmao() { # nome -> caminho absoluto do primeiro candidato que tem Dockerfile (ou o do layout principal)
  local nome="$1" candidato
  for candidato in "$E2E_CONTEXTO_OS_RESOLVIDO/../$nome" "$E2E_CONTEXTO_OS_RESOLVIDO/../../$nome"; do
    if [ -f "$candidato/Dockerfile" ]; then (cd "$candidato" && pwd); return 0; fi
  done
  echo "$E2E_CONTEXTO_OS_RESOLVIDO/../$nome"
}
E2E_CONTEXTO_BILLING_RESOLVIDO="$(cd "${E2E_CONTEXTO_BILLING:-$(achar_repo_irmao OficinaMecanica.Billing)}" 2>/dev/null && pwd || echo "${E2E_CONTEXTO_BILLING:-$(achar_repo_irmao OficinaMecanica.Billing)}")"
E2E_CONTEXTO_EXECUCAO_RESOLVIDO="$(cd "${E2E_CONTEXTO_EXECUCAO:-$(achar_repo_irmao OficinaMecanica.Execucao)}" 2>/dev/null && pwd || echo "${E2E_CONTEXTO_EXECUCAO:-$(achar_repo_irmao OficinaMecanica.Execucao)}")"
E2E_CONTEXTO_BILLING="$(win "$E2E_CONTEXTO_BILLING_RESOLVIDO")"
E2E_CONTEXTO_EXECUCAO="$(win "$E2E_CONTEXTO_EXECUCAO_RESOLVIDO")"
E2E_CONTEXTO_OS="$(win "$E2E_CONTEXTO_OS_RESOLVIDO")"
export E2E_CONTEXTO_BILLING E2E_CONTEXTO_EXECUCAO E2E_CONTEXTO_OS

carregar_env() {
  if [ -f "$GERADO/e2e.env" ]; then
    set -a; . "$GERADO/e2e.env"; set +a
  fi
  : "${E2E_PORTA_SQL:=14330}" "${E2E_PORTA_AMQP:=56720}" "${E2E_PORTA_RABBIT_UI:=15683}" "${E2E_PORTA_MONGO:=27118}"
  : "${E2E_PORTA_OS:=18080}" "${E2E_PORTA_BILLING:=18081}" "${E2E_PORTA_EXECUCAO:=18082}" "${E2E_PORTA_JWKS:=18090}"
  : "${E2E_PORTA_MAILPIT_UI:=18025}" "${E2E_PORTA_SMTP:=11025}"
  export E2E_PORTA_SQL E2E_PORTA_AMQP E2E_PORTA_RABBIT_UI E2E_PORTA_MONGO E2E_PORTA_OS E2E_PORTA_BILLING \
    E2E_PORTA_EXECUCAO E2E_PORTA_JWKS E2E_PORTA_MAILPIT_UI E2E_PORTA_SMTP
}

# Unico ponto que fala com o docker compose: project name fixo, para nenhum comando cair em outro projeto.
compose() {
  local env_arg=()
  [ -f "$GERADO/e2e.env" ] && env_arg=(--env-file "$GERADO_WIN/e2e.env")
  docker compose -p oficina-e2e -f "$AQUI_WIN/docker-compose.e2e.yml" "${env_arg[@]}" "$@"
}
