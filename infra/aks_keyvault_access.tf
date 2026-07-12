# Optei por deixar na raiz (nao dentro de infra/aks/ nem infra/keyvault/):
# a role assignment abaixo precisa do ID do Key Vault (saida do modulo
# keyvault) e o data source precisa de saidas do modulo aks - se qualquer um
# dos dois vivesse dentro do outro modulo, os dois module blocks passariam a
# depender um do outro (dependencia circular, que o Terraform recusa).

# Concede a managed identity do addon CSI Secrets Store driver (modulo aks)
# permissao de leitura no Key Vault (modulo keyvault) - mesmo papel que
# antes vivia dentro de infra/aks/main.tf.
resource "azurerm_role_assignment" "aks_keyvault_secrets_user" {
  scope                            = module.keyvault.key_vault_id
  role_definition_name             = "Key Vault Secrets User"
  principal_id                     = module.aks.key_vault_secrets_provider_object_id
  skip_service_principal_aad_check = true
}

# Descobre o IP de saida real do cluster automaticamente (em vez de fixar
# manualmente) - assim, se o cluster for destruido e recriado do zero, um
# unico "terraform apply" ja recalcula o IP novo e atualiza o firewall do
# Key Vault (infra/keyvault/main.tf) sem nenhum passo manual.
data "azurerm_public_ip" "aks_outbound" {
  name                = module.aks.outbound_public_ip_name
  resource_group_name = module.aks.node_resource_group
}
