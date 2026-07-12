output "release_name" {
  description = "Nome do Helm release criado."
  value       = helm_release.ingress_nginx.name
}

output "release_namespace" {
  description = "Namespace onde o ingress-nginx foi instalado."
  value       = helm_release.ingress_nginx.namespace
}

output "release_status" {
  description = "Status do Helm release."
  value       = helm_release.ingress_nginx.status
}

output "monitoring_namespace" {
  description = "Namespace onde o Prometheus/Grafana foram instalados."
  value       = helm_release.monitoring.namespace
}
