#!/bin/bash
# Writes Config/PurrSecrets.ini from the purr3d actor's key in SSM.
#   scripts/fetch-api-key.sh            # sandbox (default)
#   scripts/fetch-api-key.sh prod
# Remember to point BaseUrl in Config/DefaultGame.ini at the same environment.
set -euo pipefail

ENV="${1:-sandbox}"
case "$ENV" in
  sandbox) PROFILE=nakom.is-sandbox ;;
  prod)    PROFILE=nakom.is-admin ;;
  *) echo "usage: $0 [sandbox|prod]" >&2; exit 1 ;;
esac

cd "$(dirname "$0")/.."
KEY=$(AWS_PROFILE="$PROFILE" aws ssm get-parameter --region eu-west-2 \
  --name "/purr/$ENV/api-keys/purr3d" --with-decryption \
  --query Parameter.Value --output text)

umask 077
cat > Config/PurrSecrets.ini <<INI
; Written by scripts/fetch-api-key.sh for $ENV. Gitignored; never commit.
[/Script/Purr3D.PurrApiSubsystem]
ApiKey=$KEY
INI
echo "Wrote Config/PurrSecrets.ini ($ENV key for purr3d)."
