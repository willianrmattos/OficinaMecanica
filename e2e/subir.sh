#!/usr/bin/env bash
# Sobe o ambiente e2e isolado (docker compose project "oficina-e2e"): gera o que for local (par RSA, JWKS, senhas de
# DEV) em e2e/.gerado/, constroi as tres imagens e espera as tres APIs ficarem prontas.
#
# Uso: bash e2e/subir.sh [--sem-build] [--regerar]
#   --sem-build  reaproveita as imagens oficina-e2e/*:local ja construidas
#   --regerar    descarta .gerado/ (chaves e senhas novas); so faca com o ambiente derrubado (volumes guardam a senha antiga)
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib-ambiente.sh
source "$AQUI/lib-ambiente.sh"

SEM_BUILD=0
for arg in "$@"; do
  case "$arg" in
    --sem-build) SEM_BUILD=1 ;;
    --regerar) rm -rf "$GERADO" ;;
    *) echo "argumento desconhecido: $arg" >&2; exit 2 ;;
  esac
done

docker info >/dev/null 2>&1 || { echo "Docker nao responde: ligue o Docker Desktop." >&2; exit 1; }

for dir in "$E2E_CONTEXTO_BILLING_RESOLVIDO" "$E2E_CONTEXTO_EXECUCAO_RESOLVIDO"; do
  [ -f "$dir/Dockerfile" ] || { echo "Dockerfile nao encontrado em $dir (defina E2E_CONTEXTO_BILLING / E2E_CONTEXTO_EXECUCAO)." >&2; exit 1; }
done

mkdir -p "$GERADO/jwks" "$GERADO/logs"

# As senhas de DEV sao sorteadas uma vez e ficam so no .gerado/ (ignorado pelo git). Sem "$", aspas ou ";" para
# servirem em connection string e em interpolacao do compose sem escape.
if [ ! -f "$GERADO/e2e.env" ]; then
  {
    echo "E2E_SA_PASSWORD=E2e-$(openssl rand -hex 10)-Aa1"
    echo "E2E_OS_DB_PASSWORD=E2e-$(openssl rand -hex 10)-Aa1"
    echo "E2E_BILLING_DB_PASSWORD=E2e-$(openssl rand -hex 10)-Aa1"
    echo "E2E_RABBIT_USER=oficina_e2e"
    echo "E2E_RABBIT_PASSWORD=$(openssl rand -hex 12)"
    echo "E2E_FAKE_WEBHOOK_SECRET=$(openssl rand -hex 16)"
  } > "$GERADO/e2e.env"
  echo "senhas de DEV geradas em $GERADO/e2e.env"
fi

"$PY" "$AQUI/scripts/ferramentas.py" gerar-chaves "$GERADO_WIN"

# Falha cedo se alguma porta do host ja estiver em uso por outro processo.
carregar_env
ocupadas="$("$PY" - "$E2E_PORTA_SQL" "$E2E_PORTA_AMQP" "$E2E_PORTA_RABBIT_UI" "$E2E_PORTA_MONGO" "$E2E_PORTA_OS" \
  "$E2E_PORTA_BILLING" "$E2E_PORTA_EXECUCAO" "$E2E_PORTA_JWKS" "$E2E_PORTA_MAILPIT_UI" "$E2E_PORTA_SMTP" <<'PY'
import socket, sys
for porta in sys.argv[1:]:
    with socket.socket() as s:
        s.settimeout(0.5)
        if s.connect_ex(("127.0.0.1", int(porta))) == 0:
            print(porta, end=" ")
PY
)"
if [ -n "${ocupadas// /}" ]; then
  # So e problema se a porta nao for do proprio projeto (um "subir" repetido sobre o ambiente ja de pe e legitimo).
  if [ -z "$(compose ps -q 2>/dev/null)" ]; then
    echo "portas do host em uso por outro processo: $ocupadas (sobrescreva com E2E_PORTA_*)." >&2
    exit 1
  fi
fi

if [ "$SEM_BUILD" -eq 0 ]; then
  echo "== build das imagens (OS, Billing, Execucao) =="
  compose build
fi

echo "== subindo =="
compose up -d

echo "== esperando as tres APIs ficarem prontas (/health/ready) =="
inicio=$(date +%s)
for api in "OS:$E2E_PORTA_OS" "Billing:$E2E_PORTA_BILLING" "Execucao:$E2E_PORTA_EXECUCAO"; do
  nome="${api%%:*}"; porta="${api##*:}"
  until [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "http://127.0.0.1:$porta/health/ready" || true)" = "200" ]; do
    if [ $(( $(date +%s) - inicio )) -gt 420 ]; then
      echo "TIMEOUT: $nome nao ficou pronto em 7 min. Veja: docker compose -p oficina-e2e logs $nome" >&2
      compose ps >&2
      exit 1
    fi
    sleep 3
  done
  echo "  $nome pronto em $(( $(date +%s) - inicio )) s"
done

compose ps
cat <<EOF

Ambiente pronto. Portas (127.0.0.1):
  OS         http://127.0.0.1:$E2E_PORTA_OS        Billing  http://127.0.0.1:$E2E_PORTA_BILLING        Execucao http://127.0.0.1:$E2E_PORTA_EXECUCAO
  JWKS stub  http://127.0.0.1:$E2E_PORTA_JWKS/.well-known/jwks.json
  RabbitMQ   amqp $E2E_PORTA_AMQP | UI http://127.0.0.1:$E2E_PORTA_RABBIT_UI (usuario/senha em $GERADO/e2e.env)
  SQL Server $E2E_PORTA_SQL | MongoDB $E2E_PORTA_MONGO | Mailpit http://127.0.0.1:$E2E_PORTA_MAILPIT_UI
Cenarios: bash e2e/executar-cenarios.sh
EOF
