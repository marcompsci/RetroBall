#!/usr/bin/env bash
# Builds the Caller Retro Ball Xcode project from the command line (macOS).
#   tools/build_ios.sh            -> iOS Simulator build in CallerRetroBall/iOSBuild/Simulator
#   tools/build_ios.sh device     -> device build in CallerRetroBall/iOSBuild/Device
# Close the project in the Unity Editor first (Unity can't open one project twice).
# Override the editor with UNITY=/path/to/Unity.app/Contents/MacOS/Unity
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/CallerRetroBall"
TARGET="${1:-simulator}"

if [[ -z "${UNITY:-}" ]]; then
  UNITY="$(ls -d /Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -1 || true)"
fi
if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
  echo "Unity 6 not found. Install Unity 6 LTS with iOS Build Support via Unity Hub, or set UNITY=..." >&2
  exit 1
fi

case "$TARGET" in
  simulator) METHOD="CallerRetroBall.EditorTools.ReleaseTools.BuildSimulator"; OUT="iOSBuild/Simulator" ;;
  device)    METHOD="CallerRetroBall.EditorTools.ReleaseTools.BuildDevice";    OUT="iOSBuild/Device" ;;
  *) echo "Usage: $0 [simulator|device]" >&2; exit 1 ;;
esac

LOG="$PROJECT/Logs/build_ios_$TARGET.log"
mkdir -p "$PROJECT/Logs"
echo "Building ($TARGET) with $UNITY"
echo "Log: $LOG"
"$UNITY" -batchmode -quit -nographics -buildTarget iOS -projectPath "$PROJECT" -executeMethod "$METHOD" -logFile "$LOG"
echo "Done. Open $PROJECT/$OUT/Unity-iPhone.xcodeproj in Xcode."
