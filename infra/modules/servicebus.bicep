@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Service Bus SKU. Standard required for topics; Premium for VNET isolation.')
@allowed(['Standard', 'Premium'])
param skuName string

@description('Principal ID of the App Service managed identity.')
param webAppPrincipalId string

var namespaceName = '${prefix}-bus'

// Built-in roles: Azure Service Bus Data Sender / Data Receiver
var sbDataSenderRoleId   = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var sbDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

// ── Namespace ─────────────────────────────────────────────────────────────────
resource namespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    disableLocalAuth: true   // reject SAS-key connections; MI only
  }
}

// ── Queue: overdue-books ──────────────────────────────────────────────────────
resource overdueQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  parent: namespace
  name: 'overdue-books'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 5
    lockDuration: 'PT1M'
  }
}

// ── Topic: notifications ──────────────────────────────────────────────────────
resource notificationsTopic 'Microsoft.ServiceBus/namespaces/topics@2021-11-01' = {
  parent: namespace
  name: 'notifications'
  properties: {
    defaultMessageTimeToLive: 'P14D'
    requiresDuplicateDetection: false
  }
}

resource allNotificationsSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2021-11-01' = {
  parent: notificationsTopic
  name: 'all-notifications'
  properties: {
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
    lockDuration: 'PT1M'
  }
}

// ── RBAC: App Service MI can send and receive on this namespace ───────────────
resource senderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, webAppPrincipalId, sbDataSenderRoleId)
  scope: namespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sbDataSenderRoleId)
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource receiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, webAppPrincipalId, sbDataReceiverRoleId)
  scope: namespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sbDataReceiverRoleId)
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output namespaceName string = namespace.name
output namespaceFqdn string = '${namespace.name}.servicebus.windows.net'
