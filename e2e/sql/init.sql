-- Cria os dois bancos SQL do e2e com um usuario contido por banco, como o OficinaMecanica.Banco/sql/criar-usuarios.sql
-- faz no Azure SQL: a credencial existe so dentro do banco (nao ha login no master), e o usuario e db_owner so nele
-- porque a mesma conexao aplica as migrations do servico e serve o runtime.
--
-- Variaveis sqlcmd (vem do compose, nunca literais aqui): SenhaOs e SenhaBilling.
-- Optei por CONTAINMENT = PARTIAL porque o SQL Server local, ao contrario do Azure SQL, so aceita usuario contido
-- com o recurso ligado no servidor e no banco.
-- Idempotente: pode rodar de novo sobre o mesmo volume.

SET NOCOUNT ON;
GO

EXEC sp_configure 'contained database authentication', 1;
RECONFIGURE;
GO

IF DB_ID(N'OficinaMecanicaDb') IS NULL
    CREATE DATABASE [OficinaMecanicaDb] CONTAINMENT = PARTIAL;
GO

IF DB_ID(N'BillingDb') IS NULL
    CREATE DATABASE [BillingDb] CONTAINMENT = PARTIAL;
GO

USE [OficinaMecanicaDb];
GO
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'app_os')
    ALTER USER [app_os] WITH PASSWORD = N'$(SenhaOs)';
ELSE
    CREATE USER [app_os] WITH PASSWORD = N'$(SenhaOs)', DEFAULT_SCHEMA = [dbo];
GO
IF ISNULL(IS_ROLEMEMBER(N'db_owner', N'app_os'), 0) = 0
    ALTER ROLE db_owner ADD MEMBER [app_os];
GO

USE [BillingDb];
GO
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'app_billing')
    ALTER USER [app_billing] WITH PASSWORD = N'$(SenhaBilling)';
ELSE
    CREATE USER [app_billing] WITH PASSWORD = N'$(SenhaBilling)', DEFAULT_SCHEMA = [dbo];
GO
IF ISNULL(IS_ROLEMEMBER(N'db_owner', N'app_billing'), 0) = 0
    ALTER ROLE db_owner ADD MEMBER [app_billing];
GO

PRINT N'Bancos OficinaMecanicaDb e BillingDb prontos (usuarios app_os e app_billing).';
GO
