// CI identity for GitHub Actions. Deployed once by hand (the pipeline can't create its own identity):
//   az deployment group create -g AboutSamuel.com -n bootstrap -f infra/bootstrap/main.bicep
// Lives outside rg-aboutsamuel-ai so nothing deployed there can delete it.
targetScope = 'resourceGroup'

param location string = resourceGroup().location

param identityName string = 'id-aboutsamuel-github'

@description('GitHub owner/repo whose workflows may sign in as this identity.')
param repo string = 'SamOhrenberg/AboutSamuel'

@description('Resource group the pipeline deploys to.')
param targetResourceGroup string = 'rg-aboutsamuel-ai'

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
    // Contributor. Can't create role assignments; Phase 3 widens this deliberately.
    roleDefinitionId: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  }
}

output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId
output tenantId string = identity.properties.tenantId
