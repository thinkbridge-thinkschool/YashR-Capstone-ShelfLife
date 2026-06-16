@description('Resource name prefix (e.g. shelflife-dev).')
param prefix string

@description('Azure region.')
param location string

@description('Resource tags.')
param tags object

@description('Service Bus SKU. Standard required for topics; Premium for VNET isolation.')
@allowed(['Standard', 'Premium'])
param skuName string

var namespaceName = '${prefix}-bus'

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
    disableLocalAuth: false
  }
}

// ── Queue: overdue-books (consumed by ShelfLife.OverdueWorker) ────────────────
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

// ── Topic: notifications (fanned out to Notifications module) ─────────────────
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

// ── Outputs ───────────────────────────────────────────────────────────────────
output namespaceName string = namespace.name

@secure()
output connectionString string = listKeys(
  '${namespace.id}/authorizationRules/RootManageSharedAccessKey',
  '2021-11-01'
).primaryConnectionString
