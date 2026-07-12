variable "location" {
  description = "Regiao do Azure onde o Key Vault sera criado. Sempre fornecido pelo main.tf raiz (module.rg.location)."
  type        = string
}

variable "resource_group_name" {
  description = "Nome do resource group (modulo infra/rg) onde o Key Vault sera criado. Sempre fornecido pelo main.tf raiz (module.rg.resource_group_name)."
  type        = string
}

variable "key_vault_name" {
  description = "Nome do Key Vault. Globalmente unico (vira <nome>.vault.azure.net)."
  type        = string
}

variable "client_ip_address" {
  description = "IP publico autorizado a acessar o Key Vault, alem dos servicos Azure confiaveis (bypass). Null bloqueia o acesso direto ate que essa variavel seja definida."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags aplicadas aos recursos."
  type        = map(string)
  default     = {}
}
