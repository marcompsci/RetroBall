#!/usr/bin/env bash
# Double-click to re-try only the upload of the last Retro Hoops archive (no Unity build, same build number).
# Use after "Ship Retro Hoops.command" got as far as the upload step and failed.
bash "$(dirname "$0")/ship_testflight.sh" --upload-only
echo
read -n 1 -s -r -p "Done. Press any key to close this window."
