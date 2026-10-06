#!/usr/bin/env bash
# One command from Unity project to Retro Hoops running in the iOS Simulator (macOS).
#
#   bash ~/RetroHoops-push/tools/play_on_simulator.sh            # project at ~/RetroHoops (or ~/RetroBall)
#   bash ~/RetroHoops-push/tools/play_on_simulator.sh /path/to/project
#
# Quit the Unity Editor first (Unity can't open one project twice). Steps:
#   1. Unity (batch mode) writes the Xcode project to <project>/iOSBuild/Simulator
#   2. xcodebuild compiles it for the Simulator (no signing needed)
#   3. an iPhone simulator boots, the app installs and launches
# Everything is logged to <project>/Logs/ so it can be shared if something fails.
set -uo pipefail

PROJECT="${1:-$( [[ -d $HOME/RetroHoops ]] && echo $HOME/RetroHoops || echo $HOME/RetroBall )}"
LOGS="$PROJECT/Logs"
mkdir -p "$LOGS"
REPORT="$LOGS/RetroHoops-release.txt"
note() { echo "$1"; printf '=== %s  play_on_simulator\n%s\n\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$1" >> "$REPORT"; }

# Use the exact editor version the project was made with (another version would refuse to open it in batch mode).
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt" 2>/dev/null | tr -d '[:space:]')"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$UNITY" ]]; then note "STOPPED: Unity $VERSION (the project's version) not found at $UNITY."; exit 1; fi
if [[ ! -d "/Applications/Unity/Hub/Editor/$VERSION/PlaybackEngines/iOSSupport" ]]; then
  note "STOPPED: Unity $VERSION has no iOS Build Support. Unity Hub > Installs > $VERSION > gear > Add modules > iOS Build Support."; exit 1
fi
# Only stop if an Editor really has THIS project open (Unity Hub, other projects and helpers are fine).
OPEN="$(ps -axo pid=,args= | grep "Unity.app/Contents/MacOS/Unity " | grep -v -- "-batchmode" | grep -i -- "-projectpath $PROJECT" | grep -v grep)"
if [[ -n "$OPEN" ]]; then
  note "STOPPED: Unity has this project open. Quit the Unity Editor (Cmd+Q) and run this again.
$OPEN"; exit 1
fi
if ! xcode-select -p >/dev/null 2>&1; then note "STOPPED: Xcode command line tools not set up. Run: sudo xcode-select -s /Applications/Xcode.app"; exit 1; fi

echo "1/3  Unity: writing the Xcode project (a few minutes)..."
# A marker for "made by this run": an Xcode project or app left over from an earlier build doesn't count.
STAMP="$LOGS/.simulator_build_started"
touch "$STAMP"
"$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath "$PROJECT" \
  -executeMethod CallerRetroBall.EditorTools.ReleaseTools.BuildSimulator -logFile "$LOGS/unity_build_simulator.log"
XCPROJ="$PROJECT/iOSBuild/Simulator/Unity-iPhone.xcodeproj"
if grep -qi "another Unity instance is running\|multiple Unity instances" "$LOGS/unity_build_simulator.log" 2>/dev/null; then
  note "STOPPED: Unity says the project is open in another Unity window. Quit the Unity Editor (Cmd+Q) and run this again.
Unity processes:
$(ps -axo pid=,args= | grep 'Unity.app/Contents/MacOS/Unity' | grep -v grep | cut -c1-200)"
  exit 1
fi
if [[ ! -d "$XCPROJ" || ! "$XCPROJ/project.pbxproj" -nt "$STAMP" ]] || grep -q "Build Finished, Result: Failure\|iOS build Failed" "$LOGS/unity_build_simulator.log" 2>/dev/null; then
  note "FAILED at step 1 (Unity build). Errors:
$(grep -E 'error CS|iOS build Failed|^  - |Exception|aborting|another Unity' "$LOGS/unity_build_simulator.log" | grep -v 'Licensing' | head -25)
Last lines of the Unity log:
$(tail -15 "$LOGS/unity_build_simulator.log")"
  exit 1
fi

echo "2/3  Xcode: compiling for the Simulator (several minutes the first time)..."
DERIVED="$PROJECT/iOSBuild/Simulator/DerivedData"
xcodebuild -project "$XCPROJ" -scheme Unity-iPhone -configuration Release -sdk iphonesimulator \
  -derivedDataPath "$DERIVED" CODE_SIGNING_ALLOWED=NO build > "$LOGS/xcodebuild_simulator.log" 2>&1
APP="$(find "$DERIVED/Build/Products" -maxdepth 2 -name '*.app' -type d | head -1)"
# Only an app this run built counts (something inside it is newer than the start marker).
if [[ -n "$APP" && -z "$(find "$APP" -type f -newer "$STAMP" | head -1)" ]]; then APP=""; fi
if [[ -z "$APP" ]]; then
  note "FAILED at step 2 (xcodebuild). Errors:
$(grep -E 'error:|BUILD FAILED' "$LOGS/xcodebuild_simulator.log" | head -25)"
  exit 1
fi

echo "3/3  Simulator: booting an iPhone and launching Retro Hoops..."
DEVICE="$(xcrun simctl list devices available | grep -E 'iPhone' | grep -m1 -E 'Booted' | grep -oE '[0-9A-F-]{36}')"
if [[ -z "$DEVICE" ]]; then
  DEVICE="$(xcrun simctl list devices available | grep -E 'iPhone .*Pro Max' | grep -m1 -oE '[0-9A-F-]{36}')"
  [[ -z "$DEVICE" ]] && DEVICE="$(xcrun simctl list devices available | grep -E 'iPhone' | grep -m1 -oE '[0-9A-F-]{36}')"
  if [[ -z "$DEVICE" ]]; then note "FAILED at step 3: no iPhone simulator installed. Xcode > Settings > Components > iOS > Get."; exit 1; fi
  xcrun simctl boot "$DEVICE" >/dev/null 2>&1
fi
open -a Simulator
BUNDLE="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Info.plist")"
xcrun simctl install "$DEVICE" "$APP" && xcrun simctl launch "$DEVICE" "$BUNDLE" >/dev/null
note "SIMULATOR OK: $BUNDLE launched on $(xcrun simctl list devices | grep "$DEVICE" | sed 's/ (.*//' | xargs)"
