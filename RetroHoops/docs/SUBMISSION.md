# Shipping Retro Hoops: TestFlight, then the App Store

You need the paid Apple Developer Program (done) and Xcode signed in to it (**Xcode ▸ Settings ▸ Accounts**). The steps below go in order. Steps 1, 2 and 6 happen on the App Store Connect website ([appstoreconnect.apple.com](https://appstoreconnect.apple.com)). Step 3 is one command on your Mac.

Status: the upload script and the App Store build have **not been run yet**. Treat this as a first draft until the first upload goes through.

## 1. Create the app record (once)

App Store Connect ▸ **Apps ▸ ＋ ▸ New App**:

| Field | Value |
|---|---|
| Platform | iOS |
| Name | **Retro Hoops** (the name on the App Store record; "RetroBall" was already taken). |
| Primary language | English (U.S.) |
| Bundle ID | `com.phoronomicstudios.retroball`. Register it first at developer.apple.com ▸ Certificates, Identifiers & Profiles ▸ Identifiers ▸ ＋ ▸ App IDs ▸ App (Explicit; tick Game Center and iCloud). The old `com.marcompsci.retroball` belongs to the free personal team and doesn't appear in the list. |
| SKU | `retroball-ios-1` |
| User access | Full access |

Also open **Business** (formerly Agreements, Tax and Banking) and finish anything marked as pending. A free app only needs the Free Apps agreement, which is accepted when you enroll. If you distribute in the EU, App Store Connect asks you to declare **trader status** (Digital Services Act) before the app can go live there.

## 2. Game Center leaderboards and achievements (once)

Follow `docs/GAME_CENTER.md`. Create every ID in the tables exactly as written. This is optional for TestFlight, but required before the review if you want them live at launch.

## 3. Upload a build (every time)

Quit Unity, then in Terminal:

```bash
bash ~/RetroHoops-push/tools/ship_testflight.sh
```

What it does:

1. Unity builds a Release Xcode project in `iOSBuild/AppStore` and **raises the build number by one**. Apple rejects a build number it has already seen.
2. `xcodebuild` archives it, signed automatically with your team.
3. `xcodebuild` uploads it to App Store Connect.

Every result is appended to `Logs/RetroHoops-release.txt`. The detailed logs are `Logs/unity_build_appstore.log`, `Logs/xcodebuild_archive.log` and `Logs/xcodebuild_export.log`.

- **To pick the team:** run `TEAM=YOURTEAMID bash ...`. The team ID is at developer.apple.com ▸ Account ▸ Membership details. Without it, the script uses Unity's Signing Team ID, or the team you picked last time in Xcode.
- **To make the `.ipa` without uploading:** add `--export-only`.
- **To do it by hand instead:** open `iOSBuild/AppStore/Unity-iPhone.xcodeproj`, choose **Product ▸ Archive**, then **Distribute App ▸ App Store Connect**.

## 4. TestFlight

1. After processing (usually 10–30 minutes, and you get an email), the build appears under **TestFlight**.
2. **Internal testing** (you, plus up to 100 people on your team): create a group, add yourself, and install the **TestFlight** app on your iPhone. There's no review for internal testers.
3. **External testing** (friends, up to 10,000 people by email or public link): the first build needs a short Beta App Review, usually about a day.
4. Play through `docs/QA_CHECKLIST.md` on the TestFlight build. It's a Release build, so cheats and dev keys are stripped.

## 5. Store page (App Store tab ▸ version 1.0)

| Item | What to enter |
|---|---|
| Screenshots | **6.9" iPhone** set: 1320×2868, 1290×2796 or 1260×2736 portrait. Use 3–10 from **Retro Hoops ▸ Release ▸ Capture Store Screenshot** (see `LAUNCH_KIT.md`). The app is iPhone-only, so no iPad screenshots are needed. |
| Promotional text, description, keywords, subtitle | Copy from `docs/APP_STORE.md`, then add the Spanish (Mexico) localization from the same file. |
| Support URL and Privacy Policy URL | **You** host these. A simple page is fine, and `docs/APP_STORE.md` has a privacy policy draft. |
| Category | Games ▸ Sports (secondary: Games ▸ Arcade) |
| Copyright | `2026 <your name>` |
| Price | Free (or a paid tier). There are no in-app purchases. |

### Age rating (App Information ▸ Age Rating ▸ Edit)

Apple's questionnaire was expanded in 2025 and again in 2026 with a social-media question. For Retro Hoops, answer **None** or **No** to everything:

- No violence, mature themes, gambling, contests, horror, profanity, alcohol or drug references, or medical content.
- No user-generated content, messaging or chat, or unrestricted web access.
- No advertising.
- **Not** a social media app.
- The parental-controls and age-assurance questions are **No**, since the game has none and needs none.

The expected result is **4+**.

### App Privacy

Answer **Data Not Collected**:

- Saves stay on the device and in the player's own iCloud.
- Game Center is run by Apple.
- There's no analytics, no ads and no tracking.

`PrivacyInfo.xcprivacy` is in the build.

### Export compliance

Already answered in the build: `ITSAppUsesNonExemptEncryption = NO` is set in Info.plist, so App Store Connect won't ask.

## 6. Submit for review

1. On the version page, under **Build**, choose the TestFlight build you tested.
2. Add the review notes from `docs/APP_STORE.md`. No login is needed, and the game is fully offline.
3. Choose **Manually release this version**, so you pick the launch day.
4. Click **Add for Review**, then **Submit**.

Review usually takes 1–2 days. If Apple rejects it, the message says why. Fix it, run step 3 again (it gets a new build number), select the new build and resubmit.

## Common problems

| Message | Fix |
|---|---|
| "No suitable application records were found" | Step 1 isn't done, or the bundle ID differs. |
| "The bundle version must be higher than the previously uploaded version" | Run the script again; it raises the build number. |
| "No Account for Team" / "No signing certificate" | Xcode ▸ Settings ▸ Accounts: select your Apple ID and check that your paid team (not only "Personal Team") is listed. Then click **Manage Certificates ▸ ＋ ▸ Apple Distribution**. |
| Missing iCloud or Game Center capability | Xcode adds both automatically with `-allowProvisioningUpdates`. If the error persists, open Certificates, Identifiers & Profiles ▸ Identifiers ▸ com.marcompsci.retroball and tick **iCloud** and **Game Center**. |
