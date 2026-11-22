#!/bin/bash
# Writes UserSettings/PurrSecrets.json from the purr3d actor's key in SSM.
#   scripts/fetch-api-key.sh            # sandbox (default)
#   scripts/fetch-api-key.sh prod
# UserSettings/ is per-user and gitignored by Unity's template. The file also
# records the matching base URL, so the key and the environment can't drift apart.
set -euo pipefail

ENV="${1:-sandbox}"
case "$ENV" in
  sandbox) PROFILE=nakom.is-sandbox; BASE_URL=https://purr.sandbox.nakomis.com/api ;;
  prod)    PROFILE=nakom.is-admin;   BASE_URL=https://purr.nakomis.com/api ;;
  *) echo "usage: $0 [sandbox|prod]" >&2; exit 1 ;;
esac

cd "$(dirname "$0")/.."
KEY=$(AWS_PROFILE="$PROFILE" aws ssm get-parameter --region eu-west-2 \
  --name "/purr/$ENV/api-keys/purr3d" --with-decryption \
  --query Parameter.Value --output text)

mkdir -p UserSettings
umask 077
printf '{\n  "environment": "%s",\n  "baseUrl": "%s",\n  "apiKey": "%s"\n}\n' "$ENV" "$BASE_URL" "$KEY" > UserSettings/PurrSecrets.json
echo "Wrote UserSettings/PurrSecrets.json ($ENV, purr3d)."
