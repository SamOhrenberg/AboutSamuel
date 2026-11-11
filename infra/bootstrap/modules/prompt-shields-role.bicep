// Custom role that can call Prompt Shields and nothing else. The built-in roles that include it
// (Cognitive Services User, Azure AI Developer) also grant Speech, every OpenAI data action, and more.
// A data action, not an action: it allows calling the service, not changing the resource.
targetScope = 'resourceGroup'

var roleName = 'AboutSamuel Prompt Shields User'

resource role 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  // Deterministic GUID per resource group, so redeploys update the same role.
  name: guid(resourceGroup().id, roleName)
  properties: {
    roleName: roleName
    description: 'Call Azure AI Content Safety Prompt Shields (text:shieldPrompt) on Foundry accounts in this resource group.'
    type: 'CustomRole'
    permissions: [
      {
        actions: []
        dataActions: [
          'Microsoft.CognitiveServices/accounts/ContentSafety/text:shieldprompt/action'
        ]
      }
    ]
    // Assignable only within this resource group.
    assignableScopes: [
      resourceGroup().id
    ]
  }
}

output roleDefinitionId string = role.name
