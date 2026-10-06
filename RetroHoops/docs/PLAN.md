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
| 18 | One-command Simulator run, power/battery (Low Power + thermal → 60 fps, SHOW FPS, profiler markers, pooled sparks), onboarding (Coach Dee welcome, one-time coach tips) and juice (button pops, panel pops, jingles), Season 3 (Midnight Tide rival, story chapter 3, 3 courts, kits, 2 celebrations, 2 dribble moves, 3 badges, 2 codes) | **Done** (366 logic tests pass; 0 errors via tools/UnityCheck; Simulator run in progress) |
| 17 | Party games (H-O-R-S-E vs CPU/friend, 21, Around the World), Dynasty mode (aging, retirements, Hall of Fame, draft, league history), shareable highlight GIFs, iPhone build support (release log, UnityCheck) | **Done** (357 logic tests pass; compiles against Unity 6000.6.3f1 reference assemblies with 0 errors via tools/UnityCheck; not yet run on iPhone) |
| 16 | Create-a-team (colours, jerseys, shorts, shoes, logo); Season 2 (Sundown Syndicate rival, 3 courts, story chapter 2, kits, 7 badges); AI defensive schemes; 1-on-1; Shootout; 8-team Caller Cup; App Store launch kit | **Done** (342 logic tests pass; Unity changes not yet compiled; iPhone build in progress with Omari) |
| 15 | HEAT CHECK, alley-oops, hit-stop, heavy haptics, 120 Hz; CRT filter, title demo, pixel wipes, announcer voice; secret codes, hidden courts/teams, big heads, Arcade Ladder + boss; controller menus and prompts | **Done** (324 logic tests pass; Unity changes not yet compiled; iPhone build still to do with Omari) |
| 14 | Instant replay + play of the game, 3-Point Contest and Lockdown drills, King of the Court mode, Spanish language (partial) | **Done** (296 logic tests pass; Unity changes not yet compiled; iPhone build still to do with Omari) |
| 13 | Recruit your crew, Neon Static rival + Rival Challenge, story scenes, 16 badges + trophy room, iOS readiness check | **Done** (278 logic tests pass; Unity changes not yet compiled; iPhone build still to do with Omari) |
| 12 | Create-a-player, accessibility (left-handed, large buttons, tap-to-shoot, reduce motion), dunk/layup leaps, animated crowd, 3 music tracks + stingers, records/history/seasons | **Done** (262 logic tests pass; Unity changes not yet compiled in Unity) |
| 11 | How to Play tutorial, opt-in Game Center (GameKit bridge), local 2-player, Daily Challenge | **Done** (243 logic tests pass incl. a scripted tutorial run and 2-player sim tests; Unity UI and GameKit not yet run) |
| 10 | Rename to Retro Hoops; content: 2 courts + crews, 4 event cards, 7 cosmetics, First Call Classic tournament | **Done** (224 logic tests pass; Unity UI not yet seen running) |
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

## Phase 10 — Retro Hoops + content

- Renamed the game to **Retro Hoops** everywhere players see it: the app name (`productName`), Settings credits,
  Editor menus, docs, and the App Store kit. The placeholder bundle ID is now `com.retroball.game`. Code
  folders and namespaces keep their `CallerRetroBall` names so Unity references don't break.
- **Blacktop Circuit:** two new courts (Rooftop Ring, Boardwalk Slab) and two crews (Rooftop Relay, Boardwalk
  Bandits), each a bit tougher than the last. The circuit is now five games.
- **Rise:** four new event cards (Rain Delay, Highlight Tape, Trash Talk, Shoe Drop), ten in total.
- **Cosmetics:** Arcade Mint and Gold Rush jerseys, Glacier Highs and Cosmic Runners shoes, a Boardwalk Pennant
  banner, the Raise the Roof celebration, and the Behind the Back dribble move (both animated).
- **First Call Classic** (`ClassicEngine`, `GameMode.Tournament`, saved in `career.classic`): crew vs three
  league teams drawn by seed in a 1v4 / 2v3 bracket. Other games are simulated. The title bonus is 200 SP
  (smaller than the Cup), and titles count in the Locker Room.

## Phase 11 — tutorial, Game Center, 2-player, Daily Challenge

- **Engine:** `MatchSimulation` supports a second human (`MatchSetup.SecondHuman`, `Step(dt, p1, p2)`,
  `HumanIndexOf`, `IsHumanControlled`). One-player behaviour is unchanged: every earlier seeded test still passes.
- **Tutorial** (`TutorialSession`, `GameMode.Tutorial`): eight steps that advance only when the player does the
  thing. On offense the ball is handed back; for defense the ball goes to the other side. Offered on first launch,
  replayable from PLAY and Settings, with a 100 SP one-time reward.
- **Daily Challenge** (`DailyChallenges`, `GameMode.Daily`): seeded by calendar day, six goal types, streaks, and
  a capped streak bonus, once per day. Saved in `career.daily`.
- **2 Player** (`GameMode.Versus`): P1 uses touch/WASD and the first controller, P2 uses arrows + numpad or a
  controller. The camera follows the ball, P2 has a cyan ring, there are no rewards, and nothing counts toward the career.
