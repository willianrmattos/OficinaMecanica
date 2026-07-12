variable "app_display_name" {
  description = "Nome de exibicao da App Registration usada pelo GitHub Actions."
  type        = string
  default     = "github-actions-oficinamecanica"
}

variable "github_repo" {
  description = "Repositorio GitHub no formato 'owner/repo', usado para restringir a Federated Identity Credential a esse repositorio especifico."
  type        = string
}

variable "acr_id" {
  description = "ID do Container Registry (modulo infra/acr), usado para dar permissao de push ao Service Principal do GitHub Actions."
  type        = string
}

variable "aks_id" {
  description = "ID do cluster AKS (modulo infra/aks), usado para dar permissao de buscar credenciais do cluster (az aks get-credentials --admin) ao Service Principal do GitHub Actions."
  type        = string
}
