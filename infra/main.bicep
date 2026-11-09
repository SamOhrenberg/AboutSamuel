// AboutSamuel AI infrastructure. Deploy to rg-aboutsamuel-ai:
//   az deployment group create -g rg-aboutsamuel-ai -f infra/main.bicep -p infra/main.bicepparam
targetScope = 'resourceGroup'

param location string = resourceGroup().location

@description('Foundry resource name and custom subdomain. Globally unique.')
param accountName string
param projectName string

param workspaceName string
param appInsightsName string
param dailyQuotaGb string

@description('Role-named model deployments: { name, model, version, sku, capacity }.')
param deployments array

param tags object = {}

module foundry 'modules/foundry.bicep' = {
  name: 'foundry'
  params: {
    location: location
    accountName: accountName
    projectName: projectName
    tags: tags
  }
}

module modelDeployments 'modules/deployments.bicep' = {
  name: 'deployments'
  params: {
    // Using the module output (not the param) makes Bicep wait for the account to exist.
    accountName: foundry.outputs.accountName
    deployments: deployments
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    workspaceName: workspaceName
    appInsightsName: appInsightsName
    dailyQuotaGb: dailyQuotaGb
    tags: tags
  }
}

output accountName string = foundry.outputs.accountName
output openAiEndpoint string = foundry.outputs.openAiEndpoint
output projectEndpoint string = foundry.outputs.projectEndpoint
output deploymentNames array = modelDeployments.outputs.deploymentNames
output models object = modelDeployments.outputs.models
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
