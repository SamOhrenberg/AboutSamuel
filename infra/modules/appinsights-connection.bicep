// Connects App Insights to the Foundry project. The portal's Tracing page and the
// SDK's telemetry.get_application_insights_connection_string() both find it through this
// connection (category AppInsights), not by looking around the resource group.

param accountName string
param projectName string
param appInsightsName string

resource account 'Microsoft.CognitiveServices/accounts@2026-09-01' existing = {
  name: accountName

  resource project 'projects' existing = {
    name: projectName
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource connection 'Microsoft.CognitiveServices/accounts/projects/connections@2026-09-01' = {
  parent: account::project
  name: appInsightsName
  properties: {
    category: 'AppInsights'
    target: appInsights.id
    // The "key" is the connection string. App Insights has local auth off, so it's only
    // an address: sending telemetry still needs an Entra token with Monitoring Metrics Publisher.
    authType: 'ApiKey'
    credentials: {
      key: appInsights.properties.ConnectionString
    }
    isSharedToAll: false
    metadata: {
      ApiType: 'Azure'
      ResourceId: appInsights.id
    }
  }
}

output connectionName string = connection.name
