variable "location" {
  description = "Regiao do Azure onde o resource group sera criado. Sempre fornecido pelo main.tf raiz."
  type        = string
}

variable "resource_group_name" {
  description = "Nome do resource group. Sempre fornecido pelo main.tf raiz."
  type        = string
}

variable "tags" {
  description = "Tags aplicadas ao resource group."
  type        = map(string)
  default     = {}
}
