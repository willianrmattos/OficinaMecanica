output "client_id" {
  description = "Client ID (Application ID) da App Registration - usar como variavel AZURE_CLIENT_ID no GitHub."
  value       = azuread_application.github_actions.client_id
}

output "service_principal_object_id" {
  description = "Object ID do Service Principal - util para debugar/consultar role assignments."
  value       = azuread_service_principal.github_actions.object_id
}
