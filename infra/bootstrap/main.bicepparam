using './main.bicep'

// Object IDs aren't secrets. For a service principal: az ad sp show --id <appId> --query id
param appPrincipals = [
  {
    name: 'sp-aboutsamuel-railway (prod, both Railway services)'
    principalId: 'd9b2cd61-703a-419f-8c23-905dd421d325'
    principalType: 'ServicePrincipal'
  }
  {
    name: 'Samuel (local dev via az login)'
    principalId: 'e4132eaa-f16f-41fe-8229-3ea448a546ff'
    principalType: 'User'
  }
]
