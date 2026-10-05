using CallerRetroBall.Controls;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
// The game's own input struct, not UnityEngine.InputSystem.PlayerInput (the component).
using PlayerInput = CallerRetroBall.Logic.PlayerInput;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// GameScene entry point. Builds the match from the pending <see cref="MatchRequest"/>,
    /// runs the deterministic <see cref="MatchSimulation"/> on a fixed 60 Hz step, and syncs
    /// the views, camera, HUD, and controls from its state.
    ///
    /// Offense: shoot (hold/release meter), pass / ask, CALL plays. Defense: STEAL, JUMP, SWITCH.
    /// Post-game applies rewards exactly once, feeds Rise Mode, and saves the career.
    /// Practice runs a <see cref="PracticeSession"/> drill with its own HUD and end card.
    /// Editor/dev: WASD/arrows move, K shoot/jump (hold), J pass/switch, L steal, C call,
    /// Esc pause, B knocks the ball loose.
    /// </summary>
    public sealed class GameSceneController : MonoBehaviour
    {
        /// <summary>Simulation step: 1/60 s, or 1/120 s on 120 Hz screens with high frame rate on.</summary>
        private float FixedStep = 1f / 60f;
        private int MaxStepsPerFrame = 5;
        // Profiler markers (visible in Unity's Profiler and Xcode Instruments' signposts).
        private static readonly Unity.Profiling.ProfilerMarker StepMarker = new Unity.Profiling.ProfilerMarker("Retro Hoops.SimStep");
        private static readonly Unity.Profiling.ProfilerMarker ViewsMarker = new Unity.Profiling.ProfilerMarker("Retro Hoops.SyncViews");

        private MatchRequest _request;
        private MatchSimulation _match;
        private MatchArt _art;
        private PlayerView[] _playerViews;
        private BallView _ballView;
        private ShotMeterView _meter;
        private SpriteRenderer _receiverArrow;
        private CourtCameraRig _cameraRig;
        private TouchControls _controls;
        /// <summary>Tabletop 2 Player: player 2's touch controls at the top of the screen (null otherwise).</summary>
        private TouchControls _controls2;
        /// <summary>Pass-and-play Shootout between two people (null otherwise).</summary>
        private ShootoutDuel _duel;
        private MatchHud _hud;
        private readonly InputBuffer _buffer = new InputBuffer();
        private float _accumulator;
        private bool _paused;
        private bool _finalShown;
        private int _lastShooter = -1;
        private float _shootPoseUntil;
        private int _lastLabelState = -1;
        private PracticeSession _practice;
        // Phase 17: H-O-R-S-E.
        private HorseSession _horse;
        private int _horseSeen;
        private SpriteRenderer _horseMarker;
        // Phase 17: highlight GIFs.
        private bool _capturing;
        private readonly System.Collections.Generic.List<byte[]> _gifFrames = new System.Collections.Generic.List<byte[]>();
        private TutorialSession _tutorial;
        private DailyChallenge _daily;
        private readonly InputBuffer _buffer2 = new InputBuffer();
        private bool Versus => _request != null && _request.Mode == GameMode.Versus;
        /// <summary>Full Court on a landscape screen.</summary>
        private bool _landscape;

        /// <summary>A stick / key direction on screen (+y up) as a fixed-court direction.</summary>
        private static Vec2 ScreenToCourt(Vec2 screen) => CourtSpace.ScreenToCourt(screen);
        private bool Demo => _request != null && _request.Mode == GameMode.Demo;

        // Phase 15: arcade feel and secrets.
        private float _hitStopUntil;
        private float _nextFlameAt;
        private bool _rainbowBall;
        private bool _skyHigh;
        private float _demoStartedAt;
        // Phase 18: coach tips.
        private float _lastTipAt = -99f;
        private float _nextTipCheck;
        private bool _tooEarly, _tooLate;
        /// <summary>A controller is being used: hide the touch buttons and show controller hints.</summary>
        private bool _padMode;
        /// <summary>The keyboard was used last (counts as pad mode for hiding the touch controls).</summary>
        private bool _keyMode;

        // Phase 12: accessibility, leaps, crowd.
        private bool _reduceMotion;
        private bool _tapToShoot;
        private TapShoot _tap;
        private bool _shootTapped;
        private int _leaper = -1;
        private float _leapStart;
        private float _leapDuration;
        private ShotType _leapType;
        /// <summary>How the current dunk looks (Dunks.cs).</summary>
        private DunkStyle _dunkStyle;
        private struct Fan
        {
            public SpriteRenderer Renderer;
            public Sprite Idle, Cheer;
            public Vector3 Base;
        }
        private Fan[] _fans = new Fan[0];

        // Phase 14: instant replay.
        private ReplayRecorder _recorder;
        private ReplayClip _lastHighlight;
        private ReplayClip _replay;
        private float _replayT;
        private readonly BallState _replayBall = new BallState();
        private const float ReplaySpeed = 0.5f;
        private bool Replaying => _replay != null;
        private CrowdMood _crowdMood = CrowdMood.Idle;
        private float _crowdMoodUntil;
        private SpriteRenderer[] _cones;
        private SpriteRenderer _targetArrow;
        private PlayCall _pendingCall;
        private bool _resultApplied;
        private float _nextInfoAt;

        // Phase 9 presentation (never read by the simulation).
        private PixelBursts _bursts;
        private int _celebrator = -1;
        private float _celebrateStart;
        private CelebrationKind _celebration;
        private CelebrationKind _myCelebration;
        private DribbleMoveKind _myMove;
        private float _moveStart = -10f;
        private float _moveReadyAt;
        private Vec2 _lastMoveDir;
        /// <summary>The rim the offence attacks, as drawn (Full Court: whichever end that is right now).</summary>
        private Vector3 HoopWorld => CourtSpace.ToWorldSnapped(_match.Setup.Court.Hoop, CourtSpace.RimHeight);
        private bool FullCourtGame => _match != null && _match.Setup.FullCourt;
        /// <summary>What each slot wears (kept so a substitute can be drawn in the same kit).</summary>
        private KitLook[] _dress;

        /// <summary>Full Court substitution: draw the new player in the slot's kit.</summary>
        private void RedrawPlayer(int index)
        {
            if (index < 0 || index >= _playerViews.Length) return;
            var p = _match.Players[index];
            var frames = _art.PlayerFrames(p.Def, _dress[index]);
            _playerViews[index].SetFrames(frames);
            if (App.Career != null && Secrets.IsOn(App.Career.secrets, Secrets.BigHeads))
                _playerViews[index].EnableBigHead(_art.HeadFrames(frames, p.Def.appearance));
        }

        /// <summary>Shown once at tip-off when your Away kit was picked automatically.</summary>
        private string _kitNote;

        public MatchSimulation Match => _match;
        public bool IsPaused => _paused;

        private void Start()
        {
            App.EnsureInitialized();
            var catalog = App.Catalog;
            _request = App.PrepareRequest(App.PendingMatch ?? MatchRequest.QuickCallDefault(catalog));
            App.PendingMatch = null;
            if (_request.Seed == 0) _request.Seed = (uint)System.Environment.TickCount | 1u;

            int rate = App.SimulationRate;
            FixedStep = 1f / rate;
            MaxStepsPerFrame = rate / 12;
            var setup = MatchSetup.FromRequest(_request, catalog);
            _match = new MatchSimulation(setup);
            CourtSpace.Flip = false;
            CourtSpace.FlipLength = setup.Court.depth;
            // Full Court 5-on-5 plays in landscape (baskets left and right).
            _landscape = (setup.FullCourt || (App.Career != null && App.Career.settings.landscapeAll)) && !Demo && !Versus;
            CourtSpace.Landscape = _landscape;
            CourtSpace.LandscapeLength = setup.Court.depth;
            if (_landscape) Orientation.Landscape();
            if (_request.Mode == GameMode.Practice && _request.Drill == (int)DrillKind.Horse)
            {
                bool friend = _request.ContextId == "horse:friend";
                string oppTeam = _request.AwayTeamId ?? catalog.TeamsInTier(TeamTier.League)[0].id;
                _horse = new HorseSession(friend ? HorseOpponent.Friend : HorseOpponent.Cpu, setup.Court, setup.Shot, setup.Rules,
                                          friend ? null : Shootout.PickShooter(catalog, oppTeam), catalog.Difficulty(_request.DifficultyId), _request.Seed);
            }
            else if (_request.Mode == GameMode.Practice && _request.Drill >= 0)
            {
                _practice = new PracticeSession((DrillKind)_request.Drill, _match, _request.Seed);
                if (_practice.Kind == DrillKind.Shootout && IsThreeContest)
                {
                    // All-Star 3-Point Contest: the "CPU" score is the one you need to beat this round.
                    var contest = App.Career.allStar.contest;
                    var rival = contest.round >= 2 ? AllStar.Finalists(contest).Find(e => !e.you) : null;
                    _practice.SetCpu(Mathf.Max(0, AllStar.Target(contest) - 1), rival != null ? rival.name.ToUpperInvariant() : Loc.T("FINAL SPOT"));
                }
                else if (_practice.Kind == DrillKind.Shootout && _request.ContextId == ShootoutDuel.ContextId)
                {
                    // Pass and play: P1 sets the score, P2 tries to beat it (kept across the two rounds).
                    _duel = App.PendingDuel != null && !App.PendingDuel.Finished ? App.PendingDuel : new ShootoutDuel();
                    App.PendingDuel = _duel;
                    _practice.SetCpu(_duel.Round == 1 ? _duel.Points[0] : 0, "P1");
                }
                else if (_practice.Kind == DrillKind.Shootout)
                {
                    // The CPU shoots first (simulated); you get the score to beat.
                    var cpu = Shootout.PickShooter(catalog, _request.AwayTeamId ?? catalog.TeamsInTier(TeamTier.League)[0].id);
                    int cpuScore = Shootout.SimulateCpu(cpu, catalog.Difficulty(_request.DifficultyId), setup.Shot, _request.Seed,
                                                        setup.Court.arcRadius + 0.7f);
                    _practice.SetCpu(cpuScore, cpu != null ? cpu.lastName.ToUpperInvariant() : "CPU");
                }
            }
            if (_request.Mode == GameMode.Tutorial) _tutorial = new TutorialSession();
            if (_request.Mode == GameMode.Daily && _request.ContextId != null && _request.ContextId.StartsWith("daily:", System.StringComparison.Ordinal)
                && int.TryParse(_request.ContextId.Substring(6), out int day))
                _daily = DailyChallenges.For(day, catalog);

            var court = catalog.Court(_request.CourtId) ?? catalog.Court(setup.TeamA.homeCourtId);
            BuildWorld(court, setup);

            var settings = App.Career?.settings;
            _reduceMotion = settings != null && settings.reduceMotion;
            _tapToShoot = settings != null && settings.tapToShoot;
            // 2 Player with no controllers on a touch screen: tabletop mode, each player gets half the screen.
            bool tabletop = Versus && TabletopWanted();
            _controls = TouchControls.Create(_buffer, settings != null && settings.leftHanded, settings != null && settings.largeButtons,
                                             tabletop ? TouchControls.Seat.Bottom : TouchControls.Seat.Full, settings?.controlLayout);
            if (tabletop)
            {
                _controls2 = TouchControls.Create(_buffer2, false, settings != null && settings.largeButtons, TouchControls.Seat.Top);
                _controls2.Call.SetLabel("P&R");
            }
            _controls.Shoot.Pressed += () => _shootTapped = true;
            MatchHud.ReduceMotion = _reduceMotion;
            Audio.AudioManager.PlayMusic(1);
            _controls.Dunk.UnavailableHint = "NO BALL";
            _controls.Layup.UnavailableHint = "NO BALL";
            _controls.Call.UnavailableHint = "OFFENSE";
            _hud = MatchHud.Create(setup.TeamA, setup.TeamB);
            _hud.PlayChosen += play => _pendingCall = play;
            _recorder = new ReplayRecorder(_match.Players.Length, ReplayRecorder.DefaultSeconds, App.SimulationRate);
            _rainbowBall = App.Career != null && Secrets.IsOn(App.Career.secrets, Secrets.RainbowBall);
            _skyHigh = App.Career != null && Secrets.IsOn(App.Career.secrets, Secrets.SkyHigh);
            if (Demo)
            {
                SetTouchVisible(false);
                _demoStartedAt = Time.unscaledTime;
            }
            _hud.ReplayRequested += () => StartReplay(_lastHighlight);
            _hud.PlayOfTheGameRequested += () => StartReplay(_recorder.BestPlay);
            _hud.ShareRequested += ShareHighlight;
            if (settings != null && settings.leftHanded) _hud.SetCallMenuLeft(true);
            _hud.ContinueRequested += Continue;
            _hud.PauseRequested += () => SetPaused(true);
            _hud.ResumeRequested += () => SetPaused(false);
            _hud.QuitRequested += Quit;
            _hud.PhotoRequested += OpenPhotoMode;
            _hud.RematchRequested += Rematch;

            _myCelebration = Flair.CelebrationFor(App.Career?.equippedCelebration);
            _myMove = Flair.DribbleMoveFor(App.Career?.equippedMove);
            Audio.AudioManager.SetAmbience(true);

            SyncViews(0f, snapCamera: true);
            if (!Demo) Sfx(SfxId.TipOff, 0.7f);
            _hud.Toast(_tutorial != null ? "HOW TO PLAY"
                     : _daily != null ? "DAILY: " + _daily.Describe().ToUpperInvariant()
                     : Versus ? "PLAYER 1  VS  PLAYER 2"
                     : FullCourtGame ? "FULL COURT  5 ON 5"
                     : Demo ? "DEMO PLAY"
                     : _horse != null ? "H-O-R-S-E: " + _horse.Name(0) + " SHOOTS FIRST"
                     : _duel != null ? _duel.Intro
                     : _practice != null && _practice.Kind == DrillKind.Shootout ? "BEAT " + _practice.CpuName + ": " + _practice.CpuScore
                     : _practice != null ? DrillTitle(_practice.Kind)
                     : (_request.Mode == GameMode.Practice ? "PRACTICE LAB" : "CHECK BALL"), 1.8f);
        }

        private void BuildWorld(CourtDef court, MatchSetup setup)
        {
            var career = App.Career;
            var banner = Cosmetic(career?.equippedBanner);
            // Full Court draws two halves back to back; everything else is one half court.
            var artGeometry = setup.FullCourt ? FullCourt.HalfGeometry() : setup.Court;
            _art = MatchArt.Build(court, artGeometry, StableHash.Of(court.id), banner?.colorA, banner?.colorB, staticCrowd: false,
                                  fullCourt: setup.FullCourt);
            var world = new GameObject("World").transform;

            var courtGo = new GameObject("Court");
            courtGo.transform.SetParent(world, false);
            var courtSr = courtGo.AddComponent<SpriteRenderer>();
            courtSr.sprite = _art.Court;
            courtSr.sortingOrder = -10000;
            if (_landscape)
            {
                // The full-court art turned a quarter turn: top basket on the left, bottom basket on the right.
                courtGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                courtGo.transform.localPosition = new Vector3(-setup.Court.depth * 0.5f, 0f, 0f);
            }

            var hoopGo = new GameObject("Hoop");
            hoopGo.transform.SetParent(world, false);
            hoopGo.transform.position = CourtSpace.ToWorldSnapped(setup.Court.Hoop, CourtSpace.RimHeight);
            var hoopSr = hoopGo.AddComponent<SpriteRenderer>();
            hoopSr.sprite = _landscape ? _art.HoopSide : _art.Hoop;
            hoopSr.sortingOrder = CourtSpace.SortingOrder(setup.Court.Hoop, -1);
            if (setup.FullCourt)
            {
                // The other basket at the bottom of the court, backboard toward the bottom baseline.
                var bottomHoop = new Vec2(0f, setup.Court.depth - setup.Court.hoopY);
                var hoop2 = new GameObject("Hoop (bottom)");
                hoop2.transform.SetParent(world, false);
                hoop2.transform.position = CourtSpace.ToWorldSnapped(bottomHoop, CourtSpace.RimHeight);
                var hoop2Sr = hoop2.AddComponent<SpriteRenderer>();
                hoop2Sr.sprite = _landscape ? _art.HoopSide : _art.Hoop;
                if (_landscape) hoop2Sr.flipX = true; // backboard on the right
                else hoop2Sr.flipY = true;
                hoop2Sr.sortingOrder = CourtSpace.SortingOrder(bottomHoop, 1);
            }

            _playerViews = new PlayerView[_match.Players.Length];
            _dress = new KitLook[_match.Players.Length];
            var ringColor = new Color(1f, 0.82f, 0.4f, 1f);
            bool contrast = career != null && career.settings.colorblindContrast;
            var filter = career != null ? ColorAccess.Normalize(career.settings.colorFilter) : ColorFilter.Off;
            ShotMeterView.UsePalette(ColorAccess.Meter(filter));
            // Away team switches to its alternate kit when the two jerseys would look alike (for this filter).
            var homeJersey = setup.TeamA.primary;
            if (setup.HumanTeam == 0 && setup.TeamA.id == DefaultContent.PlayerCrewId && !setup.TeamA.customKit && Cosmetic(career?.equippedJersey) != null)
                homeJersey = Cosmetic(career?.equippedJersey).colorA;
            // Kit Studio: your team wears your kit (or your Away kit when it would clash with the opponent).
            KitData myKit = null;
            int kitTeam = CustomTeams.IsYours(setup.TeamA.id) ? 0 : (CustomTeams.IsYours(setup.TeamB.id) ? 1 : -1);
            if (career != null && kitTeam >= 0 && !Demo)
            {
                var other = kitTeam == 0 ? setup.TeamB : setup.TeamA;
                myKit = Kits.ForMatch(career.kits, other.primary, filter, out bool toAway);
                if (myKit != null && toAway && !Demo) _kitNote = "AWAY KIT ON: COLORS CLASHED";
                if (myKit != null && kitTeam == 0) homeJersey = Kits.Unpack(myKit.jersey);
            }
            var awayJersey = setup.TeamB.primary;
            var awayTrim = setup.TeamB.secondary;
            bool awayAlternate = ColorAccess.ResolveAwayKit(homeJersey, ref awayJersey, ref awayTrim, filter);
            var jersey = Cosmetic(career?.equippedJersey);
            var shoes = Cosmetic(career?.equippedShoes);
            for (int i = 0; i < _match.Players.Length; i++)
            {
                var p = _match.Players[i];
                var team = p.Team == 0 ? setup.TeamA : setup.TeamB;
                var primary = team.primary;
                var trim = team.secondary;
                if (p.Team == 1 && awayAlternate)
                {
                    primary = awayJersey;
                    trim = awayTrim;
                }
                // Jersey palettes are for your own crew only.
                if (p.Team == setup.HumanTeam && team.id == DefaultContent.PlayerCrewId && jersey != null && !team.customKit)
                {
                    primary = jersey.colorA;
                    trim = jersey.colorB;
                }
                RgbColor? shoeColor = i == _match.ControlledIndex && shoes != null ? shoes.colorA : (RgbColor?)null;
                var pattern = contrast ? PatternFor(p.Team, setup) : TeamPattern.Solid;
                RgbColor? shorts = null;
                if (team.customKit)
                {
                    // Your created team: its own shorts, shoes, and jersey pattern.
                    shorts = team.shorts;
                    if (shoeColor == null) shoeColor = team.shoes;
                    if (!contrast) pattern = team.pattern;
                }
                // Player 2's ring is cyan so both people can find themselves.
                var ring = i == _match.SecondControlledIndex ? new Color32(0x4C, 0xC9, 0xF0, 255) : (Color32)ringColor;
                KitLook dress;
                if (myKit != null && p.Team == kitTeam)
                {
                    dress = Kits.Look(myKit);
                    if (contrast) dress.Pattern = PatternFor(p.Team, setup); // Team Patterns still wins for readability
                }
                else dress = KitLook.Classic(primary, trim, team.accent, shoeColor, pattern, shorts);
                _dress[i] = dress;
                var frames = _art.PlayerFrames(p.Def, dress);
                _playerViews[i] = PlayerView.Create(world, p, frames, _art, ring);
                if (career != null && Secrets.IsOn(career.secrets, Secrets.BigHeads))
                    _playerViews[i].EnableBigHead(_art.HeadFrames(frames, p.Def.appearance));
            }
            _ballView = BallView.Create(world, _art);
            _meter = ShotMeterView.Create(world, _art);
            _bursts = PixelBursts.Create(world, _art);
            // The stands' fans are drawn for the portrait view; landscape leaves the stands empty.
            if (!_landscape) BuildCrowd(world, court, setup, artGeometry, banner != null);
            else BuildSidelineCrowd(world, court, setup);

            var arrowGo = new GameObject("ReceiverArrow");
            arrowGo.transform.SetParent(world, false);
            _receiverArrow = arrowGo.AddComponent<SpriteRenderer>();
            _receiverArrow.sprite = _art.Arrow;
            _receiverArrow.color = new Color32(0x4C, 0xC9, 0xF0, 255);
            _receiverArrow.sortingOrder = 31500;
            _receiverArrow.enabled = false;

            if (_practice != null) BuildPracticeMarkers(world);
            if (_horse != null)
            {
                var go = new GameObject("HorseSpot");
                go.transform.SetParent(world, false);
                _horseMarker = go.AddComponent<SpriteRenderer>();
                _horseMarker.sprite = _art.Ring;
                _horseMarker.sortingOrder = -9000;
                _horseMarker.enabled = false;
            }

            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            _cameraRig = cam.GetComponent<CourtCameraRig>();
            if (_cameraRig == null) _cameraRig = cam.gameObject.AddComponent<CourtCameraRig>();
            var bg = court.floor.Darken(0.35f);
            _cameraRig.Init(setup.Court, new Color32(bg.r, bg.g, bg.b, 255), setup.FullCourt, _landscape);
        }

        private void Update()
        {
            if (_match == null) return;
            UI.ControllerCursor.Suppressed = !_paused && !_finalShown && !Replaying;
            if (Replaying)
            {
                StepReplay();
                return;
            }
            if (Demo)
            {
                // Attract mode: any touch, key, or button goes back to the title; so does the final buzzer.
                if ((AnyInputPressed() && Time.unscaledTime - _demoStartedAt > 0.5f) || Time.unscaledTime - _demoStartedAt > DemoSeconds)
                {
                    Quit();
                    return;
                }
            }
            else if (_photo != null) return;
            else if (EscapePressed() && !_match.IsOver) SetPaused(!_paused);
            if (_paused) return;
            if (Time.unscaledTime < _hitStopUntil)
            {
                // Hit-stop: hold the frame for a beat on the biggest plays.
                SyncViews(0f, snapCamera: false);
                return;
            }

            UpdatePadMode();
            var input = ReadInput();
            var input2 = Versus ? ReadInput2() : default;
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, FixedStep * MaxStepsPerFrame);
            int steps = 0;
            while (_accumulator >= FixedStep && steps < MaxStepsPerFrame)
            {
                StepMarker.Begin();
                _match.Step(FixedStep, input, input2);
                StepMarker.End();
                CourtSpace.Flip = _match.Flipped;
                _recorder.Capture(_match);
                _practice?.Update(_match, FixedStep);
                _horse?.Update(_match);
                if (_tutorial != null)
                {
                    _tutorial.Update(_match);
                    if (_tutorial.JustAdvanced && !_tutorial.Finished)
                    {
                        _hud.Toast("NICE!  NEXT: " + Loc.T(_tutorial.Title), 1.4f);
                        Sfx(SfxId.Click, 0.8f, 1.4f);
                        Haptics.Light();
                        _nextInfoAt = 0f;
                    }
                }
                HandleEvents();
                if (Time.unscaledTime < _hitStopUntil)
                {
                    _accumulator = 0f;
                    break;
                }
                input2.ShootPressed = false;
                input2.PassPressed = false;
                input2.DefensePressed = false;
                input2.CallPlay = PlayCall.None;
                // Edge-triggered presses apply to one step only.
                input.ShootPressed = false;
                input.PassPressed = false;
                input.DefensePressed = false;
                input.CallPlay = PlayCall.None;
                _accumulator -= FixedStep;
                steps++;
            }
            ViewsMarker.Begin();
            SyncViews(steps * FixedStep, snapCamera: false);
            ViewsMarker.End();

            if (_tutorial != null && _tutorial.Finished && !_finalShown) ShowTutorialEnd();
            else if (_practice != null && _practice.Finished && !_finalShown) ShowPracticeEnd();
            else if (_horse != null && _horse.Finished && !_finalShown) ShowHorseEnd();
            else if (_match.IsOver && !_finalShown)
            {
                if (Demo) Quit();
                else ShowFinal();
            }
        }

        private const float DemoSeconds = 75f;

        /// <summary>2 Player on a touch screen with no controllers connected: split the screen between the two players.</summary>
        private static bool TabletopWanted()
        {
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.all.Count > 0) return false;
            return Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;