- **Game Center:** `CallerGameCenter.mm` (GameKit) plus `GameKitGameCenterService`. It's opt-in in Settings, and
  achievements and leaderboards come from `Achievements`. The post-processor adds GameKit and the capability. The IDs
  to create are in `docs/GAME_CENTER.md`.
- **Menus:** PLAY now opens Quick Call, Daily Challenge, 2 Player, First Call Classic, and How to Play.

## Phase 12: create-a-player, accessibility, animation & sound, stats

- **Create-a-player** (Locker Room ▸ CREATE): skin tone, hair, hair colour, build, height, number, and style of play
  (archetype baseline −10, like Rook), with a live sprite preview. `PlayerCreator` builds the human's `PlayerDef`.
  Training upgrades apply on top. It's used in Rise, the Classic, Practice, and How to Play. "Go back to Rook" is
  always available.
- **Accessibility** (Settings): left-handed layout, large buttons (+25%), tap to shoot (`TapShoot`), and reduce
  motion (no shake, sparks, score bounce, or crowd bob). VoiceOver labels were **not** added: uGUI has no
  screen-reader support, and Unity's newer accessibility API wasn't verified for this project.
- **Animation & sound:** dunk and layup leaps (`Flair.Leap`); an animated crowd (`CrowdGenerator`, fans cheer and
  groan, they skip banner spots, and the court stops drawing static people in matches); three music loops
  (menus / matches / Rise hub); stingers for HEATING UP (3 makes in a row), ON FIRE (4+), dunks, greens, records,
  and titles. These are musical stings, not a spoken announcer.
- **Stats & records** (`Records`): single-game bests with NEW RECORD callouts (announced after the first game),
  current and best win streak, the last 20 games, and per-season Rise history with the result. All of it is shown
  in Locker Room ▸ STATS.

## Phase 13: crew, rivals, story, badges, iOS readiness

- **Recruit your crew** (`CrewEngine`): beating a street crew in Rise unlocks its three players. Beating a league
  team unlocks its bench player, the fourth roster player who never plays for them, so a recruit is never also
  on the other side. A signing fee scales with overall rating; afterwards, swaps are free. Two teammate spots,
  saved in `rise.teammates/recruitable/signed`. Shown in Rise hub ▸ YOUR CREW.
- **Rival:** Neon Static (new `TeamTier.Rival`, court The Static Lot, led by Vex Marlowe). A Rival Challenge
  (`GameMode.Rival`) appears once per season from week 5. It isn't in the standings, and a win pays +150 SP
  and +50 fans.
- **Story** (`Story`, `StoryView`): seven original scenes with Coach Dee and Vex (intro, circuit cleared, rival
  intro / beaten / lost, playoffs, champions). Each plays once in the Rise hub, with a typewriter box, portraits,
  and SKIP.
- **Badges** (`Badges`): 16 in-game badges, announced once after games; Locker Room ▸ TROPHY shows titles
  and all badges.
- **Retro Hoops ▸ Release ▸ Check iOS Readiness:** iOS module, Mac/Xcode, scenes, bundle ID, team, app name, and
  icon, with an automatic fix for the icon and release settings.

## Phase 14: replay, drills, King of the Court, Spanish

- **Instant replay** (`ReplayRecorder`): a 5-second ring buffer of positions and ball state, recorded after every
  step. Big plays (dunks, green releases, blocks, steals) offer a REPLAY button; the replay plays at half speed
  with a letterbox (tap to skip). The best play is kept as **PLAY OF THE GAME** on the post-game screen.
- **Drills:** 3-POINT CONTEST (60 s, arc shots only, a rotating gold money spot is worth 2) and LOCKDOWN
  (defense: stop 6 possessions). Bests are saved.
- **King of the Court** (`KingEngine`, `GameMode.King`): pick a league team, then play short games (first to 11
  or 90 s) against the others in a shuffled order. Each win adds to the streak and pays a bonus (20 SP per
  streak win, capped at 200); one loss ends the run. Best streak is saved.
- **Spanish** (`Loc`): Settings ▸ LANGUAGE. Menus, controls, HUD callouts, tutorial, events, badges and story
  scenes are translated. **Coverage is partial:** some built-up strings (numbers + text), archetype descriptions,
  and team/player names stay English.

## Phase 15: arcade feel, retro presentation, secrets, controllers

- **HEAT CHECK:** three straight makes heat a player up (+8% make chance, +6% speed, flame embers, glowing
  ball) until they miss, get blocked, or the other team scores. "HEATING UP" warns one make early.
- **Alley-oops:** pass to a teammate within 2.3 m of the rim (finishing 60+) and it becomes a lob they dunk in
  the air. The pass arrow turns gold when a pass will be an oop. Lobs are harder to intercept; jumpers can still
  block the finish.
- **Feel:** hit-stop on dunks, alley-oops, blocks and the final buzzer (off with Reduce Motion); heavy haptics
  on dunks/oops/heating up; Settings ▸ HIGH FRAME RATE runs the simulation and screen at 120 Hz on ProMotion
  iPhones (Info.plist `CADisableMinimumFrameDurationOnPhone`).
