variable "location" {
  description = "Regiao do Azure onde o SQL Server sera criado. Independente da regiao do resource group (SQL Database e publico, nao requer VNet/peering para ser alcancado)."
  type        = string
}

variable "resource_group_name" {
  description = "Nome do resource group (modulo infra/rg) onde o SQL Server sera criado. Sempre fornecido pelo main.tf raiz (module.rg.resource_group_name)."
  type        = string
}

variable "server_name" {
  description = "Nome do SQL Server (logico). Globalmente unico - vira <nome>.database.windows.net."
  type        = string
}

variable "database_name" {
  description = "Nome do banco de dados."
  type        = string
  default     = "OficinaMecanicaDb"
}

variable "administrator_login" {
  description = "Usuario administrador do SQL Server."
  type        = string
  default     = "adminfiap"
}

variable "administrator_login_password" {
  description = "Senha do administrador do SQL Server. Sem valor padrao: definir via TF_VAR_sql_administrator_login_password ou um .tfvars nao versionado."
  type        = string
  sensitive   = true
}

variable "sku_name" {
  description = "SKU do banco. GP_S_Gen5_2 = General Purpose Serverless, Gen5, ate 2 vCores."
  type        = string
  default     = "GP_S_Gen5_2"
}

variable "max_size_gb" {
  description = "Tamanho maximo do banco em GB (32GB corresponde ao limite do tier sempre-gratis do Azure SQL Database)."
  type        = number
  default     = 32
}

variable "min_capacity" {
  description = "Capacidade minima (vCores) quando o banco nao esta pausado (serverless)."
  type        = number
  default     = 0.5
}

variable "auto_pause_delay_in_minutes" {
  description = "Minutos de inatividade antes do banco pausar automaticamente (serverless)."
  type        = number
  default     = 60
}

variable "client_ip_address" {
  description = "IP publico autorizado a acessar o banco diretamente (execucao de migrations, conexao via SSMS/Azure Data Studio). Null nao libera nenhum IP especifico, apenas servicos Azure."
  type        = string
  default     = null
}

variable "tags" {
  description = "Tags aplicadas aos recursos."
  type        = map(string)
  default     = {}
}
