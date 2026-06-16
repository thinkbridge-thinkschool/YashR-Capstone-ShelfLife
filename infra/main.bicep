targetScope = 'resourceGroup'

@description('Environment name — controls naming and SKU tiers.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('SQL administrator login name.')
param sqlAdminLogin string

@secure()
@description('SQL administrator password. Pass via CLI or Key Vault reference — never hard-code.')
param sqlAdminPassword string

@description('App Service Plan SKU (e.g. B1 for dev, P1v3 for prod).')
param appServiceSkuName string

@description('Azure SQL Database SKU name (e.g. Basic for dev, S2 for prod).')
param sqlDatabaseSkuName string

@description('Service Bus namespace SKU. Standard or Premium required for topics.')
@allowed(['Standard', 'Premium'])
param serviceBusSkuName string

var prefix = 'shelflife-${environmentName}'
var tags = {
  environment: environmentName
  project: 'ShelfLife'
  managedBy: 'Bicep'
}

// ── SQL ──────────────────────────────────────────────────────────────────────
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

// ── Service Bus ───────────────────────────────────────────────────────────────
module serviceBusModule 'modules/servicebus.bicep' = {
  name: 'deploy-servicebus'
  params: {
    prefix: prefix
    location: location
    tags: tags
    skuName: serviceBusSkuName
  }
}

// ── API (App Service) ─────────────────────────────────────────────────────────
module apiModule 'modules/api.bicep' = {
  name: 'deploy-api'
  params: {
    prefix: prefix
    location: location
    tags: tags
    skuName: appServiceSkuName
    sqlServerFqdn: sqlModule.outputs.serverFqdn
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    serviceBusConnectionString: serviceBusModule.outputs.connectionString
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output apiUrl string = apiModule.outputs.url
output sqlServerFqdn string = sqlModule.outputs.serverFqdn
output serviceBusNamespace string = serviceBusModule.outputs.namespaceName
