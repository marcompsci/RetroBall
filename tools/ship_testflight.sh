#!/usr/bin/env bash
# Builds Retro Hoops for the App Store, archives it, and uploads it to App Store Connect (TestFlight).
#
#   bash ~/RetroBall-push/tools/ship_testflight.sh                 # build, archive, upload
#   bash ~/RetroBall-push/tools/ship_testflight.sh --export-only   # build + archive + .ipa, no upload
#   TEAM=ABCDE12345 bash ~/RetroBall-push/tools/ship_testflight.sh # pick the Apple team explicitly
#
# Before the first upload: create the app in App Store Connect with bundle ID com.phoronomicstudios.retroball
# (docs/SUBMISSION.md, step 1). Quit the Unity Editor first. Xcode must be signed in to your paid
# Apple Developer account (Xcode ▸ Settings ▸ Accounts). Logs go to <project>/Logs/.
set -uo pipefail

PROJECT="${PROJECT:-$HOME/RetroBall}"
UPLOAD=1
for a in "$@"; do
  case "$a" in
    --export-only) UPLOAD=0 ;;
    /*) PROJECT="$a" ;;
  esac
done
LOGS="$PROJECT/Logs"
mkdir -p "$LOGS"
REPORT="$LOGS/RetroBall-release.txt"
note() { echo "$1"; printf '=== %s  ship_testflight\n%s\n\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$1" >> "$REPORT"; }
# Everything printed below also goes to Logs/ship_console.log, so a failed run can be read afterwards.
exec > >(tee "$LOGS/ship_console.log") 2>&1
note "STARTED: project $PROJECT, $(sw_vers -productVersion 2>/dev/null | sed 's/^/macOS /'), $(xcodebuild -version 2>/dev/null | head -1)"

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt" 2>/dev/null | tr -d '[:space:]')"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$UNITY" ]]; then note "STOPPED: Unity $VERSION (the project's version) not found at $UNITY."; exit 1; fi
OPEN="$(ps -axo pid=,args= | grep "Unity.app/Contents/MacOS/Unity " | grep -v -- "-batchmode" | grep -i -- "-projectpath $PROJECT" | grep -v grep)"
if [[ -n "$OPEN" ]]; then note "STOPPED: Unity has this project open. Quit the Unity Editor (Cmd+Q) and run this again."; exit 1; fi
if ! command -v xcodebuild >/dev/null; then note "STOPPED: xcodebuild not found. Install Xcode, open it once, and accept the licence."; exit 1; fi

# Team: $TEAM, else tools/apple_team.txt, else Unity's Signing Team ID. It must be the PAID team
# (the one that owns com.phoronomicstudios.*), not the free personal team used for early device tests.
TEAMFILE="$(cd "$(dirname "$0")" && pwd)/apple_team.txt"
if [[ -z "${TEAM:-}" && -f "$TEAMFILE" ]]; then TEAM="$(tr -d '[:space:]' < "$TEAMFILE")"; fi
if [[ -z "${TEAM:-}" ]]; then
  TEAM="$(sed -n 's/^  appleDeveloperTeamID: *//p' "$PROJECT/ProjectSettings/ProjectSettings.asset" | tr -d '[:space:]')"
fi
if [[ ! "${TEAM:-}" =~ ^[A-Z0-9]{10}$ ]]; then
  note "STOPPED: no Apple Team ID for the paid team. Find it at developer.apple.com ▸ Account ▸ Membership details
(10 letters/digits; also shown as the App ID Prefix on the com.phoronomicstudios identifiers), then either put it in
~/RetroBall-push/tools/apple_team.txt or run:  TEAM=YOURTEAMID bash ~/RetroBall-push/tools/ship_testflight.sh"
  exit 1
fi
if [[ "$TEAM" == "X6LZQ3FS36" ]]; then
  note "STOPPED: X6LZQ3FS36 is the free personal team (it owns com.marcompsci.retroball). Use the paid team's ID instead (see above)."
  exit 1
fi
echo "Team: $TEAM"

