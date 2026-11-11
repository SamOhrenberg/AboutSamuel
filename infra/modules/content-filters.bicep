// Custom content filter (RAI) policies, one per deployment role that needs its own.
// Deployments reference these by name through raiPolicyName in main.bicepparam.
// Filters run inside the model call, no extra cost. A blocked prompt returns 400
// content_filter; a blocked completion ends with finish_reason content_filter.

@description('Name of the existing Foundry resource.')
param accountName string

resource account 'Microsoft.CognitiveServices/accounts@2026-09-01' existing = {
  name: accountName
}

// Hate, Sexual, Violence, Selfharm at one threshold on one side (Prompt or Completion).
// Low blocks the most, High only blocks the most severe content.
func harmFilters(source string, threshold string) array => map(['Hate', 'Sexual', 'Violence', 'Selfharm'], category => {
  name: category
  source: source
  severityThreshold: threshold
  enabled: true
  blocking: true
  action: 'NONE'
})

// chat is shared by the public chat, Job Fit, recruiter triage, and resume analysis.
// Same settings as Microsoft.DefaultV2, written out so a change shows up in what-if and review.
resource chatFilter 'Microsoft.CognitiveServices/accounts/raiPolicies@2026-09-01' = {
  parent: account
  name: 'chat-filter'
  properties: {
    basePolicyName: 'Microsoft.DefaultV2'
    // Synchronous: each streamed chunk is checked before it's sent. Asynchronous_filter is faster but lets
    // harmful text through briefly. The chat is public, so stay synchronous.
    mode: 'Blocking'
    contentFilters: concat(harmFilters('Prompt', 'Medium'), harmFilters('Completion', 'Medium'), [
      // Prompt Shields for user prompts: direct jailbreak attempts.
      { name: 'Jailbreak', source: 'Prompt', enabled: true, blocking: true, action: 'NONE' }
      { name: 'Protected Material Text', source: 'Completion', enabled: true, blocking: true, action: 'NONE' }
      // Annotates matches against public code but doesn't block.
      { name: 'Protected Material Code', source: 'Completion', enabled: true, blocking: false, action: 'NONE' }
    ])
  }
}

// The judge reads attacks on purpose: adversarial prompts plus SamuelLM's answers to them.
// Loosen the input side so grading isn't blocked, and keep the output side at the default.
resource judgeFilter 'Microsoft.CognitiveServices/accounts/raiPolicies@2026-09-01' = {
  parent: account
  name: 'judge-filter'
  // Foundry rejects concurrent operations on one account (RequestConflict), so one policy at a time.
  dependsOn: [chatFilter]
  properties: {
    basePolicyName: 'Microsoft.DefaultV2'
    mode: 'Blocking'
    contentFilters: concat(harmFilters('Prompt', 'High'), harmFilters('Completion', 'Medium'), [
      // Annotate only: the judge's input is jailbreak attempts by design.
      { name: 'Jailbreak', source: 'Prompt', enabled: true, blocking: false, action: 'NONE' }
      { name: 'Protected Material Text', source: 'Completion', enabled: true, blocking: true, action: 'NONE' }
      { name: 'Protected Material Code', source: 'Completion', enabled: true, blocking: false, action: 'NONE' }
    ])
  }
}

output policyNames array = [chatFilter.name, judgeFilter.name]
