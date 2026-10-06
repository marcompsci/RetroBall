#!/usr/bin/env bash
# Double-click: sets up and deploys the Retro Hoops Live server (server/README.md, steps 2-7) on Cloudflare.
# You'll be asked to: log in to Cloudflare in the browser (once), paste the Issuer ID and Key ID from
# App Store Connect, and drag in the downloaded SubscriptionKey_XXXX.p8 file. The key goes straight to
# Cloudflare's secret store; it isn't copied anywhere else. Log: ~/RetroHoops/Logs/server_setup.txt
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/server" || exit 1
mkdir -p "$HOME/RetroHoops/Logs"
LOG="$HOME/RetroHoops/Logs/server_setup.txt"
exec > >(tee "$LOG") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Live server setup"

if ! command -v npm >/dev/null; then
  echo "STOPPED: Node.js isn't installed. Get the LTS version from https://nodejs.org, install it, then double-click this again."
  exit 1
fi
echo "Node $(node --version)"

echo "1/6  Installing the server's tools..."
npm install --no-audit --no-fund || { echo "STOPPED: npm install failed."; exit 1; }

echo "2/6  Cloudflare login..."
if ! npx wrangler whoami 2>/dev/null | grep -qi "logged in"; then
  echo "A browser window opens: log in to Cloudflare (or sign up, free) and click Allow."
  npx wrangler login || { echo "STOPPED: Cloudflare login didn't finish."; exit 1; }
fi

echo "3/6  Database..."
if grep -q "REPLACE_WITH_YOUR_D1_DATABASE_ID" wrangler.toml; then
  OUT="$(npx wrangler d1 create retrohoops 2>&1)"; echo "$OUT"
  ID="$(echo "$OUT" | grep -oE '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' | head -1)"
  if [[ -z "$ID" ]]; then
    ID="$(npx wrangler d1 list 2>/dev/null | grep retrohoops | grep -oE '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' | head -1)"
  fi
  [[ -n "$ID" ]] || { echo "STOPPED: couldn't create the database."; exit 1; }
  sed -i '' "s/REPLACE_WITH_YOUR_D1_DATABASE_ID/$ID/" wrangler.toml
  echo "Database id $ID written to wrangler.toml"
fi
npx wrangler d1 execute retrohoops --remote --file=schema.sql --yes || npx wrangler d1 execute retrohoops --remote --file=schema.sql || { echo "STOPPED: couldn't create the tables."; exit 1; }

echo "4/6  First deploy..."
DEPLOY="$(npx wrangler deploy 2>&1)"; echo "$DEPLOY"
URL="$(echo "$DEPLOY" | grep -oE 'https://[a-zA-Z0-9.-]+\.workers\.dev' | head -1)"
[[ -n "$URL" ]] || { echo "STOPPED: deploy didn't print an address."; exit 1; }

echo "5/6  Secrets (stored only in Cloudflare)..."
openssl rand -base64 48 | tr -d '\n' | npx wrangler secret put SESSION_SECRET >/dev/null && echo "SESSION_SECRET set"
echo
echo "App Store Connect ▸ Users and Access ▸ Integrations ▸ In-App Purchase (create a key there if you haven't)."
read -r -p "Paste the Issuer ID and press Return: " ISSUER
read -r -p "Paste the Key ID and press Return: " KEYID
read -r -p "Drag the SubscriptionKey_XXXX.p8 file into this window and press Return: " P8
P8="${P8%\"}"; P8="${P8#\"}"; P8="${P8%\'}"; P8="${P8#\'}"; P8="${P8% }"; P8="${P8//\\ / }"
[[ -f "$P8" ]] || { echo "STOPPED: couldn't find that .p8 file."; exit 1; }
printf '%s' "$ISSUER" | npx wrangler secret put APPLE_ISSUER_ID >/dev/null && echo "APPLE_ISSUER_ID set"
printf '%s' "$KEYID" | npx wrangler secret put APPLE_KEY_ID >/dev/null && echo "APPLE_KEY_ID set"
npx wrangler secret put APPLE_PRIVATE_KEY < "$P8" >/dev/null && echo "APPLE_PRIVATE_KEY set"

echo "6/6  Checking the server and switching it on in the game..."
sleep 3
HEALTH="$(curl -s "$URL/v1/health")"
echo "Health: $HEALTH"
if [[ "$HEALTH" != *'"ok":true'* ]]; then echo "STOPPED: the server didn't answer as expected."; exit 1; fi
for F in "$ROOT/RetroHoops/Assets/Scripts/Logic/Net/Backend.cs" "$HOME/RetroHoops/Assets/Scripts/Logic/Net/Backend.cs"; do
  [[ -f "$F" ]] && sed -i '' "s#public const string Url = \"[^\"]*\";#public const string Url = \"$URL\";#" "$F"
done
cd "$ROOT"
git add server/wrangler.toml RetroHoops/Assets/Scripts/Logic/Net/Backend.cs
git commit -q -m "Turn on the Retro Hoops Live server ($URL)" && git push origin main && echo "Pushed."
echo
echo "LIVE SERVER READY: $URL"
echo "Last step in App Store Connect ▸ App Information ▸ App Store Server Notifications: set Production and Sandbox to"
echo "  $URL/v1/apple/notifications   (Version 2)"
echo "=== finished. You can close this window; Claude reads the log."
