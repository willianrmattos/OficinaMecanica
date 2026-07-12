terraform {
  backend "azurerm" {
    resource_group_name  = "rgfiap"
    storage_account_name = "stfiap"
    container_name       = "tfstate"
    key                  = "terraform.tfstate"
    use_azuread_auth     = true
  }
}
