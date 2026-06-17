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

@description('Fully qualified namespace of the Service Bus (e.g. shelflife-dev-bus.servicebus.windows.net).')
param serviceBusNamespaceFqdn string

@description('Entra ID tenant ID (not a secret — it is a public identifier).')
param aadTenantId string

@description('Entra ID application (client) ID registered for this API (not a secret).')
param aadClientId string

var planName = '${prefix}-plan'
var appName  = '${prefix}-api'

// No username or password — DefaultAzureCredential supplies the token at runtime
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=ShelfLife;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

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
    reserved: true
  }
}

// ── Web App ───────────────────────────────────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'   // MI principal ID is webApp.identity.principalId
  }
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
          value: ''
        }
        // Service Bus: namespace FQDN only — no SAS key
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: serviceBusNamespaceFqdn
        }
        // Entra ID — tenant ID and client ID are public identifiers, not secrets
        {
          name: 'AzureAd__TenantId'
          value: aadTenantId
        }
        {
          name: 'AzureAd__ClientId'
          value: aadClientId
        }
        {
          name: 'AzureAd__Audience'
          value: 'api://${aadClientId}'
        }
      ]
      connectionStrings: [
        // SQL uses Active Directory Default auth — no password in the string
        {
          name: 'ShelfLife'
          connectionString: sqlConnectionString
          type: 'SQLAzure'
        }
      ]
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output url         string = 'https://${webApp.properties.defaultHostName}'
output appName     string = webApp.name
output principalId string = webApp.identity.principalId
