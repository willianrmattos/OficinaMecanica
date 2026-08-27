variable "release_name" {
  description = "Nome do Helm release."
  type        = string
  default     = "ingress-nginx"
}

variable "namespace" {
  description = "Namespace onde o ingress-nginx sera instalado."
  type        = string
  default     = "ingress-nginx"
}

variable "chart_version" {
  description = "Versao do chart ingress-nginx. Fixei na versao instalada (confirmada via `helm list -n ingress-nginx`) para evitar drift silencioso - sem isso, o Terraform reconsultaria o 'latest' do repositorio a cada plan e poderia propor upgrade automaticamente."
  type        = string
  default     = "4.15.1"
}

variable "monitoring_release_name" {
  description = "Nome do Helm release do kube-prometheus-stack (Prometheus + Grafana)."
  type        = string
  default     = "monitoring"
}

variable "monitoring_namespace" {
  description = "Namespace onde o Prometheus/Grafana serao instalados."
  type        = string
  default     = "monitoring"
}

variable "monitoring_chart_version" {
  description = "Versao do chart kube-prometheus-stack. Fixei na versao instalada (confirmada via `helm list -n monitoring`) para evitar drift silencioso, pelo mesmo motivo do chart_version do ingress-nginx."
  type        = string
  default     = "87.15.1"
}

variable "storage_class_name" {
  description = "Nome da StorageClass usada pelos PVCs do Prometheus/Grafana/Loki. Default (na raiz) e a StorageClass Premium que o proprio AKS ja cria (disk.csi.azure.com, Premium_LRS, reclaimPolicy Delete)."
  type        = string
  default     = "managed-csi-premium"
}

variable "loki_release_name" {
  description = "Nome do Helm release do Loki."
  type        = string
  default     = "loki"
}

variable "loki_chart_version" {
  description = "Versao do chart loki."
  type        = string
  default     = "18.4.4"
}

variable "alloy_release_name" {
  description = "Nome do Helm release do Grafana Alloy (coleta os logs dos pods e envia pro Loki."
  type        = string
  default     = "alloy"
}

variable "alloy_chart_version" {
  description = "Versao do chart alloy, repositorio oficial da Grafana. Fixei na versao confirmada instalada, mesmo motivo dos demais chart_version deste modulo."
  type        = string
  default     = "1.10.1"
}

variable "apim_name" {
  description = "Nome da instancia do API Management (modulo infra/apim) - usado so pra montar a URL publica (<nome>.azure-api.net) que o Grafana precisa saber que esta servindo atras dela (grafana.ini server.root_url/serve_from_sub_path em monitoring.yaml.tpl). Variavel simples (nao module.apim.xxx) de proposito, pra nao criar dependencia circular entre os modulos helm e apim."
  type        = string
}
