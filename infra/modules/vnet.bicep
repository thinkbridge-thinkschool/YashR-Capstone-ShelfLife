@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

var vnetName = '${prefix}-vnet'

// ── Virtual Network ───────────────────────────────────────────────────────────
// Two subnets:
//   integration — App Service Swift VNet Integration (outbound traffic only)
//   data        — Private endpoints for SQL and Key Vault (inbound to PaaS)
resource vnet 'Microsoft.Network/virtualNetworks@2023-09-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'integration'
        properties: {
          addressPrefix: '10.0.0.0/24'
          // Required delegation for App Service regional VNet Integration
          delegations: [
            {
              name: 'appservice-delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'data'
        properties: {
          addressPrefix: '10.0.1.0/24'
          // Must be Disabled for private endpoints to work in this subnet
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output vnetId string = vnet.id
// resourceId is index-independent and survives subnet reorder
output integrationSubnetId string = resourceId(
  'Microsoft.Network/virtualNetworks/subnets',
  vnetName,
  'integration'
)
output dataSubnetId string = resourceId(
  'Microsoft.Network/virtualNetworks/subnets',
  vnetName,
  'data'
)
