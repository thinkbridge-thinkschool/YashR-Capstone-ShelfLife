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

// App Service: Premium v3 P1v3 (2 vCores, 8 GB) — supports auto-scale, VNET, zone redundancy
param appServiceSkuName  = 'P1v3'

// SQL: Standard S2 (50 DTUs, 250 GB storage) — suitable for moderate prod traffic
param sqlDatabaseSkuName = 'S2'

// Service Bus: Standard — topics + queues; upgrade to Premium for VNET/dedicated capacity
param serviceBusSkuName  = 'Standard'
