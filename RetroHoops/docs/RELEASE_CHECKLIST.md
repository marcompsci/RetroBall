# Release checklist (iOS)

Work through this in order. Items marked ✅ are already handled in the project. ☐ items need you, because they need Unity, Xcode, a device, or your Apple account. Nothing on this list has been run yet.

## 1. Project
- ☐ Open the project in Unity 6 LTS (with iOS Build Support) and fix any compile errors.
- ☐ Run **Retro Hoops ▸ Run Project Setup**. Scenes, TMP, and content assets get created.
- ☐ All EditMode and PlayMode tests are green in the Test Runner.
- ✅ Logic tests pass on .NET: `cd tools/LogicTests && dotnet run --project Runner/Runner.csproj`

## 2. Identity and settings
- ✅ Bundle ID `com.phoronomicstudios.retrohoops`.
- ☐ Set your **Signing Team ID** in Player Settings, or choose the team in Xcode later.
- ✅ **Retro Hoops ▸ Release ▸ Apply Release Player Settings:** version 1.0.0, build 1, iOS 15+, iPhone + iPad (Macs with Apple silicon run it as Designed for iPad), portrait, full screen, status bar hidden, Unity splash off, development build off.
- ✅ `ship_testflight.sh` (Retro Hoops ▸ Release ▸ Build iOS (App Store)) raises the build number for every upload.

## 3. Icon and launch screen
- ✅ **Retro Hoops ▸ Release ▸ Generate App Icon and Launch Image** writes `Assets/Art/AppIcon/AppIcon1024.png` (opaque, 1024²) and `LaunchImage.png` (1080×1920), then assigns them.
- ☐ Check **Player Settings ▸ iOS ▸ Icon** and **Splash Image ▸ Launch Screen** to confirm they're assigned. Unity's launch-screen settings vary between versions.
- ☐ On a device, confirm the launch screen hands off cleanly to the main menu.

## 4. Build
- ✅ **Retro Hoops ▸ Release ▸ Check iOS Readiness**, plus every build result, is also written to `Logs/RetroHoops-release.txt` in the project, so you can share it or Claude can read it.
- ☐ **Simulator:** **Retro Hoops ▸ Release ▸ Build iOS (Simulator)**, or `tools/build_ios.sh` with the Editor closed. Open `iOSBuild/Simulator/Unity-iPhone.xcodeproj`, pick an iPhone simulator, and press Run.
- ☐ **Device:** **Build iOS (Device)**, or `tools/build_ios.sh device`. In Xcode, go to **Signing & Capabilities**, choose your team, plug in your iPhone, and press Run.
- ✅ The post-processor sets the Info.plist keys: `ITSAppUsesNonExemptEncryption = NO`, `UIRequiresFullScreen`, `UIStatusBarHidden`, and the Sports Games category.
- ✅ `PrivacyInfo.xcprivacy` sits in `Assets/Plugins/iOS`. ☐ Confirm it appears in the Xcode project. If it doesn't, drag it into the Unity-iPhone target with "Copy items" ticked.

## 5. Test on a device
- ☐ Run all of `docs/QA_CHECKLIST.md` on at least one small and one large iPhone.
- ☐ Watch Xcode's memory and frame-rate gauges over 10 games.
- ☐ Fresh install, upgrade install (build N → N+1, so saves survive), and delete-and-reinstall (the save is gone).

## 6. App Store Connect
- ☐ Create the app record using `docs/APP_STORE.md` (name, subtitle, categories, description, keywords).
- ☐ Host the privacy policy and a support page, then paste the URLs.
- ☐ App Privacy: **Data Not Collected**.
- ☐ Age rating questionnaire: all **None** / **No**.
- ☐ Upload screenshots (6.9" iPhone set **and** 13" iPad set, from **Retro Hoops ▸ Release ▸ Capture Store Screenshot**; see `docs/LAUNCH_KIT.md`).
- ☐ Upload with `bash ~/RetroHoops-push/tools/ship_testflight.sh` (build number goes up automatically). Full walkthrough: `docs/SUBMISSION.md`.
- ☐ Install the build through TestFlight and play a full Rise season on it.
- ☐ Submit for review with the review notes from `docs/APP_STORE.md`.

## 7. Legal sanity pass (before submitting)
- ✅ All names are original: no real leagues, teams, players, arenas, or brands.
- ✅ All art and audio are generated in code. The font is OFL (Liberation Sans).
- ☐ Search the App Store for "Retro Hoops" to confirm the name isn't taken.
