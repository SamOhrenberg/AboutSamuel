#!/usr/bin/env bash
# Turns `az deployment group what-if --no-pretty-print` JSON into a markdown summary
# with known what-if noise filtered out. Used by .github/workflows/infra-whatif.yml.
#
#   az deployment group what-if ... --no-pretty-print > whatif.json
#   infra/scripts/whatif-summary.sh whatif.json >> "$GITHUB_STEP_SUMMARY"
#
# Noise is matched on exact (resource type, property path, change type). Anything not listed,
# including a different change type on a listed path, is reported as a real change.
set -euo pipefail

[[ $# -eq 1 && -f "$1" ]] || { echo "Usage: $0 <whatif.json>" >&2; exit 2; }

jq -r '
# Server-populated or write-only properties that what-if reports on every run.
def noise: [
  {type: "microsoft.cognitiveservices/accounts", path: "properties.a365LoggingEnabled", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts", path: "properties.a365Status", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts", path: "properties.associatedProjects", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts", path: "properties.defaultProject", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts/deployments", path: "properties.currentCapacity", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts/deployments", path: "properties.deploymentState", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts/projects", path: "kind", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts/projects", path: "properties.endpoints", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts/projects", path: "properties.internalId", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts/projects", path: "properties.isDefault", change: "Delete", why: "read-only"},
  {type: "microsoft.cognitiveservices/accounts/raipolicies", path: "properties.format", change: "Delete", why: "read-only"},
  {type: "microsoft.insights/components", path: "properties.Flow_Type", change: "Create", why: "write-only, never read back"},
  {type: "microsoft.insights/components", path: "properties.Request_Source", change: "Create", why: "write-only, never read back"}
];

# .../providers/Ns/typeA/nameA/typeB/nameB -> ns/typea/typeb (lowercase; Azure IDs vary in case).
def rtype: (.resourceId | split("/providers/") | last | split("/")) as $s
  | ([$s[0]] + [range(1; $s | length; 2) as $i | $s[$i]]) | join("/") | ascii_downcase;
def rname: .resourceId | split("/") | last;

# Flatten nested deltas to leaf property changes with full dotted paths.
def leaves($prefix): .[] | (if $prefix == "" then .path else "\($prefix).\(.path)" end) as $p
  | if (.children // []) | length > 0 then .children | leaves($p)
    else {path: $p, change: .propertyChangeType, before: .before, after: .after} end;

def short: if . == null then "" else tojson | gsub("\\|"; "\\|") | if length > 80 then .[0:77] + "..." else . end end;

[ .changes[] | rtype as $t | rname as $n | .changeType as $c
  | if $c == "Modify" then
      [ (.delta // []) | leaves("") | . as $l
        | {type: $t, name: $n, resource: $c} + $l
        + {why: ([noise[] | select(.type == $t and .path == $l.path and .change == $l.change) | .why] | first)} ]
    else [{type: $t, name: $n, resource: $c, path: "", change: $c, why: null}] end
  | .[] ] as $rows
| ($rows | map(select(.resource != "Ignore" and .resource != "NoChange" and .why == null))) as $real
| ($rows | map(select(.why != null))) as $noise
| ($rows | map(select(.resource == "Ignore" or .resource == "NoChange"))) as $quiet

| "## Bicep what-if",
  "",
  if ($real | length) == 0 then "**No changes.** The deployment would leave Azure as it is."
  else "**\($real | map(.type + "/" + .name) | unique | length) resource(s) would change.** Review before merging.",
    "",
    "| Change | Resource | Property | Before | After |",
    "|---|---|---|---|---|",
    ($real[] | "| \(.change) | `\(.type | split("/") | .[1:] | join("/"))/\(.name)` | \(if .path == "" then "" else "`\(.path)`" end) | \(.before | short) | \(.after | short) |")
  end,
  "",
  "<details><summary>Filtered as known noise (\($noise | length))</summary>",
  "",
  "These show up on every run. The list lives in `infra/scripts/whatif-summary.sh`.",
  "",
  "| Change | Resource | Property | Why |",
  "|---|---|---|---|",
  ($noise[] | "| \(.change) | `\(.name)` | `\(.path)` | \(.why) |"),
  "",
  "</details>",
  "",
  "<details><summary>Unchanged or not managed by this template (\($quiet | length))</summary>",
  "",
  ($quiet[] | "- \(.resource): `\(.type)/\(.name)`"),
  "",
  "</details>"
' "$1"

echo
echo "<details><summary>Raw what-if JSON</summary>"
echo
echo '```json'
jq '.changes' "$1"
echo '```'
echo
echo "</details>"
