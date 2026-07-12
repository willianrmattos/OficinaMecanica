variable "location" {
  description = "Regiao do Azure onde o cluster sera criado. Sempre fornecido pelo main.tf raiz (module.rg.location)."
  type        = string
}

variable "resource_group_name" {
  description = "Nome do resource group (modulo infra/rg) onde o cluster sera criado. Sempre fornecido pelo main.tf raiz (module.rg.resource_group_name)."
  type        = string
}

variable "cluster_name" {
  description = "Nome do cluster AKS."
  type        = string
}

variable "dns_prefix" {
  description = "Prefixo DNS do cluster (usado no FQDN do API server)."
  type        = string
  default     = "aksfiap-dns"
}

variable "kubernetes_version" {
  description = "Versao do Kubernetes. Deixar null usa a versao padrao/mais recente suportada pelo Azure no momento do apply."
  type        = string
  default     = null
}

variable "node_vm_size" {
  description = "Tamanho da VM do node pool. Tentei a serie B (burstable, mais barata) mas o AKS nao aceita como node de sistema; tentei tambem Standard_F2s_v2, que apareceu como indisponivel nesta combinacao de regiao/assinatura. Fechei em Standard_D2s_v3 (familia Standard DSv3), compativel com a cota e as restricoes de disponibilidade da assinatura Student utilizada."
  type        = string
  default     = "Standard_D2s_v3"
}

variable "node_count" {
  description = "Numero de nodes no node pool. Mantive em 1: o control plane do AKS e sempre gratis, mas o node_vm_size atual (Standard_D2s_v3) nao entra no free tier de VMs - cada node adicional gera custo."
  type        = number
  default     = 1
}

variable "sku_tier" {
  description = "Tier do cluster AKS. Escolhi Free (sem SLA contratual) porque o control plane nao gera custo adicional neste tier."
  type        = string
  default     = "Free"
}

variable "acr_id" {
  description = "ID do Container Registry (modulo infra/acr), usado para dar permissao de pull ao cluster."
  type        = string
}

variable "key_vault_id" {
  description = "ID do Key Vault (modulo infra/keyvault), usado para dar permissao de leitura de secrets a managed identity do CSI driver."
  type        = string
}

variable "authorized_ip_ranges" {
  description = "IPs (CIDR) autorizados a acessar o API server publico. Lista vazia = sem restricao (qualquer IP acessa, equivalente a deixar 'Set authorized IP ranges' desmarcado no portal)."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Tags aplicadas aos recursos."
  type        = map(string)
  default     = {}
}
