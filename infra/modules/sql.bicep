@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('SQL server administrator login.')
param adminLogin string

@secure()
@description('SQL server administrator password.')
param adminPassword string

@description('Database SKU name. Use Basic for dev, S2 for prod.')
param databaseSkuName string

var serverName = '${prefix}-sql'
var databaseName = 'ShelfLife'

// ── SQL Server ────────────────────────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Allow Azure-internal traffic (App Service → SQL via Azure backbone)
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ── Database ──────────────────────────────────────────────────────────────────
resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: databaseSkuName
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    requestedBackupStorageRedundancy: 'Local'
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = databaseName
