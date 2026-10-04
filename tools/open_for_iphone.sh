#!/usr/bin/env bash
# Builds the Retro Hoops Xcode project for a real iPhone and opens it in Xcode (macOS).
#
#   bash ~/RetroHoops-push/tools/open_for_iphone.sh            # project at ~/RetroHoops (or ~/RetroBall)
#
# Quit the Unity Editor first. Then, in Xcode: Signing & Capabilities > Team (your Apple ID),
# choose your iPhone at the top, and press Run. Logs go to <project>/Logs/.
set -uo pipefail

PROJECT="${1:-$( [[ -d $HOME/RetroHoops ]] && echo $HOME/RetroHoops || echo $HOME/RetroBall )}"
LOGS="$PROJECT/Logs"
mkdir -p "$LOGS"
REPORT="$LOGS/RetroHoops-release.txt"
note() { echo "$1"; printf '=== %s  open_for_iphone\n%s\n\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$1" >> "$REPORT"; }

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt" 2>/dev/null | tr -d '[:space:]')"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$UNITY" ]]; then note "STOPPED: Unity $VERSION (the project's version) not found at $UNITY."; exit 1; fi
OPEN="$(ps -axo pid=,args= | grep "Unity.app/Contents/MacOS/Unity " | grep -v -- "-batchmode" | grep -i -- "-projectpath $PROJECT" | grep -v grep)"
if [[ -n "$OPEN" ]]; then note "STOPPED: Unity has this project open. Quit the Unity Editor (Cmd+Q) and run this again."; exit 1; fi

echo "1/2  Unity: writing the iPhone Xcode project (a few minutes)..."
"$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath "$PROJECT" \
  -executeMethod CallerRetroBall.EditorTools.ReleaseTools.BuildDevice -logFile "$LOGS/unity_build_device.log"
XCPROJ="$PROJECT/iOSBuild/Device/Unity-iPhone.xcodeproj"
if [[ ! -d "$XCPROJ" ]]; then
  note "FAILED (Unity device build). Last lines of the Unity log:
$(grep -E 'error|Exception' "$LOGS/unity_build_device.log" | grep -v Licensing | head -15)
$(tail -10 "$LOGS/unity_build_device.log")"
  exit 1
fi
echo "2/2  Opening it in Xcode..."
open "$XCPROJ"
note "DEVICE PROJECT OK: opened $XCPROJ in Xcode. Next: Signing & Capabilities > Team, pick your iPhone, press Run."
