targetScope = 'resourceGroup'

@description('Environment name — controls naming and SKU tiers.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('SQL administrator login name.')
param sqlAdminLogin string

@secure()
@description('SQL administrator password. Used only to create the SQL server resource — never stored in app settings. Store the value in Key Vault manually after first deploy.')
param sqlAdminPassword string

@description('App Service Plan SKU (e.g. B1 for dev, P1v3 for prod).')
param appServiceSkuName string

@description('Azure SQL Database SKU name (e.g. Basic for dev, S2 for prod).')
param sqlDatabaseSkuName string

@description('Service Bus namespace SKU. Standard or Premium required for topics.')
@allowed(['Standard', 'Premium'])
param serviceBusSkuName string

@description('Entra ID tenant ID. Public identifier — not a secret.')
param aadTenantId string

@description('Entra ID client (application) ID for this API. Public identifier — not a secret.')
param aadClientId string

var prefix = 'shelflife-${environmentName}'
var tags = {
  environment: environmentName
  project: 'ShelfLife'
  managedBy: 'Bicep'
}

// ── SQL ───────────────────────────────────────────────────────────────────────
module sqlModule 'modules/sql.bicep' = {
  name: 'deploy-sql'
  params: {
    prefix: prefix
    location: location
    tags: tags
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    databaseSkuName: sqlDatabaseSkuName
  }
}

// ── API — must deploy before Service Bus and Key Vault so its MI principal ID
//         is available for the RBAC role assignments in those modules.
//         Note: serviceBusNamespaceFqdn is computed, not taken from the
//         servicebus module output, to avoid a circular dependency.
module apiModule 'modules/api.bicep' = {
  name: 'deploy-api'
  params: {
    prefix: prefix
    location: location
    tags: tags
    skuName: appServiceSkuName
    sqlServerFqdn: sqlModule.outputs.serverFqdn
    serviceBusNamespaceFqdn: '${prefix}-bus.servicebus.windows.net'
    aadTenantId: aadTenantId
    aadClientId: aadClientId
  }
}

// ── Service Bus — depends on apiModule for the MI principal ID ────────────────
module serviceBusModule 'modules/servicebus.bicep' = {
  name: 'deploy-servicebus'
  params: {
    prefix: prefix
    location: location
    tags: tags
    skuName: serviceBusSkuName
    webAppPrincipalId: apiModule.outputs.principalId
  }
}

// ── Key Vault — depends on apiModule for the MI principal ID ──────────────────
module kvModule 'modules/keyvault.bicep' = {
  name: 'deploy-keyvault'
  params: {
    prefix: prefix
    location: location
    tags: tags
    webAppPrincipalId: apiModule.outputs.principalId
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output apiUrl              string = apiModule.outputs.url
output sqlServerFqdn       string = sqlModule.outputs.serverFqdn
output serviceBusNamespace string = serviceBusModule.outputs.namespaceName
output keyVaultName        string = kvModule.outputs.keyVaultName
