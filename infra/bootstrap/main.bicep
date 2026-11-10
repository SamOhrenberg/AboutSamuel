// Identities and role assignments the pipeline can't (or shouldn't) manage itself:
// the GitHub Actions CI identity and who may call the Foundry models. Deployed by hand:
//   az deployment group create -g AboutSamuel.com -n bootstrap -p infra/bootstrap/main.bicepparam
// Lives outside rg-aboutsamuel-ai so nothing deployed there can delete it.
targetScope = 'resourceGroup'

import { principal } from 'modules/foundry-role.bicep'

param location string = resourceGroup().location

param identityName string = 'id-aboutsamuel-github'

@description('GitHub owner/repo whose workflows may sign in as this identity.')
param repo string = 'SamOhrenberg/AboutSamuel'

@description('Resource group the pipeline deploys to.')
param targetResourceGroup string = 'rg-aboutsamuel-ai'

param foundryAccountName string = 'aboutsamuel-ai'

@description('Who may call the Foundry models with Entra ID tokens (keyless).')
param inferencePrincipals principal[]

param tags object = {
  app: 'aboutsamuel'
  managedBy: 'bicep'
  purpose: 'github-actions'
}

var issuer = 'https://token.actions.githubusercontent.com'
var audiences = ['api://AzureADTokenExchange']

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

// Deploy on push to main.
resource mainBranch 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-main'
  properties: {
    issuer: issuer
    subject: 'repo:${repo}:ref:refs/heads/main'
    audiences: audiences
  }
}

// what-if on pull requests. Fork PRs never get an OIDC token, so this only matches branches in the repo.
resource pullRequest 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: identity
  name: 'github-pull-request'
  properties: {
    issuer: issuer
    subject: 'repo:${repo}:pull_request'
    audiences: audiences
  }
  // Concurrent writes of federated credentials on one identity fail, so serialize them.
  dependsOn: [mainBranch]
}

module contributor 'modules/role-assignment.bicep' = {
  name: 'github-contributor'
  scope: resourceGroup(targetResourceGroup)
  params: {
    principalId: identity.properties.principalId
    // Contributor. Can't create role assignments, which is why they live in this file.
    roleDefinitionId: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  }
}

module inference 'modules/foundry-role.bicep' = {
  name: 'foundry-inference'
  scope: resourceGroup(targetResourceGroup)
  params: {
    accountName: foundryAccountName
    // Cognitive Services OpenAI User: inference only. Can't list keys or change deployments.
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principals: inferencePrincipals
  }
}

output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId
output tenantId string = identity.properties.tenantId
