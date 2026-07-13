resource "helm_release" "loki" {
  name             = var.loki_release_name
  repository       = "https://grafana-community.github.io/helm-charts"
  chart            = "loki"
  namespace        = var.monitoring_namespace
  create_namespace = true
  version          = var.loki_chart_version

  values = [
    templatefile("${path.module}/loki.yaml.tpl", {
      storage_class_name = var.storage_class_name
    })
  ]
}

# O Alloy le os arquivos de log dos containers direto do disco do node
# (alloy.mounts.varlog, em alloy.yaml.tpl) - abordagem de tailing de arquivo,
# mais leve que o modo alternativo de streaming via API do Kubernetes. Como
# o cluster tem 1 node so (infra/aks), o DaemonSet do Alloy sobe apenas 1 pod.
resource "helm_release" "alloy" {
  name             = var.alloy_release_name
  repository       = "https://grafana.github.io/helm-charts"
  chart            = "alloy"
  namespace        = var.monitoring_namespace
  create_namespace = true
  version          = var.alloy_chart_version

  values = [
    templatefile("${path.module}/alloy.yaml.tpl", {
      loki_push_url = "http://${var.loki_release_name}.${var.monitoring_namespace}.svc.cluster.local:3100/loki/api/v1/push"
    })
  ]

  depends_on = [helm_release.loki]
}
