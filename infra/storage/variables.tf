variable "location" {
  description = "Regiao do Azure onde a storage account sera criada. Sempre fornecido pelo main.tf raiz (module.rg.location)."
  type        = string
}

variable "resource_group_name" {
  description = "Nome do resource group (modulo infra/rg) onde a storage account sera criada. Sempre fornecido pelo main.tf raiz (module.rg.resource_group_name)."
  type        = string
}

variable "storage_account_name" {
  description = "Nome da storage account. Globalmente unico em todo o Azure, so minusculas/numeros, 3-24 caracteres."
  type        = string
}

variable "tags" {
  description = "Tags aplicadas a storage account."
  type        = map(string)
  default     = {}
}
