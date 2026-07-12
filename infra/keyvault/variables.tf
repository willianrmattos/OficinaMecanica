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

variable "aks_outbound_ip_address" {
  description = "IP de saida do cluster AKS (modulo aks), autorizado a acessar o Key Vault - necessario pro CSI Secrets Store driver conseguir ler segredos de dentro dos pods. Diferente do IP do Load Balancer do ingress-nginx (usado pra trafego de entrada); esse aqui e o IP usado pelo cluster pra trafego de saida (conferir com 'az aks show --query networkProfile.loadBalancerProfile.effectiveOutboundIPs' + 'az network public-ip list' se precisar atualizar apos recriar o cluster). Null bloqueia o acesso ate que essa variavel seja definida."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags aplicadas aos recursos."
  type        = map(string)
  default     = {}
}