OUT="$PROJECT/iOSBuild/AppStore"
ARCHIVE="$PROJECT/iOSBuild/RetroBall.xcarchive"
EXPORT="$PROJECT/iOSBuild/Export"

echo "1/3  Unity: App Store build (raises the build number; a few minutes)..."
"$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath "$PROJECT" \
  -executeMethod CallerRetroBall.EditorTools.ReleaseTools.BuildAppStore -teamId "$TEAM" -logFile "$LOGS/unity_build_appstore.log"
if [[ ! -d "$OUT/Unity-iPhone.xcodeproj" ]]; then
  note "FAILED (Unity App Store build). Last lines of the Unity log:
$(grep -E 'error|Exception' "$LOGS/unity_build_appstore.log" | grep -v Licensing | head -15)
$(tail -10 "$LOGS/unity_build_appstore.log")"
  exit 1
fi
BUILD="$(awk '/^  buildNumber:/{f=1;next} f&&/iPhone:/{print $2;exit}' "$PROJECT/ProjectSettings/ProjectSettings.asset")"

echo "2/3  Xcode: archiving (5-15 minutes)..."
rm -rf "$ARCHIVE"
xcodebuild -project "$OUT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -destination 'generic/platform=iOS' -archivePath "$ARCHIVE" -allowProvisioningUpdates \
  DEVELOPMENT_TEAM="$TEAM" CODE_SIGN_STYLE=Automatic archive > "$LOGS/xcodebuild_archive.log" 2>&1
if [[ ! -d "$ARCHIVE" ]]; then
  note "FAILED (archive). Errors from Logs/xcodebuild_archive.log:
$(grep -E 'error:|requires a provisioning|No Account|No profiles' "$LOGS/xcodebuild_archive.log" | sort -u | head -15)
If it mentions signing or accounts: Xcode ▸ Settings ▸ Accounts, select your Apple ID, make sure your paid team is listed (not only \"Personal Team\")."
  exit 1
fi

if [[ $UPLOAD -eq 1 ]]; then DEST="upload"; echo "3/3  Uploading to App Store Connect..."; else DEST="export"; echo "3/3  Exporting the .ipa (no upload)..."; fi
PLIST="$PROJECT/iOSBuild/ExportOptions.plist"
cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>$DEST</string>
  <key>teamID</key><string>$TEAM</string>
  <key>signingStyle</key><string>automatic</string>
  <key>uploadSymbols</key><true/>
  <key>manageAppVersionAndBuildNumber</key><false/>
</dict>
</plist>
EOF
rm -rf "$EXPORT"
xcodebuild -exportArchive -archivePath "$ARCHIVE" -exportOptionsPlist "$PLIST" -exportPath "$EXPORT" \
  -allowProvisioningUpdates > "$LOGS/xcodebuild_export.log" 2>&1
STATUS=$?
if [[ $STATUS -ne 0 ]]; then
  HINT=""
  if grep -qiE "no suitable application records|Cannot determine the Apple ID|app record" "$LOGS/xcodebuild_export.log"; then
    HINT="App Store Connect has no app with bundle ID com.phoronomicstudios.retroball yet. Create it first (docs/SUBMISSION.md, step 1), then run this again."
  elif grep -qiE "bundle version must be higher|has already been uploaded|redundant binary" "$LOGS/xcodebuild_export.log"; then
    HINT="That build number was already uploaded. Just run this again: it raises the build number every time."
  fi
  note "FAILED (export/upload). Errors from Logs/xcodebuild_export.log:
$(grep -iE 'error' "$LOGS/xcodebuild_export.log" | sort -u | head -12)
$HINT"
  exit 1
fi

if [[ $UPLOAD -eq 1 ]]; then
  note "UPLOADED: Retro Hoops build $BUILD (team $TEAM). It shows in App Store Connect ▸ TestFlight after Apple finishes processing (usually 10-30 minutes; you get an email)."
else
  note "EXPORTED: $(ls "$EXPORT"/*.ipa 2>/dev/null) (build $BUILD). Not uploaded. Run without --export-only to upload."
fi
