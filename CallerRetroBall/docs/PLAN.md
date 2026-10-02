# Implementation plan & phase log

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit, architecture, data model, default content, validator, scene skeleton, boot flow, menu shell | **Done** (logic tests pass in .NET; Unity compile pending first open) |
| 2 | Court rendering, player sprites, movement, ball possession, camera, joystick + action buttons | **Done** (99 logic tests pass in .NET; Unity compile pending first open) |
| 3 | Passing, shot meter + shot model, scoring, game/shot clocks, possession, basic AI | **Done** (133 logic tests pass in .NET, incl. full AI-vs-AI games at every difficulty; Unity compile pending first open) |
| 4 | Defense (steal/contest/block), rebounds + box-out, match state machine, post-game stats & rewards | **Done** (logic tested; Unity compile pending) |
| 5 | Quick Call team select, Practice Lab drills, Rise Mode season/standings/bracket, versioned save | **Done** (logic tested; Unity compile pending) |
| 6 | Locker Room, upgrades, cosmetics, Settings (audio, haptics, shake, UI scale, colourblind, difficulty, reset), audio hooks | **Done** (logic tested; Unity compile pending) |
| 7 | Test pass, performance cleanup, full README/DESIGN, manual QA checklist, final report | **Done** (192 logic tests pass; QA checklist written but not yet run) |
| 9 | Polish: celebrations, dribble moves, pixel bursts, dunk SLAM + shake, crowd ambience, score bounce, reward count-up | **Done** (207 logic tests pass; not yet seen running) |
| 8 | Release prep: app icon, launch image, release settings, iOS build tooling, Info.plist, privacy manifest, App Store kit | **Done** (198 logic tests pass; icon previewed; no build run yet) |

## Phase 1 — what exists

- **Logic (engine-free):** `AttributeSet`/`RatingScale` (1–99), `RgbColor`, all definitions
  (Archetype, Player, Team, Court, GameRules, Difficulty, Upgrade, Cosmetic, SeasonConfig),
  `DefaultContent` (12 archetypes, 8 league teams × 4 players, 3 Blacktop crews, player crew with Rook,
  12 courts, 2 rule sets, 3 difficulties, 8 upgrades, 16 cosmetics, 1 season), `ContentValidator`,
  `Scoring` (1/2-point zones, target score, clock, sudden death, win-by-two), `MatchRequest`,
  `StableHash`/`SeededRandom`, `PixelCanvas`, `LogoGenerator`, `BackdropGenerator`.
- **Unity layer:** ScriptableObject wrappers + `ContentDatabase`; `App`, `SceneFlow` (fade + input
  blocker + double-load guard), `BootController`; `UiKit`, `Theme`, `SafeAreaFitter`, `ScreenBase`;
  Main Menu, Game (match preview), Rise Mode (league preview), Locker Room (Rook card), Settings (credits).
- **Editor:** `ProjectSetup` (auto on first open + menu).
- **Tests:** 42 logic tests (EditMode/Logic), 3 editor checks (EditMode/Unity), 2 PlayMode tests.

## Phase 2 — what exists

- **Logic:** `Vec2`; `CourtGeometry` (15 x 11 m half court, hoop, arc + straight corners, paint,
  check spot, 1/2-point zone test, bounds clamp); `Movement` (rating-based top speed, dribble
  penalty, accel/brake, 8-way facing, arrive steering, body separation); `BallState`/`BallPhysics`
  (dribble curve, loose-ball gravity/bounce/friction, pickup rule); `Formation` (spacing spots,
  guard-between-man-and-hoop); `MatchSimulation` (6 players, check-ball setup, fixed-step update,
  possession + control switching, loose-ball pickups, frozen clocks until Phase 3, event list);
  `InputBuffer`, `JoystickMath`, `CameraMath` (follow, clamp, integer pixel zoom).
- **Art (procedural):** `CourtGenerator` (asphalt or hardwood floor, paint, lines, crowd strip),
  `CharacterSpriteGenerator` (16x24 sheets: front/back/side x idle 2 + run 4, 6 hair styles,
  3 builds, 3 heights, 6 skin tones, 1px outline), `PropSpriteGenerator` (ball, shadow, ring, hoop).
