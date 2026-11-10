#!/usr/bin/env bash
# Pushes Azure AI settings from the Bicep deployment record into Railway.
#
# The deployment outputs (endpoint, role-named deployments, the model behind each role)
# are the source of truth. This script owns the variables listed in CSHARP_VARS and
# PYTHON_VARS below; anything set by hand in the Railway dashboard for those keys gets
# overwritten on the next run. Everything else on the services is left alone.
#
# Usage:
#   infra/scripts/sync-railway-vars.sh --dry-run      # show the plan, key redacted
#   infra/scripts/sync-railway-vars.sh                # set vars, then redeploy each service
#   infra/scripts/sync-railway-vars.sh --no-redeploy  # set vars only; next deploy picks them up
#
# Needs: az (logged in), railway (logged in, or RAILWAY_API_TOKEN / RAILWAY_TOKEN in CI).
set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-aboutsamuel-ai}"
DEPLOYMENT_NAME="${DEPLOYMENT_NAME:-main}"
RAILWAY_ENVIRONMENT="${RAILWAY_ENVIRONMENT:-production}"
CSHARP_SERVICE="${CSHARP_SERVICE:-AboutSamuel}"
PYTHON_SERVICE="${PYTHON_SERVICE:-Python Agent Service}"

DRY_RUN=false
REDEPLOY=true
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=true ;;
    --no-redeploy) REDEPLOY=false ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

# output NAME [FIELD]: an output's value, or one field of an object output.
# ARM wraps every output as { type, value }, so the field goes after .value.
output() {
  local path="properties.outputs.$1.value${2:+.$2}"
  local val
  val="$(az deployment group show -g "$RESOURCE_GROUP" -n "$DEPLOYMENT_NAME" --query "$path" -o tsv)"
  [[ -n "$val" ]] || { echo "Deployment output '$path' is empty" >&2; exit 1; }
  printf '%s' "$val"
}

echo "Reading outputs of deployment '$DEPLOYMENT_NAME' in $RESOURCE_GROUP..."
ACCOUNT_NAME="$(output accountName)"
ENDPOINT="$(output openAiEndpoint)"
CHAT_MODEL="$(output models chat)"
JUDGE_MODEL="$(output models judge)"

# Deployment names are fixed by role. Fail loudly if Bicep and this script disagree.
DEPLOYED="$(output deploymentNames | tr '\n\r' '  ')"
for role in chat embedding judge; do
  [[ " $DEPLOYED " == *" $role "* ]] || { echo "Deployment '$role' missing from Bicep outputs: $DEPLOYED" >&2; exit 1; }
done

# Until Phase 3 (keyless auth) removes it, the key is the only secret.
API_KEY="$(az cognitiveservices account keys list -g "$RESOURCE_GROUP" -n "$ACCOUNT_NAME" --query key1 -o tsv)"
[[ -n "$API_KEY" ]] || { echo "Could not read API key for $ACCOUNT_NAME" >&2; exit 1; }
# In GitHub Actions, redact the key from logs in case anything ever prints it.
[[ "${GITHUB_ACTIONS:-}" == "true" ]] && echo "::add-mask::$API_KEY"

CSHARP_VARS=(
  "AzureOpenAI__Endpoint=$ENDPOINT"
  "AzureOpenAI__EmbeddingDeployment=embedding"
  "ChatSettings__ToolUse__Model=chat"
  "ChatSettings__Question__Model=chat"
)
CSHARP_KEY_VAR="AzureOpenAI__ApiKey"

PYTHON_VARS=(
  "AZURE_OPENAI_ENDPOINT=$ENDPOINT"
  "AZURE_OPENAI_CHAT_DEPLOYMENT=chat"
  "AZURE_OPENAI_CHAT_MODEL=$CHAT_MODEL"
  "AZURE_OPENAI_EMBEDDING_DEPLOYMENT=embedding"
  "AZURE_OPENAI_JUDGE_DEPLOYMENT=judge"
  "AZURE_OPENAI_JUDGE_MODEL=$JUDGE_MODEL"
)
PYTHON_KEY_VAR="AZURE_OPENAI_API_KEY"

sync_service() {
  local service="$1" key_var="$2"; shift 2
  echo
  echo "== $service ($RAILWAY_ENVIRONMENT)"
  for kv in "$@"; do echo "   $kv"; done
  echo "   $key_var=<redacted>"

  $DRY_RUN && return 0

  railway variable set "$@" -s "$service" -e "$RAILWAY_ENVIRONMENT" --skip-deploys > /dev/null
  # stdin keeps the key out of the command line and process list
  printf '%s' "$API_KEY" | railway variable set "$key_var" --stdin -s "$service" -e "$RAILWAY_ENVIRONMENT" --skip-deploys > /dev/null
  echo "   set"

  if $REDEPLOY; then
    railway redeploy -s "$service" -e "$RAILWAY_ENVIRONMENT" -y > /dev/null
    echo "   redeploy triggered"
  fi
}

sync_service "$CSHARP_SERVICE" "$CSHARP_KEY_VAR" "${CSHARP_VARS[@]}"
sync_service "$PYTHON_SERVICE" "$PYTHON_KEY_VAR" "${PYTHON_VARS[@]}"

echo
$DRY_RUN && echo "Dry run: nothing was changed." || echo "Done."
