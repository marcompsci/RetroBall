# App Store requirements: where Retro Hoops stands (Phase 40)

This is the checklist for App Review. **Done in code** items are checked by `tools/AppStoreAudit/app_store_audit.py` (35 checks, all passing on 7 Oct 2026) or by tests. **You** items happen in App Store Connect or on the Mac and can only be done by Omari (sign-in, agreements, bank, tax).

## Done in code / in the repo

| Requirement (App Review Guideline) | Where |
|---|---|
| Privacy policy reachable in the app and in metadata (5.1.1) | `docs/PRIVACY.md` (public on GitHub); Settings ► PRIVACY POLICY and LIVE screen |
| Support page with a way to contact you (1.5) | `docs/SUPPORT.md` (FAQ + rodlonbell16@gmail.com); Settings ► HELP & SUPPORT |
| Privacy manifest (required-reason APIs, collected data) | `Assets/Plugins/iOS/PrivacyInfo.xcprivacy` (System boot time 35F9.1 for frame timing; User ID + Gameplay Content for Live; no tracking. Unity ships its own manifest for the engine) |
| Account / data deletion (5.1.1(v)) | No accounts. LIVE ► DELETE MY LIVE DATA erases server data |
| Subscription paywall shows price, length, auto-renew terms, Terms of Use (EULA) and privacy links, RESTORE PURCHASES (3.1.2) | `MainMenuController.Live.cs`; checked by the audit |
| Everything except Live works without the subscription and offline (3.1.2(a)) | By design; Live is the only purchase |
| No debug or DEV menus in release builds (2.3.1) | DEV tools are compiled only in development builds (audit) |
| Export compliance | `ITSAppUsesNonExemptEncryption = NO` set by the build post-processor |
| No real leagues, teams, players or brands (5.2) | Original content; the audit scans names and metadata (Phase 40 renamed four Legacy names that matched real players) |
| Metadata fits App Store limits (name 30, subtitle 30, keywords 100, promo 170) | `APP_STORE.md`; checked by the audit |
| App icon (1024, no alpha) and launch screen | Icon tests + build settings |
| Screenshots at the required sizes | `tools/App Store Screenshots.command`: iPhone 6.9" 1320×2868 and iPad 13" 2064×2752, 7 each |
| Stability (2.1) | Phase 40 release tests: damaged saves load, every mode survives random play, Full Court step < 0.5 ms |
| Accessibility basics | VoiceOver labels, left-handed controls, text size, reduce flashing (earlier phases) |

## You (App Store Connect, Mac)

1. **Agreements, Tax and Banking:** accept the Paid Apps agreement and add bank + tax info. Without it the Live subscription can't be sold or even reviewed. *(Claude can't do this.)*
2. **Upload a build:** run `tools/Ship Retro Hoops.command` on the Mac (build 3). Then wait for it to finish processing and add it to TestFlight.
3. **In-App Purchase:** finish the Retro Hoops Live subscription (group, price, localized name/description, review screenshot of the paywall, review notes) and attach it to the first app version for review.
4. **Game Center:** enable it on the version and create the leaderboards `retrohoops.lb.clutch`, `retrohoops.lb.gauntlet` and the recurring `retrohoops.live.monthly` (Claude will do this once you're signed in in the browser pane).
5. **App Privacy:** answer as in `APP_STORE.md` ► App Privacy (server on: User ID + Gameplay Content, linked, not tracking, App Functionality).
6. **Age rating:** answer as in `APP_STORE.md` ► Age rating (expected 4+).
7. **Metadata:** paste name, subtitle, description, keywords, What's New, Support URL and Privacy Policy URL from `APP_STORE.md`; upload the screenshots from `~/RetroHoops/AppStoreScreenshots`.
8. **Review notes:** explain Live (needs two Game Center players; offer a sandbox tester note) and that everything else is offline. Template in `SUBMISSION.md`.
9. **Live server:** if Live should launch with server ratings, deploy `server/` first (needs your Cloudflare account) and set `BackendConfig.Url`; otherwise ship with ratings kept on each phone and remove the two collected-data entries from the privacy manifest (see `APP_STORE.md`).
10. **Device test:** play the TestFlight build on a real iPhone and iPad and work through `QA_CHECKLIST.md` before you press Submit for Review.

## Not verified yet

- Phases 39–40 haven't been compiled in Unity/Xcode on the Mac or run in the Simulator.
- Nothing has been uploaded to App Store Connect; App Review may still raise things these checks can't see.
