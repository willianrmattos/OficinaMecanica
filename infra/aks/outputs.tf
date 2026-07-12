output "cluster_name" {
  description = "Nome do cluster AKS criado."
  value       = azurerm_kubernetes_cluster.this.name
}

output "cluster_id" {
  description = "ID do cluster AKS criado."
  value       = azurerm_kubernetes_cluster.this.id
}

output "kube_config" {
  description = "Kubeconfig bruto do cluster (sensivel). Utilizei 'az aks get-credentials' para conectar (que usa a autenticacao Azure AD existente, nao uma credencial estatica)."
  value       = azurerm_kubernetes_cluster.this.kube_config_raw
  sensitive   = true
}

output "oidc_issuer_url" {
  description = "URL do emissor OIDC do cluster - necessaria para configurar federated credential (Workload Identity) em managed identities."
  value       = azurerm_kubernetes_cluster.this.oidc_issuer_url
}

# Campos estruturados do kube_config (em vez do kube_config_raw acima),
# usados apenas para configurar o provider "helm" (infra/providers.tf), que
# requer host/certificados separados em vez do YAML inteiro.
output "kube_config_host" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].host
  sensitive = true
}

output "kube_config_client_certificate" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].client_certificate
  sensitive = true
}

output "kube_config_client_key" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].client_key
  sensitive = true
}

output "kube_config_cluster_ca_certificate" {
  value     = azurerm_kubernetes_cluster.this.kube_config[0].cluster_ca_certificate
  sensitive = true
}
