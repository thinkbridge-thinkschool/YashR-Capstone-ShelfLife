using '../main.bicep'

// ── Dev environment — smallest billable SKUs ──────────────────────────────────
// Deploy:
//   az deployment group create \
//     --resource-group rg-shelflife-dev \
//     --template-file infra/main.bicep \
//     --parameters infra/parameters/dev.bicepparam \
//     --parameters sqlAdminPassword='<secret>'

param environmentName = 'dev'
param location        = 'southeastasia'

param sqlAdminLogin      = 'sqladmin'
// sqlAdminPassword is @secure() — pass via CLI flag or az.getSecret() Key Vault ref.
// Do NOT commit plaintext passwords.
param sqlAdminPassword   = 'REPLACE_WITH_SECRET'

// App Service: Basic B1 (1 core, 1.75 GB) — no auto-scale, no custom domain SSL
param appServiceSkuName  = 'B1'

// SQL: Basic DTU (5 DTUs, 2 GB storage) — cheapest billable tier
param sqlDatabaseSkuName = 'Basic'

// Service Bus: Standard required for topics (Notifications module)
param serviceBusSkuName  = 'Standard'
