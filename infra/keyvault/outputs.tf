output "key_vault_name" {
  description = "Nome do Key Vault criado."
  value       = azurerm_key_vault.this.name
}

output "key_vault_id" {
  description = "ID do Key Vault criado."
  value       = azurerm_key_vault.this.id
}

output "key_vault_uri" {
  description = "URI do Key Vault (https://<nome>.vault.azure.net/), usado por SDKs e pelo CSI driver do AKS para acessar os secrets."
  value       = azurerm_key_vault.this.vault_uri
}