- **Presentation:** Settings ▸ CRT FILTER (off / soft / strong scanlines + vignette); pixel block wipe between
  screens; the title logo bobs; after 30 s idle on the title an AI-vs-AI demo game plays (any input exits);
  the announcer "speaks" callouts as synthesised chiptune voice blips.
- **Secrets:** Settings ▸ SECRETS ▸ ENTER A CODE (four symbols). Codes: BIG HEADS, RAINBOW BALL, ALWAYS HOT
  (toggles), PIXEL VOID (hidden court), CARTRIDGE KIDS (secret crew). Hints are earned by playing and appear
  in Locker Room ▸ TROPHY.
- **Arcade Ladder** (`ArcadeEngine`, `GameMode.Arcade`): five league teams weakest to strongest, then the
  secret boss THE GLITCH on The Glitch Grid. Difficulty climbs Rookie → Caller → Legend. Three continues.
  Clearing unlocks the boss team and court for Quick Call (+500 SP first time, +150 after).
- **Controllers:** menus are navigable with a gamepad (gold pixel cursor, A select, B back); in matches the
  touch buttons hide when a controller is used and the controls hint shows controller buttons; START pauses.
  Info.plist declares extended-gamepad support.

## Phase 16: create-a-team, Season 2, smarter AI, new modes, launch kit

- **Create-a-team** (Locker Room ▸ TEAM, `CustomTeams`): name, city, scoreboard name, 16-colour palette for
  jersey / trim / accent / shorts / shoes, 8 jersey patterns, logo shape + icon, home court. Your player and crew
  play for it in Quick Call, King, Arcade Ladder, 1-on-1, Caller Cup and 2 Player; "Wear it in Rise Mode" dresses
  the First Callers in it. Kits draw the pattern always and use their own shorts and shoe colours.
- **Season 2:** the Sundown Syndicate (led by Kaia Sol) is the rival in even Rise seasons; four new story scenes
  (Season 2 opener, Syndicate intro / beaten, two-time champions) in English and Spanish; courts Sundown Yard,
  Rain Alley, Snowline Park (all playable in Quick Call); 3 jerseys, 2 shoes, 1 banner; badges for the
  Syndicate, two titles, 10 alley-oops, 10 heat checks, creating a team, the Caller Cup and the Shootout.
- **Smarter AI:** every AI team has a defence (man, full pressure, pack the paint, zone). Caller/Legend AI
  switch out of a zone or pack-line after two threes in a row, and into pack-line after two baskets at the rim;
  the HUD says when they change.
- **Modes:** 1-on-1 (teammates sit out at the far corners), Practice ▸ SHOOTOUT (3-point contest against a CPU
  shooter whose round is simulated with the real shot model), and the Caller Cup (8-team knockout, +300 SP title).
- **Launch kit:** `docs/LAUNCH_KIT.md` (screenshot sizes and shot list with EN/ES captions, app preview
  storyboard, TestFlight checklist, privacy answers, launch-day list, press blurb); updated `APP_STORE.md`
  with the new features and a Spanish (Mexico) listing; **Capture Store Screenshot** saves opaque PNGs; the
  readiness check and builds write `Logs/RetroHoops-release.txt`.

## Phase 17: party games, Dynasty, highlights, iPhone prep

- **Party games** (PLAY ▸ PARTY GAMES): H-O-R-S-E against the CPU (its shots use the real shot model from a
  league team's best shooter) or a friend (pass the phone); 21 (1-on-1 to exactly 21, make it take it, bust
  back to 13); Around the World (seven mid-range spots in order, timed); the Shootout. Shooting games hand the
  ball straight back where you stand (`MatchSimulation.ResumeWithBall`).
- **Dynasty** (`DynastyEngine`): when a Rise season ends, START NEXT SEASON runs an off-season. Players age
  (≤23 grow +2, 24–26 +1, 27–29 hold, 30–32 −1, 33+ −2, plus a little luck), veterans retire (35, or sometimes
  33+), great careers (peak 78+, 4+ seasons) enter the Hall of Fame, teams draft generated rookies into the
  open spots, and you pick one of three prospects for your crew. LEAGUE HISTORY lists champions by season,
  titles by team and the Hall of Fame. Your own player never declines. Everything is saved and rebuilt on
  launch (`DynastyEngine.Apply`).
- **Share highlights:** post-game SHARE HIGHLIGHT replays the play of the game, records it at 15 fps / 270 px
  wide, encodes an animated GIF with an original encoder (`GifEncoder`, tested by decoding it back), saves it in
  the app's Highlights folder and opens the iOS share sheet (`RetroShare.mm`; Save Image needs
  `NSPhotoLibraryAddUsageDescription`, set by the build post-processor).
- **iPhone prep:** iOS readiness checks, builds and screenshots are logged to `Logs/RetroHoops-release.txt`;
  `tools/UnityCheck` compiles every assembly against Unity's own reference assemblies before delivery.

## Phase 18: Simulator, performance, onboarding, Season 3

- **Run it:** `tools/play_on_simulator.sh` (quit Unity first) builds with the project's exact Unity version,
  compiles with xcodebuild for the Simulator (no signing), boots an iPhone simulator and launches the game;
  every step logs to `Logs/` and failures are summarised in `Logs/RetroHoops-release.txt`.
