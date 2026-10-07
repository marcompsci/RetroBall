#!/usr/bin/env bash
# Double-click: takes the App Store screenshots from the Simulator build the Build Check made (run that first).
# For each shot the game is launched with RH_SCREENSHOT=<shot>, opens straight onto that screen, and the picture is
# saved to ~/RetroHoops/AppStoreScreenshots/<simulator>/. iPhone 6.9" (iPhone 17 Pro Max: 1320 x 2868) is the size
# App Store Connect needs; a 13" iPad set is taken too if an iPad simulator is installed.
# The summary Claude reads is ~/RetroHoops/Logs/store_screenshots.txt.
cd "$(dirname "$0")/.." || exit 1
PROJECT="$HOME/RetroHoops"
LOGS="$PROJECT/Logs"
OUT_ROOT="$PROJECT/AppStoreScreenshots"
mkdir -p "$LOGS" "$OUT_ROOT"
exec > >(tee "$LOGS/store_screenshots.txt") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  App Store screenshots"

APP="$(find "$PROJECT/iOSBuild/Simulator/DerivedData/Build/Products" -maxdepth 2 -name '*.app' -type d 2>/dev/null | head -1)"
if [[ -z "$APP" ]]; then echo "STOPPED: no Simulator build yet. Run 'Build Check (Simulator).command' first."; exit 1; fi
echo "App: $APP (built $(stat -f '%Sm' "$APP" 2>/dev/null))"
BUNDLE="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Info.plist")"

# name:seconds to wait, in App Store order (keep in step with StoreShots.All in Logic/Feel/StoreShots.cs).
SHOTS="game:14 title:8 fullcourt:14 play:9 clutch:9 park:9 locker:10"

shoot_on() {
  local NAME="$1" DEVICE="$2"
  local DIR="$OUT_ROOT/$NAME"
  mkdir -p "$DIR"
  echo "--- $NAME ($DEVICE)"
  xcrun simctl boot "$DEVICE" >/dev/null 2>&1
  xcrun simctl bootstatus "$DEVICE" -b >/dev/null 2>&1
  open -a Simulator --args -CurrentDeviceUDID "$DEVICE" >/dev/null 2>&1
  # A tidy status bar: 9:41, full battery and signal.
  xcrun simctl status_bar "$DEVICE" override --time "9:41" --batteryState charged --batteryLevel 100 \
    --cellularMode active --cellularBars 4 --wifiBars 3 >/dev/null 2>&1
  xcrun simctl install "$DEVICE" "$APP" || { echo "FAILED: couldn't install on $NAME"; return 1; }
  local n=1
  for entry in $SHOTS; do
    local shot="${entry%%:*}" wait="${entry##*:}"
    xcrun simctl terminate "$DEVICE" "$BUNDLE" >/dev/null 2>&1
    SIMCTL_CHILD_RH_SCREENSHOT="$shot" xcrun simctl launch "$DEVICE" "$BUNDLE" >/dev/null || { echo "FAILED: launch for $shot"; continue; }
    sleep "$wait"
    local file="$DIR/$(printf '%02d' $n)-$shot.png"
    if xcrun simctl io "$DEVICE" screenshot --type=png "$file" >/dev/null 2>&1; then
      # Full Court plays sideways (Unity LandscapeLeft: phone turned counter-clockwise). simctl saves the screen in
      # the phone's portrait framebuffer, so turn that picture a quarter counter-clockwise to stand it upright.
      if [[ "$shot" == "fullcourt" ]]; then
        local w h
        w="$(sips -g pixelWidth "$file" 2>/dev/null | awk '/pixelWidth/{print $2}')"
        h="$(sips -g pixelHeight "$file" 2>/dev/null | awk '/pixelHeight/{print $2}')"
        if [[ -n "$w" && -n "$h" && "$h" -gt "$w" ]]; then sips -r 270 "$file" >/dev/null 2>&1; fi
      fi
      echo "  $(basename "$file")  $(sips -g pixelWidth -g pixelHeight "$file" 2>/dev/null | awk '/pixel/{printf "%s ", $2}')"
    else
      echo "  FAILED: screenshot $shot"
    fi
    n=$((n + 1))
  done
  xcrun simctl terminate "$DEVICE" "$BUNDLE" >/dev/null 2>&1
  xcrun simctl status_bar "$DEVICE" clear >/dev/null 2>&1
}

PHONE="$(xcrun simctl list devices available | grep -E 'iPhone [0-9]+ Pro Max' | tail -1 | grep -oE '[0-9A-F-]{36}')"
if [[ -z "$PHONE" ]]; then echo "STOPPED: no 'Pro Max' iPhone simulator. Xcode > Settings > Components > iOS."; exit 1; fi
shoot_on "iPhone-6.9" "$PHONE"

IPAD="$(xcrun simctl list devices available | grep -E 'iPad Pro 13-inch' | tail -1 | grep -oE '[0-9A-F-]{36}')"
if [[ -n "$IPAD" ]]; then shoot_on "iPad-13" "$IPAD"; else echo "(No 13-inch iPad Pro simulator: iPad screenshots skipped.)"; fi

echo
echo "SCREENSHOTS DONE: $OUT_ROOT"
open "$OUT_ROOT" >/dev/null 2>&1
echo "=== finished $(date '+%H:%M:%S'). You can close this window; Claude reads the logs."
