// Everything that changes lives here. When a model retires, edit its row and redeploy.
using './main.bicep'

param accountName = 'aboutsamuel-ai'
param projectName = 'aboutsamuel'

param workspaceName = 'log-aboutsamuel'
param appInsightsName = 'appi-aboutsamuel'
param dailyQuotaGb = '0.15'

param deployments = [
  {
    name: 'chat'
    model: 'gpt-4.1-mini'
    version: '2025-04-14'
    sku: 'GlobalStandard'
    capacity: 100
  }
  {
    // Changing this model means re-embedding everything in pgvector.
    name: 'embedding'
    model: 'text-embedding-3-small'
    version: '1'
    sku: 'GlobalStandard'
    capacity: 500
  }
  {
    name: 'judge'
    model: 'gpt-5.2'
    version: '2025-12-11'
    sku: 'GlobalStandard'
    // Subscription quota for gpt-5.2 GlobalStandard is 250, pooled across regions.
    capacity: 250
  }
]

param tags = {
  app: 'aboutsamuel'
  managedBy: 'bicep'
}
