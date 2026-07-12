provider "azurerm" {
  features {}
}

# Configurei o provider a partir dos outputs do modulo aks (cluster ja
# existente no state) - requer que o aksfiap ja tenha sido criado
# previamente. Criar o cluster e instalar um helm_release na mesma execucao
# inicial resultaria em erro de dependencia circular na configuracao do
# provider, ja que os valores de conexao ainda nao existiriam.
provider "helm" {
  kubernetes {
    host                   = module.aks.kube_config_host
    client_certificate     = base64decode(module.aks.kube_config_client_certificate)
    client_key             = base64decode(module.aks.kube_config_client_key)
    cluster_ca_certificate = base64decode(module.aks.kube_config_cluster_ca_certificate)
  }
}
