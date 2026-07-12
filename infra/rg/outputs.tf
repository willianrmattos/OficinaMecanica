output "resource_group_name" {
  description = "Nome do resource group criado."
  value       = azurerm_resource_group.this.name
}

output "location" {
  description = "Regiao do resource group criado."
  value       = azurerm_resource_group.this.location
}

output "resource_group_id" {
  description = "ID do resource group criado."
  value       = azurerm_resource_group.this.id
}
