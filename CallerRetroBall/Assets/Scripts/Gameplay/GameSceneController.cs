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
        private const float FixedStep = 1f / 60f;
        private const int MaxStepsPerFrame = 5;

        private MatchRequest _request;
        private MatchSimulation _match;
        private MatchArt _art;
        private PlayerView[] _playerViews;
        private BallView _ballView;
        private ShotMeterView _meter;
        private SpriteRenderer _receiverArrow;
        private CourtCameraRig _cameraRig;
        private TouchControls _controls;
        private MatchHud _hud;
        private readonly InputBuffer _buffer = new InputBuffer();
        private float _accumulator;
        private bool _paused;
        private bool _finalShown;
        private int _lastShooter = -1;
        private float _shootPoseUntil;
        private int _lastLabelState = -1;
        private PracticeSession _practice;
        private TutorialSession _tutorial;
        private DailyChallenge _daily;
        private readonly InputBuffer _buffer2 = new InputBuffer();
        private bool Versus => _request != null && _request.Mode == GameMode.Versus;

        // Phase 12: accessibility, leaps, crowd.
        private bool _reduceMotion;
        private bool _tapToShoot;
        private TapShoot _tap;
        private bool _shootTapped;
        private int _leaper = -1;
        private float _leapStart;
        private float _leapDuration;
        private ShotType _leapType;
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
        private Vector3 _hoopWorld;

        public MatchSimulation Match => _match;
        public bool IsPaused => _paused;

        private void Start()
        {
            App.EnsureInitialized();
            var catalog = App.Catalog;
            _request = App.PrepareRequest(App.PendingMatch ?? MatchRequest.QuickCallDefault(catalog));
            App.PendingMatch = null;
            if (_request.Seed == 0) _request.Seed = (uint)System.Environment.TickCount | 1u;

            var setup = MatchSetup.FromRequest(_request, catalog);
            _match = new MatchSimulation(setup);
            if (_request.Mode == GameMode.Practice && _request.Drill >= 0)
                _practice = new PracticeSession((DrillKind)_request.Drill, _match, _request.Seed);
            if (_request.Mode == GameMode.Tutorial) _tutorial = new TutorialSession();
            if (_request.Mode == GameMode.Daily && _request.ContextId != null && _request.ContextId.StartsWith("daily:", System.StringComparison.Ordinal)
                && int.TryParse(_request.ContextId.Substring(6), out int day))
                _daily = DailyChallenges.For(day, catalog);

            var court = catalog.Court(_request.CourtId) ?? catalog.Court(setup.TeamA.homeCourtId);
            BuildWorld(court, setup);

            var settings = App.Career?.settings;
            _reduceMotion = settings != null && settings.reduceMotion;
            _tapToShoot = settings != null && settings.tapToShoot;
            _controls = TouchControls.Create(_buffer, settings != null && settings.leftHanded, settings != null && settings.largeButtons);
            _controls.Shoot.Pressed += () => _shootTapped = true;
            MatchHud.ReduceMotion = _reduceMotion;
            Audio.AudioManager.PlayMusic(1);
            _controls.Defense.UnavailableHint = "OFFENSE";
            _controls.Call.UnavailableHint = "OFFENSE";
            _hud = MatchHud.Create(setup.TeamA, setup.TeamB);
            _hud.PlayChosen += play => _pendingCall = play;
            _recorder = new ReplayRecorder(_match.Players.Length);
            _hud.ReplayRequested += () => StartReplay(_lastHighlight);
            _hud.PlayOfTheGameRequested += () => StartReplay(_recorder.BestPlay);
            if (settings != null && settings.leftHanded) _hud.SetCallMenuLeft(true);
            _hud.ContinueRequested += Continue;
            _hud.PauseRequested += () => SetPaused(true);
            _hud.ResumeRequested += () => SetPaused(false);
            _hud.QuitRequested += Quit;
            _hud.RematchRequested += Rematch;

            _myCelebration = Flair.CelebrationFor(App.Career?.equippedCelebration);
            _myMove = Flair.DribbleMoveFor(App.Career?.equippedMove);
            Audio.AudioManager.SetAmbience(true);

            SyncViews(0f, snapCamera: true);
            _hud.Toast(_tutorial != null ? "HOW TO PLAY"
                     : _daily != null ? "DAILY: " + _daily.Describe().ToUpperInvariant()
                     : Versus ? "PLAYER 1  VS  PLAYER 2"
                     : _practice != null ? DrillTitle(_practice.Kind)
                     : (_request.Mode == GameMode.Practice ? "PRACTICE LAB" : "CHECK BALL"), 1.8f);
        }

        private void BuildWorld(CourtDef court, MatchSetup setup)
        {
            var career = App.Career;
            var banner = Cosmetic(career?.equippedBanner);
            _art = MatchArt.Build(court, setup.Court, StableHash.Of(court.id), banner?.colorA, banner?.colorB, staticCrowd: false);
            var world = new GameObject("World").transform;

            var courtGo = new GameObject("Court");
            courtGo.transform.SetParent(world, false);
            var courtSr = courtGo.AddComponent<SpriteRenderer>();
            courtSr.sprite = _art.Court;
            courtSr.sortingOrder = -10000;

            var hoopGo = new GameObject("Hoop");
            hoopGo.transform.SetParent(world, false);
            hoopGo.transform.position = CourtSpace.ToWorldSnapped(setup.Court.Hoop, CourtSpace.RimHeight);
            var hoopSr = hoopGo.AddComponent<SpriteRenderer>();
            hoopSr.sprite = _art.Hoop;
            hoopSr.sortingOrder = CourtSpace.SortingOrder(setup.Court.Hoop, -1);

            _playerViews = new PlayerView[_match.Players.Length];
            var ringColor = new Color(1f, 0.82f, 0.4f, 1f);
            bool contrast = career != null && career.settings.colorblindContrast;
            var jersey = Cosmetic(career?.equippedJersey);
            var shoes = Cosmetic(career?.equippedShoes);
            for (int i = 0; i < _match.Players.Length; i++)
            {
                var p = _match.Players[i];
                var team = p.Team == 0 ? setup.TeamA : setup.TeamB;
                var primary = team.primary;
                var trim = team.secondary;
                // Jersey palettes are for your own crew only.
                if (p.Team == setup.HumanTeam && team.id == DefaultContent.PlayerCrewId && jersey != null)
                {
                    primary = jersey.colorA;
                    trim = jersey.colorB;
                }
                RgbColor? shoeColor = i == _match.ControlledIndex && shoes != null ? shoes.colorA : (RgbColor?)null;
                var pattern = contrast ? PatternFor(p.Team, setup) : TeamPattern.Solid;
                // Player 2's ring is cyan so both people can find themselves.
                var ring = i == _match.SecondControlledIndex ? new Color32(0x4C, 0xC9, 0xF0, 255) : (Color32)ringColor;
                _playerViews[i] = PlayerView.Create(world, p, _art.PlayerFrames(p.Def, primary, trim, team.accent, shoeColor, pattern), _art, ring);
            }
            _ballView = BallView.Create(world, _art);
            _meter = ShotMeterView.Create(world, _art);
            _bursts = PixelBursts.Create(world, _art);
            BuildCrowd(world, court, setup, banner != null);
            _hoopWorld = CourtSpace.ToWorldSnapped(setup.Court.Hoop, CourtSpace.RimHeight);

            var arrowGo = new GameObject("ReceiverArrow");
            arrowGo.transform.SetParent(world, false);
            _receiverArrow = arrowGo.AddComponent<SpriteRenderer>();
            _receiverArrow.sprite = _art.Arrow;
            _receiverArrow.color = new Color32(0x4C, 0xC9, 0xF0, 255);
            _receiverArrow.sortingOrder = 31500;
            _receiverArrow.enabled = false;

            if (_practice != null) BuildPracticeMarkers(world);

            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            _cameraRig = cam.GetComponent<CourtCameraRig>();
            if (_cameraRig == null) _cameraRig = cam.gameObject.AddComponent<CourtCameraRig>();
            var bg = court.floor.Darken(0.35f);
            _cameraRig.Init(setup.Court, new Color32(bg.r, bg.g, bg.b, 255));
        }

        private void Update()
        {
            if (_match == null) return;
            if (Replaying)
            {
                StepReplay();
                return;
            }
            if (EscapePressed() && !_match.IsOver) SetPaused(!_paused);
            if (_paused) return;

            var input = ReadInput();
            var input2 = Versus ? ReadInput2() : default;
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, FixedStep * MaxStepsPerFrame);
            int steps = 0;
            while (_accumulator >= FixedStep && steps < MaxStepsPerFrame)
            {
                _match.Step(FixedStep, input, input2);
                _recorder.Capture(_match);
                _practice?.Update(_match, FixedStep);
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
            SyncViews(steps * FixedStep, snapCamera: false);

            if (_tutorial != null && _tutorial.Finished && !_finalShown) ShowTutorialEnd();
            else if (_practice != null && _practice.Finished && !_finalShown) ShowPracticeEnd();
            else if (_match.IsOver && !_finalShown) ShowFinal();
        }

        private PlayerInput ReadInput()
        {
            var move = _controls.CourtMove;
            bool shootHeld = _controls.Shoot.IsHeld;
#if ENABLE_INPUT_SYSTEM
            // Keyboard / gamepad fallback for the Editor and controllers.
            var kb = Keyboard.current;
            if (kb != null)
            {
                // In 2-player the arrow keys belong to player 2.
                bool arrows = !Versus;
                float x = (kb.dKey.isPressed || arrows && kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || arrows && kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.sKey.isPressed || arrows && kb.downArrowKey.isPressed ? 1f : 0f) - (kb.wKey.isPressed || arrows && kb.upArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f) move = Vec2.ClampMagnitude(new Vec2(x, y), 1f);
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
                if (stick.sqrMagnitude > 0.02f) move = new Vec2(stick.x, -stick.y);
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
                _buffer2.Consume(ActionButton.Defense, now);
                if (callPressed && teamHasBall && _match.Phase == MatchPhase.Live && _match.ActivePlay == PlayCall.None) input.CallPlay = PlayCall.PickAndRoll;
            }
            else if (_match.Phase == MatchPhase.Live)
            {
                if (_buffer2.Consume(ActionButton.Defense, now)) input.DefensePressed = true;
                if (_buffer2.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (_buffer2.Consume(ActionButton.Pass, now)) input.PassPressed = true;
            }
            return input;
        }

        private void HandleEvents()
        {
            int human = _match.Setup.HumanTeam;
            foreach (var e in _match.Events)
            {
                NoteHighlight(e);
                switch (e.Type)
                {
                    case MatchEventType.ShotReleased:
                        _lastShooter = e.PlayerIndex;
                        _shootPoseUntil = Time.unscaledTime + 0.35f;
                        if (_match.Ball.Phase == BallPhase.Shot && (_match.Ball.ShotType == ShotType.Dunk || _match.Ball.ShotType == ShotType.Layup))
                        {
                            _leaper = e.PlayerIndex;
                            _leapStart = Time.unscaledTime;
                            _leapType = _match.Ball.ShotType;
                            _leapDuration = Mathf.Max(0.3f, _match.Ball.FlightDuration + 0.15f);
                        }
                        if (e.PlayerIndex == _match.ControlledIndex)
                        {
                            if (e.Value == (int)ShotFeedback.Green) Haptics.Light();
                            _meter.ShowRelease(_match.LastReleaseMeter, _match.Ball.ShotGrade);
                            _hud.Toast(ShotModel.FeedbackText((ShotFeedback)e.Value), 0.9f);
                        }
                        break;
                    case MatchEventType.ShotMade:
                        bool swish = _match.Ball.ShotGrade == TimingGrade.Green;
                        bool dunk = _match.Ball.ShotType == ShotType.Dunk;
                        _hud.Toast((dunk ? "SLAM!  +" : swish ? "SWISH  +" : "+") + e.Value, 1.1f);
                        if (!_reduceMotion) _bursts.Spawn(_hoopWorld, swish ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : Color.white, dunk ? 28 : (swish ? 20 : 12), dunk ? 5f : 4f);
                        if (dunk) _cameraRig.Shake(0.22f);
                        bool crowdHappy = Versus || e.Team == human;
                        SetCrowd(crowdHappy ? CrowdMood.Cheer : CrowdMood.Groan, crowdHappy ? 1.6f : 1.1f);
                        if (_match.IsHumanControlled(e.PlayerIndex))
                        {
                            int streak = _match.Players[e.PlayerIndex].HotStreak;
                            if (streak == 3) Callout("HEATING UP", SfxId.Stinger);
                            else if (streak >= 4) Callout("ON FIRE!", SfxId.OnFire);
                            else if (dunk) Sfx(SfxId.Stinger, 0.8f);
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
                    case MatchEventType.CheckBall:
                        _hud.Toast(e.Team == human ? "YOUR BALL" : "DEFENSE", 0.9f);
                        break;
                }
            }
        }

        private void SyncViews(float dt, bool snapCamera)
        {
            float now = Time.unscaledTime;
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
                if (i == _leaper) jump = Mathf.Max(jump, Flair.Leap(_leapType, now - _leapStart, _leapDuration));
                _playerViews[i].Sync(dt, _match.IsHumanControlled(i), shooting, jump, flair);
            }

            int holderOrder = _match.Holder != null ? CourtSpace.SortingOrder(_match.Holder.Position) : 0;
            var ballOffset = _match.HumanHasBall ? new Vector2Int(move.BallOffsetX, move.BallLift + move.Lift) : Vector2Int.zero;
            _ballView.Sync(_match.Ball, holderOrder, ballOffset);

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
            int receiver = _match.Phase == MatchPhase.Live && _match.ChargingIndex < 0 ? _match.PreviewPassTarget(_controls.CourtMove) : -1;
            _receiverArrow.enabled = receiver >= 0;
            if (receiver >= 0)
                _receiverArrow.transform.position = CourtSpace.ToWorldSnapped(_match.Players[receiver].Position) + new Vector3(0f, 1.75f, 0f);

            // 2-player: follow the ball so neither player is left off screen.
            _cameraRig.Follow(Versus ? _match.Ball.Position : _match.Controlled.Position, Mathf.Max(dt, Time.unscaledDeltaTime), snapCamera);
            _hud.Sync(_match);
            // The info line builds a string, so refresh it ~10x a second rather than every frame.
            if (Time.unscaledTime >= _nextInfoAt)
            {
                _nextInfoAt = Time.unscaledTime + 0.1f;
                _hud.SetInfo(InfoLine());
            }
            SyncPracticeMarkers();
            UpdateControlLabels();
        }

        private void UpdateControlLabels()
        {
            bool offense = _match.OffenseTeam == _match.Setup.HumanTeam;
            string shoot, pass, def;
            bool canShoot, canPass;
            if (offense && _match.HumanHasBall)
            {
                shoot = _match.MustClear ? "CLEAR" : "SHOOT";
                pass = "PASS";
                def = "DEF";
                canShoot = !_match.MustClear;
                canPass = true;
                _controls.Shoot.UnavailableHint = "TAKE IT BACK";
            }
            else if (offense)
            {
                shoot = "SHOOT";
                pass = "ASK";
                def = "DEF";
                canShoot = false;
                canPass = _match.HumanTeamHasBall;
                _controls.Shoot.UnavailableHint = "NO BALL";
            }
            else
            {
                shoot = "JUMP";
                pass = "SWITCH";
                def = "STEAL";
                canShoot = true;
                canPass = true;
            }
            _controls.Pass.UnavailableHint = "NO BALL";
            bool canCall = CanCall();

            int state = (offense ? 1 : 0) | (_match.HumanHasBall ? 2 : 0) | (_match.MustClear ? 4 : 0) | (_match.HumanTeamHasBall ? 8 : 0) | (canCall ? 16 : 0);
            if (state == _lastLabelState) return;
            _lastLabelState = state;
            _controls.SetLabels(shoot, pass, def);
            _controls.SetAvailability(canShoot, canPass, !offense, canCall);
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
            _practice == null && _match.OffenseTeam == _match.Setup.HumanTeam && _match.HumanTeamHasBall
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
            if (_practice != null)
            {
                string time = _practice.TimeLimit > 0f ? Mathf.CeilToInt(_practice.TimeLeft) + "s  ·  " : _practice.Elapsed.ToString("0.0") + "s  ·  ";
                return time + _practice.ResultText().ToUpperInvariant();
            }
            // On a computer, show the keyboard controls for the first seconds of a match.
            if (!Application.isMobilePlatform && _match.Time < 8f)
                return "WASD MOVE · K SHOOT (HOLD) · J PASS · L STEAL · C CALL · ESC PAUSE";
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
                default: return "DRIBBLE LANE";
            }
        }

        private void ShowFinal()
        {
            _finalShown = true;
            _controls.SetVisible(false);
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
                // Local 2-player: box score, no rewards, nothing saved.
                var vs = MatchSummary.From(_match, _request.Mode, "versus");
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
                        ? "NEON STATIC BEATEN!  +" + RivalEngine.WinBonus + " SP  +" + RivalEngine.WinFans + " FANS"
                        : "Static wins this one. They'll be back next season.";
                    if (rivalOutcome == RivalOutcome.Won) title = "STATIC SILENCED";
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
                if (rewarded && _request.Mode == GameMode.Tournament)
                {
                    var outcome = ClassicEngine.ApplyResult(App.Career.classic, App.Catalog, summary);
                    App.OpenClassicOnMenu = true;
                    note = ClassicText(outcome);
                    if (outcome == ClassicOutcome.Champion) title = "CLASSIC CHAMPS";
                }
                if (App.Career.rise.recruitable.Count > recruitsBefore)
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + "New players can join your crew (Rise hub ▸ YOUR CREW).";
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
            if (title == "CHAMPIONS" || title == "CLASSIC CHAMPS") Sfx(SfxId.Fanfare);

            // Rise and the Classic continue their run instead of offering a rematch.
            _hud.HasPlayOfTheGame = _recorder.BestPlay != null;
            bool run = _request.Mode == GameMode.Rise || _request.Mode == GameMode.Tournament || _request.Mode == GameMode.Rival || _request.Mode == GameMode.King;
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
            _controls.SetVisible(false);
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

        private void ShowPracticeEnd()
        {
            _finalShown = true;
            _controls.SetVisible(false);
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
                    p.Kind == DrillKind.Lockdown ? p.Stops : 0);
                App.SaveCareer();
            }
            if (best) Haptics.Success();
            Sfx(SfxId.Whistle, 0.7f);
            _hud.ShowPracticeEnd(DrillTitle(_practice.Kind), _practice.ResultText().ToUpperInvariant(), best);
        }

        // ------------------------------------------------------------------ practice markers

        private void BuildPracticeMarkers(Transform world)
        {
            // Dribble Lane cones, or the 3-Point Contest's money-ball spots.
            var spots = _practice.Kind == DrillKind.ThreePoint ? _practice.MoneySpots : _practice.Cones;
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
                    if (_practice.Kind == DrillKind.ThreePoint)
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
            if (_practice != null || _tutorial != null || e.PlayerIndex < 0 || !_match.IsHumanControlled(e.PlayerIndex)) return;
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

        private void StartReplay(ReplayClip clip)
        {
            if (clip == null || clip.Frames.Count < 2) return;
            _replay = clip;
            _replayT = 0f;
            _controls.SetVisible(false);
            _hud.ShowCallMenu(false);
            _hud.ShowReplayOverlay(true);
        }

        private void StepReplay()
        {
            float dt = Time.unscaledDeltaTime;
            _replayT += dt * ReplaySpeed;
            bool skip = false;
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            skip = pointer != null && pointer.press.wasPressedThisFrame && _replayT > 0.3f;
            var kb = Keyboard.current;
            skip |= kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
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

        private void EndReplay()
        {
            _replay = null;
            _accumulator = 0f;
            _hud.ShowReplayOverlay(false);
            if (!_finalShown && !_paused) _controls.SetVisible(true);
            SyncViews(0f, snapCamera: true);
        }

        /// <summary>Big centre callout with a musical sting (the game's "announcer").</summary>
        private void Callout(string text, SfxId sting)
        {
            _hud.Toast(text, 1.4f);
            Sfx(sting, 0.8f);
        }

        // ------------------------------------------------------------------ crowd

        /// <summary>Animated fans in the stands behind the baseline; they cheer and groan with the game.</summary>
        private void BuildCrowd(Transform world, CourtDef court, MatchSetup setup, bool bannerUp)
        {
            if (court.crowdDensity <= 0f) return;
            var g = setup.Court;
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
                int bob = _reduceMotion ? 0 : CrowdGenerator.Bob(_crowdMood, now, i);
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
            SceneFlow.GoTo(_request.Mode == GameMode.Rise || _request.Mode == GameMode.Rival ? SceneNames.Season : SceneNames.MainMenu);
        }

        private void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            _accumulator = 0f;
            _buffer.Clear();
            _controls.SetVisible(!paused);
            _hud.ShowPause(paused);
        }

        private void Rematch()
        {
            if (_request.Mode == GameMode.Rise || _request.Mode == GameMode.Tournament || _request.Mode == GameMode.Rival || _request.Mode == GameMode.King) { Continue(); return; }
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
            Audio.AudioManager.SetAmbience(false);
            Audio.AudioManager.PlayMusic(0);
            _art?.Dispose();
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
