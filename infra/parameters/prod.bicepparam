using '../main.bicep'

// ── Prod environment — production-grade SKUs ──────────────────────────────────
// Deploy:
//   az deployment group create \
//     --resource-group rg-shelflife-prod \
//     --template-file infra/main.bicep \
//     --parameters infra/parameters/prod.bicepparam \
//     --parameters sqlAdminPassword='<secret>'

param environmentName = 'prod'
param location        = 'southeastasia'

param sqlAdminLogin      = 'sqladmin'
// sqlAdminPassword is @secure() — pass via CLI flag or az.getSecret() Key Vault ref.
param sqlAdminPassword   = 'REPLACE_WITH_SECRET'

// App Service: Basic B1 (1 core, 1.75 GB) — matches dev tier; VNet Integration works on B1 Linux.
// Upgrade to S2 only if auto-scaling or deployment slots are needed post-capstone.
param appServiceSkuName  = 'B1'

// SQL: Basic (5 DTUs, 2 GB) — matches dev tier; sufficient for capstone demo data volume.
// Private endpoint and Managed Identity auth are DTU-tier agnostic — no security regression.
// Verify data size < 2 GB before deploying: SELECT SUM(reserved_page_count)*8.0/1024/1024 FROM sys.dm_db_partition_stats
param sqlDatabaseSkuName = 'Basic'

// Service Bus: Standard — topics + queues; upgrade to Premium for VNET/dedicated capacity
param serviceBusSkuName  = 'Standard'

// Entra ID — same tenant, same app registration as dev (or create a separate prod app registration)
param aadTenantId  = '7e394fc8-4b86-4cfe-810e-43f86f8bec47'
param aadClientId  = 'e003ddf2-2a79-48de-9db1-2da4ea9893d3'