- **Power:** `PowerMonitor` checks Low Power Mode and thermal state (iOS plugin `RetroPower.mm`) every 5 s;
  while saving power the screen runs at 60 fps and embers/crowd bobbing switch off. Settings ▸ SHOW FPS shows
  a rolling fps/avg/worst readout. Sim step and view sync have Profiler markers; spark bursts no longer
  allocate.
- **Onboarding:** first launch plays a short Coach Dee welcome scene before the tutorial offer. During your
  first 6 games Coach Dee gives one-time tips at the right moment (meter, early/late release, clearing,
  defence, alley-oop, heating up, shot clock); Settings ▸ COACH TIPS turns them off.
- **Juice:** every button squashes and springs back; menus and dialogs pop open; tip-off, victory and defeat
  jingles (all off or plain with Reduce Motion where it matters).
- **Season 3:** Midnight Tide (Mara Quill) is the rival every third Rise season, with intro/beaten scenes and
  a three-peat scene (EN/ES); courts Ferry Deck, Lantern Market, Canyon Rim; kits; Pixel Wave and Take a Bow
  celebrations; Double Cross and Step Back moves; badges Tide Turner, Three-Peat, Dynasty; codes POCKET
  GREEN (old-handheld green screen) and SKY HIGH (double-height jumps, looks only).

## Phase 19: App Store, Game Center + iCloud, local multiplayer, accessibility, Season 4

- **TestFlight / App Store:** `tools/ship_testflight.sh` runs Unity's `ReleaseTools.BuildAppStore` (raises the
  build number, Release config, `iOSBuild/AppStore`), then `xcodebuild archive` and `-exportArchive` with an
  App Store Connect export (upload, or `.ipa` with `--export-only`). The team comes from `$TEAM`, Unity's
  Signing Team ID, or the last Xcode device build. The app is iPhone-only. Step-by-step guide: `docs/SUBMISSION.md`.
- **Game Center:** 23 achievements (890 points) and 9 leaderboards (King streak, Arcade clears, Shootout wins,
  Around the World time, best win streak, most points), defined as data in `Achievements` (tested against
  Apple's limits). Reported after practice, party and 2 Player games too.
- **iCloud:** the career syncs through the player's own iCloud key-value storage (`CloudSync`, `RetroCloud.mm`,
  capability added by `IosPostProcess`). The career that's further along wins; a fresh install loads it,
  otherwise the menu asks LOAD FROM ICLOUD / KEEP THIS ONE. Settings stay per device; Reset also clears iCloud.
- **Local multiplayer:** tabletop 2 Player on one phone (P1 bottom half, P2 top half turned 180°) when no
  controller is connected; Shootout vs Friend (pass and play, two 60-second rounds).
- **Accessibility:** COLOR FILTER (red-green / blue-yellow meter palettes and automatic away-kit swap, chosen
  with colour-vision simulation and ΔE checks in `ColorAccess`); CAPTIONS for announcer calls (also follows
  iOS Closed Captions); VoiceOver for menus via Unity's screen-reader API (`ScreenReader`).
- **Season 4:** the Paper Cranes (Juno Vale) take their turn as the fourth rival (rivals now rotate every four
  seasons), chapter 4 scenes (EN/ES), courts Laundromat Lot, Drive-In Lot and Paper Garden, kits, Shoulder
  Brush and Paper Plane celebrations, Rocker Step and Snatch Back moves, a crane logo, and four badges.

## Full Court and winners' ball (after Phase 19)

- **Full Court 5-on-5** (Play ▸ FULL COURT, `GameMode.FullCourt`): both baskets, 2s and 3s, four minutes,
  20-second shot clock, tip-off at half court, inbound from the far baseline after a basket. Teams with
  fewer than five players get deterministic generated reserves (`FullCourt.Reserves`). The simulation runs
  in the attacking team's frame (its hoop always "up"), and the picture turns 180° when possession changes
  (`MatchSimulation.TurnFrame`, `Flipped`, `ToWorldCourt`), so shooting, passing and the AI are unchanged.
  Off-ball defenders get back to half court unless pressing; on-ball defenders give a cushion in the
  backcourt. Views draw on the fixed court through `CourtSpace.Flip`; the camera scrolls up and down;
  the art is two half courts back to back with a second hoop. Pays like Quick Call; its points don't count
  toward the half-court single-game points record.
- **Winners' ball:** every half-court 3-on-3 game (Quick Call, Rise, King, Cup, Arcade, 2 Player and the
  rest) keeps the ball with the team that scores. Full Court and 1-on-1 still alternate.

## Phase 20: Kit Studio, Holiday Games, Full Court rules, sharing

- **Kit Studio** (Locker Room ▸ KIT): Home / Away / Alt kits. Jersey: colour, trim, accent, cut (tank, tee,
  long sleeve), collar (V, crew, none), side stripes, chest (logo, band, number), pattern. Shorts: colour, trim,
  length (classic, long, short), side stripe, waistband. Shoes: colour, sole, laces, stripe, height (low, mid,
  high). 64-colour palette + RGB sliders (32 steps a channel, the grid kits are stored on). Live looping
  preview, RANDOMIZE (harmonious schemes, unlocked styles only), UNDO, RESET, presets from owned shop items.
  Long sleeves, high tops, long shorts, double stripes, chest number and four patterns unlock by playing.
  Your team wears the kit in every game; the Away kit goes on automatically when colours clash (ΔE check).
  The classic look is pixel-identical to the old sprites (tested).
