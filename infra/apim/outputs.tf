output "gateway_url" {
  description = "URL publica do gateway da APIM - novo front door da API para os clientes (substitui o acesso direto via IP do ingress-nginx do ponto de vista de quem consome a API)."
  value       = azurerm_api_management.this.gateway_url
}

output "ingress_ip" {
  description = "IP publico do LoadBalancer do ingress-nginx, lido via data source - usado como backend da API na APIM."
  value       = local.ingress_ip
}
