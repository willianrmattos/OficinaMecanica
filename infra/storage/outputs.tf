output "storage_account_name" {
  description = "Nome da storage account criada."
  value       = azurerm_storage_account.this.name
}

output "storage_account_id" {
  description = "ID da storage account criada."
  value       = azurerm_storage_account.this.id
}

output "container_name" {
  description = "Nome do container de blob criado para guardar o tfstate."
  value       = azurerm_storage_container.tfstate.name
}
