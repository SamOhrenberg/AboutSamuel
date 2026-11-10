// Foundry resource (AIServices account) plus one project.
// Model deployments live in deployments.bicep so a model retirement only touches that file.

@description('Azure region for the Foundry resource.')
param location string

@description('Name of the Foundry resource. Also used as the custom subdomain, so it must be globally unique.')
param accountName string

@description('Name of the Foundry project inside the resource.')
param projectName string

@description('Entra ID only: API keys are rejected (401) and can\'t be used even if leaked. Both apps authenticate with tokens since Phase 3.')
param disableLocalAuth bool = true

param tags object = {}

resource account 'Microsoft.CognitiveServices/accounts@2026-09-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // Makes this a current Foundry resource that can hold projects.
    allowProjectManagement: true
    // Required for Entra ID auth and gives the <name>.openai.azure.com endpoint.
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: disableLocalAuth
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2026-09-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'AboutSamuel'
    description: 'Portfolio site chat, job fit, recruiter triage, and evaluation agents.'
  }
}

output accountName string = account.name
output accountId string = account.id
output endpoint string = account.properties.endpoint
output openAiEndpoint string = 'https://${accountName}.openai.azure.com/'
output projectName string = project.name
output projectEndpoint string = 'https://${accountName}.services.ai.azure.com/api/projects/${projectName}'
output accountPrincipalId string = account.identity.principalId
