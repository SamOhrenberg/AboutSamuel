// Assigns one role to several principals on the App Insights component, the narrowest scope
// that covers sending telemetry to it.
targetScope = 'resourceGroup'

import { principal } from 'foundry-role.bicep'

param appInsightsName string
param roleDefinitionId string
param principals principal[]

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for p in principals: {
    name: guid(appInsights.id, p.principalId, roleDefinitionId)
    scope: appInsights
    properties: {
      principalId: p.principalId
      principalType: p.principalType
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
      description: p.name
    }
  }
]
