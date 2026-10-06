# Retro Hoops Live: setting up the $10.99/month subscription

Retro Hoops Live is an online head-to-head mode. Game Center finds the opponent and relays the game (Apple runs the servers, so there's no server of ours). Live needs an auto-renewing monthly subscription. Everything else in the game stays free.

**Status:** the code is written (StoreKit 2 in `Plugins/iOS/RetroStore.swift`, Game Center matchmaking in `Plugins/iOS/RetroLive.mm`, screens under PLAY ▸ LIVE). It has **not** been compiled in Xcode or tested with a real or sandbox purchase yet.

## 1. Paid Apps agreement, tax and banking (once)

App Store Connect ▸ **Business**: accept the **Paid Apps Agreement**, then add a bank account and the tax forms. App Store Connect won't sell a subscription until all three are marked Active, and this can take a few days.

## 2. Create the subscription (once)

App Store Connect ▸ Apps ▸ Retro Hoops ▸ **Monetization ▸ Subscriptions**:

1. **Subscription Group** ▸ ＋. Reference name: `Retro Hoops Live`.
2. In the group, ＋ **Create Subscription**:

| Field | Value |
|---|---|
| Reference Name | Retro Hoops Live Monthly |
| Product ID | `com.phoronomicstudios.retrohoops.live.monthly` (it must match `LiveMode.ProductId` exactly) |
| Duration | 1 Month |
| Price | $10.99 (USD). Accept Apple's suggested prices for other countries, or set them yourself. |
| Display name | Retro Hoops Live |
| Description | Play people online, with a Live rating and leaderboard. |
| Review screenshot | A screenshot of the LIVE paywall (PLAY ▸ LIVE before subscribing). |
| Review notes | See "App Review notes" below. |

3. Optional: add a free trial under **Subscription Prices ▸ Introductory Offers**. The paywall text doesn't mention a trial, so update `LiveMode.Terms` if you add one.
4. The first subscription has to be submitted **together with an app version**. On the version page, under **In-App Purchases and Subscriptions**, add it before you press Submit for Review.

## 3. Game Center

Game Center must be on for the app (it already is, through the capability). Add the leaderboard **`retrohoops.live.rating`** ("Live Rating (best)", Integer, high score is best) the same way as the others in `GAME_CENTER.md`. Matchmaking needs no extra setup.

## 4. Privacy policy and terms (required for subscriptions)

- **Privacy Policy URL:** host `docs/PRIVACY.md` somewhere **public**, put the URL in App Store Connect ▸ App Privacy, and change `LiveMode.PrivacyPolicyUrl` to match. It currently points to the file in the GitHub repo, and that link only works if the repo is public.
- **Terms of Use:** the paywall links to Apple's standard licence agreement (EULA). To use it, nothing else is needed. To use your own terms, add them in App Store Connect ▸ App Information ▸ License Agreement and change `LiveMode.TermsOfUseUrl`.
- The paywall shows the title, length, the price from the App Store, auto-renew terms, Restore Purchases, and both links, as App Review guideline 3.1.2 asks.

## 5. Testing before release

1. App Store Connect ▸ **Users and Access ▸ Sandbox ▸ Test Accounts**: create a sandbox tester (it needs an email address that isn't a real Apple Account).
2. Install a TestFlight or Xcode build on an iPhone. In **Settings ▸ App Store ▸ Sandbox Account**, sign in with the tester.
3. PLAY ▸ LIVE ▸ SUBSCRIBE. In sandbox, a month renews every 5 minutes, up to 12 times, and then the subscription expires. That lets you watch it lapse.
4. For a Live game you need **two devices with two different Game Center accounts**, both subscribed (two sandbox testers), both on the same build. TestFlight builds match each other.
5. Check: RESTORE PURCHASES on a reinstall; Ask to Buy shows "Waiting for approval"; cancel in Settings ▸ Subscriptions, and Live locks again when the period ends.

## How a Live game works

- Each player picks a league team. Game Center pairs players in the same **player group**, which is worked out from the game version, so both always run identical code.
- The player whose Game Center ID sorts first hosts. The host's team is home and the host's home court is used. AI teammates play at Caller difficulty.
- The game uses the same lockstep as two-phone play, with an 8-step (133 ms) input delay for internet latency. Only controller input travels between the phones, and checksums catch any mismatch.
- **Rating:** Elo starting at 1000 (K 32). Tiers are ROOKIE, HOOPER (900), STARTER (1100), ALL-STAR (1300) and LEGEND (1500). Leaving after the first 20 seconds counts as a loss. If your opponent leaves, you win. A dropped connection or a mismatch counts for nobody.
- **Without the server:** ratings live on each player's phone, so a determined cheater could edit their own number.
- **With the server** (`server/README.md`, Phase 28): sign-in is checked with Game Center's signature, and the subscription is checked with Apple's App Store Server API. Ratings change only on the server, and only when both players' reports agree.

## App Review notes for the subscription

> Retro Hoops Live (auto-renewing monthly subscription) unlocks online head-to-head games through Game Center matchmaking. To review: sign in to Game Center, open PLAY ▸ LIVE, subscribe with the sandbox account, then FIND A GAME. A match needs a second device on the same build with a different Game Center account. Without one, the search keeps looking and BACK stops it. The rest of the game is free and offline.

## Signed match requests (Phase 32)

`/v1/match/start` and `/v1/match/result` bodies must carry `t` (ms since 1970) and `tag` (HMAC-SHA256, key = the
match key, message = `"{t}|start|{matchKey}|{opponentId}|{seat}"` or
`"{t}|result|{matchKey}|{scoreA}|{scoreB}|{hash}|{outcome}"`, lowercase hex). The server answers 401 for a missing
or wrong tag, or a `t` more than 5 minutes from its clock. The game does this in `Backend.StartBody/ResultBody`
(`server/src/sign.ts` on the server side). Phones need a roughly correct clock (automatic time is the iOS default).

