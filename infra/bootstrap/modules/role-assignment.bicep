// Assigns one role at resource group scope. A module because the target RG differs from the deploying one.
targetScope = 'resourceGroup'

param principalId string
param roleDefinitionId string

resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  // Deterministic name so redeploys are idempotent.
  name: guid(resourceGroup().id, principalId, roleDefinitionId)
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    // Skips the Entra lookup that fails while a brand-new identity is still replicating.
    principalType: 'ServicePrincipal'
  }
}
