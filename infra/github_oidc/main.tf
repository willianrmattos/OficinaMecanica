# Optei pela autenticacao do GitHub Actions no Azure via OIDC (OpenID
# Connect), sem nenhum secret de longa duracao armazenado no GitHub. O
# GitHub emite um token de identidade a cada execucao do workflow, e o
# Azure AD confia nesse token atraves da Federated Identity Credential
# abaixo, restrita a um repositorio e branch especificos (subject claim).

resource "azuread_application" "github_actions" {
  display_name = var.app_display_name
}

resource "azuread_service_principal" "github_actions" {
  client_id = azuread_application.github_actions.client_id
}

resource "azuread_application_federated_identity_credential" "main_branch" {
  application_id = azuread_application.github_actions.id
  display_name   = "github-actions-main-branch"
  description    = "Permite ao workflow do GitHub Actions autenticar via OIDC, restrito a branch main de ${var.github_repo}."
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = "repo:${var.github_repo}:ref:refs/heads/main"
}

# Limitei o escopo ao minimo necessario para este estagio do pipeline: apenas
# enviar imagens ao ACR. Vou adicionar permissoes extras (ex: deploy no AKS)
# como role assignments separados quando implementar esse estagio, sem
# ampliar este aqui.
resource "azurerm_role_assignment" "acr_push" {
  scope                            = var.acr_id
  role_definition_name             = "AcrPush"
  principal_id                     = azuread_service_principal.github_actions.object_id
  skip_service_principal_aad_check = true
}
