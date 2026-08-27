data "azurerm_client_config" "current" {}

module "rg" {
  source = "./rg"

  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

module "storage" {
  source = "./storage"

  location             = module.rg.location
  resource_group_name  = module.rg.resource_group_name
  storage_account_name = var.storage_account_name
  tags                 = var.tags
}

module "acr" {
  source = "./acr"

  location            = module.rg.location
  resource_group_name = module.rg.resource_group_name
  registry_name       = var.registry_name
  tags                = var.tags
}

module "aks" {
  source = "./aks"

  location            = module.rg.location
  resource_group_name = module.rg.resource_group_name
  cluster_name        = var.aks_cluster_name
  acr_id              = module.acr.registry_id
  tags                = var.tags
}

module "keyvault" {
  source = "./keyvault"

  location                = module.rg.location
  resource_group_name     = module.rg.resource_group_name
  key_vault_name          = var.key_vault_name
  client_ip_address       = var.key_vault_client_ip_address
  aks_outbound_ip_address = data.azurerm_public_ip.aks_outbound.ip_address
  tags                    = var.tags
}

module "helm" {
  source = "./helm"

  storage_class_name = var.monitoring_storage_class_name
  apim_name          = var.apim_name

  depends_on = [module.aks]
}

module "sqldb" {
  source = "./sqldb"

  location                     = var.sql_location
  resource_group_name          = module.rg.resource_group_name
  server_name                  = var.sql_server_name
  database_name                = var.sql_database_name
  administrator_login          = var.sql_administrator_login
  administrator_login_password = var.sql_administrator_login_password
  client_ip_address            = var.sql_client_ip_address
  tags                         = var.tags
}

module "github_oidc" {
  source = "./github_oidc"

  github_repo = var.github_repo
  acr_id      = module.acr.registry_id
  aks_id      = module.aks.cluster_id
}

module "apim" {
  source = "./apim"

  location                = module.rg.location
  resource_group_name     = module.rg.resource_group_name
  apim_name               = var.apim_name
  publisher_name          = var.apim_publisher_name
  publisher_email         = var.apim_publisher_email
  ingress_nginx_namespace = module.helm.release_namespace
  tags                    = var.tags
}