- **Sharing (offline):** a kit is a 33-character code (15-bit colours + styles + CRC-8, so every single-character
  typo is caught) and a QR code (own encoder: byte mode, level M, versions 1–10, checked against OpenCV's
  decoder) for `retroball://kit/<code>`; the iPhone camera opens the app straight into the Kit Studio. ENTER
  CODE / PASTE loads a friend's kit. TRADING CARD draws your player in your kit with OVR, role and stats
  (PNG via the share sheet).
- **Holiday Games** (Play ▸ HOLIDAY GAMES): Christmas (snow, candy-cane lines, trees, lights), Halloween
  (pumpkins, bats, moon), Easter (eggs, flowers, bunting) and Fourth of July (stars in the paint, striped
  lines, fireworks) courts; the one in season (all of October, Dec 1–26, Easter week, Jul 1–7) gets a main
  menu button. Holiday courts are also in the Quick Call court list.
- **Full Court polish:** inbound pass from the baseline after a basket (5-second count), backcourt and
  eight-second violations, fast-break lanes and get-back defence, two-player benches with substitutions at
  dead balls when AI players tire (subs keep their own box-score lines), and the Rise Mode final is Full Court.

## Phase 21: Franchise, All-Star Weekend, Court Builder, soundtrack, ship 1.0

