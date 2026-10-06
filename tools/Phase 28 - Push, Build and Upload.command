#!/usr/bin/env bash
# Double-click: pushes Retro Hoops to GitHub, builds it in Unity and Xcode (the real compile check for
# Phases 26-28), and tries the TestFlight upload. Quit the Unity Editor first. Logs: ~/RetroHoops/Logs.
cd "$(dirname "$0")/.." || exit 1
LOG="$HOME/RetroHoops/Logs/phase28_run.txt"
mkdir -p "$HOME/RetroHoops/Logs"
exec > >(tee "$LOG") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Phase 28 run"

echo "1/2  Pushing to GitHub..."
if git push origin main; then echo "PUSH OK: $(git log --oneline -1)"; else echo "PUSH FAILED (see above). Carrying on with the build."; fi

echo "2/2  Building in Unity and Xcode, then uploading to TestFlight (10-20 minutes)..."
bash tools/ship_testflight.sh
echo "=== finished $(date '+%H:%M:%S'). You can close this window; Claude reads the logs."
