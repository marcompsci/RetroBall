#!/usr/bin/env bash
# Double-click in Finder to build, archive and upload Retro Hoops to App Store Connect.
# Quit Unity first. Output is saved to ~/RetroBall/Logs/ship_console.log.
cd "$(dirname "$0")/.." && git pull --ff-only >/dev/null 2>&1
bash "$(dirname "$0")/ship_testflight.sh" "$@"
echo
read -n 1 -s -r -p "Done. Press any key to close this window."
