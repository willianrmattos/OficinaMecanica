output "server_name" {
  description = "Nome do SQL Server criado."
  value       = azurerm_mssql_server.this.name
}

output "server_fqdn" {
  description = "Endereco completo do servidor (usar na connection string, ex: Server=<valor>,1433)."
  value       = azurerm_mssql_server.this.fully_qualified_domain_name
}

output "database_name" {
  description = "Nome do banco de dados criado."
  value       = azurerm_mssql_database.this.name
}
