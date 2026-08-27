# Le o IP publico do LoadBalancer do ingress-nginx (modulo infra/helm)
# dinamicamente, em vez de hardcodar - o mesmo IP usado hoje pelos hosts
# nip.io dos demais Ingress (k8s/monitoring, k8s/mailpit, k8s/jaeger), que ja
# muda se o Service for recriado.
data "kubernetes_service" "ingress_nginx" {
  metadata {
    name      = var.ingress_nginx_service_name
    namespace = var.ingress_nginx_namespace
  }
}

locals {
  ingress_ip = data.kubernetes_service.ingress_nginx.status[0].load_balancer[0].ingress[0].ip

  # Mesmo host nip.io que k8s/monitoring/ingress.yaml ja usa pro Grafana -
  # precisa do host especifico (nao so o IP puro) porque o ingress-nginx
  # roteia Grafana/Prometheus por hostname, nao por path.
  grafana_host = "grafana.${local.ingress_ip}.nip.io"
}

# Consumption_0 e o unico valor aceito pro tier serverless (sem capacidade
# dedicada, escala automatica)
resource "azurerm_api_management" "this" {
  name                = var.apim_name
  location            = var.location
  resource_group_name = var.resource_group_name
  publisher_name      = var.publisher_name
  publisher_email     = var.publisher_email
  sku_name            = "Consumption_0"
  tags                = var.tags
}

# Entidades de Backend proprias (em vez de "service_url" solto em cada API) -
# pra ter varios backends conforme mais servicos entrarem atras da
# APIM, cada um reutilizavel/gerenciado a parte (util pra circuit breaker,
# TLS customizado, etc. no futuro, mesmo que nao usado ainda).
resource "azurerm_api_management_backend" "ingress_nginx" {
  name                = "ingress-nginx"
  resource_group_name = var.resource_group_name
  api_management_name = azurerm_api_management.this.name
  protocol            = "http"
  url                 = "http://${local.ingress_ip}"
}

resource "azurerm_api_management_backend" "grafana" {
  name                = "grafana"
  resource_group_name = var.resource_group_name
  api_management_name = azurerm_api_management.this.name
  protocol            = "http"
  url                 = "http://${local.grafana_host}"
}

# Sem import de OpenAPI de proposito: passthrough tipo proxy reverso (mesmo
# modelo do Grafana abaixo), repassando QUALQUER path/metodo pro backend -
# inclusive coisas que nao vem em nenhum OpenAPI, tipo a propria Swagger UI
# (/swagger/*), /health e /metrics (mapeados fora do MVC, o Swashbuckle nem
# documentaria). Troca a curadoria de operations (visiveis uma a uma no
# portal da APIM) por cobertura total sem manutencao manual por endpoint.
resource "azurerm_api_management_api" "oficinamecanica" {
  name                = "oficinamecanica-api"
  resource_group_name = var.resource_group_name
  api_management_name = azurerm_api_management.this.name
  display_name        = "OficinaMecanica API"
  revision            = "1"

  path      = "oficinaserver"
  protocols = ["https"]

  # A API ja tem seu proprio JWT Bearer pras rotas administrativas, e varias
  # rotas sao publicas de proposito (consulta de OS por numero, aprovacao e
  # recusa de orcamento via link de e-mail). Exigir subscription key da APIM
  # por cima quebraria esse fluxo publico
  subscription_required = false
}

# Mesmo motivo do Grafana: a APIM nao tem um metodo HTTP "curinga" de
# verdade, precisa de uma operation coringa (url_template = "/*") por
# metodo. GET/POST/PUT/DELETE/PATCH cobre os mesmos 5 metodos que a API ja
# usa (confirmado nas rotas reais do Swagger).
resource "azurerm_api_management_api_operation" "oficinamecanica_wildcard" {
  for_each = toset(["GET", "POST", "PUT", "DELETE", "PATCH"])

  operation_id        = "oficinamecanica-passthrough-${lower(each.key)}"
  api_name            = azurerm_api_management_api.oficinamecanica.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name
  display_name        = "OficinaMecanica (passthrough ${each.key})"
  method              = each.key
  url_template        = "/*"
}

