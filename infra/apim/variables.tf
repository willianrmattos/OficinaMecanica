variable "location" {
  description = "Regiao do Azure onde a APIM sera criada. Sempre fornecido pelo main.tf raiz (module.rg.location)."
  type        = string
}

variable "resource_group_name" {
  description = "Nome do resource group (modulo infra/rg) onde a APIM sera criada. Sempre fornecido pelo main.tf raiz (module.rg.resource_group_name)."
  type        = string
}

variable "apim_name" {
  description = "Nome da instancia do API Management. Globalmente unico (vira <nome>.azure-api.net)."
  type        = string
  default     = "apimfiap"
}

variable "publisher_name" {
  description = "Nome do publisher exibido no portal da APIM."
  type        = string
  default     = "OficinaMecanica"
}

variable "publisher_email" {
  description = "E-mail do publisher (usado pela APIM para notificacoes). Sem valor padrao: definir via TF_VAR_apim_publisher_email ou um .tfvars nao versionado."
  type        = string
}

variable "ingress_nginx_namespace" {
  description = "Namespace onde o ingress-nginx foi instalado via Helm (modulo infra/helm) - usado pra ler o IP publico do LoadBalancer via data source do Kubernetes."
  type        = string
}

variable "ingress_nginx_service_name" {
  description = "Nome do Service do controller do ingress-nginx (chart oficial cria com este nome por padrao)."
  type        = string
  default     = "ingress-nginx-controller"
}

variable "tags" {
  description = "Tags aplicadas aos recursos."
  type        = map(string)
  default     = {}
}