#else
            return Application.isMobilePlatform;
#endif
        }

        private void SetTouchVisible(bool visible)
        {
            _controls.SetVisible(visible);
            if (_controls2 != null) _controls2.SetVisible(visible);
        }

        private void UpdatePadMode()
        {
#if ENABLE_INPUT_SYSTEM
            bool was = _padMode;
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.1f || pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
                _padMode = true;
            // A keyboard (Mac, or an iPad keyboard) hides the touch controls too, until the next touch.
            var keys = Keyboard.current;
            if (keys != null && (keys.wKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame
                                 || keys.jKey.wasPressedThisFrame || keys.kKey.wasPressedThisFrame || keys.lKey.wasPressedThisFrame
                                 || keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame))
            {
                _padMode = true;
                _keyMode = true;
            }
            var touch = Pointer.current;
            if (touch != null && touch.press.wasPressedThisFrame && Application.isMobilePlatform) _padMode = _keyMode = false;
            if (Gamepad.all.Count == 0 && !_keyMode) _padMode = false;
            if (was != _padMode && !_paused && !_finalShown && !Demo) SetTouchVisible(!_padMode);
#endif
        }

        /// <summary>Any touch, key, or controller button this frame (ends the attract-mode demo).</summary>
        private static bool AnyInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) return true;
