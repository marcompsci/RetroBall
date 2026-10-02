# Caller Retro Ball

*Call your shot. Build your legacy.*

An original, offline, portrait-first retro arcade basketball game for iPhone, built in Unity. It plays 3v3 on a half court with touch controls and a skill-based shot meter, and has a light season and progression loop. All teams, players, courts, logos, and art are original to this project.

> **Status: Phase 3 of 7.** Menus, league content, and playable 3v3 games: shoot with a timing meter, pass and call for the ball, score, beat the game and shot clocks, and play against AI at three difficulty levels. Steals, blocks, box-outs, and the post-game screen with rewards arrive in Phase 4. See [`docs/PLAN.md`](docs/PLAN.md).

## Requirements

| | |
|---|---|
| Unity | **Unity 6 LTS** (6000.0 or newer 6.x LTS) with the **iOS Build Support** module |
| Packages | Declared in `Packages/manifest.json`: URP 17, Input System 1.11, uGUI 2.0 (includes TextMeshPro), Test Framework 1.4, 2D Sprite. Unity may upgrade these to the versions that match your editor. |
| iOS builds | macOS + Xcode (current release) + an Apple Developer account for device builds |

Cinemachine is not used. A small custom camera fits a single-hoop half court better.

## First-time setup

1. In **Unity Hub**, choose **Add ▸ Add project from disk** and select this `CallerRetroBall` folder. Open it with Unity 6 LTS.
2. If the Input System asks whether to enable the new input backend, choose **Yes**. The editor will restart.
3. On first open, the editor runs **Caller Retro Ball ▸ Run Project Setup** by itself. You can also run it from the menu at any time. It:
   - creates the six scenes in `Assets/Scenes` and adds them to Build Settings, with Boot first;
   - imports the TextMeshPro Essential Resources;
   - applies portrait-only, iOS 15+ Player Settings;
   - writes the default league content to editable ScriptableObjects in `Assets/Resources/Data/…`;
   - validates the content and logs a report.
4. **URP 2D (manual, about 1 minute):** go to **Assets ▸ Create ▸ Rendering ▸ URP Asset (with 2D Renderer)** and save it to `Assets/Settings`. Then assign it in **Project Settings ▸ Graphics** and **Project Settings ▸ Quality**. The game also runs without this step; URP is needed for later lighting and effects.
5. Open `Assets/Scenes/BootScene.unity` and press **Play**. For a phone-shaped preview, set the Game view to an iPhone portrait resolution such as 1179×2556.

### Menu commands

- **Run Project Setup:** safe to run again. It never overwrites existing scenes or content.
- **Rebuild Scenes (overwrite):** regenerates the six scenes.
- **Regenerate Content Assets (overwrite):** resets every content asset to the built-in defaults.
- **Validate Content:** checks for duplicate IDs, out-of-range ratings, broken references, unfair difficulty settings, and colour clashes.

## Running tests

**In Unity:** open **Window ▸ General ▸ Test Runner**.
- **EditMode** covers the logic tests (content, validator, scoring, colours, pixel art, determinism) plus project-structure checks.
- **PlayMode** covers the boot flow and scene-transition guard.

**Without Unity (logic only):** the engine-free logic and its tests also compile and run on .NET 8:

```bash
cd tools/LogicTests
dotnet run --project Runner/Runner.csproj
```

This builds `Assets/Scripts/Logic` as .NET Standard 2.1 / C# 9, the same API level Unity uses, with warnings treated as errors. It then runs every test in `Assets/Tests/EditMode/Logic` through a small NUnit-compatible shim.

`tools/SyntaxCheck` parses every C# file under `Assets/` and reports syntax errors. It doesn't resolve Unity APIs, so it is not a substitute for compiling in Unity.

## Controls (Phase 3)

