using CallerRetroBall.Controls;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        private SpriteRenderer[] _cones;
        private SpriteRenderer _targetArrow;
        private PlayCall _pendingCall;
        private bool _resultApplied;

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

            var court = catalog.Court(_request.CourtId) ?? catalog.Court(setup.TeamA.homeCourtId);
            BuildWorld(court, setup);

            _controls = TouchControls.Create(_buffer);
            _controls.Defense.UnavailableHint = "OFFENSE";
            _controls.Call.UnavailableHint = "OFFENSE";
            _hud = MatchHud.Create(setup.TeamA, setup.TeamB);
            _hud.PlayChosen += play => _pendingCall = play;
            _hud.ContinueRequested += Continue;
            _hud.PauseRequested += () => SetPaused(true);
            _hud.ResumeRequested += () => SetPaused(false);
            _hud.QuitRequested += Quit;
            _hud.RematchRequested += Rematch;

            SyncViews(0f, snapCamera: true);
            _hud.Toast(_practice != null ? DrillTitle(_practice.Kind) : (_request.Mode == GameMode.Practice ? "PRACTICE LAB" : "CHECK BALL"), 1.6f);
        }

        private void BuildWorld(CourtDef court, MatchSetup setup)
        {
            var career = App.Career;
            var banner = Cosmetic(career?.equippedBanner);
            _art = MatchArt.Build(court, setup.Court, StableHash.Of(court.id), banner?.colorA, banner?.colorB);
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
                RgbColor? shoeColor = p.IsHuman && shoes != null ? shoes.colorA : (RgbColor?)null;
                var pattern = contrast ? PatternFor(p.Team, setup) : TeamPattern.Solid;
                _playerViews[i] = PlayerView.Create(world, p, _art.PlayerFrames(p.Def, primary, trim, team.accent, shoeColor, pattern), _art, ringColor);
            }
            _ballView = BallView.Create(world, _art);
            _meter = ShotMeterView.Create(world, _art);

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
            if (EscapePressed() && !_match.IsOver) SetPaused(!_paused);
            if (_paused) return;

            var input = ReadInput();
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, FixedStep * MaxStepsPerFrame);
            int steps = 0;
            while (_accumulator >= FixedStep && steps < MaxStepsPerFrame)
            {
                _match.Step(FixedStep, input);
                _practice?.Update(_match, FixedStep);
                HandleEvents();
                // Edge-triggered presses apply to one step only.
                input.ShootPressed = false;
                input.PassPressed = false;
                input.DefensePressed = false;
                input.CallPlay = PlayCall.None;
                _accumulator -= FixedStep;
                steps++;
            }
            SyncViews(steps * FixedStep, snapCamera: false);

            if (_practice != null && _practice.Finished && !_finalShown) ShowPracticeEnd();
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
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f) - (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f) move = Vec2.ClampMagnitude(new Vec2(x, y), 1f);
                if (kb.jKey.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, Time.unscaledTime);
                if (kb.kKey.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, Time.unscaledTime);
                if (kb.lKey.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, Time.unscaledTime);
                if (kb.cKey.wasPressedThisFrame) _buffer.Press(ActionButton.Call, Time.unscaledTime);
                shootHeld |= kb.kKey.isPressed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (kb.bKey.wasPressedThisFrame) _match.KnockLoose(new Vec2(Random.Range(-3f, 3f), Random.Range(1f, 3f)));
#endif
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f) move = new Vec2(stick.x, -stick.y);
                if (pad.buttonSouth.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, Time.unscaledTime);
                if (pad.buttonWest.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, Time.unscaledTime);
                if (pad.buttonEast.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, Time.unscaledTime);
                if (pad.buttonNorth.wasPressedThisFrame) _buffer.Press(ActionButton.Call, Time.unscaledTime);
                shootHeld |= pad.buttonSouth.isPressed;
            }