#endif
            return false;
        }

        private PlayerInput ReadInput()
        {
            var move = ScreenToCourt(_controls.ScreenStick);
            bool shootHeld = _controls.Shoot.IsHeld;
#if ENABLE_INPUT_SYSTEM
            // Keyboard / gamepad fallback for the Editor and controllers.
            var kb = Keyboard.current;
            if (kb != null)
            {
                // In 2-player the arrow keys belong to player 2.
                bool arrows = !Versus;
                float x = (kb.dKey.isPressed || arrows && kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || arrows && kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.wKey.isPressed || arrows && kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || arrows && kb.downArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f) move = ScreenToCourt(Vec2.ClampMagnitude(new Vec2(x, y), 1f));
                if (kb.uKey.wasPressedThisFrame) _buffer.Press(ActionButton.Dunk, Time.unscaledTime);
                if (kb.iKey.wasPressedThisFrame) _buffer.Press(ActionButton.Layup, Time.unscaledTime);
                if (kb.jKey.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, Time.unscaledTime);
                if (kb.kKey.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, Time.unscaledTime);
                if (kb.lKey.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, Time.unscaledTime);
                if (kb.cKey.wasPressedThisFrame) _buffer.Press(ActionButton.Call, Time.unscaledTime);
                shootHeld |= kb.kKey.isPressed;
                if (kb.kKey.wasPressedThisFrame) _shootTapped = true;
#if UNITY_EDITOR || DEBUG
                if (kb.bKey.wasPressedThisFrame) _match.KnockLoose(new Vec2(Random.Range(-3f, 3f), Random.Range(1f, 3f)));
#endif
            }
            // 2-player: with two controllers player 1 takes the first; with one, it goes to player 2.
            var pad = Versus ? (Gamepad.all.Count >= 2 ? Gamepad.all[0] : null) : Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f) move = ScreenToCourt(new Vec2(stick.x, stick.y));
                if (pad.rightShoulder.wasPressedThisFrame) _buffer.Press(ActionButton.Dunk, Time.unscaledTime);
                if (pad.leftShoulder.wasPressedThisFrame) _buffer.Press(ActionButton.Layup, Time.unscaledTime);
                if (pad.buttonSouth.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, Time.unscaledTime);
                if (pad.buttonWest.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, Time.unscaledTime);
                if (pad.buttonEast.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, Time.unscaledTime);
                if (pad.buttonNorth.wasPressedThisFrame) _buffer.Press(ActionButton.Call, Time.unscaledTime);
                shootHeld |= pad.buttonSouth.isPressed;
                if (pad.buttonSouth.wasPressedThisFrame) _shootTapped = true;
            }
#endif
            if (_tapToShoot)
            {
                // Accessibility: tap to start the meter, tap again to release.
                bool mine = _match.ChargingIndex == _match.ControlledIndex;
                if (_match.OffenseTeam == _match.Setup.HumanTeam && (_match.HumanHasBall || mine))
                    shootHeld = _tap.Update(_shootTapped, mine);
                else
                {
                    _tap = default;
                    shootHeld = false;
                }
            }
            _shootTapped = false;
            float now = Time.unscaledTime;
            var input = new PlayerInput { Move = move, ShootHeld = shootHeld };

            // Buffered presses are consumed only when they can act, so a tap a moment early still counts.
            bool live = _match.Phase == MatchPhase.Live || _match.Phase == MatchPhase.CheckBall;
            bool offense = _match.OffenseTeam == _match.Setup.HumanTeam;
            if (offense)
            {
                if (live && _match.HumanHasBall && !_match.MustClear && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Shoot, now))
                    input.ShootPressed = true;
                if (live && (_match.HumanHasBall || _match.HumanTeamHasBall) && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Pass, now))
                    input.PassPressed = true;
                bool canFinish = _match.Phase == MatchPhase.Live && _match.HumanHasBall && !_match.MustClear && !_match.MustInbound && _match.ChargingIndex < 0;
                if (canFinish && _buffer.Consume(ActionButton.Dunk, now)) input.DunkPressed = true;
                if (canFinish && _buffer.Consume(ActionButton.Layup, now)) input.LayupPressed = true;
                if (!_match.HumanHasBall) { _buffer.Consume(ActionButton.Dunk, now); _buffer.Consume(ActionButton.Layup, now); }
                _buffer.Consume(ActionButton.Defense, now);
                if (_buffer.Consume(ActionButton.Call, now) && CanCall()) _hud.ShowCallMenu(!_hud.CallMenuOpen);
                if (_pendingCall != PlayCall.None)
                {
                    input.CallPlay = _pendingCall;
                    _pendingCall = PlayCall.None;
                }
            }
            else
            {
                bool liveOnly = _match.Phase == MatchPhase.Live;
                if (_hud.CallMenuOpen) _hud.ShowCallMenu(false);
                _pendingCall = PlayCall.None;
                if (liveOnly && _buffer.Consume(ActionButton.Defense, now)) input.DefensePressed = true;
                if (liveOnly && _buffer.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (liveOnly && _buffer.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                // U / right shoulder steal on defence too.
                if (liveOnly && _buffer.Consume(ActionButton.Dunk, now)) input.DefensePressed = true;
                _buffer.Consume(ActionButton.Layup, now);
                _buffer.Consume(ActionButton.Call, now);
            }
            return input;
        }

        /// <summary>
        /// Player 2 in local 2-player: arrow keys + numpad (1 shoot, 2 pass, 3 steal, 0 pick-and-roll),
        /// or the second controller (the only one if just one is connected). Touch controls stay with player 1.
        /// </summary>
        private PlayerInput ReadInput2()
        {
            var move = Vec2.Zero;
            bool shootHeld = false;
            float now = Time.unscaledTime;
            bool callPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.downArrowKey.isPressed ? 1f : 0f) - (kb.upArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f) move = Vec2.ClampMagnitude(new Vec2(x, y), 1f);
                if (kb.numpad1Key.wasPressedThisFrame || kb.periodKey.wasPressedThisFrame) _buffer2.Press(ActionButton.Shoot, now);
                if (kb.numpad2Key.wasPressedThisFrame || kb.commaKey.wasPressedThisFrame) _buffer2.Press(ActionButton.Pass, now);
                if (kb.numpad3Key.wasPressedThisFrame || kb.slashKey.wasPressedThisFrame) _buffer2.Press(ActionButton.Defense, now);
                if (kb.numpad0Key.wasPressedThisFrame || kb.mKey.wasPressedThisFrame) callPressed = true;
                shootHeld |= kb.numpad1Key.isPressed || kb.periodKey.isPressed;
            }
            var pad = Gamepad.all.Count >= 2 ? Gamepad.all[1] : (Gamepad.all.Count == 1 ? Gamepad.all[0] : null);
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f) move = new Vec2(stick.x, -stick.y);
                if (pad.buttonSouth.wasPressedThisFrame) _buffer2.Press(ActionButton.Shoot, now);
                if (pad.buttonWest.wasPressedThisFrame) _buffer2.Press(ActionButton.Pass, now);
                if (pad.buttonEast.wasPressedThisFrame) _buffer2.Press(ActionButton.Defense, now);
                if (pad.buttonNorth.wasPressedThisFrame) callPressed = true;
                shootHeld |= pad.buttonSouth.isPressed;
            }
