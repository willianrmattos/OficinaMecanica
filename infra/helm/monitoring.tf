# Instalei o kube-prometheus-stack (Prometheus + Grafana + kube-state-metrics
# + node-exporter) self-hospedado no proprio cluster. Ele compartilha
# CPU/memoria com os demais workloads no node unico do aksfiap (ver infra/aks
# para o dimensionamento do node pool).
#
# Configurei PVC para Prometheus e Grafana, usando a StorageClass Premium ja
# provisionada pelo AKS (managed-csi-premium: disk.csi.azure.com, Premium_LRS,
# reclaimPolicy Delete por padrao). Sem persistencia, os dados do Prometheus
# e os dashboards/usuarios criados via UI do Grafana seriam perdidos a cada
# reinicio do pod.
#
# Dimensionei os volumes pelo uso real esperado, nao pelo limite do free tier
# de Managed Disks: para uma retencao de 6h, o volume de dados do Prometheus
# fica na casa de dezenas de MB (aproximadamente 500 amostras/s * 21.600s *
# ~1,5 byte comprimido por amostra). 8Gi ja oferece margem confortavel, sem
# superdimensionar o recurso.
#
# Deixei o campo "grafana.adminPassword" sem valor fixo: o chart gera uma
# senha aleatoria a cada instalacao (armazenada no Secret
# "<release>-grafana"), evitando expor credenciais no state ou no
# codigo-fonte.
#
# Configurei dashboards do Grafana como codigo (grafana.sidecar.dashboards,
# em monitoring.yaml.tpl): os dashboards versionados em ConfigMap
# (k8s/monitoring/) sao reimportados automaticamente pelo sidecar,
# independente da persistencia do disco - o volume passa a ser apenas
# cache/conveniencia, nao a unica copia do conteudo.
resource "helm_release" "monitoring" {
  name             = var.monitoring_release_name
  repository       = "https://prometheus-community.github.io/helm-charts"
  chart            = "kube-prometheus-stack"
  namespace        = var.monitoring_namespace
  create_namespace = true
  version          = var.monitoring_chart_version

  # Tive um erro de timeout (context deadline exceeded) na primeira
  # instalacao com o default de 300s: chart grande, com varias imagens sem
  # cache local no node (pulls de ate alguns minutos cada), e o Grafana
  # demorou ainda mais na primeira subida (centenas de migrations no banco
  # interno, cada uma com um fsync no disco Premium pequeno - gargalo de I/O,
  # nao de CPU/memoria). Aumentei para 900s porque a instalacao continua em
  # segundo plano mesmo apos o timeout do Terraform; a demora ocorre apenas
  # na primeira instalacao, ja que o schema passa a existir no disco e
  # reinicios futuros nao repetem as migrations.
  timeout = 900

  values = [
    templatefile("${path.module}/monitoring.yaml.tpl", {
      storage_class_name   = var.storage_class_name
      loki_release_name    = var.loki_release_name
      monitoring_namespace = var.monitoring_namespace
    })
  ]

  # O datasource "Loki" que configurei em monitoring.yaml.tpl
  # (grafana.additionalDataSources) aponta pro Service que o helm_release.loki
  # cria - sem essa dependencia explicita, o Terraform poderia tentar
  # instalar os dois charts em paralelo (a ordem entre helm_release sem
  # dependencia direta no codigo nao e garantida).
  depends_on = [helm_release.loki]
}
