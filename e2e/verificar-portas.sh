#!/usr/bin/env bash
# Confere que nenhuma porta de host do e2e colide com as publicadas pelos docker-compose do OS Service, do Billing, da
# Execucao e do Seguranca (incluindo os perfis standalone, que publicam um RabbitMQ proprio), e que as portas do e2e
# sao distintas entre si. Le as portas dos arquivos, nao de uma lista fixa, para acompanhar mudancas nos composes.
#
# Uso: bash e2e/verificar-portas.sh      (exit 0 = sem colisao; exit 1 = lista as colisoes)
set -uo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib-ambiente.sh
source "$AQUI/lib-ambiente.sh"
carregar_env

os="$E2E_CONTEXTO_OS_RESOLVIDO"
repos=("$os/docker-compose.yml" "$E2E_CONTEXTO_BILLING_RESOLVIDO/docker-compose.yml" "$E2E_CONTEXTO_EXECUCAO_RESOLVIDO/docker-compose.yml")
seguranca="$(cd "$os/.." 2>/dev/null && pwd)/OficinaMecanica.Seguranca/docker-compose.yml"
[ -f "$seguranca" ] && repos+=("$seguranca")

repos_win=()
for arq in "${repos[@]}"; do
  [ -f "$arq" ] && repos_win+=("$(win "$arq")") || echo "AVISO: $arq nao encontrado, ignorado" >&2
done

# As portas do e2e sao as efetivas (padrao ou sobrescrita por E2E_PORTA_*).
e2e_portas="SQL=$E2E_PORTA_SQL AMQP=$E2E_PORTA_AMQP UI_RABBIT=$E2E_PORTA_RABBIT_UI MONGO=$E2E_PORTA_MONGO OS=$E2E_PORTA_OS BILLING=$E2E_PORTA_BILLING EXECUCAO=$E2E_PORTA_EXECUCAO JWKS=$E2E_PORTA_JWKS MAILPIT_UI=$E2E_PORTA_MAILPIT_UI SMTP=$E2E_PORTA_SMTP"

AQUI_PY="$(win "$AQUI")" "$PY" - "$e2e_portas" "${repos_win[@]}" <<'PY'
import subprocess, sys, os
e2e = dict(p.split("=") for p in sys.argv[1].split())
ferr = os.path.join(os.environ["AQUI_PY"], "scripts", "ferramentas.py")
saida = subprocess.run([sys.executable, ferr, "portas-compose", *sys.argv[2:]], capture_output=True, text=True, check=True).stdout
publicadas = {}
for linha in saida.splitlines():
    porta, repo = linha.split()
    publicadas.setdefault(porta, set()).add(repo)
print(f"portas publicadas pelos composes dos repositorios: {len(publicadas)} ({', '.join(sorted(publicadas, key=int))})")
colisoes = [(nome, porta, sorted(publicadas[porta])) for nome, porta in e2e.items() if porta in publicadas]
repetidas = sorted({p for p in e2e.values() if list(e2e.values()).count(p) > 1})
print("portas do e2e: " + ", ".join(f"{n}={p}" for n, p in e2e.items()))
for nome, porta, repos in colisoes:
    print(f"COLISAO: {nome}={porta} e publicada por {', '.join(repos)}")
for p in repetidas:
    print(f"REPETIDA: a porta {p} aparece mais de uma vez no e2e")
if colisoes or repetidas:
    sys.exit(1)
print("OK: nenhuma porta do e2e colide com os composes dos repositorios nem entre si.")
PY
