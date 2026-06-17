@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Principal ID of the App Service managed identity.')
param webAppPrincipalId string

var kvName = '${prefix}-kv'

// Built-in: Key Vault Secrets User — allows Get/List on secrets (read-only)
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ── Key Vault ─────────────────────────────────────────────────────────────────
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
    enableRbacAuthorization: true        // RBAC instead of legacy access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
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

// ── Outputs ───────────────────────────────────────────────────────────────────
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
