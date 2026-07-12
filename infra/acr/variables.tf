variable "location" {
  description = "Regiao do Azure onde o registry sera criado. Sempre fornecido pelo main.tf raiz (module.rg.location)."
  type        = string
}

variable "resource_group_name" {
  description = "Nome do resource group (modulo infra/rg) onde o registry sera criado. Sempre fornecido pelo main.tf raiz (module.rg.resource_group_name)."
  type        = string
}

variable "registry_name" {
  description = "Nome do Container Registry. Globalmente unico em todo o Azure, so alfanumerico (sem hifen)."
  type        = string
}

variable "sku" {
  description = "SKU do registry (Basic, Standard ou Premium). Standard esta no free tier por 12 meses (100GB storage, 10 webhooks)."
  type        = string
  default     = "Standard"
}

variable "public_network_access_enabled" {
  description = "Permite acesso publico ao registry (necessario para o AKS puxar imagens sem configuracao extra de rede privada)."
  type        = bool
  default     = true
}

variable "zone_redundancy_enabled" {
  description = "Habilita zone redundancy. So disponivel no SKU Premium; manter false no Standard."
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags aplicadas ao registry."
  type        = map(string)
  default     = {}
}
