#!/usr/bin/env bash
# Confere que os contratos de mensagem (OficinaMecanica.Contracts.*) copiados em cada repositorio sao identicos ao do
# OS Service, que e a fonte da verdade (ADR 0005): os goldens JSON de Contracts.Tests (bytes) e os tipos em
# src/OficinaMecanica.Contracts (sha256 por arquivo).
#
# Optei por tratar o OS como superconjunto: Billing e Execucao copiam so o que usam, entao a lista de arquivos NAO e
# igual nos tres. O que o script exige e (1) todo arquivo copiado existe no OS e tem o mesmo hash, (2) cada servico tem
# TODAS as mensagens do seu lado da conversa, e (3) o que Billing e Execucao compartilham e identico entre si.
#
# Uso: bash e2e/verificar-contratos.sh      (exit 0 = tudo identico; exit 1 = divergencia ou contrato faltando)
set -uo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib-ambiente.sh
source "$AQUI/lib-ambiente.sh"

OS="$E2E_CONTEXTO_OS_RESOLVIDO"
BILLING="$E2E_CONTEXTO_BILLING_RESOLVIDO"
EXECUCAO="$E2E_CONTEXTO_EXECUCAO_RESOLVIDO"
GOLDEN="tests/OficinaMecanica.Contracts.Tests/Golden"
FONTES="src/OficinaMecanica.Contracts"

# Mensagens de cada lado da conversa (quem consome o comando, quem publica o evento), de acordo com a Filas.cs.
CONTRATOS_BILLING=(GerarOrcamento SolicitarPagamento CancelarOrcamento CancelarPagamento EstornarPagamento
  OrcamentoGerado OrcamentoNaoGerado PagamentoSolicitado PagamentoNaoSolicitado PagamentoAprovado PagamentoCancelado
  PagamentoEstornado EstornoFalhou)
CONTRATOS_EXECUCAO=(SolicitarDiagnostico IniciarExecucao CancelarProducao DiagnosticoIniciado DiagnosticoConcluido
  ExecucaoFinalizada ExecucaoInviavel)

falhas=0
ok=0

hash_bytes() { sha256sum "$1" | cut -d' ' -f1; }
hash_sem_cr() { tr -d '\r' < "$1" | sha256sum | cut -d' ' -f1; }

registrar_ok() { ok=$((ok + 1)); }
registrar_falha() { falhas=$((falhas + 1)); echo "  FALHA: $1"; }

# Lista os arquivos de uma pasta (caminho relativo), sem bin/obj.
listar() { (cd "$1" 2>/dev/null && find . -type f \( -name '*.json' -o -name '*.cs' \) -not -path './bin/*' -not -path './obj/*' | sed 's|^\./||' | sort); }

comparar_com_os() {
  local nome="$1" base="$2" subpasta="$3" modo="$4" # modo: bytes | sem-cr
  local total=0 divergentes=0
  while IFS= read -r arq; do
    [ -z "$arq" ] && continue
    total=$((total + 1))
    local origem="$OS/$subpasta/$arq" copia="$base/$subpasta/$arq"
    if [ ! -f "$origem" ]; then
      registrar_falha "$nome: $subpasta/$arq nao existe no OS (copia sem origem)"; divergentes=$((divergentes + 1)); continue
    fi
    if [ "$(hash_bytes "$origem")" = "$(hash_bytes "$copia")" ]; then
      registrar_ok
    elif [ "$modo" = "sem-cr" ] && [ "$(hash_sem_cr "$origem")" = "$(hash_sem_cr "$copia")" ]; then
      echo "  AVISO: $nome: $subpasta/$arq difere so no fim de linha (CRLF x LF); conteudo igual"; registrar_ok
    else
      registrar_falha "$nome: $subpasta/$arq DIVERGE do OS (sha256 $(hash_bytes "$copia" | cut -c1-12) x $(hash_bytes "$origem" | cut -c1-12))"
      divergentes=$((divergentes + 1))
    fi
  done < <(listar "$base/$subpasta")
  echo "  $nome $subpasta: $total arquivo(s) copiado(s), $divergentes divergente(s)"
}

exigir_contratos() {
  local nome="$1" base="$2"; shift 2
  local faltando=0
  for c in "$@"; do
    [ -f "$base/$GOLDEN/$c.json" ] || { registrar_falha "$nome: golden $c.json ausente"; faltando=$((faltando + 1)); }
    if [ ! -f "$base/$FONTES/Commands/$c.cs" ] && [ ! -f "$base/$FONTES/Events/$c.cs" ]; then
      registrar_falha "$nome: tipo $c.cs ausente em $FONTES"; faltando=$((faltando + 1))
    fi
  done
  [ -f "$base/$GOLDEN/Filas.json" ] || { registrar_falha "$nome: golden Filas.json ausente"; faltando=$((faltando + 1)); }
  echo "  $nome: $# mensagem(ns) obrigatoria(s) do seu lado, $faltando ausencia(s)"
}

echo "== Fonte da verdade (OS): $OS =="
echo "  $(listar "$OS/$GOLDEN" | wc -l) goldens, $(listar "$OS/$FONTES" | wc -l) tipos"
for repo in "Billing:$BILLING" "Execucao:$EXECUCAO"; do
  nome="${repo%%:*}"; base="${repo#*:}"
  echo "== $nome ($base) =="
  [ -d "$base/$GOLDEN" ] || { registrar_falha "$nome: pasta $GOLDEN nao encontrada"; continue; }
  comparar_com_os "$nome" "$base" "$GOLDEN" bytes
  comparar_com_os "$nome" "$base" "$FONTES" sem-cr
done

echo "== Cobertura: cada servico tem todas as mensagens do seu lado =="
exigir_contratos "Billing" "$BILLING" "${CONTRATOS_BILLING[@]}"
exigir_contratos "Execucao" "$EXECUCAO" "${CONTRATOS_EXECUCAO[@]}"

echo "== Billing x Execucao (interseccao dos dois conjuntos copiados) =="
comuns=0
while IFS= read -r arq; do
  [ -z "$arq" ] && continue
  for pasta in "$GOLDEN" "$FONTES"; do
    if [ -f "$BILLING/$pasta/$arq" ] && [ -f "$EXECUCAO/$pasta/$arq" ]; then
      comuns=$((comuns + 1))
      if [ "$(hash_sem_cr "$BILLING/$pasta/$arq")" = "$(hash_sem_cr "$EXECUCAO/$pasta/$arq")" ]; then registrar_ok
      else registrar_falha "$pasta/$arq difere entre Billing e Execucao"; fi
    fi
  done
done < <(listar "$BILLING/$GOLDEN"; listar "$BILLING/$FONTES")
echo "  $comuns arquivo(s) presentes nos dois, comparados"

echo
if [ "$falhas" -eq 0 ]; then
  echo "RESULTADO: contratos IDENTICOS nos 3 repositorios ($ok comparacao(oes) de hash ok)."
  exit 0
fi
echo "RESULTADO: $falhas divergencia(s)/ausencia(s) nos contratos ($ok comparacao(oes) ok)." >&2
exit 1
