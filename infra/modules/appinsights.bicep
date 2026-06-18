@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

var lawName = '${prefix}-law'
var aiName  = '${prefix}-ai'

// ── Log Analytics Workspace (workspace-based App Insights requirement) ────────
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: lawName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Application Insights (workspace-based, feeds the KQL tables) ──────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: aiName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output connectionString     string = appInsights.properties.ConnectionString
output instrumentationKey   string = appInsights.properties.InstrumentationKey
output appInsightsName      string = appInsights.name
output logAnalyticsId       string = logAnalyticsWorkspace.id
