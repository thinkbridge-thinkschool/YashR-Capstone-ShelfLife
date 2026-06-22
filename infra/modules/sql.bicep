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

@description('Resource ID of the VNet (used to link the private DNS zone).')
param vnetId string

@description('Resource ID of the data subnet where the private endpoint is placed.')
param dataSubnetId string

var serverName   = '${prefix}-sql'
var databaseName = 'ShelfLife'
var peName       = '${serverName}-pe'
// Use environment() so the DNS zone name resolves correctly in sovereign clouds
var dnsZoneName  = 'privatelink${environment().suffixes.sqlServerHostname}'

// ── SQL Server ────────────────────────────────────────────────────────────────
// publicNetworkAccess disabled — all traffic flows through the private endpoint.
// The AllowAzureServices firewall rule is removed; only private-endpoint traffic
// can reach the server.
resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
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

// ── Private Endpoint ──────────────────────────────────────────────────────────
// Places the SQL server's NIC inside the data subnet so App Service can reach it
// over the private IP without traversing the public internet.
resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
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
          privateLinkServiceId: sqlServer.id
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

// ── Private DNS Zone — resolves *.database.windows.net to the private IP ─────
resource sqlDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: dnsZoneName
  location: 'global'
  tags: tags
}

resource sqlDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: sqlDnsZone
  name: '${prefix}-sql-dns-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnetId
    }
    registrationEnabled: false
  }
}

resource sqlDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: sqlPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql-config'
        properties: {
          privateDnsZoneId: sqlDnsZone.id
        }
      }
    ]
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output serverFqdn    string = sqlServer.properties.fullyQualifiedDomainName
output databaseName  string = databaseName