- **Franchise** (Play ▸ FRANCHISE): run one of the eight Caller League clubs for as many seasons as you like.
  7–9 man rosters (the league's players plus generated depth), contracts in tenths of a million under an
  $85.0M cap (minimum deals always fit; your own players can be re-signed over the cap; releasing leaves
  half a season's salary as dead money). A 14-game double round robin of Full Court games (PLAY or SIM),
  top-four playoffs, MVP and rookie awards. Off-season in order: aging (growth toward potential before 26,
  decline after 29), retirements and a Hall of Fame, a draft lottery for the four non-playoff teams (40/30/20/10
  for the top two picks), a 12-man draft class with scouting (each visit narrows the rating range; the potential
  grade shows after two), rookie scale contracts, re-signing, free agency (AI clubs fill their rosters), preseason
  cuts. Trades with the seven AI GMs (up to 3 players a side, deadline week 10): they value rating (squared,
  so stars beat depth), youth with potential, age and contract value, need to win the deal by a margin, and
  both sides must stay roster- and cap-legal (or take back ≤125% of outgoing salary). History book: every
  season's record and finish, champions, awards, transactions.
- **All-Star Weekend**: Dunk Contest (11 original dunks with arrow combos 2–7 long, a clock to enter them,
  a slam meter, two tries a dunk; five judges score 5–10 on difficulty, execution, timing, with repeats marked
  down; you plus three league dunkers, two rounds), 3-Point Contest (the Shootout drill against three of the
  league's best shooters, top two to a final), and the All-Star Game (Team Sunrise vs Team Moonlight, the
  league's best split by snake draft, Full Court, you start). In Rise it opens at mid-season and stays open to
  the end of the regular season; everything is also on Play ▸ ALL-STAR CONTESTS.
- **Court Builder** (Locker Room ▸ COURT): three slots; floor style (asphalt, hardwood, neon grid, tiles,
  rubber) and colour, lines, paint, what's behind the baseline (crowd, chain-link fence, brick wall), sky
  presets, crowd size, centre-court logo (motif, shape, colour); live preview from the real court generator.
  Saved courts are in Quick Call and can be your team's home court.
- **Soundtrack + Music Player** (Settings ▸ MUSIC PLAYER): six new original songs written by an in-code
  composer (pulse lead over chord tones, triangle bass, arpeggio, noise drums, A-A-B-A loops, all in key)
  plus the three original loops; level meter; menu track and match track (or shuffle) are saved.
- **Game Center**: three new achievements (Franchise title, Dunk Contest, 3-Point Contest; 1,000 points total)
  and a Best Dunk Contest Round leaderboard.
- **Ship it**: store listing, screenshots plan and review notes updated for 1.0; upload via `tools/ship_testflight.sh`.

## Phase 22: Legacy, The Park, Tournament Builder, polish

- **Legacy** (Play ▸ LEGACY): your created player's career. Senior year of high school (7 games + state
  playoffs) earns 1–5 recruiting stars; college offers by stars (or skip college and declare); a college season
  sets draft stock; draft night (the weakest league team picks first; low stock = undrafted); then pro seasons
  with a Caller League team (10 games + playoffs) until you retire (33+, forced at 38). Every game is Full
  Court with you at the top of the lineup, or SIM for half XP. Game grades (A+..F) give XP; levels give skill
  points for a 24-node skill tree in three branches (tiers need the tier below). You start a senior at overall
  52 whatever your base, grow with age and skills, decline after 30. Coach's trust and your role, salaries
  and agent offers when a contract ends, up to three invented sponsors (unlocked by fans), a private trainer
  bought with cash, 12 story moments with choices, awards (State/Conference champion, All-State,
  All-American, MVP, All-League, Rookie of the Year), legacy points and a Hall of Fame vote.
- **The Park** (Play ▸ THE PARK): twelve original street legends with crews, formats 1-on-1 to 4-on-4 on their
  own courts. Street rules (first to 15, win by two, make it take it) and **ankle breakers**: a sharp cut near a
  defender can make them stumble for 0.9 s (handle vs feet, 8–55%, 1.6 s cooldown; "ANKLES!"). Rep from
  Rookie to Legend opens tougher callers; first wins pay double; beat everyone for King of the Park.
- **Half-court 2-on-2 and 4-on-4** in the engine (MatchRequest.TeamSize), tested with AI-vs-AI games.
- **Tournament Builder** (Play ▸ TOURNAMENT BUILDER): name, 4/8/16 teams from everything you can play,
  2-on-2, 3-on-3 or Full Court, your team; seeded by strength with a standard bracket; others simulated.
- **Polish:** the PLAY menu is grouped (Play now, Careers, Events, With friends); composed songs are written
  on a worker thread so screens never hitch, and only the playing song stays in memory; two leaderboards.
- **Ship it:** the upload script now logs every run to `Logs/ship_console.log`; double-click
  `tools/Ship Retro Hoops.command`.

### Phase 23: launch 1.0, iPad + Mac, online-free extras

- **App icon "Sunset Swish"** (AppIconGenerator): ball dropping through the net under a backboard, striped
  synthwave sun, neon grid floor, gold sparkles; 64x64 pixel art scaled to 1024, fully opaque.
- **iPad + Mac:** release settings now target iPhone and iPad (portrait, full screen); Apple silicon Macs run
  the iPad build as "Designed for iPad". The court camera picks the largest integer zoom that shows 11.5 m
  across *and* 20 m top to bottom, so wide screens no longer crop the court (phones are unchanged). Canvases
  match height on anything wider than 9:16 (and re-match when a window changes shape). iPad launch image set.
  A keyboard (WASD/J/K/L) hides the touch controls like a controller does, until the next touch; Macs show
  the keyboard hint. 13" iPad screenshot sizes accepted by Capture Store Screenshot.
- **Weekly Challenges** (Play ▸ WEEKLY & HOOPS PASS): three goals each Monday from the week number (offline,
  same for everyone), progress adds up over every game vs the CPU; +100 SP each, +150 for all three.
- **Hoops Pass:** free 20-tier season track (6-week seasons): XP from games (+25, +15 for a win), weekly goals
  (+120) and the daily (+60, once a day). Tiers pay SP; tiers 5/10/15/20 unlock pass-only gear (two alternating
  sets of four; owned gear pays 250 SP instead). No purchases, no ads.
- **Photo mode** (pause ▸ PHOTO MODE): frozen game, HUD hidden, drag/zoom (or stick/triggers), six filters
  (Arcade scanlines, 4-Color dither, Dusk, Neon, Mono), optional frame with RETRO HOOPS, score and date;
  SNAP samples one pixel per art pixel, scales up crisp, saves a PNG and opens the share sheet.

### Phase 24: controls, finishes, alley-oops, landscape Full Court

- **Boot screen:** sunset court, RETRO HOOPS logo, spinning pixel basketball loader (was the old text card).
- **Button layout** (TouchControls + ControlLayout): ring buttons with original pixel icons in a thumb arc in the
  lower-right corner: SHOOT (biggest, outside), PASS beside it, DUNK above, LAYUP on the diagonal, CALL further in;
  the stick's resting place is shown bottom-left. On defence they become BLOCK, SWITCH and STEAL (LAYUP and CALL
  hide). PASS turns gold and reads OOP when a lob is on.
- **Customize controls** (Settings ▸ CUSTOMIZE CONTROLS): drag buttons, tap and −/+ to resize (70–150%), RESET,
  SAVE; warns about overlaps; saved in the career; used in portrait and landscape.
- **DUNK / LAYUP:** with the ball, close enough you go straight up; further out you attack the rim at full speed
  (up to 2.6 s; pull the stick away to back out) and finish there. Release is automatic, spread by Finishing;
  contests and blocks apply. DUNK becomes a layup without the rating (65) or distance (2.2 m). Keys U / I,
  controller RB / LB.
- **Baseline alley-oops:** while you handle the ball away from the rim, an athletic AI teammate (Finishing 60+)
  sometimes sprints to the corner and runs the baseline; within 4.6 m of the rim they're a lob target and PASS
  throws them the oop (whatever the stick aim), which they catch and throw down.
- **Full Court 5-on-5 in landscape:** the screen turns sideways for Full Court (not the attract demo or 2 Player),
  baskets left and right, side-view hoops, camera pans with the ball (the whole floor fits on most phones),
  sprites face the right way, UI lays out on 1920 x 1080. Info.plist allows portrait + both landscapes; the game
  locks portrait at boot and returns to portrait after the game. The stands' animated fans are off in landscape.

### Phase 25: feel, landscape polish, Season 5

- **Dunk packages** (Dunks.cs, Locker Room ▸ DUNK PACKAGE): Two-Hand Jam (default), Tomahawk, Reverse Jam,
  Windmill, Cradle Rock, Three-Sixty (SP + fans), and the pass-only Skyline Slam. Each has its own pose, extra
  lift/hang time, shake and callout ("WINDMILL! +2"). The AI picks by Finishing (only flyers go 360).
- **Euro step:** a driving handler with Playmaking 55+ side-steps a defender planted in the lane ("EURO STEP").
- **Rim protection:** on a DUNK/LAYUP drive the defender nearest the rim (not the driver's man) steps in front
  and goes up with the finish; a defender whose man runs the baseline stays between him and the rim.
  Both scale with difficulty. "NO DUNK FROM THERE: LAYUP" when a DUNK has to be a layup.
- **Landscape polish:** bleachers with cheering fans along the far sideline; the camera fits the near sideline
  to the top of the stands; a compact centred score bar. Settings ▸ LANDSCAPE: ALL GAMES plays every mode
  sideways (2 Player and the demo stay portrait).
- **Season 5:** the Cassette Club (fifth rival, every fifth Rise season; Echo Rivera; story scenes in English
  and Spanish; REWOUND badge; NO RIVALS LEFT now needs all five), Record Shop Roof and Night Bus Depot courts,
  and a third Hoops Pass gear set (Cassette Deck jersey, Tape Runners, Boombox banner, Skyline Slam).

### Phase 26: two phones, Couch Cup, Summer Story, performance

- **Two-phone play** (Logic/Net/Link.cs, Core/NearbyLink.cs, Plugins/iOS/RetroLink.mm): Multipeer Connectivity,
  no server. The host picks both teams; both teams' rosters and ratings travel in the setup, so career changes
  on either phone don't matter. Hand-shake checks the link version, app version and content (courts, rules,
  play styles). Deterministic lockstep: input is quantized to 4 bytes per step and scheduled 4 steps (67 ms)
  ahead; a step runs only when both inputs are in; checksums are compared every second. Both phones simulate
  at 60 Hz; no hit-stop, mid-game replays or photo mode; the pause menu doesn't stop the game. A friend leaving,
  a dropped connection or a checksum mismatch ends the game with no result. Info.plist gets
  NSLocalNetworkUsageDescription and NSBonjourServices (_retrohoops._tcp/_udp).
- **Couch Cup:** 2-8 players, shuffled knockout with byes, ties replayed, saved between games.
- **Summer Story:** 8 chapters with goals, scenes before and after, SP for first clears (150, finale 600).
  New original cast in StoryMode.Cast. Fixed a crash in story scenes for the fifth rival (the portrait cache
  was one slot short).
- **Mic Tally commentary** (Logic/Feel/Commentary.cs): runs, lead changes, ties, game point, dunks, threes,
  oops, blocks, steals, heat, euro steps, broken ankles, buzzer beaters; one line per 5 s at most (big
  moments can cut in), no back-to-back repeats.
- **Performance and battery:** menus draw at 60 fps while touched and 30 when idle (15 when saving power);
  gameplay draws every frame (OnDemandRendering, PowerPolicy.RenderInterval). Low-memory warnings drop cached
  art. Boot timing goes to the device log. Release builds strip engine code, use Low managed stripping with
  Assets/link.xml keeping our assemblies, and size-optimised IL2CPP code. Measured here (desktop .NET):
  a simulation step costs about 10 µs and the biggest generated texture (Full Court floor) about 5 ms, so
  neither is a battery or load-time problem; drawing is.

### Phase 27: Retro Hoops Live, ship-ready docs, two-phone rematch

- **Retro Hoops Live** (Omari's request: online play for $10.99/month): PLAY ▸ LIVE. The subscription uses
  StoreKit 2 (`Plugins/iOS/RetroStore.swift`, product `com.phoronomicstudios.retrohoops.live.monthly`). The App
  Store decides whether it's active, and the career caches the end date. The paywall shows the localised price,
  terms, Restore, Terms of Use and Privacy Policy (guideline 3.1.2). Matchmaking uses Game Center
  (`Plugins/iOS/RetroLive.mm`, GKMatchmaker); the player group is derived from the game version. The host is the
  lower Game Center ID. Each player brings their own team (the Hello message), and the host's team is home.
  Same lockstep as two phones with an 8-step input delay. Elo rating (1000 start, K 32), tiers ROOKIE to
  LEGEND, Game Center leaderboard `retrohoops.live.rating`. Quitting after 20 s is a loss; an opponent leaving
  is a win; a drop or desync counts for nobody. Setup steps in `docs/LIVE.md`.
- **Ship-ready docs:** `docs/LIVE.md` (subscription, agreements, sandbox testing, review notes), `docs/PRIVACY.md`
  (hostable policy covering Live and two phones), APP_STORE.md (description sections, age-rating answers,
  privacy label notes, review notes) and SUBMISSION.md (upload error fix, subscription step).
- **Two-phone rematch:** after a two-phone game both players tap REMATCH. The same teams play with a new seed
  and no reconnecting. Hand-shake messages that arrive while the other phone finishes are kept.
- **Not done this phase (moved to Phase 28):** spectator view, a two-phone Couch Cup, sharing two-phone
  replays, gameplay depth (fatigue/subs in half court, new plays, AI defence, tuning) and the polish items
  (crowd sounds, new animation frames, court intro, post-game screen).

### Phase 28: dunks that reach the rim, the Live server (backend security)

- **Real dunks** (Omari's request: "the hands of the players touch the basket"): `DunkPath` (Logic/Feel/Dunks.cs). The dunker
  glides from the take-off spot to just in front of the rim while rising, the raised hands meet the rim
  (+2 px over it) at the exact moment the simulation's ball arrives, then they hang on the rim for a beat (the rim dips a
  pixel) and drop back to where the simulation has them. The ball rides in the hands until the slam, and the dunk packages
  swing it. The lift comes from the sprite's real hand height (`CharacterSpriteGenerator.HandReachPx`) and the rim's screen
  height, so it works for every player height and in landscape. Drawing only; the simulation is unchanged, so two-phone
  and Live games stay in sync.
- **Live server** (`server/`, Cloudflare Workers + D1, TypeScript): Game Center identity verification
  (RSA-SHA256 over teamPlayerID + bundle + timestamp + salt, cert only from https://*.apple.com, 10-minute window,
  single use), HS256 sessions (1 h), subscription checks through Apple's App Store Server API (ES256 key; one
  subscription ↔ one player, `appAccountToken` tagging), App Store Server Notifications V2 (re-check with Apple), server-side
  Elo that only changes when both reports agree (score + final SimHash), stale games settled by cron, a pair farming cap,
  rate limits, an 8 KB body cap with strict validation, parameterised SQL, security headers with no CORS, and data deletion. 20 tests;
  type-checked; ran in the local Workers runtime. Game side: `BackendConfig.Url` (off until deployed), `BackendClient`
  (UnityWebRequest, token in memory only), Game Center identity fetch in RetroLive.mm, `originalTransactionId` +
  `appAccountToken` in RetroStore.swift, rated Live games report results, LIVE ▸ DELETE MY LIVE DATA.
- **Merged with the Phase 28 commit made on the Mac** (6cbc1f7, Claude Sonnet): `SaveGuard` (an HMAC envelope for the save
  file; the code and tests are in, but it isn't wired into SaveStore yet because that needs a Keychain key on iOS), a per-session nonce in
  `LinkSetup.Session` (rematches get a new one), and `LiveMode.Sanitize` / `DeltaIsPlausible` (now applied when loading the
  save). Its tamper test was fixed: it looked for compact JSON, but saves are pretty-printed.
- **Not done this phase (moved to Phase 29):** gameplay depth, polish & juice, spectator / two-phone Couch Cup / replay sharing.

### Phase 29: gameplay depth, arena polish

- **Two new plays** in the CALL menu: **BACKDOOR** (the best cutter jogs out high for 0.9 s, then cuts behind
  their defender to the rim; the others stay wide) and **POST UP** (the best finisher + rebounder seals on the
  ball-side block; the others clear to the corners). AI teams now mix them in with the pick-and-roll. Both
  travel in two-phone and Live games.
- **Smart defences double the hot hand:** Caller and Legend AI send the nearest off-ball defender at a
  heated-up scorer within 8 m of the rim ("DOUBLE TEAM! FIND THE OPEN MAN"), which leaves someone open. Rookie
  AI never does. Mic Tally calls it.
- **Half-court benches:** a team's own extra player (street crews, story crews, your crew) sits on the bench
  and checks in for a tired AI player at a dead ball, as in Full Court. There's no bench in 1-on-1, practice
  or the tutorial.
- **Difficulty curve check** (AI vs an idle player, 16 games each): opponent FG% 34 / 39 / 51 and an average margin of
  +5.7 / +7.6 / +14.3 for Rookie / Caller / Legend. The curve climbs. Rookie and Caller are close, so there's room
  to spread them once real players give feedback. A test now guards the ordering.
- **Arena sounds:** a crowd "OOOH" swell (blocks, broken ankles), an arena horn (final buzzer, substitutions)
  and rhythmic crowd clapping in a tight finish (last 20 s, within 3).
- **Tip-off card:** tonight's court, both teams in their colours, and the starters' numbers and names. It's
  skippable and fades after 3 s without stopping play.
- **Post-game TEAM STATS:** FG%, deep shots, assists, rebounds, steals, blocks and turnovers side by side, with
  split bars, above the box scores.
- Two-phone games already offer PLAY OF THE GAME and SHARE HIGHLIGHT after the final.
- **Not done (still open):** spectator view on a third phone, a Couch Cup across two phones, new animation frames.

## Deviations from the brief (deliberate)

- "Oakland Voltage" → **Eastbay Voltage** (real city + electric branding sat too close to real pro-sports naming).
- "Solar Kings" → **Solar Crowns** ("Kings" is an existing pro team nickname).
- Northline Owls use ice blue / **black** / silver as primary / secondary / accent: ice blue and silver were too similar for readability.
- Cinemachine omitted: a single fixed half court needs only a small custom camera (Phase 2).
