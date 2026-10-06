#!/usr/bin/env bash
# Double-click: pushes Retro Hoops (this folder, ~/RetroHoops-push) to GitHub using your Mac's saved GitHub login.
# Nothing is built or uploaded. Log: ~/RetroHoops/Logs/push.txt
cd "$(dirname "$0")/.." || exit 1
LOG="$HOME/RetroHoops/Logs/push.txt"
mkdir -p "$HOME/RetroHoops/Logs"
exec > >(tee "$LOG") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Push to GitHub"
echo "Local:  $(git log --oneline -1)"
if git push origin main; then
  echo "PUSH OK: GitHub now has $(git rev-parse --short HEAD)"
else
  echo "PUSH FAILED (see above). If it asks for a password, GitHub needs a personal access token, not your account password."
fi
echo "=== finished. You can close this window."