| Action | Touch | Keyboard / gamepad (Editor, controllers) |
|---|---|---|
| Move | Floating thumbstick: touch anywhere in the lower-left area | WASD / arrow keys / left stick |
| Shoot: hold to fill the meter, release in the green | SHOOT button | K (hold) / gamepad A |
| Pass (aim with the stick), or ASK for the ball when a teammate has it | PASS / ASK button | J / gamepad X |
| Defense (steal / contest) | DEF button | L |
| Call a play | CALL button | — |
| Pause | II button (top-left) | Esc |
| Debug: knock the ball loose | — | B (Editor and development builds only) |

You always control your own player. After a steal or defensive rebound, SHOOT reads CLEAR until you take the ball back beyond the arc. DEF and CALL are dimmed until Phase 4 and flash "SOON" when pressed. Leaving the app, or switching away from it, pauses the match.

## iOS build (outline; fully documented in Phase 7)

1. Go to **File ▸ Build Profiles** (or **Build Settings**) ▸ **iOS** ▸ **Switch Platform**.
2. In **Player Settings**, replace the placeholder bundle ID `com.callerretroball.game` with your own, and set your Team ID.
3. Build to a folder such as `iOSBuild/`, open the `.xcodeproj` in Xcode, sign it, and run it on a device.

## Project architecture

```
Assets/Scripts/
  Logic/        Engine-free C# (noEngineReferences): content definitions, default league
                content, validator, scoring rules, deterministic RNG, procedural pixel art.
                Unit-tested inside and outside Unity.
  Data/         ScriptableObject wrappers around Logic definitions + ContentDatabase loader
                (falls back to built-in defaults if assets are missing).
  Core/         App service hub, SceneFlow (guarded fade transitions), BootController.
  UI/           Code-built uGUI/TextMeshPro kit, safe-area fitter, theme, menu screens.
  Gameplay/     Match controller, views (players, ball, court), camera rig, HUD + pause.
  Input/        Touch controls: floating joystick and hold/release action buttons.
  AI/ Progression/ Save/ Audio/   Filled in Phases 3–6.
  Editor/       ProjectSetup: scenes, build settings, player settings, content assets.
Assets/Tests/EditMode/Logic   Engine-free tests (also run by tools/LogicTests)
Assets/Tests/EditMode/Unity   Editor-only checks
Assets/Tests/PlayMode         Boot, scene-flow, and match start-up tests
```

**Key decisions**

- **Logic is split from the engine.** Rules and content live in a separate assembly with no Unity dependency, so they stay deterministic and can be tested anywhere. Gameplay randomness uses `SeededRandom`, never `UnityEngine.Random`.
- **Every screen's UI is built in code** from `UiKit`. That gives one consistent look and no fragile prefabs.
- **No hidden AI boosts.** Difficulty changes only reaction time, decision quality, and error rate. The validator rejects any AI movement multiplier above 1.0.
- **Treat content definitions as read-only at runtime.** In the Editor they are live ScriptableObject data, so changing one would change the asset.

## Known limitations (Phase 3)

- You can't steal, contest, block, or box out yet (Phase 4); AI defenders still contest shots by position.
- The final card shows only the score; the box score, player of the game, and rewards screen arrive in Phase 4.
- CALL (play calling) arrives in Phase 4.
- Settings, save data, Locker Room editing, and Rise Mode progression are still placeholders.
- The URP asset has to be assigned by hand (setup step 4).
- The Unity-side scripts were syntax-checked but haven't yet been compiled inside the Unity Editor. Please report any compile errors from your first open.

## Asset and licence disclosure

- **Art:** every sprite, logo, and backdrop is generated procedurally at runtime from code in `Assets/Scripts/Logic/PixelArt`. No external images are used.
- **Font:** Liberation Sans, the TextMeshPro default font. It ships with Unity's TextMeshPro and uses the SIL Open Font License 1.1.
- **Audio:** none yet. Any placeholder tones added later will be generated in-project.
- **Names and branding:** all league, team, player, court, and event names are fictional and original. There are no real leagues, teams, players, brands, or likenesses.
- **Privacy:** no ads, analytics, tracking, accounts, network access, or in-app purchases.
