# Requer o cluster aksfiap em execucao. Configurei o provider helm (na raiz,
# infra/providers.tf) para se comunicar diretamente com o Kubernetes API
# server do cluster, diferente dos demais modulos, que utilizam apenas a API
# do Azure Resource Manager.
resource "helm_release" "ingress_nginx" {
  name             = var.release_name
  repository       = "https://kubernetes.github.io/ingress-nginx"
  chart            = "ingress-nginx"
  namespace        = var.namespace
  create_namespace = true
  version          = var.chart_version

  set {
    name  = "controller.service.type"
    value = "LoadBalancer"
  }

  # Tive um incidente em que todo o trafego externo (API, Grafana, Prometheus)
  # ficou bloqueado, mesmo com o cluster funcionando normalmente por dentro.
  # Causa: sem essa annotation, o health probe do Load Balancer do Azure
  # verifica GET / na mesma porta usada pelo trafego real; como a API possui
  # uma regra catch-all (k8s/oficinamecanica-api/ingress.yaml, sem "host"), a
  # raiz e roteada para ela, e a Swagger UI retorna 301 (RoutePrefix vazio
  # redireciona para index.html). O Azure so considera o backend saudavel
  # quando recebe 200, entao o ingress-nginx era marcado como unhealthy.
  # Corrigi apontando o probe para /healthz, servido diretamente pelo
  # ingress-nginx (server block interno), sem depender de nenhuma regra de
  # Ingress ou backend da aplicacao.
  set {
    name  = "controller.service.annotations.service\\.beta\\.kubernetes\\.io/azure-load-balancer-health-probe-request-path"
    value = "/healthz"
  }
}
