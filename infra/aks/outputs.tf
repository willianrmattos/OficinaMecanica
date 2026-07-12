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

output "key_vault_secrets_provider_client_id" {
  description = "Client ID da managed identity do addon do CSI Secrets Store driver (key_vault_secrets_provider) - usado no SecretProviderClass (k8s/oficinamecanica-api/secret-provider-class.yaml) para autenticar no Key Vault via useVMManagedIdentity."
  value       = azurerm_kubernetes_cluster.this.key_vault_secrets_provider[0].secret_identity[0].client_id
}

output "key_vault_secrets_provider_object_id" {
  description = "Object ID da mesma managed identity acima - usado para conceder role assignments a ela (ex: Key Vault Secrets User). Fica separado do client_id porque quem usa esse valor e o infra/aks_keyvault_access.tf na raiz, nao este modulo (evita a dependencia circular aks <-> keyvault)."
  value       = azurerm_kubernetes_cluster.this.key_vault_secrets_provider[0].secret_identity[0].object_id
}

output "node_resource_group" {
  description = "Nome do resource group gerenciado automaticamente pelo AKS (MC_*), onde ficam os recursos internos do cluster (node VMSS, IP publico de saida, etc.) - necessario para localizar o IP de saida real via data source (infra/aks_keyvault_access.tf)."
  value       = azurerm_kubernetes_cluster.this.node_resource_group
}

# Calculei esse nome automaticamente a partir de effective_outbound_ips (um
# resource ID, nao o IP em si) em vez de fixar manualmente - se o cluster for
# destruido e recriado, o Azure atribui um IP de saida novo, com um nome de
# recurso novo, e este output acompanha sozinho, sem precisar de nenhum
# passo manual pra descobrir e atualizar o valor.
output "outbound_public_ip_name" {
  description = "Nome do recurso de IP publico usado para trafego de saida do cluster - usado para consultar o IP real via 'data azurerm_public_ip' (infra/aks_keyvault_access.tf)."
  value = element(
    split("/", tolist(azurerm_kubernetes_cluster.this.network_profile[0].load_balancer_profile[0].effective_outbound_ips)[0]),
    length(split("/", tolist(azurerm_kubernetes_cluster.this.network_profile[0].load_balancer_profile[0].effective_outbound_ips)[0])) - 1
  )
}
