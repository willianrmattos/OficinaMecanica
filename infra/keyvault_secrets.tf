# Optei por guardar os segredos da aplicacao no Key Vault (modulo keyvault),
# em vez de aplicados manualmente via k8s/oficinamecanica-api/secret.yaml. O
# CSI Secrets Store driver (addon do modulo aks) sincroniza esses valores pro
# Secret nativo do Kubernetes automaticamente - ver
# k8s/oficinamecanica-api/secret-provider-class.yaml.
#
# Deixei esses recursos na raiz (nao dentro do modulo keyvault) de proposito:
# o modulo keyvault so cuida do vault em si (recurso generico e reutilizavel);
# quem sabe quais segredos esta aplicacao especifica precisa e a raiz.

resource "azurerm_key_vault_secret" "jwt_secret_key" {
  name         = "jwt-secret-key"
  value        = var.jwt_secret_key
  key_vault_id = module.keyvault.key_vault_id
}

resource "azurerm_key_vault_secret" "admin_senha" {
  name         = "admin-senha"
  value        = var.admin_senha
  key_vault_id = module.keyvault.key_vault_id
}

resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = "sql-connection-string"
  value        = "Server=tcp:${module.sqldb.server_fqdn},1433;Database=${module.sqldb.database_name};User Id=${var.sql_administrator_login};Password=${var.sql_administrator_login_password};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  key_vault_id = module.keyvault.key_vault_id
}
