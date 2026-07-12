resource "azurerm_kubernetes_cluster" "this" {
  name                = var.cluster_name
  location            = var.location
  resource_group_name = var.resource_group_name
  dns_prefix          = var.dns_prefix
  kubernetes_version  = var.kubernetes_version
  sku_tier            = var.sku_tier
  tags                = var.tags

  # Habilitei oidc_issuer_enabled porque e pre-requisito de
  # workload_identity_enabled - o Workload Identity federa credenciais via o
  # emissor OIDC do proprio cluster.
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  # Habilitei o addon gerenciado do CSI driver (Secrets Store) para montar
  # secrets do infra/keyvault direto como volume no pod. O addon cria uma
  # managed identity propria (secret_identity), que recebe permissao no Key
  # Vault via role assignment abaixo - o mesmo padrao ja usado no
  # kubelet_identity + AcrPull.
  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  default_node_pool {
    name       = "agentpool"
    vm_size    = var.node_vm_size
    node_count = var.node_count
    max_pods   = 110

    # Declarei explicitamente os mesmos valores que o Azure ja aplica por
    # conta propria quando esse bloco nao e informado - sem isso, todo
    # "terraform plan" mostra um diff fantasma tentando remover um bloco que
    # o proprio Azure recoloca de qualquer forma.
    upgrade_settings {
      max_surge                     = "10%"
      drain_timeout_in_minutes      = 0
      node_soak_duration_in_minutes = 0
    }
  }

  # Deixei sem vnet_subnet_id: usei Azure CNI Overlay sem "bring your own
  # VNet" - o proprio AKS cria e gerencia a rede dele no node resource group
  # (MC_*).
  network_profile {
    network_plugin      = "azure"
    network_plugin_mode = "overlay"
    network_data_plane  = "azure"
    load_balancer_sku   = "standard"
  }

  identity {
    type = "SystemAssigned"
  }

  dynamic "api_server_access_profile" {
    for_each = length(var.authorized_ip_ranges) > 0 ? [1] : []
    content {
      authorized_ip_ranges = var.authorized_ip_ranges
    }
  }
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                            = var.acr_id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_kubernetes_cluster.this.kubelet_identity[0].object_id
  skip_service_principal_aad_check = true
}

resource "azurerm_role_assignment" "keyvault_secrets_user" {
  scope                            = var.key_vault_id
  role_definition_name             = "Key Vault Secrets User"
  principal_id                     = azurerm_kubernetes_cluster.this.key_vault_secrets_provider[0].secret_identity[0].object_id
  skip_service_principal_aad_check = true
}
