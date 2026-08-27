provider "azurerm" {
  features {}
}

# Nao precisei de configuracao adicional aqui: usa a mesma sessao autenticada
# do az CLI que o provider azurerm. Gerencia recursos de Azure AD (App
# Registration, Service Principal, Federated Identity Credential do modulo
# github_oidc).
provider "azuread" {}

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

# Usado pelo modulo infra/apim pra ler o IP publico do LoadBalancer do
# ingress-nginx (data "kubernetes_service") - diferente do provider "helm"
# acima, o provider "kubernetes" espera os campos direto na raiz do bloco,
# nao aninhados dentro de um bloco kubernetes {} (isso e especifico do
# schema do provider helm).
provider "kubernetes" {
  host                   = module.aks.kube_config_host
  client_certificate     = base64decode(module.aks.kube_config_client_certificate)
  client_key             = base64decode(module.aks.kube_config_client_key)
  cluster_ca_certificate = base64decode(module.aks.kube_config_cluster_ca_certificate)
}
