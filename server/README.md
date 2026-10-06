# Retro Hoops Live server

This is a small backend for **Retro Hoops Live**. It runs on Cloudflare Workers with a D1 (SQLite) database. It does four jobs:

- **Sign-in:** checks a Game Center signature, so a request really comes from the player it claims to be.
- **Subscriptions:** asks **Apple's App Store Server API** whether a player's Retro Hoops Live subscription is active. It never takes the app's word for it.
- **Ratings:** keeps the official Live ratings. A rating changes only when **both** players' reports of a game agree.
- **Notifications:** receives Apple's subscription notifications (renewals, refunds, expiries).

**Status:** the server code type-checks, has 20 passing tests (`npm test`), and starts in Cloudflare's local runtime (`wrangler dev`), where it answered real requests. It has **not** been deployed, and it hasn't talked to the real Apple APIs yet: the tests use stand-in keys and a fake Apple. The game's side is switched **off** until you set `BackendConfig.Url`.

## Cost

Cloudflare's free plan covers 100,000 requests a day and 5 GB of D1. Workers Paid is $5/month if you outgrow it. One Live game uses about 6 requests.

## Setup (about 30 minutes)

1. **Cloudflare account:** sign up at [dash.cloudflare.com](https://dash.cloudflare.com) (free).
2. On your Mac, in this folder:
   ```bash
   cd ~/RetroHoops-push/server
   npm install
   npx wrangler login                      # opens the browser once
   npx wrangler d1 create retrohoops       # copy the database_id it prints into wrangler.toml
   npm run db:init                         # creates the tables
   ```
3. **Apple In-App Purchase key** (lets the server ask Apple about subscriptions): go to App Store Connect ▸ Users and Access ▸ **Integrations ▸ In-App Purchase** ▸ ＋. Download the `.p8` file. **You can only download it once**, so keep it somewhere safe and never commit it. Note the **Key ID** and the **Issuer ID** shown on that page.
4. **Secrets.** These go into Cloudflare, never into git:
   ```bash
   openssl rand -base64 48 | npx wrangler secret put SESSION_SECRET
   npx wrangler secret put APPLE_ISSUER_ID       # paste the Issuer ID
   npx wrangler secret put APPLE_KEY_ID          # paste the Key ID
   npx wrangler secret put APPLE_PRIVATE_KEY < ~/Downloads/SubscriptionKey_XXXXXXXX.p8
   ```
5. **Deploy:** run `npm run deploy`. It prints the address, for example `https://retrohoops-api.<you>.workers.dev`. Check it with `curl <address>/v1/health`, which should return `{"ok":true}`.
6. **App Store Server Notifications:** in App Store Connect ▸ your app ▸ App Information ▸ **App Store Server Notifications**, set both the Production and Sandbox URLs to `<address>/v1/apple/notifications` and choose **Version 2**.
7. **Turn it on in the game:** put the address in `RetroHoops/Assets/Scripts/Logic/Net/Backend.cs` ▸ `BackendConfig.Url` (https only), then rebuild.
8. **Before release:** set `APPLE_ENV = "production"` in `wrangler.toml` and run `npm run deploy` again. While you test with TestFlight and sandbox testers, keep it at `"sandbox"`, or use `"both"` if you need both kinds of purchase at once.
9. **Privacy:** with the server on, the app now sends data to you. Update the App Privacy answers and `PrivacyInfo.xcprivacy` as described in `RetroHoops/docs/PRIVACY.md` ("Retro Hoops Live server").

## Security model

| Threat | What stops it |
|---|---|
| Someone pretends to be another player | Every session starts with Apple's Game Center signature. The server checks the RSA-SHA256 signature using Apple's certificate (downloaded only from `https://*.apple.com`, cached, and date-checked), rejects signatures older than 10 minutes, and accepts each signature only once. |
| Session theft or forgery | Sessions are HS256 tokens signed with `SESSION_SECRET`, valid for 1 hour, and checked in constant time. `alg:none` and edited tokens are refused. The app keeps the token in memory only. |
| Fake subscription | The app sends only the transaction ID. The server asks Apple's App Store Server API, signed with your In-App Purchase key, and checks the product, bundle ID, expiry and revocation. |
| Sharing one subscription between players | A subscription is linked to one Game Center player. Purchases are tagged with the player's `appAccountToken`, and a token for someone else is refused. |
| Refunds and cancellations | Apple's notifications make the server re-ask Apple. The notification's contents are never trusted directly. The server also re-checks every 6 hours. |
| Faked ratings | Ratings change only on the server. Both phones report the score and the game's final checksum. Matching reports settle the game once, with a race-safe claim. Mismatched reports are marked "disputed" and change nobody's rating. A lone report settles after 10 minutes. |
| Rating farming with a second account | At most 5 rated games a day between the same two players, and both accounts need a paid subscription. |
| Abuse and flooding | Rate limits: 120 requests/min per IP, 10 sign-ins/min per IP, and 60 requests/min per player. Bodies are capped at 8 KB (64 KB for Apple notifications), must be JSON, and every field is validated. |
| SQL injection | Every query uses bound parameters. |
| Browser-based attacks | No CORS headers (the app isn't a browser), plus `no-store`, `nosniff`, a `CSP default-src 'none'` and `X-Frame-Options: DENY` on every response. |
| Leaking secrets | Secrets are kept only in Cloudflare (`wrangler secret`), `.dev.vars` is git-ignored, and error messages are short and generic. |
| Data deletion | LIVE ▸ DELETE MY LIVE DATA (`POST /v1/me/delete`) removes the player's rating, record and subscription link, and blanks their id in past games. |

**Known limits:**

- Both phones run the simulation, so a hacked app could send a wrong score. The server can't replay the game. But a mismatch with the honest player's report blocks any rating change. A cheater can only win if the other phone never reports, for example if it crashed.
- Apple's Game Center certificate is trusted because of where it is downloaded from (HTTPS, `apple.com`). The server doesn't walk its certificate chain.

## API

| Method and path | Auth | What it does |
|---|---|---|
| `POST /v1/session` | Game Center proof | `{playerId, name, publicKeyUrl, signature, salt, timestamp}` → `{token, expiresIn, accountToken, player}` |
| `POST /v1/subscription` | session | `{originalTransactionId}` → `{active, expiresAt}` |
| `GET /v1/me` | session | `{player, subscription}` |
| `POST /v1/me/delete` | session | Erases your data |
| `POST /v1/match/start` | session plus subscription | `{matchKey, opponentId, seat}` |
| `POST /v1/match/result` | session | `{matchKey, scoreA, scoreB, hash, outcome}` → `{state, change, player}` |
| `GET /v1/leaderboard` | none | Top 50 (name, rating, games) |
| `POST /v1/apple/notifications` | none (it only triggers a re-check with Apple) | App Store Server Notifications V2 |

## Developing

Use `npm test` (Node 22+), `npm run typecheck` and `npm run dev` (local runtime). For `npm run dev`, first create `.dev.vars` with your secrets and run `npm run db:init:local`.
