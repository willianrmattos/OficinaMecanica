output "registry_name" {
  description = "Nome do Container Registry criado."
  value       = azurerm_container_registry.this.name
}

output "registry_id" {
  description = "ID do Container Registry criado."
  value       = azurerm_container_registry.this.id
}

output "login_server" {
  description = "Endereco de login do registry (usado em docker login / docker push)."
  value       = azurerm_container_registry.this.login_server
}
