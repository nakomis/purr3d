#!/bin/bash
# Signs Builds/Purr3D.app with a Developer ID, has Apple notarise it, staples the ticket, and asks Gatekeeper.
#   scripts/sign-and-notarise.sh "Developer ID Application: Your Name (TEAMID)" <notary-profile> [path/to/Purr3D.app]
#
# One-off setup:
#   security find-identity -v -p codesigning                # find your "Developer ID Application: ..." identity
#   xcrun notarytool store-credentials <notary-profile> \
#     --apple-id <you@example.com> --team-id <TEAMID>       # asks for an app-specific password, keeps it in the keychain
#
# See docs/guides/10-build-a-mac-app.md for what each step does (PURR3D-14).
set -euo pipefail

IDENTITY="${1:?usage: $0 \"Developer ID Application: ...\" <notary-profile> [Purr3D.app]}"
PROFILE="${2:?usage: $0 \"Developer ID Application: ...\" <notary-profile> [Purr3D.app]}"
cd "$(dirname "$0")/.."
APP="${3:-Builds/Purr3D.app}"
ENTITLEMENTS=macos/Purr3D.entitlements
ZIP="${APP%.app}.zip"

[ -d "$APP" ] || { echo "no app at $APP: build it first (File > Build Profiles > Build)" >&2; exit 1; }

echo "1/4 Signing $APP (hardened runtime, with $ENTITLEMENTS)"
codesign --deep --force --options runtime --timestamp \
  --entitlements "$ENTITLEMENTS" --sign "$IDENTITY" "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

echo "2/4 Sending to Apple's notary service (this waits; usually a few minutes)"
rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"
xcrun notarytool submit "$ZIP" --keychain-profile "$PROFILE" --wait

echo "3/4 Stapling the ticket to the app"
xcrun stapler staple "$APP"

echo "4/4 Asking Gatekeeper"
spctl --assess --type execute --verbose=2 "$APP"
rm -f "$ZIP"
echo "Done: $APP is signed, notarised and stapled."
