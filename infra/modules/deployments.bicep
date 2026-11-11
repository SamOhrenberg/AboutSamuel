// Model deployments on an existing Foundry resource.
// Deployments are named by role (chat, embedding, judge), not by model, so swapping a
// retired model is a parameter change and app config never moves.

@description('Name of the existing Foundry resource.')
param accountName string

@description('One entry per deployment: { name, model, version, sku, capacity, raiPolicyName? }.')
param deployments array

resource account 'Microsoft.CognitiveServices/accounts@2026-09-01' existing = {
  name: accountName
}

// Foundry rejects concurrent deployment operations on one account, so go one at a time.
@batchSize(1)
resource modelDeployments 'Microsoft.CognitiveServices/accounts/deployments@2026-09-01' = [
  for d in deployments: {
    parent: account
    name: d.name
    sku: {
      name: d.sku
      // Thousands of tokens per minute. A rate limit, not a cost, on pay-as-you-go.
      capacity: d.capacity
    }
    properties: {
      model: {
        format: 'OpenAI'
        name: d.model
        version: d.version
      }
      // Azure moves to the new default version on its own. When it does, update the
      // pinned version in main.bicepparam or the next deploy will try to roll it back.
      versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
      // Content filter policy: a custom one from content-filters.bicep, or the built-in default.
      raiPolicyName: d.?raiPolicyName ?? 'Microsoft.DefaultV2'
    }
  }
]

output deploymentNames array = [for (d, i) in deployments: modelDeployments[i].name]

// Role -> model name, e.g. { chat: 'gpt-4.1-mini' }. The Railway sync script reads this so
// apps learn the model from the deployment record instead of a hand-typed env var.
output models object = toObject(deployments, d => d.name, d => d.model)