# O backend de verdade (pra onde o trafego vai) e definido aqui via policy,
# nao no "service_url" da API acima - assim a API referencia uma entidade de
# Backend nomeada e reutilizavel, em vez de uma URL solta.
resource "azurerm_api_management_api_policy" "oficinamecanica" {
  api_name            = azurerm_api_management_api.oficinamecanica.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name

  xml_content = <<XML
<policies>
  <inbound>
    <base />
    <set-backend-service backend-id="${azurerm_api_management_backend.ingress_nginx.name}" />
    <!-- A API le esses headers (Program.cs, ForwardedHeadersMiddleware +
         leitura manual do Prefix) pra montar o servers[] do OpenAPI e o
         link de fetch do Swagger UI com a URL publica certa da APIM, nao
         com o scheme/host/path internos que o pod realmente recebe
         (http, IP do ingress-nginx, sem prefixo). -->
    <set-header name="X-Forwarded-Proto" exists-action="override">
      <value>https</value>
    </set-header>
    <set-header name="X-Forwarded-Host" exists-action="override">
      <value>${var.apim_name}.azure-api.net</value>
    </set-header>
    <set-header name="X-Forwarded-Prefix" exists-action="override">
      <value>/oficinaserver</value>
    </set-header>
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
XML
}

# Grafana nao tem um OpenAPI pra importar (e uma UI web, nao uma API REST) -
# criada "em branco" com operations coringa (/*) que repassam qualquer path
# pro backend, igual um proxy reverso comum.
resource "azurerm_api_management_api" "grafana" {
  name                = "grafana"
  resource_group_name = var.resource_group_name
  api_management_name = azurerm_api_management.this.name
  display_name        = "Grafana"
  revision            = "1"

  # Com prefixo "grafana" (diferente da API principal, que fica na raiz) -
  # vira https://<apim>.azure-api.net/grafana/...
  path      = "grafana"
  protocols = ["https"]

  subscription_required = false
}

# A APIM nao tem um metodo HTTP "curinga" de verdade (method = "*" e aceito
# pelo provider Terraform sem erro, mas o runtime da Azure nunca casa com
# nada - 404 silencioso pra qualquer request). O jeito certo de fazer um
# passthrough tipo proxy reverso e uma operation coringa (url_template = "/*")
# por metodo HTTP que a UI do Grafana realmente usa.
resource "azurerm_api_management_api_operation" "grafana_wildcard" {
  for_each = toset(["GET", "POST", "PUT", "DELETE", "PATCH"])

  operation_id        = "grafana-passthrough-${lower(each.key)}"
  api_name            = azurerm_api_management_api.grafana.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name
  display_name        = "Grafana (passthrough ${each.key})"
  method              = each.key
  url_template        = "/*"
}

resource "azurerm_api_management_api_policy" "grafana" {
  api_name            = azurerm_api_management_api.grafana.name
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name

  xml_content = <<XML
<policies>
  <inbound>
    <base />
    <set-backend-service backend-id="${azurerm_api_management_backend.grafana.name}" />
    <!-- Sem isso, o Grafana entra num loop de redirect em /login e / (raiz):
         a cadeia APIM[https] -> ingress-nginx[http] -> Grafana[http] nao
         carrega scheme nenhum, entao o Grafana nao sabe que a conexao
         original do cliente foi https (config server.root_url, no chart)
         e fica tentando "corrigir" o proprio scheme redirecionando pra
         ele mesmo, infinitamente. -->
    <set-header name="X-Forwarded-Proto" exists-action="override">
      <value>https</value>
    </set-header>
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
XML
}

# Nome generico de proposito: esse Product agrupa TUDO que passa pela APIM
# (a API + Grafana, e o que mais entrar depois) - so existe porque toda API
# precisa pertencer a um Product pra ficar acessivel pelo gateway, nao
# porque exista controle de acesso de verdade aqui (subscription_required
# = false nos dois).
resource "azurerm_api_management_product" "this" {
  product_id            = "gateway"
  api_management_name   = azurerm_api_management.this.name
  resource_group_name   = var.resource_group_name
  display_name          = "Gateway OficinaMecanica"
  subscription_required = false
  published             = true
}

resource "azurerm_api_management_product_api" "oficinamecanica" {
  api_name            = azurerm_api_management_api.oficinamecanica.name
  product_id          = azurerm_api_management_product.this.product_id
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name
}

resource "azurerm_api_management_product_api" "grafana" {
  api_name            = azurerm_api_management_api.grafana.name
  product_id          = azurerm_api_management_product.this.product_id
  api_management_name = azurerm_api_management.this.name
  resource_group_name = var.resource_group_name
}