#endif
            if (_controls2 != null)
            {
                // Tabletop: player 2's half of the screen.
                var touchMove = ScreenToCourt(_controls2.ScreenStick);
                if (touchMove.x != 0f || touchMove.y != 0f) move = touchMove;
                shootHeld |= _controls2.Shoot.IsHeld;
                if (_buffer2.Consume(ActionButton.Call, now)) callPressed = true;
            }
            int me = _match.SecondControlledIndex;
            var input = new PlayerInput { Move = move, ShootHeld = shootHeld };
            if (me < 0) return input;
            int team = _match.Players[me].Team;
            bool live = _match.Phase == MatchPhase.Live || _match.Phase == MatchPhase.CheckBall;
            bool hasBall = _match.Ball.IsHeld && _match.Ball.HolderIndex == me;
            bool teamHasBall = _match.Ball.IsHeld && _match.Players[_match.Ball.HolderIndex].Team == team;
            if (_match.OffenseTeam == team)
            {
                if (live && hasBall && !_match.MustClear && _match.ChargingIndex < 0 && _buffer2.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (live && teamHasBall && _match.ChargingIndex < 0 && _buffer2.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                bool finish = _match.Phase == MatchPhase.Live && hasBall && !_match.MustClear && !_match.MustInbound && _match.ChargingIndex < 0;
                if (finish && _buffer2.Consume(ActionButton.Dunk, now)) input.DunkPressed = true;
                if (finish && _buffer2.Consume(ActionButton.Layup, now)) input.LayupPressed = true;
                _buffer2.Consume(ActionButton.Defense, now);
                if (callPressed && teamHasBall && _match.Phase == MatchPhase.Live && _match.ActivePlay == PlayCall.None) input.CallPlay = PlayCall.PickAndRoll;
            }
            else if (_match.Phase == MatchPhase.Live)
            {
                if (_buffer2.Consume(ActionButton.Defense, now)) input.DefensePressed = true;
                if (_buffer2.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (_buffer2.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                _buffer2.Consume(ActionButton.Layup, now);
            }
            return input;
        }

        private void HandleEvents()
        {
            int human = _match.Setup.HumanTeam;
            float stop = 0f;
            foreach (var e in _match.Events)
            {
                NoteHighlight(e);
                stop = HitStop.Combine(stop, HitStop.For(e, _match.Ball.ShotType, _reduceMotion));
                switch (e.Type)
                {
                    case MatchEventType.HeatUp:
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.PlayerIndex].Position, 1.2f), new Color32(0xFF, 0x7E, 0x1F, 255), 24, 4f, 0.6f);
                        Sfx(SfxId.HeatUp, 0.9f);
                        if (e.Team == human || Versus) Haptics.Heavy();
                        break;
                    case MatchEventType.Bust:
                        _hud.Toast("BUST! BACK TO " + e.Value, 1.4f);
                        Sfx(SfxId.Error, 0.7f);
                        break;
                    case MatchEventType.SchemeChanged:
                        // The AI adjusts its defence: tell the person it's now facing something new.
                        if (e.Team != human || Versus) _hud.Toast(Loc.T("DEFENSE:") + " " + Loc.T(MatchSimulation.SchemeName((DefenseScheme)e.Value)), 1.4f);
                        break;
                    case MatchEventType.HeatEnded:
                        if (_match.IsHumanControlled(e.PlayerIndex)) _hud.Toast("COOLED OFF", 0.9f);
                        break;
                    case MatchEventType.AlleyOop:
                        Callout("ALLEY-OOP!", SfxId.Stinger);
                        if (!_reduceMotion) _bursts.Spawn(HoopWorld, new Color32(0x4C, 0xC9, 0xF0, 255), 20, 4.5f, 0.6f);
                        _cameraRig.Shake(0.2f);
                        if (e.Team == human || Versus) Haptics.Heavy();
                        break;
                    case MatchEventType.EuroStep:
                        if (_match.IsHumanControlled(e.PlayerIndex)) _hud.Toast("EURO STEP", 0.7f);
                        break;
                    case MatchEventType.DunkToLayup:
                        if (_match.IsHumanControlled(e.PlayerIndex)) _hud.Toast("NO DUNK FROM THERE: LAYUP", 0.9f);
                        break;
                    case MatchEventType.PassThrown:
                        if (_match.AlleyOopInFlight) Sfx(SfxId.Lob, 0.7f);
                        break;
                    case MatchEventType.ShotReleased:
                        _lastShooter = e.PlayerIndex;
                        _shootPoseUntil = Time.unscaledTime + 0.35f;
                        if (_match.Ball.Phase == BallPhase.Shot && (_match.Ball.ShotType == ShotType.Dunk || _match.Ball.ShotType == ShotType.Layup))
                        {
                            _leaper = e.PlayerIndex;
                            _leapStart = Time.unscaledTime;
                            _leapType = _match.Ball.ShotType;
                            _leapDuration = Mathf.Max(0.3f, _match.Ball.FlightDuration + 0.15f);
                            if (_leapType == ShotType.Dunk)
                            {
                                // Your dunk package for you; the AI throws down what its finishing allows.
                                _dunkStyle = e.PlayerIndex == _match.ControlledIndex && !Demo
                                    ? Dunks.ForCosmetic(App.Career?.equippedDunk)
                                    : Dunks.ForAi(_match.Players[e.PlayerIndex].Def.attributes.finishing, Random.value);
                                _leapDuration *= 1f + (Dunks.LiftScale(_dunkStyle) - 1f) * 0.6f; // a bit more hang time
                            }
                        }
                        if (e.PlayerIndex == _match.ControlledIndex)
                        {
                            _tooEarly = e.Value == (int)ShotFeedback.TooEarly;
                            _tooLate = e.Value == (int)ShotFeedback.TooLate;
                            if (e.Value == (int)ShotFeedback.Green) Haptics.Light();
                            _meter.ShowRelease(_match.LastReleaseMeter, _match.Ball.ShotGrade);
                            _hud.Toast(ShotModel.FeedbackText((ShotFeedback)e.Value), 0.9f);
                        }
                        break;
                    case MatchEventType.ShotMade:
                        bool swish = _match.Ball.ShotGrade == TimingGrade.Green;
                        bool dunk = _match.Ball.ShotType == ShotType.Dunk;
                        _hud.Toast((dunk ? Dunks.Name(_dunkStyle) + "  +" : swish ? "SWISH  +" : "+") + e.Value, 1.1f);
                        if (!_reduceMotion) _bursts.Spawn(HoopWorld, swish ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : Color.white, dunk ? 28 : (swish ? 20 : 12), dunk ? 5f : 4f);
                        if (dunk)
                        {
                            _cameraRig.Shake(Dunks.Shake(_dunkStyle) + 0.04f);
                            if (e.Team == human || Versus) Haptics.Heavy();
                        }
                        bool crowdHappy = Versus || e.Team == human;
                        SetCrowd(crowdHappy ? CrowdMood.Cheer : CrowdMood.Groan, crowdHappy ? 1.6f : 1.1f);
                        if (_match.IsHumanControlled(e.PlayerIndex))
                        {
                            int streak = _match.Players[e.PlayerIndex].HotStreak;
                            int heat = _match.Setup.Shot.heatThreshold;
                            if (streak == heat) Callout("HEAT CHECK!", SfxId.OnFire);
                            else if (streak == heat - 1) Callout("HEATING UP", SfxId.Stinger);
                            else if (dunk) { Sfx(SfxId.Stinger, 0.8f); Audio.AudioManager.Voice("SLAM!"); }
                            else if (swish) Sfx(SfxId.Stinger, 0.5f);
                        }
                        _celebrator = e.PlayerIndex;
                        _celebrateStart = Time.unscaledTime;
                        _celebration = e.PlayerIndex == _match.ControlledIndex ? _myCelebration : CelebrationKind.FistPump;
                        Sfx(swish ? SfxId.Swish : SfxId.Rim);
                        Sfx(e.Team == human ? SfxId.CrowdCheer : SfxId.CrowdGroan, 0.6f);
                        if (e.Team == human) Haptics.Success();
                        break;
                    case MatchEventType.ShotMissed:
                        Sfx(SfxId.Rim, 0.8f);
                        break;
                    case MatchEventType.PassCaught:
                        Sfx(SfxId.Bounce, 0.5f, 1.2f);
                        break;
                    case MatchEventType.StealAttempt:
                        Sfx(SfxId.Squeak, 0.6f);
                        break;
                    case MatchEventType.Steal:
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.PlayerIndex].Position, 1f), new Color32(0x4C, 0xC9, 0xF0, 255), 8, 2.5f, 0.4f);
                        _hud.Toast(e.Team == human ? "STEAL!" : "STRIPPED", 1.1f);
                        Sfx(SfxId.Steal);
                        if (e.Team == human) Haptics.Medium();
                        break;
                    case MatchEventType.Block:
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.PlayerIndex].Position, 2f), new Color32(0xF4, 0xF1, 0xDE, 255), 14, 3.5f, 0.5f);
                        _hud.Toast(e.Team == human ? "BLOCKED!" : "SENT BACK", 1.1f);
                        Sfx(SfxId.Block);
                        _cameraRig.Shake(0.18f);
                        Haptics.Medium();
                        break;
                    case MatchEventType.Jump:
                        if (e.PlayerIndex == _match.ControlledIndex) Sfx(SfxId.Squeak, 0.5f, 1.3f);
                        break;
                    case MatchEventType.Switch:
                        if (e.Team == human) _hud.Toast("SWITCH", 0.7f);
                        break;
                    case MatchEventType.Screen:
                        if (e.Team == human) _hud.Toast("SCREEN SET", 0.8f);
                        break;
                    case MatchEventType.PlayCalled:
                        if (e.Team == human) _hud.Toast(PlayName((PlayCall)e.Value), 1.0f);
                        break;
                    case MatchEventType.GameOver:
                        Sfx(SfxId.Buzzer, 0.8f);
                        break;
                    case MatchEventType.Interception:
                        _hud.Toast(e.Team == human ? "PICKED OFF!" : "STOLEN", 1.1f);
                        Sfx(SfxId.Steal, 0.8f);
                        if (e.Team == human) Haptics.Light();
                        break;
                    case MatchEventType.Rebound:
                        if (e.Team == human) _hud.Toast("BOARD!", 0.8f);
                        break;
                    case MatchEventType.PossessionChanged:
                        if (e.Team == human && _match.MustClear) _hud.Toast("TAKE IT BACK", 1.2f);
                        break;
                    case MatchEventType.BallCleared:
                        if (e.Team == human) _hud.Toast("CLEARED", 0.7f);
                        break;
                    case MatchEventType.ShotClockViolation:
                        _hud.Toast("SHOT CLOCK", 1.2f);
                        Sfx(SfxId.Whistle, 0.7f);
                        break;
                    case MatchEventType.Violation:
                        // Full Court: backcourt, 8 seconds, or 5-second inbound.
                        _hud.Toast(MatchSimulation.ViolationName((ViolationKind)e.Value), 1.3f);
                        Sfx(SfxId.Whistle, 0.8f);
                        break;
                    case MatchEventType.AnkleBreaker:
                        // Street rules: the defender stumbles.
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.Value].Position, 0.5f), new Color32(0xFF, 0xD1, 0x66, 255), 12, 3f, 0.5f);
                        _hud.Toast(e.Team == human ? "ANKLES!" : "GOT YOU!", 1.2f);
                        Sfx(SfxId.CrowdCheer, 0.9f);
                        Sfx(SfxId.Squeak, 0.8f, 0.8f);
                        _cameraRig.Shake(0.15f);
                        if (e.Team == human) { Haptics.Medium(); Audio.AudioManager.Voice("OOOH!"); }
                        break;
                    case MatchEventType.Substitution:
                        RedrawPlayer(e.PlayerIndex);
                        if (e.Team == human) _hud.Toast("SUB: " + _match.Players[e.PlayerIndex].Def.lastName.ToUpperInvariant() + " IN", 1.1f);
                        break;
                    case MatchEventType.CheckBall:
                        if (!Demo) _hud.Toast(e.Team == human ? (_match.MustInbound && _match.HumanHasBall ? "INBOUND: PASS IT IN" : "YOUR BALL") : "DEFENSE", 0.9f);
                        break;
                }
            }
            if (stop > 0f) _hitStopUntil = Time.unscaledTime + stop;
        }

        private void SyncViews(float dt, bool snapCamera)
        {
            CourtSpace.Flip = _match.Flipped;
            float now = Time.unscaledTime;
            if (_kitNote != null && _match.Time > 1.9f)
            {
                _hud.Toast(_kitNote, 1.6f);
                _kitNote = null;
            }
            if (_leaper >= 0 && now - _leapStart >= _leapDuration) _leaper = -1;
            SyncCrowd(now);
            var move = UpdateDribbleMove(now);
            var celebrate = _celebrator >= 0 ? Flair.Celebration(_celebration, now - _celebrateStart) : FlairPose.None;
            if (_celebrator >= 0 && now - _celebrateStart >= Flair.CelebrationSeconds) _celebrator = -1;
            for (int i = 0; i < _playerViews.Length; i++)
            {
                bool shooting = i == _match.ChargingIndex || (i == _lastShooter && now < _shootPoseUntil);
                var flair = i == _celebrator ? celebrate : (i == _match.ControlledIndex ? move : FlairPose.None);
                float jump = _match.JumpHeight01(i);
                if (i == _leaper)
                {
                    float leap = Flair.Leap(_leapType, now - _leapStart, _leapDuration);
                    if (_leapType == ShotType.Dunk)
                    {
                        leap *= Dunks.LiftScale(_dunkStyle);
                        if (!_reduceMotion) flair = Dunks.Pose(_dunkStyle, (now - _leapStart) / _leapDuration);
                    }
                    jump = Mathf.Max(jump, leap);
                }
                if (_skyHigh) jump *= 2f; // SKY HIGH secret: looks only
                _playerViews[i].Sync(dt, _match.IsHumanControlled(i), shooting, jump, flair);
            }

            int holderOrder = _match.Holder != null ? CourtSpace.SortingOrder(_match.Holder.Position) : 0;
            var ballOffset = _match.HumanHasBall ? new Vector2Int(move.BallOffsetX, move.BallLift + move.Lift) : Vector2Int.zero;
            _ballView.Sync(_match.Ball, holderOrder, ballOffset);
            if (_rainbowBall) _ballView.Tint(ToColor(ArcadeColors.RainbowAt(now)));
            else _ballView.Tint(_match.Ball.IsHeld && _match.IsHeatedUp(_match.Ball.HolderIndex) ? ToColor(ArcadeColors.FlameAt(now, 1)) : Color.white);
            SpawnFlames(now);

            // Shot meter (human only).
            if (_match.ChargingIndex >= 0 && _match.IsHumanControlled(_match.ChargingIndex))
            {
                var shooter = _match.Players[_match.ChargingIndex];
                _meter.ShowCharging(CourtSpace.ToWorld(shooter.Position), _match.ChargeMeter,
                                    shooter.Def.attributes.shooting, _match.Setup.Shot);
            }
            else
            {
                _meter.Idle();
            }

            // Intended receiver marker.
            int receiver = _match.Phase == MatchPhase.Live && _match.ChargingIndex < 0 ? _match.PreviewPassTarget(ScreenToCourt(_controls.ScreenStick)) : -1;
            _receiverArrow.enabled = receiver >= 0;
            if (receiver >= 0)
            {
                // Gold arrow = this pass will be an alley-oop.
                bool oop = _match.IsAlleyOopTarget(receiver);
                _receiverArrow.color = oop ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : new Color32(0x4C, 0xC9, 0xF0, 255);
                float bob = oop ? Mathf.Round(Mathf.Sin(now * 10f) * 2f) / CourtSpace.PixelsPerUnit : 0f;
                _receiverArrow.transform.position = CourtSpace.ToWorldSnapped(_match.Players[receiver].Position) + new Vector3(0f, 1.75f + bob, 0f);
            }

            // 2-player and Full Court: follow the ball so nobody is left off screen.
            var follow = Versus || FullCourtGame ? _match.Ball.Position : _match.Controlled.Position;
            _cameraRig.Follow(_match.ToWorldCourt(follow), Mathf.Max(dt, Time.unscaledDeltaTime), snapCamera);
            _hud.Sync(_match);
            // The info line builds a string, so refresh it ~10x a second rather than every frame.
            if (Time.unscaledTime >= _nextInfoAt)
            {
                _nextInfoAt = Time.unscaledTime + 0.1f;
                _hud.SetInfo(InfoLine());
            }
            SyncPracticeMarkers();
            SyncHorse();
            UpdateCoachTips();
            UpdateControlLabels();
        }

        /// <summary>HEAT CHECK: embers rise off heated-up players (and the ball they carry).</summary>
        private void SpawnFlames(float now)
        {
            if (_reduceMotion || Core.PowerMonitor.SavingPower || now < _nextFlameAt) return;
            _nextFlameAt = now + 0.07f;
            for (int i = 0; i < _match.Players.Length; i++)
            {
                if (!_match.IsHeatedUp(i)) continue;
                var at = _match.Ball.IsHeld && _match.Ball.HolderIndex == i
                    ? CourtSpace.ToWorldSnapped(_match.Ball.Position, _match.Ball.Height + 0.2f)
                    : CourtSpace.ToWorldSnapped(_match.Players[i].Position, 0.9f);
                _bursts.Spawn(at, ToColor(ArcadeColors.FlameAt(now, i)), 2, 1.3f, 0.32f);
            }
        }

        private static Color ToColor(RgbColor c) => new Color32(c.r, c.g, c.b, c.a);

        private void UpdateControlLabels()
        {
            bool offense = _match.OffenseTeam == _match.Setup.HumanTeam;
            bool oop = offense && _match.HumanHasBall && _match.Phase == MatchPhase.Live && _match.AlleyOopCandidate(_match.ControlledIndex) >= 0;
            string shoot, pass, dunk;
            bool canShoot, canPass, canDunk;
            if (offense && _match.HumanHasBall)
            {
                shoot = _match.MustClear ? "CLEAR" : (_match.MustInbound ? "WAIT" : "SHOOT");
                pass = _match.MustInbound ? "INBOUND" : (oop ? "OOP" : "PASS");
                dunk = "DUNK";
                canShoot = !_match.MustClear && !_match.MustInbound;
                canDunk = canShoot;
                canPass = true;
                _controls.Shoot.UnavailableHint = _match.MustInbound ? "PASS IT IN" : "TAKE IT BACK";
                _controls.Dunk.UnavailableHint = _controls.Layup.UnavailableHint = _controls.Shoot.UnavailableHint;
            }
            else if (offense)
            {
                shoot = "SHOOT";
                pass = "ASK";
                dunk = "DUNK";
                canShoot = false;
                canDunk = false;
                canPass = _match.HumanTeamHasBall;
                _controls.Shoot.UnavailableHint = "NO BALL";
                _controls.Dunk.UnavailableHint = _controls.Layup.UnavailableHint = "NO BALL";
            }
            else
            {
                shoot = "BLOCK";
                pass = "SWITCH";
                dunk = "STEAL";
                canShoot = true;
                canDunk = true;
                canPass = true;
            }
            _controls.Pass.UnavailableHint = "NO BALL";
            bool canCall = CanCall();

            int state = (offense ? 1 : 0) | (_match.HumanHasBall ? 2 : 0) | (_match.MustClear ? 4 : 0) | (_match.HumanTeamHasBall ? 8 : 0) | (canCall ? 16 : 0)
                        | (oop ? 32 : 0) | (_match.MustInbound ? 64 : 0);
            if (state == _lastLabelState) return;
            _lastLabelState = state;
            _controls.SetRole(!offense, oop);
            _controls.SetLabels(shoot, pass, dunk);
            _controls.SetAvailability(canShoot, canPass, canDunk, canDunk, canCall);
            if (_controls2 != null)
            {
                bool p2Offense = _match.SecondControlledIndex >= 0 && _match.OffenseTeam == _match.Players[_match.SecondControlledIndex].Team;
                _controls2.SetRole(!p2Offense, false);
                _controls2.Call.SetLabel("P&R");
            }
        }

        /// <summary>
        /// Plays your equipped dribble move when you cut sharply with the ball (looks only:
        /// speed and handling come from the simulation, not from this).
        /// </summary>
        private FlairPose UpdateDribbleMove(float now)
        {
            if (!_match.HumanHasBall || _match.ChargingIndex >= 0)
            {
                _lastMoveDir = Vec2.Zero;
                return FlairPose.None;
            }
            var dir = _match.Controlled.Motion.velocity;
            if (now >= _moveReadyAt && Flair.IsSharpCut(_lastMoveDir, dir))
            {
                _moveStart = now;
                _moveReadyAt = now + Flair.DribbleMoveCooldown;
                Sfx(SfxId.Squeak, 0.5f, 1.1f);
            }
            if (dir.SqrMagnitude >= 0.25f) _lastMoveDir = dir;
            return Flair.DribbleMove(_myMove, now - _moveStart);
        }

        private bool CanCall() =>
            _practice == null && _horse == null && _match.OffenseTeam == _match.Setup.HumanTeam && _match.HumanTeamHasBall
            && !_match.MustClear && _match.Phase == MatchPhase.Live && _match.ActivePlay == PlayCall.None;

        private string InfoLine()
        {
            if (_tutorial != null && !_tutorial.Finished)
            {
                string touch = Loc.T(_tutorial.TouchHint);
                string hint = Application.isMobilePlatform ? touch : touch + "  (" + _tutorial.KeyHint + ")";
                return "STEP " + ((int)_tutorial.Step + 1) + "/" + TutorialSession.StepCount + " · " + Loc.T(_tutorial.Title) + " — " + hint.ToUpperInvariant();
            }
            if (Versus && _match.Time < 10f)
                return "P1: WASD · K SHOOT · J PASS · L STEAL · C CALL     P2: ARROWS · NUM1 SHOOT · NUM2 PASS · NUM3 STEAL · NUM0 CALL";
            if (_horse != null)
            {
                string turn = _horse.IsCpuTurn ? _horse.Name(1) + " IS SHOOTING..."
                            : (_horse.Opponent == HorseOpponent.Friend ? _horse.Name(_horse.Shooter) + ": " : "") + (_horse.Matching ? "MATCH THE SHOT (GOLD RING)" : "SET A SHOT");
                return _horse.Name(0) + " " + Pad(_horse.LettersOf(0)) + "  ·  " + _horse.Name(1) + " " + Pad(_horse.LettersOf(1)) + "  ·  " + turn;
            }
            if (_practice != null)
            {
                string time = _practice.TimeLimit > 0f ? Mathf.CeilToInt(_practice.TimeLeft) + "s  ·  " : _practice.Elapsed.ToString("0.0") + "s  ·  ";
                return time + _practice.ResultText().ToUpperInvariant();
            }
            // On a computer, show the keyboard controls for the first seconds of a match.
            if (Demo) return "DEMO PLAY  ·  TAP OR PRESS ANY BUTTON";
            if (_padMode && !_keyMode && _match.Time < 8f)
                return "STICK MOVE · A SHOOT (HOLD) · X PASS · RB DUNK · LB LAYUP · B STEAL · Y CALL";
            if ((_keyMode || !Application.isMobilePlatform || Core.DeviceInfo.IsMac) && _match.Time < 8f)
                return "WASD MOVE · K SHOOT (HOLD) · J PASS · U DUNK · I LAYUP · L STEAL · C CALL";
            if (_match.ActivePlay != PlayCall.None && _match.ActivePlayTeam == _match.Setup.HumanTeam) return PlayName(_match.ActivePlay);
            if (_match.IsBoxingOut(_match.ControlledIndex)) return "BOX OUT";
            if (_daily != null) return "DAILY: " + _daily.Describe().ToUpperInvariant();
            return "";
        }

        private static string PlayName(PlayCall play)
        {
            switch (play)
            {
                case PlayCall.PickAndRoll: return "PICK & ROLL";
                case PlayCall.GiveAndGo: return "GIVE & GO";
                case PlayCall.ClearOut: return "CLEAR OUT";
                default: return "";
            }
        }

        private static string DrillTitle(DrillKind kind)
        {
            switch (kind)
            {
                case DrillKind.FreeShoot: return "FREE SHOOT";
                case DrillKind.PassingTargets: return "PASSING TARGETS";
                case DrillKind.ThreePoint: return "3-POINT CONTEST";
                case DrillKind.Lockdown: return "LOCKDOWN";
                case DrillKind.Shootout: return "SHOOTOUT";
                case DrillKind.AroundTheWorld: return "AROUND THE WORLD";
                case DrillKind.Horse: return "H-O-R-S-E";
                default: return "DRIBBLE LANE";
            }
        }

        private void ShowFinal()
        {
            _finalShown = true;
            SetTouchVisible(false);
            var s = _match.Setup;
            int human = s.HumanTeam;
            string title = _match.Winner < 0 ? "TIE" : (_match.Winner == human ? "YOU WIN" : "FINAL");

            if (_request.Mode == GameMode.Practice || _request.Mode == GameMode.Tutorial)
            {
                string line = s.TeamA.abbreviation + "  " + _match.Score[0] + "  -  " + _match.Score[1] + "  " + s.TeamB.abbreviation;
                _hud.ShowFinal(title, line);
                return;
            }

            if (Versus)
            {
                // Local 2-player: box score, no rewards (only counted for a badge and an achievement).
                var vs = MatchSummary.From(_match, _request.Mode, "versus");
                if (App.Career != null && !_resultApplied)
                {
                    _resultApplied = true;
                    App.Career.totals.versusGames++;
                    App.SaveCareer();
                    App.ReportGameCenter();
                }
                _hud.HasPlayOfTheGame = _recorder.BestPlay != null;
                string winner = _match.Winner < 0 ? "TIE" : (_match.Winner == 0 ? "PLAYER 1 WINS" : "PLAYER 2 WINS");
                Haptics.Success();
                _hud.ShowPostGame(winner, vs, default, false, null, null, true);
                return;
            }

            string matchId = string.IsNullOrEmpty(_request.ContextId)
                ? System.Guid.NewGuid().ToString("N")
                : _request.ContextId + "#" + _request.Seed;
            var summary = MatchSummary.From(_match, _request.Mode, matchId);
            summary.day = App.Today;
            summary.isPlayoff = _request.Round >= 1;
            summary.isFinal = _request.Round >= 2;
            var grant = Rewards.For(summary, App.Rewards);

            string note = null;
            bool rewarded = false;
            if (App.Career != null && !_resultApplied)
            {
                _resultApplied = true;
                rewarded = Career.ApplyMatch(App.Career, summary, grant);
                int recruitsBefore = App.Career.rise.recruitable.Count;
                if (rewarded && _request.Mode == GameMode.Rival)
                {
                    var rivalOutcome = RivalEngine.ApplyResult(App.Career, summary);
                    note = rivalOutcome == RivalOutcome.Won
                        ? "RIVAL BEATEN!  +" + RivalEngine.WinBonus + " SP  +" + RivalEngine.WinFans + " FANS"
                        : "They win this one. They'll be back next season.";
                    if (rivalOutcome == RivalOutcome.Won) title = "RIVAL DOWN";
                }
                if (rewarded && _request.Mode == GameMode.Rise)
                {
                    var outcome = RiseEngine.ApplyResult(App.Career.rise, App.Catalog, summary, App.Career);
                    App.LastRiseOutcome = outcome;
                    note = OutcomeText(outcome);
                    if (outcome == RiseOutcome.Champion) title = "CHAMPIONS";
                }
                if (rewarded && _daily != null)
                {
                    if (DailyChallenges.IsMet(_daily, summary))
                    {
                        int bonus = DailyChallenges.Complete(App.Career.daily, App.Career, _daily.Day);
                        note = bonus > 0
                            ? "DAILY COMPLETE!  +" + bonus + " SP  ·  STREAK " + App.Career.daily.streak
                            : "Daily already done today. Come back tomorrow!";
                        if (bonus > 0) title = "DAILY DONE";
                    }
                    else
                    {
                        note = "Daily goal: " + _daily.Describe() + ". Not this time. Rematch?";
                    }
                }
                if (rewarded && _request.Mode == GameMode.King)
                {
                    var king = KingEngine.ApplyResult(App.Career.king, App.Catalog, summary, App.Career, out int kingBonus);
                    App.OpenKingOnMenu = true;
                    if (king == KingOutcome.Defended)
                    {
                        title = "STILL KING";
                        note = "STREAK " + App.Career.king.streak + "  ·  +" + kingBonus + " SP BONUS";
                    }
                    else if (king == KingOutcome.Dethroned)
                    {
                        title = "DETHRONED";
                        note = "Run over at " + App.Career.king.streak + " (best " + App.Career.king.best + ").";
                    }
                }
                if (rewarded && _request.Mode == GameMode.Arcade)
                {
                    var arcade = ArcadeEngine.ApplyResult(App.Career, summary, out int arcadeBonus);
                    var a = App.Career.secrets.arcade;
                    App.OpenArcadeOnMenu = true;
                    switch (arcade)
                    {
                        case ArcadeOutcome.Advanced:
                            title = "STAGE CLEAR";
                            note = "Next: stage " + (a.rung + 1) + " of " + ArcadeEngine.Rungs + (ArcadeEngine.IsBossRung(a.rung) ? "  ·  THE BOSS" : "");
                            break;
                        case ArcadeOutcome.Continue:
                            title = "CONTINUE?";
                            note = "Continues left: " + a.continues + ". Try the stage again.";
                            Sfx(SfxId.Coin, 0.7f);
                            break;
                        case ArcadeOutcome.GameOver:
                            title = "GAME OVER";
                            note = "Reached stage " + (a.rung + 1) + " of " + ArcadeEngine.Rungs + ".";
                            Audio.AudioManager.Voice("GAME OVER!");
                            break;
                        case ArcadeOutcome.Cleared:
                            title = "LADDER CLEARED";
                            note = "THE GLITCH and its court are unlocked!  +" + arcadeBonus + " SP";
                            Sfx(SfxId.Fanfare);
                            break;
                    }
                }
                if (rewarded && _request.Mode == GameMode.Cup)
                {
                    var cupOutcome = CupEngine.ApplyResult(App.Career.cup, App.Catalog, summary, App.Career, out int cupBonus);
                    App.OpenCupOnMenu = true;
                    var nextCup = CupEngine.NextGame(App.Career.cup);
                    if (cupOutcome == CupOutcome.Champion)
                    {
                        title = "CUP CHAMPIONS";
                        note = "CALLER CUP CHAMPIONS!  +" + cupBonus + " SP";
                        Sfx(SfxId.Fanfare);
                    }
                    else if (cupOutcome == CupOutcome.Advanced && nextCup != null)
                        note = "Next: " + CupEngine.RoundName(nextCup.round);
                    else if (cupOutcome == CupOutcome.Eliminated)
                        note = "Knocked out. Champion: " + (App.Catalog.Team(App.Career.cup.championId)?.FullName ?? "?");
                }
                if (rewarded && _request.Mode == GameMode.Legacy)
                {
                    var lg = App.Career.legacy;
                    var stageBefore = lg.stage;
                    var lgLine = Legacy.RecordGame(lg, App.Catalog, summary.HumanScore, summary.OpponentScore, summary.HumanLine?.stats);
                    App.OpenLegacyOnMenu = true;
                    if (lgLine != null)
                    {
                        note = "GRADE " + lgLine.grade + "  ·  " + Legacy.Level(lg) + " LV  ·  " + lg.skillPoints + " SKILL POINTS";
                        if (lg.stage != stageBefore || (lg.stage == LegacyStage.Pro && lg.season == null))
                        {
                            var h = lg.history.Count > 0 ? lg.history[lg.history.Count - 1] : null;
                            if (h != null)
                            {
                                title = h.result == "CHAMPIONS" ? "CHAMPIONS" : title;
                                note += "\n" + h.label + " OVER: " + h.result + (h.awards.Count > 0 ? "  ·  " + string.Join(", ", h.awards) : "");
                            }
                        }
                    }
                }
                var street = Street.FromContext(_request.ContextId);
                if (rewarded && street != null)
                {
                    int ankles = summary.HumanLine?.stats?.ankleBreakers ?? 0;
                    int rep = Street.ApplyResult(App.Career.street, street, summary.HumanWon, ankles);
                    App.OpenParkOnMenu = true;
                    title = summary.HumanWon ? "YOU RUN THE PARK" : street.Nickname + " WINS";
                    note = (rep >= 0 ? "+" : "") + rep + " REP  ·  " + Street.RepNames[Street.RepLevel(App.Career.street.rep)]
                           + (ankles > 0 ? "  ·  " + ankles + " ANKLE BREAKER" + (ankles == 1 ? "" : "S") : "");
                }
                if (rewarded && _request.Mode == GameMode.CustomCup)
                {
                    var cup = App.Career.customCup;
                    var outcome = CustomCup.ApplyResult(cup, App.Catalog, summary.HumanScore, summary.OpponentScore);
                    App.OpenCustomCupOnMenu = true;
                    var nextCup = CustomCup.NextGame(cup);
                    if (outcome == CustomCupOutcome.Champion) { title = cup.name.ToUpperInvariant() + " CHAMPIONS"; Sfx(SfxId.Fanfare); }
                    else if (outcome == CustomCupOutcome.Advanced && nextCup != null) note = "Next: " + CustomCup.RoundName(cup, nextCup.round);
                    else if (outcome == CustomCupOutcome.Eliminated) note = "Knocked out. Champion: " + (App.Catalog.Team(cup.championId)?.FullName ?? "?");
                }
                if (rewarded && _request.Mode == GameMode.AllStar)
                {
                    int bonus = AllStar.ApplyGame(App.Career, summary);
                    title = summary.HumanWon ? "ALL-STAR WIN" : "ALL-STAR GAME";
                    note = (summary.HumanWon ? "Team Sunrise takes it." : "Team Moonlight takes it.") + (bonus > 0 ? "  +" + bonus + " SP" : "");
                }
                if (rewarded && _request.Mode == GameMode.Franchise)
                {
                    var fr = App.Career.franchise;
                    var frGame = Franchise.NextGame(fr);
                    int frRound = frGame != null ? frGame.round : 0;
                    App.OpenFranchiseOnMenu = true;
                    if (Franchise.RecordYourGame(fr, App.Catalog, summary.HumanScore, summary.OpponentScore))
                    {
                        string champ = Franchise.ChampionId(fr);
                        if (frRound == 2 && champ == Franchise.TeamId(fr.you))
                        {
                            title = "CHAMPIONS";
                            note = "FRANCHISE CHAMPIONS! Year " + (fr.year - 1) + " is yours.";
                        }
                        else if (fr.phase == FranchisePhase.Playoffs && frRound == 0)
                            note = Franchise.NextGame(fr) != null ? "Regular season over: you're in the playoffs!" : "Regular season over. You missed the playoffs.";
                        else if (frRound == 0)
                            note = "Record: " + SeasonEngine.Standings(fr.season).Find(r => r.TeamId == Franchise.TeamId(fr.you))?.Wins + "-"
                                   + SeasonEngine.Standings(fr.season).Find(r => r.TeamId == Franchise.TeamId(fr.you))?.Losses;
                        else if (frRound == 1)
                            note = summary.HumanWon ? "On to the final!" : "Knocked out in the semifinal.";
                        else note = "Runners-up. The off-season starts in the front office.";
                    }
                }
                if (rewarded && _request.Mode == GameMode.Tournament)
                {
                    var outcome = ClassicEngine.ApplyResult(App.Career.classic, App.Catalog, summary);
                    App.OpenClassicOnMenu = true;
                    note = ClassicText(outcome);
                    if (outcome == ClassicOutcome.Champion) title = "CLASSIC CHAMPS";
                }
                // Weekly Challenges and the Hoops Pass (every counted game; the daily adds its own pass XP once a day).
                if (rewarded)
                {
                    var weekly = Weekly.ApplyGame(App.Career, App.Catalog, summary, App.Today, street != null);
                    if (_daily != null && DailyChallenges.CompletedToday(App.Career.daily, _daily.Day))
                        weekly.PassRewards.AddRange(HoopsPass.AddDailyXp(App.Career, App.Catalog, App.Today));
                    string weeklyNote = weekly.Note();
                    if (weeklyNote != null)
                    {
                        note = string.IsNullOrEmpty(note) ? weeklyNote : note + "\n" + weeklyNote;
                        Sfx(SfxId.Coin, 0.8f);
                    }
                }
                if (App.Career.rise.recruitable.Count > recruitsBefore)
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + "New players can join your crew (Rise hub ▸ YOUR CREW).";
                var hints = Secrets.RevealHints(App.Career);
                if (hints.Count > 0)
                {
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + "SECRET CODE HINT FOUND! (Locker Room ▸ TROPHY)";
                    Sfx(SfxId.Coin, 0.8f);
                }
                var badges = Badges.TakeNew(App.Career);
                if (badges.Count > 0)
                {
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + "BADGE: " + string.Join(", ", badges.ConvertAll(b => b.Title));
                    Sfx(SfxId.Fanfare, 0.7f);
                }
                var records = App.Career.lastNewRecords;
                if (rewarded && records != null && records.Count > 0)
                {
                    string line = "NEW RECORD: " + string.Join(", ", records);
                    note = string.IsNullOrEmpty(note) ? line : note + "\n" + line;
                    Sfx(SfxId.Fanfare, 0.8f);
                }
                App.SaveCareer();
                App.ReportGameCenter();
            }
            if (summary.HumanWon) Haptics.Success();
            Sfx(summary.HumanWon ? SfxId.Victory : SfxId.Defeat, 0.8f);
            if (title == "CHAMPIONS" || title == "CLASSIC CHAMPS") Sfx(SfxId.Fanfare);

            // Rise and the Classic continue their run instead of offering a rematch.
            _hud.HasPlayOfTheGame = _recorder.BestPlay != null;
            bool run = IsRun;
            _hud.ShowPostGame(title, summary, grant, rewarded, note, run ? "CONTINUE" : null, !run);
        }

        private static string OutcomeText(RiseOutcome o)
        {
            switch (o)
            {
                case RiseOutcome.CircuitWin: return "Crew beaten. The Blacktop Circuit continues.";
                case RiseOutcome.CircuitLoss: return "They held their court. Run it back from the Rise hub.";
                case RiseOutcome.EnteredLeague: return "Circuit cleared! The Caller League wants you.";
                case RiseOutcome.MadePlayoffs: return "PLAYOFFS! The Gold Signal Cup is in reach.";
                case RiseOutcome.MissedPlayoffs: return "Season over: missed the playoffs.";
                case RiseOutcome.AdvancedToFinal: return "On to the final!";
                case RiseOutcome.Eliminated: return "Eliminated. Next season starts from the Rise hub.";
                case RiseOutcome.Champion: return "GOLD SIGNAL CUP CHAMPIONS!";
                default: return null;
            }
        }

        private static string ClassicText(ClassicOutcome o)
        {
            switch (o)
            {
                case ClassicOutcome.WonSemi: return "Semifinal won! The " + DefaultContent.RookieTournamentName + " final is next.";
                case ClassicOutcome.Champion: return DefaultContent.RookieTournamentName.ToUpperInvariant() + " CHAMPIONS!";
                case ClassicOutcome.Eliminated: return "Knocked out. Enter the next Classic from the main menu.";
                default: return null;
            }
        }

        private void ShowTutorialEnd()
        {
            _finalShown = true;
            SetTouchVisible(false);
            int reward = App.Career != null ? Career.CompleteTutorial(App.Career) : 0;
            if (App.Career != null) Badges.TakeNew(App.Career);
            if (App.Career != null)
            {
                App.SaveCareer();
                App.ReportGameCenter();
            }
            Haptics.Success();
            Sfx(SfxId.CrowdCheer, 0.7f);
            _hud.ShowPracticeEnd("TUTORIAL COMPLETE", reward > 0 ? "+" + reward + " SP · YOU'RE READY" : "YOU'RE READY TO PLAY", false);
        }

        /// <summary>Coach Dee's one-time tips during your first games (normal games only).</summary>
        private void UpdateCoachTips()
        {
            if (Time.unscaledTime < _nextTipCheck || App.Career == null) return;
            _nextTipCheck = Time.unscaledTime + 0.25f;
            if (_practice != null || _horse != null || _tutorial != null || Demo || Versus || _match.IsOver) return;
            int me = _match.ControlledIndex;
            int handler = _match.HolderIndex;
            bool onDefense = _match.OffenseTeam != _match.Setup.HumanTeam;
            int target = _match.HumanHasBall ? _match.PreviewPassTarget(ScreenToCourt(_controls.ScreenStick)) : -1;
            var s = new TipSituation
            {
                Live = _match.Phase == MatchPhase.Live,
                HumanHasBall = _match.HumanHasBall,
                OnDefense = onDefense,
                MustClear = _match.MustClear,
                AlleyOopOpen = target >= 0 && _match.IsAlleyOopTarget(target),
                HumanHotStreak = _match.Players[me].HotStreak,
                HeatThreshold = _match.Setup.Shot.heatThreshold,
                LastShotTooEarly = _tooEarly,
                LastShotTooLate = _tooLate,
                NearBallHandler = onDefense && handler >= 0 && Vec2.Distance(_match.Players[me].Position, _match.Players[handler].Position) < 2f,
                ShotClock = _match.Setup.Rules.shotClockSeconds < 99f ? _match.ShotClock : 0f,
                MatchTime = _match.Time,
            };
            _tooEarly = _tooLate = false;
            var tip = CoachTips.Next(App.Career, s, Time.unscaledTime, ref _lastTipAt);
            if (tip == null) return;
            _hud.Toast(Loc.T("COACH:") + " " + Loc.T(tip.Text), 3.2f);
            Sfx(SfxId.Click, 0.6f, 1.3f);
        }

        private static string Pad(string letters) => letters.Length == 0 ? "-" : letters;

        private void SyncHorse()
        {
            if (_horse == null) return;
            if (_horse.Version != _horseSeen)
            {
                _horseSeen = _horse.Version;
                if (!string.IsNullOrEmpty(_horse.LastCall))
                {
                    _hud.Toast(_horse.LastCall, 1.6f);
                    Sfx(_horse.LastCall.Contains("GETS") ? SfxId.Whistle : SfxId.Click, 0.7f);
                }
            }
            bool show = _horse.Matching || _horse.IsCpuTurn;
            _horseMarker.enabled = show;
            if (show)
            {
                var at = _horse.Matching ? _horse.SpotToMatch : _horse.LastCpuSpot;
                _horseMarker.transform.position = CourtSpace.ToWorldSnapped(at);
                _horseMarker.color = new Color32(0xFF, 0xD1, 0x66, 255);
            }
        }

        private void ShowHorseEnd()
        {
            _finalShown = true;
            SetTouchVisible(false);
            bool youWon = _horse.Winner == 0;
            if (App.Career != null && _horse.Opponent == HorseOpponent.Cpu)
            {
                Career.RecordPractice(App.Career, 0, 0, 0, 0f, 0, 0, false, 0f, youWon);
                App.SaveCareer();
                App.ReportGameCenter();
            }
            if (youWon || _horse.Opponent == HorseOpponent.Friend) Sfx(SfxId.Fanfare, 0.8f);
            Audio.AudioManager.Voice("H-O-R-S-E!");
            _hud.ShowPracticeEnd(_horse.Name(_horse.Winner) + " WINS",
                                 _horse.Name(0) + " " + Pad(_horse.LettersOf(0)) + "  ·  " + _horse.Name(1) + " " + Pad(_horse.LettersOf(1)), youWon);
        }

        private void ShowPracticeEnd()
        {
            _finalShown = true;
            SetTouchVisible(false);
            if (_duel != null)
            {
                ShowDuelEnd();
                return;
            }
            if (IsThreeContest)
            {
                ShowThreeContestEnd();
                return;
            }
            bool best = false;
            if (App.Career != null)
            {
                var p = _practice;
                best = Career.RecordPractice(App.Career,
                    p.Kind == DrillKind.FreeShoot ? p.Makes : 0,
                    p.Kind == DrillKind.FreeShoot ? p.BestStreak : 0,
                    p.Kind == DrillKind.PassingTargets ? p.PassScore : 0,
                    p.Kind == DrillKind.DribbleLane && p.Finished ? p.CourseTime : 0f,
                    p.Kind == DrillKind.ThreePoint ? p.ContestPoints : 0,
                    p.Kind == DrillKind.Lockdown ? p.Stops : 0,
                    p.ShootoutWon,
                    p.Kind == DrillKind.AroundTheWorld && p.Finished ? p.CourseTime : 0f);
                App.SaveCareer();
                App.ReportGameCenter();
            }
            if (best) Haptics.Success();
            Sfx(SfxId.Whistle, 0.7f);
            string endTitle = _practice.Kind == DrillKind.Shootout ? (_practice.ShootoutWon ? "YOU WIN THE SHOOTOUT" : "CPU WINS") : DrillTitle(_practice.Kind);
            if (_practice.ShootoutWon) Sfx(SfxId.Fanfare, 0.8f);
            _hud.ShowPracticeEnd(endTitle, _practice.ResultText().ToUpperInvariant(), best);
        }

        private bool IsThreeContest => _request != null && _request.ContextId != null
                                       && _request.ContextId.StartsWith(AllStar.ThreeContext, System.StringComparison.Ordinal)
                                       && App.Career != null && App.Career.allStar.contest.kind == "three";

        /// <summary>All-Star 3-Point Contest: record the round, then back to the contest.</summary>
        private void ShowThreeContestEnd()
        {
            var contest = App.Career.allStar.contest;
            int round = contest.round;
            AllStar.RecordThrees(contest, App.Catalog, _practice.ContestPoints);
            App.SaveCareer();
            Sfx(SfxId.Whistle, 0.7f);
            var me = AllStar.You(contest);
            string title;
            if (contest.round >= 3) title = contest.youWon ? "3-POINT CHAMPION!" : contest.championName.ToUpperInvariant() + " WINS IT";
            else title = round == 1 && AllStar.InFinal(contest, me) ? "YOU'RE IN THE FINAL" : "ROUND DONE";
            if (contest.youWon) { Sfx(SfxId.Fanfare, 0.8f); Haptics.Success(); }
            App.OpenAllStar = true;
            _hud.ShowPracticeEnd(title, _practice.ContestPoints + " POINTS", false, "CONTINUE");
        }

        /// <summary>Pass-and-play Shootout: hand over to P2 after round one, or show who won.</summary>
        private void ShowDuelEnd()
        {
            _duel.Record(_practice.ContestPoints);
            Sfx(SfxId.Whistle, 0.7f);
            if (!_duel.Finished)
            {
                _hud.ShowPracticeEnd("P1: " + _duel.Points[0] + " POINTS", "PASS THE PHONE TO P2", false, "P2: GO");
                return;
            }
            Sfx(SfxId.Fanfare, 0.8f);
            Haptics.Success();
            if (App.Career != null)
            {
                App.Career.totals.versusGames++;
                App.SaveCareer();
                App.ReportGameCenter();
            }
            _hud.ShowPracticeEnd(_duel.ResultTitle, _duel.ResultLine, false, "NEW DUEL");
        }

        // ------------------------------------------------------------------ practice markers

        private void BuildPracticeMarkers(Transform world)
        {
            // Dribble Lane cones, or the 3-Point Contest's money-ball spots.
            var spots = _practice.ThreePointStyle ? _practice.MoneySpots
                      : _practice.Kind == DrillKind.AroundTheWorld ? _practice.WorldSpots : _practice.Cones;
            _cones = new SpriteRenderer[spots.Count];
            for (int i = 0; i < _cones.Length; i++)
            {
                var go = new GameObject("Cone " + i);
                go.transform.SetParent(world, false);
                go.transform.position = CourtSpace.ToWorldSnapped(spots[i]);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _art.Ring;
                sr.sortingOrder = -9000;
                _cones[i] = sr;
            }
            if (_practice.Kind == DrillKind.PassingTargets)
            {
                var go = new GameObject("TargetArrow");
                go.transform.SetParent(world, false);
                _targetArrow = go.AddComponent<SpriteRenderer>();
                _targetArrow.sprite = _art.Arrow;
                _targetArrow.color = new Color32(0xFF, 0xD1, 0x66, 255);
                _targetArrow.sortingOrder = 31400;
            }
        }

        private void SyncPracticeMarkers()
        {
            if (_practice == null) return;
            if (_cones != null)
            {
                for (int i = 0; i < _cones.Length; i++)
                {
                    if (_practice.Kind == DrillKind.AroundTheWorld)
                    {
                        // Done spots green, the current one gold, the rest faint.
                        _cones[i].color = i < _practice.WorldSpot ? new Color(0.3f, 0.9f, 0.5f, 0.5f)
                                        : i == _practice.WorldSpot ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : new Color(1f, 1f, 1f, 0.25f);
                        continue;
                    }
                    if (_practice.ThreePointStyle)
                    {
                        // The money-ball spot glows gold; the rest are faint.
                        _cones[i].color = i == _practice.MoneySpot ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : new Color(1f, 1f, 1f, 0.25f);
                        continue;
                    }
                    bool done = i < _practice.NextCone;
                    bool next = i == _practice.NextCone;
                    _cones[i].color = done ? new Color(0.3f, 0.9f, 0.5f, 0.5f)
                                          : (next ? new Color32(0xFF, 0x8C, 0x42, 255) : new Color(1f, 0.55f, 0.26f, 0.45f));
                }
            }
            if (_targetArrow != null)
            {
                int t = _practice.TargetPlayer;
                _targetArrow.enabled = t >= 0;
                if (t >= 0)
                {
                    float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 2f) / CourtSpace.PixelsPerUnit;
                    _targetArrow.transform.position = CourtSpace.ToWorldSnapped(_match.Players[t].Position) + new Vector3(0f, 2.1f + bob, 0f);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        // ------------------------------------------------------------------ replay

        /// <summary>Big plays by a person (dunks, greens, blocks, steals) can be replayed.</summary>
        private void NoteHighlight(MatchEvent e)
        {
            if (_practice != null || _horse != null || _tutorial != null || Demo || e.PlayerIndex < 0) return;
            // Alley-oops count if either end (passer or finisher) is a person; the clip is saved when the dunk lands.
            if (e.Type == MatchEventType.AlleyOop && (_match.IsHumanControlled(e.PlayerIndex) || _match.IsHumanControlled(e.Value)))
            {
                _oopFinisher = e.PlayerIndex;
                return;
            }
            if (e.Type == MatchEventType.ShotMade && e.PlayerIndex == _oopFinisher)
            {
                _oopFinisher = -1;
                _recorder.OfferBestPlay("ALLEY-OOP!", 95);
                _lastHighlight = _recorder.Snapshot(3.5f, "ALLEY-OOP!", 95);
                _hud.OfferReplay(3f);
                return;
            }
            if (e.Type == MatchEventType.ShotMissed || e.Type == MatchEventType.Block) _oopFinisher = -1;
            if (!_match.IsHumanControlled(e.PlayerIndex)) return;
            int score = ReplayRecorder.PlayScore(e.Type, _match.Ball.ShotType, _match.Ball.ShotGrade, e.Type == MatchEventType.ShotMade ? e.Value : 0);
            if (score <= 0) return;
            string label = e.Type == MatchEventType.Block ? "BLOCKED!" : e.Type == MatchEventType.Steal ? "STEAL!"
                         : _match.Ball.ShotType == ShotType.Dunk ? "SLAM!" : "SWISH";
            _recorder.OfferBestPlay(label, score);
            if (score >= 50)
            {
                _lastHighlight = _recorder.Snapshot(3.5f, label, score);
                _hud.OfferReplay(3f);
            }
        }

        private int _oopFinisher = -1;

        private void StartReplay(ReplayClip clip)
        {
            if (clip == null || clip.Frames.Count < 2) return;
            _replay = clip;
            _replayT = 0f;
            SetTouchVisible(false);
            _hud.ShowCallMenu(false);
            _hud.ShowReplayOverlay(true);
        }

        private void StepReplay()
        {
            // Replays are recorded as drawn on the fixed court.
            CourtSpace.Flip = false;
            float dt = Time.unscaledDeltaTime;
            _replayT += dt * ReplaySpeed;
            bool skip = false;
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            skip = pointer != null && pointer.press.wasPressedThisFrame && _replayT > 0.3f;
            var kb = Keyboard.current;
            skip |= kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
            var gp = Gamepad.current;
            skip |= gp != null && _replayT > 0.3f && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame);
#endif
            if (skip || _replayT > _replay.Duration + 0.4f)
            {
                EndReplay();
                return;
            }
            var f = ReplayRecorder.FrameAt(_replay, _replayT);
            for (int i = 0; i < _playerViews.Length && i < f.Players.Length; i++)
                _playerViews[i].SyncReplay(dt * ReplaySpeed, f.Players[i], _match.IsHumanControlled(i));
            _replayBall.Phase = f.BallPhase;
            _replayBall.HolderIndex = f.BallHolder;
            _replayBall.Position = f.BallPosition;
            _replayBall.Height = f.BallHeight;
            int holderOrder = f.BallHolder >= 0 ? CourtSpace.SortingOrder(f.Players[f.BallHolder].Position) : 0;
            _ballView.Sync(_replayBall, holderOrder);
            _meter.Idle();
            _receiverArrow.enabled = false;
            _cameraRig.Follow(f.BallPosition, dt, false);
        }

        /// <summary>Plays the play of the game once more while recording it, then saves a GIF and opens the share sheet.</summary>
        private void ShareHighlight()
        {
            if (_capturing || _recorder.BestPlay == null) return;
            _capturing = true;
            _gifFrames.Clear();
            StartReplay(_recorder.BestPlay);
            StartCoroutine(CaptureHighlight());
        }

        private System.Collections.IEnumerator CaptureHighlight()
        {
            int w = HighlightClip.Width, h = HighlightClip.HeightFor(Screen.width, Screen.height);
            var screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            var small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var read = new Texture2D(w, h, TextureFormat.RGB24, false);
            float nextAt = 0f;
            while (Replaying && _gifFrames.Count < HighlightClip.MaxFrames)
            {
                yield return new WaitForEndOfFrame();
                if (_replayT < nextAt) continue;
                nextAt += 1f / HighlightClip.Fps;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
                Graphics.Blit(screen, small);
                var prev = RenderTexture.active;
                RenderTexture.active = small;
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                read.Apply(false);
                RenderTexture.active = prev;
                var raw = read.GetRawTextureData();
                // GIF rows go top to bottom; flip unless the capture already came out top-down.
                var frame = new byte[w * h * 3];
                bool flip = !SystemInfo.graphicsUVStartsAtTop;
                for (int y = 0; y < h; y++)
                    System.Buffer.BlockCopy(raw, (flip ? h - 1 - y : y) * w * 3, frame, y * w * 3, w * 3);
                _gifFrames.Add(frame);
            }
            Destroy(screen);
            Destroy(small);
            Destroy(read);
            if (Replaying) EndReplay();

            if (_gifFrames.Count < 2)
            {
                _capturing = false;
                _hud.Toast("COULDN'T RECORD THE HIGHLIGHT", 1.4f);
                yield break;
            }
            _hud.Toast("SAVING HIGHLIGHT...", 1.2f);
            var frames = new System.Collections.Generic.List<byte[]>(_gifFrames);
            _gifFrames.Clear();
            string folder = System.IO.Path.Combine(Application.persistentDataPath, "Highlights");
            string path = System.IO.Path.Combine(folder, HighlightClip.FileName(System.DateTime.Now));
            // Encode off the main thread (pure C#), then share on the main thread.
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes(path, GifEncoder.Encode(w, h, frames, 100 / HighlightClip.Fps));
            });
            while (!task.IsCompleted) yield return null;
            _capturing = false;
            if (task.IsFaulted)
            {
                _hud.Toast("COULDN'T SAVE THE HIGHLIGHT", 1.4f);
                Debug.LogWarning("[Retro Hoops] Highlight GIF failed: " + task.Exception?.GetBaseException().Message);
                yield break;
            }
            Haptics.Success();
            Share.File(path, "My play of the game in Retro Hoops");
        }

        private void EndReplay()
        {
            _replay = null;
            _accumulator = 0f;
            _hud.ShowReplayOverlay(false);
            if (!_finalShown && !_paused) SetTouchVisible(!_padMode);
            SyncViews(0f, snapCamera: true);
        }

        /// <summary>Big centre callout with a musical sting (the game's "announcer").</summary>
        private void Callout(string text, SfxId sting)
        {
            _hud.Toast(text, 1.4f);
            Sfx(sting, 0.8f);
            Audio.AudioManager.Voice(text);
        }

        // ------------------------------------------------------------------ crowd

        /// <summary>Animated fans in the stands behind the baseline; they cheer and groan with the game.</summary>
        private void BuildCrowd(Transform world, CourtDef court, MatchSetup setup, CourtGeometry g, bool bannerUp)
        {
            if (court.crowdDensity <= 0f) return;
            int texW = CourtGenerator.TextureWidth(g);
            CourtGenerator.CourtToPixel(g, Vec2.Zero, out float originPx, out float originPy);
            var rng = new SeededRandom(StableHash.Of(court.id + ":crowd"));
            var skins = CharacterSpriteGenerator.SkinTones;
            RgbColor[] shirts =
            {
                setup.TeamA.primary, setup.TeamA.secondary, setup.TeamB.primary,
                court.skyBottom.Darken(0.2f), court.paint, court.lines.Darken(0.3f),
            };
            var sprites = new (Sprite idle, Sprite cheer)[shirts.Length * 2];
            for (int i = 0; i < sprites.Length; i++)
            {
                var shirt = shirts[i % shirts.Length];
                var skin = skins[(i * 7) % skins.Length];
                sprites[i] = (_art.CrowdFan(shirt, skin, false), _art.CrowdFan(shirt, skin, true));
            }

            var fans = new System.Collections.Generic.List<Fan>();
            var parent = new GameObject("Crowd").transform;
            parent.SetParent(world, false);
            const float ppu = CourtSpace.PixelsPerUnit;
            for (int row = 0; row < 3; row++)
            {
                int bottom = CourtGenerator.CrowdRowBottom(g, row);
                for (int x = 2; x < texW - 6; x += 6)
                {
                    if (!rng.Chance(court.crowdDensity)) continue;
                    if (bannerUp && CourtGenerator.BannerCovers(g, texW, x, bottom, CrowdGenerator.Width, CrowdGenerator.Height)) continue;
                    var pick = sprites[rng.Range(0, sprites.Length)];
                    var go = new GameObject("Fan");
                    go.transform.SetParent(parent, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = pick.idle;
                    sr.sortingOrder = -9500 - row; // above the court, behind everything that moves
                    var pos = new Vector3((x - originPx) / ppu, (bottom - originPy) / ppu, 0f);
                    go.transform.position = pos;
                    fans.Add(new Fan { Renderer = sr, Idle = pick.idle, Cheer = pick.cheer, Base = pos });
                }
            }
            _fans = fans.ToArray();
        }

        /// <summary>
        /// Landscape: bleachers along the far sideline (top of the screen) with cheering fans, since the
        /// baseline stands are side-on in this view.
        /// </summary>
        private void BuildSidelineCrowd(Transform world, CourtDef court, MatchSetup setup)
        {
            const float ppu = CourtSpace.PixelsPerUnit;
            float length = setup.Court.depth + CourtGenerator.BaselineMargin * 2f;
            int widthPx = Mathf.CeilToInt(length * ppu);
            var canvas = CrowdGenerator.SidelineStands(widthPx, court.skyTop, court.floor.Lighten(0.12f), court.paint);
            var standsGo = new GameObject("SidelineStands");
            standsGo.transform.SetParent(world, false);
            var sr = standsGo.AddComponent<SpriteRenderer>();
            sr.sprite = _art.SidelineStands(canvas);
            sr.sortingOrder = -9600;
            var origin = new Vector3(-length * 0.5f, setup.Court.HalfWidth + CourtGenerator.SideMargin, 0f);
            standsGo.transform.position = origin;

            var rng = new SeededRandom(StableHash.Of(court.id + ":sideline"));
            var skins = CharacterSpriteGenerator.SkinTones;
            RgbColor[] shirts = { setup.TeamA.primary, setup.TeamA.secondary, setup.TeamB.primary, setup.TeamB.secondary, court.paint, court.lines.Darken(0.3f) };
            var sprites = new (Sprite idle, Sprite cheer)[shirts.Length * 2];
            for (int i = 0; i < sprites.Length; i++)
            {
                var shirt = shirts[i % shirts.Length];
                var skin = skins[(i * 5) % skins.Length];
                sprites[i] = (_art.CrowdFan(shirt, skin, false), _art.CrowdFan(shirt, skin, true));
            }
            var fans = new System.Collections.Generic.List<Fan>();
            var parent = new GameObject("SidelineCrowd").transform;
            parent.SetParent(world, false);
            float density = Mathf.Clamp01(Mathf.Max(0.35f, court.crowdDensity * 1.3f));
            for (int row = CrowdGenerator.StandRows - 1; row >= 0; row--)
            {
                int floor = CrowdGenerator.StandRowFloor(row);
                for (int x = 2 + (row % 2) * 3; x < widthPx - CrowdGenerator.Width; x += 6)
                {
                    if (!rng.Chance(density)) continue;
                    var pick = sprites[rng.Range(0, sprites.Length)];
                    var go = new GameObject("Fan");
                    go.transform.SetParent(parent, false);
                    var fanSr = go.AddComponent<SpriteRenderer>();
                    fanSr.sprite = pick.idle;
                    fanSr.sortingOrder = -9500 - row; // front row in front of the rows behind it
                    var pos = origin + new Vector3(x / ppu, floor / ppu, 0f);
                    go.transform.position = pos;
                    fans.Add(new Fan { Renderer = fanSr, Idle = pick.idle, Cheer = pick.cheer, Base = pos });
                }
            }
            _fans = fans.ToArray();
        }

        private void SetCrowd(CrowdMood mood, float seconds)
        {
            _crowdMood = mood;
            _crowdMoodUntil = Time.unscaledTime + seconds;
        }

        private void SyncCrowd(float now)
        {
            if (_fans.Length == 0) return;
            if (_crowdMood != CrowdMood.Idle && now > _crowdMoodUntil) _crowdMood = CrowdMood.Idle;
            const float px = 1f / CourtSpace.PixelsPerUnit;
            for (int i = 0; i < _fans.Length; i++)
            {
                ref var f = ref _fans[i];
                bool armsUp = _crowdMood == CrowdMood.Cheer && (i % 3 != 0);
                f.Renderer.sprite = armsUp ? f.Cheer : f.Idle;
                int bob = _reduceMotion || Core.PowerMonitor.SavingPower ? 0 : CrowdGenerator.Bob(_crowdMood, now, i);
                f.Renderer.transform.position = f.Base + new Vector3(0f, bob * px, 0f);
            }
        }

        private static CosmeticDef Cosmetic(string id)
        {
            if (string.IsNullOrEmpty(id) || App.Catalog == null) return null;
            return App.Catalog.Find(App.Catalog.Cosmetics, id);
        }

        /// <summary>Colourblind mode: each team wears a distinct jersey pattern, not just a colour.</summary>
        private static TeamPattern PatternFor(int team, MatchSetup s)
        {
            var a = s.TeamA.pattern == TeamPattern.Solid ? TeamPattern.Stripes : s.TeamA.pattern;
            if (team == 0) return a;
            var b = s.TeamB.pattern == TeamPattern.Solid ? TeamPattern.Dots : s.TeamB.pattern;
            if (b == a) b = a == TeamPattern.Dots ? TeamPattern.Checker : TeamPattern.Dots;
            return b;
        }

        private static void Sfx(SfxId id, float volume = 1f, float pitch = 1f) => Audio.AudioManager.Play(id, volume, pitch);

        private void Continue()
        {
            if (_request.Mode == GameMode.AllStar)
            {
                App.OpenAllStar = true;
                SceneFlow.GoTo(AllStarHost());
                return;
            }
            SceneFlow.GoTo(_request.Mode == GameMode.Rise || _request.Mode == GameMode.Rival ? SceneNames.Season : SceneNames.MainMenu);
        }

        /// <summary>The All-Star Weekend lives in the Rise hub while it's open; otherwise the contests are on the main menu.</summary>
        private static string AllStarHost() => App.Career != null && AllStar.WeekendOpen(App.Career) ? SceneNames.Season : SceneNames.MainMenu;

        private PhotoModeView _photo;

        /// <summary>Photo mode from the pause menu: the game stays frozen until DONE brings the pause menu back.</summary>
        private void OpenPhotoMode()
        {
            if (_photo != null || !_paused || Demo) return;
            _hud.ShowPause(false);
            _hud.SetHudVisible(false);
            SetTouchVisible(false);
            var s = _match.Setup;
            string caption = (s.TeamA?.abbreviation ?? "HOME") + " " + _match.Score[0] + "-" + _match.Score[1] + " " + (s.TeamB?.abbreviation ?? "AWAY")
                             + "  " + System.DateTime.Now.ToString("M/d/yy", System.Globalization.CultureInfo.InvariantCulture);
            _photo = PhotoModeView.Open(Camera.main != null ? Camera.main : _cameraRig.GetComponent<Camera>(), caption, () =>
            {
                _photo = null;
                _hud.SetHudVisible(true);
                _hud.ShowPause(true);
            });
        }

        private void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            _accumulator = 0f;
            _buffer.Clear();
            SetTouchVisible(!paused && !_padMode);
            _hud.ShowPause(paused);
        }

        /// <summary>Modes that continue a run (no rematch button).</summary>
        private bool IsRun => _request.Mode == GameMode.Rise || _request.Mode == GameMode.Tournament || _request.Mode == GameMode.Rival
                              || _request.Mode == GameMode.King || _request.Mode == GameMode.Arcade || _request.Mode == GameMode.Cup
                              || _request.Mode == GameMode.Franchise || _request.Mode == GameMode.AllStar
                              || _request.Mode == GameMode.Legacy || _request.Mode == GameMode.CustomCup;

        private void Rematch()
        {
            if (IsRun) { Continue(); return; }
            if (_request.ContextId != null && _request.ContextId.StartsWith(AllStar.ThreeContext, System.StringComparison.Ordinal))
            {
                App.OpenAllStar = true;
                SceneFlow.GoTo(AllStarHost());
                return;
            }
            _request.Seed = 0;
            App.PendingMatch = _request;
            SceneFlow.GoTo(SceneNames.Game);
        }

        private void Quit()
        {
            SceneFlow.GoTo(SceneNames.MainMenu);
        }

        // Backgrounding the app (call, home swipe) always pauses; the player resumes manually.
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _match != null && !_match.IsOver) SetPaused(true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _match != null && !_match.IsOver && !Application.isEditor) SetPaused(true);
        }

        private void OnDestroy()
        {
            UI.ControllerCursor.Suppressed = false;
            CourtSpace.Flip = false;
            if (_landscape)
            {
                CourtSpace.Landscape = false;
                // Straight into another landscape game (rematch / next Full Court game): stay sideways.
                var next = App.PendingMatch;
                bool nextSideways = next != null && next.Mode != GameMode.Versus && next.Mode != GameMode.Demo
                                    && (next.Mode == GameMode.FullCourt || next.FullCourt || (App.Career != null && App.Career.settings.landscapeAll));
                if (!nextSideways) Orientation.Portrait();
            }
            Audio.AudioManager.SetAmbience(false);
            Audio.AudioManager.PlayMusic(0);
            _art?.Dispose();
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
#else
            return false;
#endif
        }
    }
}
