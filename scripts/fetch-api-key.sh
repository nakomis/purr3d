#!/bin/bash
# Writes the purr3d actor's key (from SSM) where the game looks for it.
#   scripts/fetch-api-key.sh                   # sandbox, for the editor: UserSettings/PurrSecrets.json
#   scripts/fetch-api-key.sh prod              # production
#   scripts/fetch-api-key.sh --player [prod]   # also for a built Purr3D.app:
#                                              #   ~/Library/Application Support/Nakomis/Purr3D/PurrSecrets.json
# UserSettings/ is per-user and gitignored by Unity's template. The file also records the matching base URL, so
# the key and the environment can't drift apart. The game's own "Connect to Purr" screen writes the same file, so
# this script is a shortcut, not a requirement (PURR3D-14). Both files are readable only by you.
set -euo pipefail

PLAYER=false
ENV=sandbox
for arg in "$@"; do
  case "$arg" in
    --player) PLAYER=true ;;
    sandbox|prod) ENV="$arg" ;;
    *) echo "usage: $0 [--player] [sandbox|prod]" >&2; exit 1 ;;
  esac
done

case "$ENV" in
  sandbox) PROFILE=nakom.is-sandbox; BASE_URL=https://purr.sandbox.nakomis.com/api ;;
  prod)    PROFILE=nakom.is-admin;   BASE_URL=https://purr.nakomis.com/api ;;
esac

cd "$(dirname "$0")/.."
KEY=$(AWS_PROFILE="$PROFILE" aws ssm get-parameter --region eu-west-2 \
  --name "/purr/$ENV/api-keys/purr3d" --with-decryption \
  --query Parameter.Value --output text)

write() {
  mkdir -p "$(dirname "$1")"
  (umask 077; printf '{\n  "environment": "%s",\n  "baseUrl": "%s",\n  "apiKey": "%s"\n}\n' "$ENV" "$BASE_URL" "$KEY" > "$1")
  chmod 600 "$1"
  echo "Wrote $1 ($ENV, purr3d)."
}

write UserSettings/PurrSecrets.json
# Company and product as set in Project Settings > Player (Application.persistentDataPath on macOS).
if $PLAYER; then write "$HOME/Library/Application Support/Nakomis/Purr3D/PurrSecrets.json"; fi
