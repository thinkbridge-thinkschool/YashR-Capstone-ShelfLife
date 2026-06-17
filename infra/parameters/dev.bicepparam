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

// Entra ID — register an app in Azure Portal → App registrations, then paste the IDs here.
// These are public identifiers, not secrets. Safe to commit.
param aadTenantId  = '7e394fc8-4b86-4cfe-810e-43f86f8bec47'
param aadClientId  = 'e003ddf2-2a79-48de-9db1-2da4ea9893d3'
