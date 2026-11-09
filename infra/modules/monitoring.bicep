// Log Analytics workspace + workspace-based Application Insights.
// The workspace is where data is stored and billed, so the daily cap lives here.

param location string
param workspaceName string
param appInsightsName string

@description('Daily ingestion cap in GB, as a string because Bicep has no decimal literals. 5 GB/month is free, about 0.16/day.')
param dailyQuotaGb string = '0.15'

param tags object = {}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      // Once hit, ingestion stops until the next UTC day. Nothing is billed past it.
      dailyQuotaGb: json(dailyQuotaGb)
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id  }
}

output workspaceId string = workspace.id
output appInsightsId string = appInsights.id
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