- **Unity:** `GameSceneController` (60 Hz fixed step, keyboard/gamepad fallback, pause on
  background), `MatchArt`, `PlayerView`, `BallView`, `CourtCameraRig`, `CourtSpace`, `MatchHud`
  (scores, clocks, possession highlight, pause menu, toasts), `TouchControls` + `VirtualJoystick`
  + `TouchActionButton` (floating stick, hold/release buttons, "SOON" feedback for actions that
  arrive in Phases 3–4).
- **Tests:** +57 logic tests (Phase2Tests.cs), +3 PlayMode tests (GameSceneTests.cs).
- Placeholder behaviour to be replaced in Phase 3: AI teammates hold spacing spots, defenders
  shadow their matchup, everyone chases loose balls; a defender picking up a loose ball takes
  possession immediately (no clear-the-ball rule yet).

## Phase 3 — what exists

- **Shot model** (`ShotModel`, all numbers in `ShotTuning`): hold-to-charge meter (0.9 s jumpers,
  0.6 s layups/dunks), five timing grades with a green window that widens with Shooting,
  layup / dunk / mid-range / arc profiles, and
  `makeChance = base + timing + openLook + hotStreak + clutch − distance − contest − fatigue`,
  clamped to 2–96 %. Green is best but not guaranteed (debug flag `debugGreenAlwaysMakes`).
  Callouts: GREEN, CLEAN LOOK, CONTESTED, TOO EARLY, TOO LATE. Seeded, so tests are exact.
- **Pass model** (`PassModel`, `PassTuning`): best-target scoring (openness, stick direction,
  distance, lane risk), automatic chest/bounce choice, interception only when a defender is
  genuinely in the lane (defense vs. playmaking), and a receiver arrow while you have the ball.
- **Match flow** (`MatchSimulation`): check-ball → live → dead ball → final; game clock and
  14 s shot clock (paused while a shot is in the air, reset on rim contact); violations;
  1/2-point scoring from the release spot; possession change after makes; clear-the-ball rule
  after steals and defensive rebounds; misses carom off the rim into predictable loose balls;
  offensive rebounds keep possession; sudden death if tied at the horn; box score
  (`MatchStats`: points, FG, arc, assists, rebounds, steals, turnovers) and player of the game.
- **Control model:** the human always controls their own player (slot 0). With a teammate
  holding the ball, PASS becomes **ASK** and the AI teammate passes it back.
- **AI** (`AiBrain`): utility decisions every reaction-time interval — shoot vs. the difficulty's
  quality bar, pass (friendly AI looks for the human), drive when the lane is open, hold; clears
  the ball when required; give-and-go and backdoor cuts; on-ball defense that closes out on
  shooters; help defense on drives (late rotations via error rate); rebound crashing by
  archetype. Difficulty changes reaction time, decision quality, shot selection, release
  accuracy, and mistakes only.
- **Unity:** shot meter beside your player, receiver arrow, arms-up shooting sprite frame,
  contextual buttons (SHOOT/CLEAR, PASS/ASK; defense actions dimmed until Phase 4), toasts
  (GREEN, SWISH +2, PICKED OFF, BOARD!, TAKE IT BACK, SHOT CLOCK), final card with Rematch/Home.
- **Practice Lab:** opponents are passive, no shot clock, you keep the ball after scoring.
- Balance snapshot (20 AI-vs-AI games per level, AI team only): FG% Rookie 33 / Caller 38 /
  Legend 51; 0 shot-clock violations by the AI.

## Phases 4–6 — what exists

- **Defense & plays** (`DefenseAndPlays.cs`, `DefenseTuning`): matchups, steal with cooldown and stun on a miss,
  jump to contest and block, switch onto the handler, box-outs and weighted rebounding, stamina, and
  three play calls (Pick & Roll, Give & Go, Clear Out). AI defenders jump at shooters and gamble for steals
  by archetype; AI offenses run pick-and-rolls.
