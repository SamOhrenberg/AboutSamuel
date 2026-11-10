// Assigns one role to several principals on the Foundry account itself, the narrowest scope
// that covers inference. Resource group scope would also reach anything added there later.
targetScope = 'resourceGroup'

@export()
type principal = {
  @description('Label for readers; not used by Azure.')
  name: string
  @description('Object ID. For a service principal, the SP object ID, not the app (client) ID.')
  principalId: string
  principalType: 'User' | 'Group' | 'ServicePrincipal'
}

param accountName string
param roleDefinitionId string
param principals principal[]

resource account 'Microsoft.CognitiveServices/accounts@2026-09-01' existing = {
  name: accountName
}

resource assignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for p in principals: {
    // Deterministic name so redeploys are idempotent.
    name: guid(account.id, p.principalId, roleDefinitionId)
    scope: account
    properties: {
      principalId: p.principalId
      principalType: p.principalType
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
      description: p.name
    }
  }
]
