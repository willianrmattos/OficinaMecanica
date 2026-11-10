#!/usr/bin/env bash
# Valida os manifests de k8s/oficinamecanica-api e k8s/mailpit com o kubeconform (-strict: campo desconhecido ou de tipo
# errado reprova) contra o schema do Kubernetes. Renderiza antes o unico placeholder (${IMAGE}), exatamente como o job
# deploy-to-aks faz, para validar o que de fato seria aplicado. Precisa de Docker (o kubeconform roda em container) e de
# envsubst.
#
# Uso: bash scripts/validar-k8s.sh
# Variaveis: K8S_DIRS (diretorios separados por espaco; padrao: k8s/oficinamecanica-api k8s/mailpit),
#            K8S_VERSAO (versao do schema do Kubernetes)
#
# O SecretProviderClass e um CRD do CSI Secrets Store: o kubeconform nao traz o schema dele, entao ele e ignorado
# (-ignore-missing-schemas) e so os recursos nativos sao validados de verdade.
set -euo pipefail

cd "$(dirname "$0")/.."

K8S_DIRS="${K8S_DIRS:-k8s/oficinamecanica-api k8s/mailpit}"
K8S_VERSAO="${K8S_VERSAO:-1.33.0}"
KUBECONFORM_IMAGEM="ghcr.io/yannh/kubeconform:v0.7.0"
IMAGEM_DE_VALIDACAO="acrfiap.azurecr.io/oficinamecanica-api:validacao"
RENDERIZADO="artifacts/k8s-renderizado"

rm -rf "$RENDERIZADO"
mkdir -p "$RENDERIZADO"
# Um subdiretorio por pasta de origem: os nomes dos arquivos (deployment.yaml, service.yaml...) se repetem entre elas.
for diretorio in $K8S_DIRS; do
  destino="$RENDERIZADO/$(basename "$diretorio")"
  mkdir -p "$destino"
  for arquivo in "$diretorio"/*.yaml; do
    nome="$(basename "$arquivo")"
    # shellcheck disable=SC2016  # aspas simples de proposito: o envsubst recebe o NOME da variavel, nao o valor
    IMAGE="$IMAGEM_DE_VALIDACAO" envsubst '${IMAGE}' < "$arquivo" > "$destino/$nome"
  done
done

# No Git Bash do Windows o docker precisa do caminho do host no formato Windows e sem a conversao automatica de /manifests.
export MSYS_NO_PATHCONV=1
RAIZ="$(pwd -W 2>/dev/null || pwd)"

docker run --rm -v "$RAIZ/$RENDERIZADO:/manifests:ro" "$KUBECONFORM_IMAGEM" \
  -strict -summary -kubernetes-version "$K8S_VERSAO" -ignore-missing-schemas /manifests
