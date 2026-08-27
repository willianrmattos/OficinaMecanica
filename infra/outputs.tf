output "resource_group_name" {
  description = "Nome do resource group criado."
  value       = module.rg.resource_group_name
}

output "resource_group_id" {
  description = "ID do resource group criado."
  value       = module.rg.resource_group_id
}

output "storage_account_name" {
  description = "Nome da storage account criada (guarda o tfstate remoto)."
  value       = module.storage.storage_account_name
}

output "storage_container_name" {
  description = "Nome do container de blob criado para o tfstate."
  value       = module.storage.container_name
}

output "registry_name" {
  description = "Nome do Container Registry criado."
  value       = module.acr.registry_name
}

output "registry_login_server" {
  description = "Endereco de login do registry (docker login / docker push)."
  value       = module.acr.login_server
}

output "aks_cluster_name" {
  description = "Nome do cluster AKS criado."
  value       = module.aks.cluster_name
}

output "aks_cluster_id" {
  description = "ID do cluster AKS criado."
  value       = module.aks.cluster_id
}

output "aks_kube_config" {
  description = "Kubeconfig bruto do cluster (sensivel). Utilizei 'az aks get-credentials --resource-group <rg> --name <cluster>' para conectar."
  value       = module.aks.kube_config
  sensitive   = true
}

output "key_vault_name" {
  description = "Nome do Key Vault criado."
  value       = module.keyvault.key_vault_name
}

output "key_vault_uri" {
  description = "URI do Key Vault (usado por SDKs e pelo CSI driver do AKS para acessar os secrets)."
  value       = module.keyvault.key_vault_uri
}

output "ingress_nginx_namespace" {
  description = "Namespace onde o ingress-nginx foi instalado via Helm."
  value       = module.helm.release_namespace
}

output "monitoring_namespace" {
  description = "Namespace onde o Prometheus/Grafana foram instalados via Helm."
  value       = module.helm.monitoring_namespace
}

output "sql_server_fqdn" {
  description = "Endereco completo do SQL Server (usar na connection string)."
  value       = module.sqldb.server_fqdn
}

output "sql_database_name" {
  description = "Nome do banco de dados criado."
  value       = module.sqldb.database_name
}

output "github_actions_client_id" {
  description = "Client ID da App Registration do GitHub Actions - configurar como variavel AZURE_CLIENT_ID no repositorio (gh variable set)."
  value       = module.github_oidc.client_id
}

output "azure_tenant_id" {
  description = "Tenant ID da assinatura Azure atual - configurar como variavel AZURE_TENANT_ID no repositorio."
  value       = data.azurerm_client_config.current.tenant_id
}

output "azure_subscription_id" {
  description = "Subscription ID da assinatura Azure atual - configurar como variavel AZURE_SUBSCRIPTION_ID no repositorio."
  value       = data.azurerm_client_config.current.subscription_id
}

output "key_vault_secrets_provider_client_id" {
  description = "Client ID da managed identity do addon CSI Secrets Store driver - usar no campo userAssignedIdentityID do SecretProviderClass (k8s/oficinamecanica-api/secret-provider-class.yaml)."
  value       = module.aks.key_vault_secrets_provider_client_id
}

output "apim_gateway_url" {
  description = "URL publica do gateway da APIM - novo front door da API para os clientes."
  value       = module.apim.gateway_url
}

