data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "this" {
  name                       = var.key_vault_name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true

  # Soft-delete e obrigatorio desde 2020 e nao pode ser desabilitado; apenas o
  # periodo de retencao e configuravel. Configurei a retencao minima (7 dias)
  # para reduzir o tempo em que o nome do vault ficaria reservado no estado
  # "soft-deleted", caso seja necessario destruir e recriar o recurso com o
  # mesmo nome.
  soft_delete_retention_days = 7
  purge_protection_enabled   = false

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
    ip_rules = concat(
      var.client_ip_address != null ? [var.client_ip_address] : [],
      var.aks_outbound_ip_address != null ? [var.aks_outbound_ip_address] : []
    )
  }

  tags = var.tags
}

resource "azurerm_role_assignment" "admin" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}
