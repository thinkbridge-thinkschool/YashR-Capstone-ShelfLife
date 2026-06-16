@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('App Service Plan SKU name.')
param skuName string

@description('Fully qualified domain name of the SQL server.')
param sqlServerFqdn string

@description('SQL administrator login.')
param sqlAdminLogin string

@secure()
@description('SQL administrator password.')
param sqlAdminPassword string

@secure()
@description('Service Bus primary connection string.')
param serviceBusConnectionString string

var planName = '${prefix}-plan'
var appName = '${prefix}-api'
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=ShelfLife;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=true;TrustServerCertificate=False;Connection Timeout=30;'

// ── App Service Plan ──────────────────────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: skuName
  }
  kind: 'linux'
  properties: {
    reserved: true  // required for Linux
  }
}

// ── Web App ───────────────────────────────────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      http20Enabled: true
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: ''  // set post-deploy when App Insights is wired up
        }
      ]
      connectionStrings: [
        {
          name: 'ShelfLife'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
        {
          name: 'ServiceBus'
          connectionString: serviceBusConnectionString
          type: 'Custom'
        }
      ]
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output url string = 'https://${webApp.properties.defaultHostName}'
output appName string = webApp.name