#endif
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

        private void HandleEvents()
        {
            int human = _match.Setup.HumanTeam;
            foreach (var e in _match.Events)
            {
                switch (e.Type)
                {
                    case MatchEventType.ShotReleased:
                        _lastShooter = e.PlayerIndex;
                        _shootPoseUntil = Time.unscaledTime + 0.35f;
                        if (e.PlayerIndex == _match.ControlledIndex)
                        {
                            if (e.Value == (int)ShotFeedback.Green) Haptics.Light();
                            _meter.ShowRelease(_match.LastReleaseMeter, _match.Ball.ShotGrade);
                            _hud.Toast(ShotModel.FeedbackText((ShotFeedback)e.Value), 0.9f);
                        }
                        break;
                    case MatchEventType.ShotMade:
                        bool swish = _match.Ball.ShotGrade == TimingGrade.Green;
                        _hud.Toast((swish ? "SWISH  +" : "+") + e.Value, 1.1f);
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
                        _hud.Toast(e.Team == human ? "STEAL!" : "STRIPPED", 1.1f);
                        Sfx(SfxId.Steal);
                        if (e.Team == human) Haptics.Medium();
                        break;
                    case MatchEventType.Block:
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
            for (int i = 0; i < _playerViews.Length; i++)
            {
                bool shooting = i == _match.ChargingIndex || (i == _lastShooter && now < _shootPoseUntil);
                _playerViews[i].Sync(dt, i == _match.ControlledIndex, shooting, _match.JumpHeight01(i));
            }

            int holderOrder = _match.Holder != null ? CourtSpace.SortingOrder(_match.Holder.Position) : 0;
            _ballView.Sync(_match.Ball, holderOrder);

            // Shot meter (human only).
            if (_match.ChargingIndex == _match.ControlledIndex && _match.ChargingIndex >= 0)
            {
                var shooter = _match.Controlled;
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

            _cameraRig.Follow(_match.Controlled.Position, Mathf.Max(dt, Time.unscaledDeltaTime), snapCamera);
            _hud.Sync(_match);
            _hud.SetInfo(InfoLine());
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

        private bool CanCall() =>
            _practice == null && _match.OffenseTeam == _match.Setup.HumanTeam && _match.HumanTeamHasBall
            && !_match.MustClear && _match.Phase == MatchPhase.Live && _match.ActivePlay == PlayCall.None;

        private string InfoLine()
        {
            if (_practice != null)
            {
                string time = _practice.TimeLimit > 0f ? Mathf.CeilToInt(_practice.TimeLeft) + "s  ·  " : _practice.Elapsed.ToString("0.0") + "s  ·  ";
                return time + _practice.ResultText().ToUpperInvariant();
            }
            if (_match.ActivePlay != PlayCall.None && _match.ActivePlayTeam == _match.Setup.HumanTeam) return PlayName(_match.ActivePlay);
            if (_match.IsBoxingOut(_match.ControlledIndex)) return "BOX OUT";
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

            if (_request.Mode == GameMode.Practice)
            {
                string line = s.TeamA.abbreviation + "  " + _match.Score[0] + "  -  " + _match.Score[1] + "  " + s.TeamB.abbreviation;
                _hud.ShowFinal(title, line);
                return;
            }

            string matchId = string.IsNullOrEmpty(_request.ContextId)
                ? System.Guid.NewGuid().ToString("N")
                : _request.ContextId + "#" + _request.Seed;
            var summary = MatchSummary.From(_match, _request.Mode, matchId);
            summary.isPlayoff = _request.Round >= 1;
            summary.isFinal = _request.Round >= 2;
            var grant = Rewards.For(summary, App.Rewards);

            string note = null;
            bool rewarded = false;
            if (App.Career != null && !_resultApplied)
            {
                _resultApplied = true;
                rewarded = Career.ApplyMatch(App.Career, summary, grant);
                if (rewarded && _request.Mode == GameMode.Rise)
                {
                    var outcome = RiseEngine.ApplyResult(App.Career.rise, App.Catalog, summary, App.Career);
                    App.LastRiseOutcome = outcome;
                    note = OutcomeText(outcome);
                    if (outcome == RiseOutcome.Champion) title = "CHAMPIONS";
                }
                App.SaveCareer();
            }
            if (summary.HumanWon) Haptics.Success();

            bool rise = _request.Mode == GameMode.Rise;
            _hud.ShowPostGame(title, summary, grant, rewarded, note, rise ? "CONTINUE" : null, !rise);
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
                    p.Kind == DrillKind.DribbleLane && p.Finished ? p.CourseTime : 0f);
                App.SaveCareer();
            }
            if (best) Haptics.Success();
            Sfx(SfxId.Whistle, 0.7f);
            _hud.ShowPracticeEnd(DrillTitle(_practice.Kind), _practice.ResultText().ToUpperInvariant(), best);
        }

        // ------------------------------------------------------------------ practice markers

        private void BuildPracticeMarkers(Transform world)
        {
            _cones = new SpriteRenderer[_practice.Cones.Count];
            for (int i = 0; i < _cones.Length; i++)
            {
                var go = new GameObject("Cone " + i);
                go.transform.SetParent(world, false);
                go.transform.position = CourtSpace.ToWorldSnapped(_practice.Cones[i]);
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
            SceneFlow.GoTo(_request.Mode == GameMode.Rise ? SceneNames.Season : SceneNames.MainMenu);
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
            if (_request.Mode == GameMode.Rise) { Continue(); return; }
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
