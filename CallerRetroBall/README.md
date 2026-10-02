# Caller Retro Ball

*Call your shot. Build your legacy.*

An original, offline, portrait-first retro arcade basketball game for iPhone, built in Unity. It plays 3v3 on a half court with touch controls, a skill-based shot meter, real defense, play calling, and a season-and-progression loop. All teams, players, courts, logos, art, and audio are original to this project and generated in code.

> **Status: all seven phases implemented; not yet compiled in the Unity Editor.** The engine-free game logic (rules, AI, shot and pass models, defense, progression, save, season, Rise Mode, drills, audio synthesis) compiles on .NET and passes 192 automated tests. The Unity-side scripts pass a syntax check and a name-resolution check, but nobody has opened the project in Unity, built it for iOS, or played it on a device yet. Expect a few compile fixes on first open. See [Known limitations](#known-limitations).

## Requirements

| | |
|---|---|
| Unity | **Unity 6 LTS** (6000.0 or newer 6.x LTS) with the **iOS Build Support** module |
| Packages | Declared in `Packages/manifest.json`: URP 17, Input System 1.11, uGUI 2.0 (includes TextMeshPro), Test Framework 1.4, 2D Sprite. Unity may upgrade these to match your editor. |
| iOS builds | macOS, current Xcode, and an Apple Developer account for device builds |

## First-time setup

1. In **Unity Hub**, choose **Add ▸ Add project from disk**, select this `CallerRetroBall` folder, and open it with Unity 6 LTS.
2. If the Input System asks to enable the new input backend, choose **Yes**. The editor restarts.
3. On first open the editor runs **Caller Retro Ball ▸ Run Project Setup** automatically (you can rerun it from the menu). It:
   - creates the six scenes in `Assets/Scenes` and adds them to Build Settings, Boot first;
   - imports TextMeshPro Essential Resources;
   - applies portrait-only, iOS 15+ Player Settings;
   - writes default league content to editable ScriptableObjects in `Assets/Resources/Data/…`;
   - validates the content and logs a report.
4. **URP 2D (manual, about a minute):** **Assets ▸ Create ▸ Rendering ▸ URP Asset (with 2D Renderer)**, save it to `Assets/Settings`, then assign it in **Project Settings ▸ Graphics** and **▸ Quality**. The game also runs on the built-in pipeline.
5. Open `Assets/Scenes/BootScene.unity` and press **Play**. For a phone-shaped preview, set the Game view to an iPhone portrait resolution such as 1179×2556.

**Menu commands** (Caller Retro Ball menu): Run Project Setup (safe to rerun), Rebuild Scenes (overwrite), Regenerate Content Assets (overwrite), Validate Content.

## Modes

- **Quick Call (PLAY):** pick one of your two unlocked teams, cycle the opponent, choose difficulty, tip off. Pays half rewards.
- **Rise Mode:** play as the First Callers. Beat three street crews in The Blacktop Circuit, join The Caller League for a 10-game season (event cards between games, energy and chemistry), then a four-team bracket for The Gold Signal Cup. Next season starts from the hub.
- **Practice Lab:** Free Shoot (60 s), Passing Targets (45 s), Dribble Lane (5 cones, timed). Personal bests are saved. No rewards.
- **Locker Room:** nickname, trained ratings, upgrades (Signal Points + a game played between purchases), cosmetics shop and equip, career stats and practice bests.
- **Settings:** music and SFX volume, haptics, screen shake, UI scale, colourblind team patterns, difficulty, reset save (with confirmation), credits and licences.

## Controls

| Action | Touch | Keyboard / gamepad (Editor, controllers) |
|---|---|---|
| Move | Floating thumbstick anywhere in the lower-left area | WASD / arrows / left stick |
| Shoot: hold to fill the meter, release in the green | SHOOT | K (hold) / A |
| Pass (aim with the stick); ASK when a teammate has the ball | PASS / ASK | J / X |
| Call a play: Pick & Roll, Give & Go, Clear Out | CALL (offense, your team has the ball) | C / Y |
| Steal | STEAL (on defense) | L / B |
| Jump to contest or block | JUMP (the SHOOT button on defense) | K / A |
| Switch onto the ball handler | SWITCH (the PASS button on defense) | J / X |
| Pause | II (top-left) | Esc |
| Debug: knock the ball loose | — | B key (Editor and development builds only) |

After a steal or defensive rebound, SHOOT reads CLEAR until you take the ball back beyond the arc. Leaving or switching away from the app pauses the match.

## Running tests

**In Unity:** **Window ▸ General ▸ Test Runner**.
- **EditMode:** all logic tests plus project-structure checks.
- **PlayMode:** boot flow, scene transitions, match start-up, every menu scene building without errors, and a practice drill start.

**Without Unity (logic only):**

```bash
cd ../tools/LogicTests
dotnet run --project Runner/Runner.csproj
```

This compiles `Assets/Scripts/Logic` as .NET Standard 2.1 / C# 9 (Unity's API level) with warnings as errors, then runs every test in `Assets/Tests/EditMode/Logic` through a small NUnit-compatible shim. Current result: **192 passed, 0 failed**.

`tools/SyntaxCheck` parses every C# file under `Assets/`. It does not resolve Unity APIs, so it is not a substitute for compiling in Unity.

## iOS build

1. **File ▸ Build Profiles ▸ iOS ▸ Switch Platform.**
2. **Player Settings:** replace the placeholder bundle ID `com.callerretroball.game` with your own, set your Team ID, and check Version (`1.0.0`) and Build number. Orientation is portrait only; minimum iOS is 15.
3. **Build** into a folder such as `iOSBuild/`. Open `Unity-iPhone.xcodeproj` in Xcode.
4. In Xcode, select the **Unity-iPhone** target ▸ **Signing & Capabilities**, choose your team, and let Xcode manage signing.
5. **Simulator:** in Player Settings set **Target SDK ▸ Simulator SDK** before building, then pick an iPhone simulator in Xcode and Run. **Device:** use Device SDK, plug in the phone, and Run.
6. The haptics plugin (`Assets/Plugins/iOS/CallerHaptics.mm`) is compiled by Xcode automatically. It only uses UIKit feedback generators.
7. Before submitting: add your own app icon and launch screen, fill in App Store Connect privacy answers (the game collects no data), and test on a real device. None of this has been done yet.

## Project architecture

```
Assets/Scripts/
  Logic/         Engine-free C# (noEngineReferences), unit-tested inside and outside Unity:
    Definitions/ Content/     data model, default league content, validator
    Rules/ Math/ Court/       scoring, deterministic RNG, geometry
    Match/                    MatchSimulation (partial: core, defense & plays), AiBrain, shot/pass models, stats
    Progression/              rewards, career, season & playoffs, Rise Mode, practice drills
    Save/                     MiniJson + versioned SaveCodec
    PixelArt/ Audio/          procedural sprites, logos, courts, sounds, music
  Data/          ScriptableObject wrappers + ContentDatabase (falls back to built-in defaults)
  Core/          App hub, SceneFlow, SaveStore (atomic writes + backups), Haptics, Game Center interface
  UI/            Code-built uGUI/TMP kit, controls, menu screens (main, Rise hub, Locker Room, Settings)
  Gameplay/      Match controller, views, camera rig, HUD, post-game
  Input/         Floating joystick and hold/release action buttons
  Audio/         AudioManager (pooled voices, synthesised clips)
  Editor/        ProjectSetup
Assets/Plugins/iOS/   CallerHaptics.mm
Assets/Tests/         EditMode/Logic, EditMode/Unity, PlayMode
```

**Key decisions**

- **Logic is separate from the engine.** Rules, AI, and progression are deterministic, seeded, and testable anywhere. Gameplay never uses `UnityEngine.Random`.
- **All UI is built in code** from `UiKit`, so there are no fragile prefabs and one consistent look.
- **No hidden AI boosts.** Difficulty changes only reaction time, decision quality, shot selection, release accuracy, and error rate. The validator rejects anything else.
- **Rewards are applied once.** Every match has an id; `Career.ApplyMatch` ignores ids it has already seen, so a rematch, scene reload, or double tap can't grant twice.
- **Save is local and forgiving.** Versioned JSON in `persistentDataPath/career.json`, written via temp file + move. A corrupt file is backed up and replaced with a fresh career, and the player is told.
- **Game Center** sits behind `IGameCenterService`. Only a no-op implementation ships.

## Known limitations

- **Not compiled in Unity yet.** The Unity layer has passed a syntax check and a name-resolution check only. No Editor run, iOS build, simulator run, or device test has happened.
- Not tuned by hand: balance numbers come from AI-vs-AI simulations (AI field-goal rate about 29 % Rookie, 38 % Caller, 56 % Legend), not from people playing.
- Celebrations and dribble moves are collectible tags with no animation yet.
- The URP asset must be assigned by hand (setup step 4).
- No app icon, launch screen art, or localisation.
- No Game Center implementation (interface only), by design for the offline MVP.

## Asset and licence disclosure

- **Art:** every sprite, logo, court, and backdrop is generated procedurally from code in `Assets/Scripts/Logic/PixelArt`. No external images.
- **Audio:** every sound effect and the music loop are synthesised at runtime by `AudioSynth`. No recordings or samples.
- **Font:** Liberation Sans, TextMeshPro's default font, SIL Open Font License 1.1.
- **Code:** all original. The iOS haptics plugin is original and uses only Apple's public UIKit APIs.
- **Names:** every league, team, player, court, and event name is fictional and original. No real leagues, teams, players, brands, arenas, or likenesses.
- **Privacy:** no ads, analytics, tracking, accounts, network access, or in-app purchases. Progress stays on the device.
