#!/usr/bin/env bash
# Gives both Railway services the sp-aboutsamuel-railway identity for keyless Azure OpenAI
# calls: a new client secret plus the tenant and client IDs, read by EnvironmentCredential.
# Railway has no managed identity, so this secret is the one credential prod holds. Its only
# role is Cognitive Services OpenAI User on the Foundry account (infra/bootstrap).
#
# Run by hand, not in CI: creating app secrets needs Microsoft Graph rights the CI identity
# shouldn't have. Also the rotation procedure:
#   1. infra/scripts/set-railway-sp-credential.sh     # adds a secret; older ones keep working
#   2. confirm both services log "Azure OpenAI auth: entra_id" and chat works
#   3. az ad app credential list --id <appId>        # then delete the old one:
#      az ad app credential delete --id <appId> --key-id <old keyId>
#
# Usage:
#   infra/scripts/set-railway-sp-credential.sh --dry-run    # show the plan, create nothing
#   infra/scripts/set-railway-sp-credential.sh              # set vars, no redeploy
#   infra/scripts/set-railway-sp-credential.sh --redeploy   # set vars, then redeploy each service
#
# Needs: az (logged in as someone who owns the app), railway (logged in).
set -euo pipefail

APP_NAME="${APP_NAME:-sp-aboutsamuel-railway}"
SECRET_YEARS="${SECRET_YEARS:-1}"
RAILWAY_ENVIRONMENT="${RAILWAY_ENVIRONMENT:-production}"
SERVICES=("${CSHARP_SERVICE:-AboutSamuel}" "${PYTHON_SERVICE:-Python Agent Service}")

DRY_RUN=false
REDEPLOY=false
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=true ;;
    --redeploy) REDEPLOY=true ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

# Display names aren't unique in Entra, so insist on exactly one match.
APP_IDS="$(az ad app list --display-name "$APP_NAME" --query "[].appId" -o tsv)"
[[ "$(wc -w <<< "$APP_IDS")" -eq 1 ]] || { echo "Expected one app named $APP_NAME, found: ${APP_IDS:-none}" >&2; exit 1; }
APP_ID="$APP_IDS"
TENANT_ID="$(az account show --query tenantId -o tsv)"

# Same variables in both services: they're the Azure SDK's standard names, not app settings.
VARS=(
  "AZURE_TENANT_ID=$TENANT_ID"
  "AZURE_CLIENT_ID=$APP_ID"
  # DefaultAzureCredential tries only this; no probing for managed identity or a CLI.
  "AZURE_TOKEN_CREDENTIALS=EnvironmentCredential"
)

echo "App $APP_NAME ($APP_ID), tenant $TENANT_ID"
for s in "${SERVICES[@]}"; do
  echo
  echo "== $s ($RAILWAY_ENVIRONMENT)"
  for kv in "${VARS[@]}"; do echo "   $kv"; done
  echo "   AZURE_CLIENT_SECRET=<new, ${SECRET_YEARS}y>"
done

if $DRY_RUN; then echo; echo "Dry run: nothing was changed."; exit 0; fi

# --append keeps existing secrets valid, so services using the old one don't break mid-rotation.
SECRET="$(az ad app credential reset --id "$APP_ID" --append --years "$SECRET_YEARS" \
  --display-name "railway-$(date +%Y-%m-%d)" --query password -o tsv 2>/dev/null)"
[[ -n "$SECRET" ]] || { echo "Could not create a secret for $APP_ID" >&2; exit 1; }

for s in "${SERVICES[@]}"; do
  railway variable set "${VARS[@]}" -s "$s" -e "$RAILWAY_ENVIRONMENT" --skip-deploys > /dev/null
  # stdin keeps the secret out of the command line and process list
  printf '%s' "$SECRET" | railway variable set AZURE_CLIENT_SECRET --stdin -s "$s" -e "$RAILWAY_ENVIRONMENT" --skip-deploys > /dev/null
  echo "$s: set"
  if $REDEPLOY; then
    railway redeploy -s "$s" -e "$RAILWAY_ENVIRONMENT" -y > /dev/null
    echo "$s: redeploy triggered"
  fi
done
unset SECRET

echo
echo "Secrets on $APP_NAME now:"
az ad app credential list --id "$APP_ID" --query "[].{name:displayName, keyId:keyId, expires:endDateTime}" -o table
