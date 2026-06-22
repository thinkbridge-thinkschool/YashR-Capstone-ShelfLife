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

@description('Resource ID of the integration subnet for App Service VNet Integration (outbound).')
param integrationSubnetId string

var planName = '${prefix}-plan'
var appName  = '${prefix}-api'
var kvName   = '${prefix}-kv'   // must match keyvault.bicep naming convention

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
      // Route all outbound traffic from the app through the VNet so it can
      // reach SQL and Key Vault over their private endpoints.
      vnetRouteAllEnabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          // Key Vault reference — App Service resolves this at runtime using its MI
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: '@Microsoft.KeyVault(VaultName=${kvName};SecretName=appinsights-cs)'
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

// ── VNet Integration (Swift / regional) ──────────────────────────────────────
// Links the App Service to the integration subnet so all outbound requests
// (to SQL private endpoint, Key Vault private endpoint, Service Bus) flow
// through the VNet rather than over the public internet.
resource vnetIntegration 'Microsoft.Web/sites/networkConfig@2023-12-01' = {
  parent: webApp
  name: 'virtualNetwork'
  properties: {
    subnetResourceId: integrationSubnetId
    swiftSupported: true
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output url         string = 'https://${webApp.properties.defaultHostName}'
output appName     string = webApp.name
output principalId string = webApp.identity.principalId
