@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Principal ID of the App Service managed identity.')
param webAppPrincipalId string

@description('Application Insights connection string to store as a Key Vault secret.')
@secure()
param appInsightsConnectionString string

@description('Resource ID of the VNet (used to link the private DNS zone).')
param vnetId string

@description('Resource ID of the data subnet where the private endpoint is placed.')
param dataSubnetId string

var kvName       = '${prefix}-kv'
var peName       = '${kvName}-pe'
var dnsZoneName  = 'privatelink.vaultcore.azure.net'

// Built-in: Key Vault Secrets User — allows Get/List on secrets (read-only)
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ── Key Vault ─────────────────────────────────────────────────────────────────
// publicNetworkAccess disabled — secrets are only reachable through the private
// endpoint from within the VNet (or from App Service via VNet integration).
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

// ── Grant App Service MI read access to secrets ───────────────────────────────
resource miSecretsRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webAppPrincipalId, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── App Insights connection string secret ─────────────────────────────────────
resource aiConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'appinsights-cs'
  parent: keyVault
  properties: {
    value: appInsightsConnectionString
  }
}

// ── Private Endpoint ──────────────────────────────────────────────────────────
resource kvPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: peName
  location: location
  tags: tags
  properties: {
    subnet: {
      id: dataSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${peName}-conn'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

// ── Private DNS Zone — resolves *.vaultcore.azure.net to private IP ───────────
resource kvDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: dnsZoneName
  location: 'global'
  tags: tags
}

resource kvDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: kvDnsZone
  name: '${prefix}-kv-dns-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnetId
    }
    registrationEnabled: false
  }
}

resource kvDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: kvPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'kv-config'
        properties: {
          privateDnsZoneId: kvDnsZone.id
        }
      }
    ]
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output keyVaultName string = keyVault.name
output keyVaultUri  string = keyVault.properties.vaultUri