- **Progression** (`Progression/`): `MatchSummary`, `Rewards`, `Career` (apply once per match id, upgrades,
  cosmetics, practice bests), `SeasonEngine` (8 teams, 10 games, tie-breakers, playoffs), `RiseEngine`
  (circuit, season, playoffs, energy, chemistry, six event cards), `PracticeSession` (three drills).
- **Save** (`Save/`, `Core/SaveStore`): MiniJson + versioned `SaveCodec`; atomic writes; corrupt files backed up.
- **Audio** (`AudioSynth`, `AudioManager`): 12 synthesised SFX and a 32-beat chiptune loop.
- **Unity:** defense buttons, CALL menu, jump lift and BOX OUT line, post-game box score with player of the game
  and rewards, Rise hub (stage, objective, energy, chemistry, next game, standings, bracket, event modal),
  Quick Call and Practice pickers, Locker Room tabs, full Settings, iOS haptics plugin, colourblind jersey
  patterns, cosmetic jersey/shoe/banner colours in games.

## Phase 7 — what was done

- Removed empty folders; throttled the per-frame HUD info string; added PlayMode tests that every menu
  scene and a practice drill build without errors.
- Rewrote README (setup, controls, iOS build, architecture, limitations, asset disclosure) and completed DESIGN.
- Added `docs/QA_CHECKLIST.md` for a manual pass on device.

## Phase 8 — release prep

- **Art:** `AppIconGenerator`, an original 64x64 pixel basketball with gold "signal" arcs on a dithered sunset, scaled to an
  opaque 1024x1024 icon. The launch image is the main-menu backdrop with the ball emblem and no text.
- **Editor:** `ReleaseTools`, with menu items to generate and assign the icon and launch image, apply release Player Settings
  (1.0.0 / build 1, iOS 15+, portrait, full screen, no Unity splash), and build Simulator or Device Xcode projects.
  The same builds run headless via `tools/build_ios.sh`. `IosPostProcess` sets the Info.plist keys.
- **iOS:** `PrivacyInfo.xcprivacy` (no tracking, no data collected).
- **Docs:** `docs/APP_STORE.md` (metadata within Apple's character limits, privacy policy text, privacy label, age rating,
  export compliance, screenshot plan, review notes) and `docs/RELEASE_CHECKLIST.md`.
- **Version:** the menus now show the Player Settings version (`Application.version`) instead of a hard-coded string.

## Phase 9 — polish (presentation only, no gameplay or balance changes)

- `Logic/Feel/Flair.cs`: celebrations (Fist Pump, Call It, Shimmy Step) and dribble moves (Crossover, Hesi Hop,
  Spin Cycle) as pure pose functions chosen by the equipped cosmetic. A sharp cut (>110°) with the ball triggers
  your move, with a 0.7 s cooldown. Also deterministic `Bursts` for sparks.
- `AudioSynth.CrowdAmbience`: a 4 s seamless crowd loop under matches. Music ducks while it plays.
- Unity: `PixelBursts` (pooled 1-px sparks: white on makes, gold on greens, bigger on dunks; dust on blocks;
  cyan on steals), SLAM! toast and camera shake on dunks, score bounce in the HUD, post-game rewards count up.
- `BallState.ShotType` records the kind of shot in the air, for presentation.
- First-compile fixes from Unity 6000.6.3f1: a `PlayerInput` name clash, a definite-assignment error in Quick
  Call, and the deprecated `DEVELOPMENT_BUILD` define replaced by `DEBUG`. The overlays now scroll, and the keyboard
  controls show on screen.

## Deviations from the brief (deliberate)

- "Oakland Voltage" → **Eastbay Voltage** (real city + electric branding sat too close to real pro-sports naming).
- "Solar Kings" → **Solar Crowns** ("Kings" is an existing pro team nickname).
- Northline Owls use ice blue / **black** / silver as primary / secondary / accent: ice blue and silver were too similar for readability.
- Cinemachine omitted: a single fixed half court needs only a small custom camera (Phase 2).
