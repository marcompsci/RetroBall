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
    /// PHASE 3: shooting (hold/release meter), passing and calling for the ball, scoring,
    /// game and shot clocks, clear-the-ball rule, AI on both teams, and a final card.
    /// Editor/dev: WASD/arrows move, K shoot (hold), J pass, Esc pause, B knocks the ball loose.
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

        public MatchSimulation Match => _match;
        public bool IsPaused => _paused;

        private void Start()
        {
            App.EnsureInitialized();
            var catalog = App.Catalog;
            _request = App.PendingMatch ?? MatchRequest.QuickCallDefault(catalog);
            App.PendingMatch = null;
            if (_request.Seed == 0) _request.Seed = (uint)System.Environment.TickCount | 1u;

            var setup = MatchSetup.FromRequest(_request, catalog);
            _match = new MatchSimulation(setup);

            var court = catalog.Court(_request.CourtId) ?? catalog.Court(setup.TeamA.homeCourtId);
            BuildWorld(court, setup);

            _controls = TouchControls.Create(_buffer);
            _controls.Defense.UnavailableHint = "SOON";
            _controls.Call.UnavailableHint = "SOON";
            _hud = MatchHud.Create(setup.TeamA, setup.TeamB);
            _hud.PauseRequested += () => SetPaused(true);
            _hud.ResumeRequested += () => SetPaused(false);
            _hud.QuitRequested += Quit;
            _hud.RematchRequested += Rematch;

            SyncViews(0f, snapCamera: true);
            _hud.Toast(_request.Mode == GameMode.Practice ? "PRACTICE LAB" : "CHECK BALL", 1.4f);
        }

        private void BuildWorld(CourtDef court, MatchSetup setup)
        {
            _art = MatchArt.Build(court, setup.Court, StableHash.Of(court.id));
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
            for (int i = 0; i < _match.Players.Length; i++)
            {
                var p = _match.Players[i];
                var team = p.Team == 0 ? setup.TeamA : setup.TeamB;
                _playerViews[i] = PlayerView.Create(world, p, _art.PlayerFrames(p.Def, team), _art, ringColor);
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
                HandleEvents();
                // Edge-triggered presses apply to one step only.
                input.ShootPressed = false;
                input.PassPressed = false;
                _accumulator -= FixedStep;
                steps++;
            }
            SyncViews(steps * FixedStep, snapCamera: false);

            if (_match.IsOver && !_finalShown) ShowFinal();
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
                shootHeld |= pad.buttonSouth.isPressed;
            }
#endif
            float now = Time.unscaledTime;
            var input = new PlayerInput { Move = move, ShootHeld = shootHeld };

            // Buffered presses are consumed only when they can act, so a tap a moment early still counts.
            bool live = _match.Phase == MatchPhase.Live || _match.Phase == MatchPhase.CheckBall;
            if (live && _match.HumanHasBall && !_match.MustClear && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Shoot, now))
                input.ShootPressed = true;
            if (live && (_match.HumanHasBall || _match.HumanTeamHasBall) && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Pass, now))
                input.PassPressed = true;
            // Actions with no Phase 3 effect are cleared so they don't fire later.
            _buffer.Consume(ActionButton.Defense, now);
            _buffer.Consume(ActionButton.Call, now);
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
                            _meter.ShowRelease(_match.LastReleaseMeter, _match.Ball.ShotGrade);
                            _hud.Toast(ShotModel.FeedbackText((ShotFeedback)e.Value), 0.9f);
                        }
                        break;
                    case MatchEventType.ShotMade:
                        bool swish = _match.Ball.ShotGrade == TimingGrade.Green;
                        _hud.Toast((swish ? "SWISH  +" : "+") + e.Value, 1.1f);
                        break;
                    case MatchEventType.Interception:
                        _hud.Toast(e.Team == human ? "PICKED OFF!" : "STOLEN", 1.1f);
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
                _playerViews[i].Sync(dt, i == _match.ControlledIndex, shooting);
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
                canShoot = false;
                canPass = false;
                _controls.Shoot.UnavailableHint = "SOON";
            }
            _controls.Pass.UnavailableHint = offense ? "NO BALL" : "SOON";

            int state = (offense ? 1 : 0) | (_match.HumanHasBall ? 2 : 0) | (_match.MustClear ? 4 : 0) | (_match.HumanTeamHasBall ? 8 : 0);
            if (state == _lastLabelState) return;
            _lastLabelState = state;
            _controls.SetLabels(shoot, pass, def);
            _controls.SetAvailability(canShoot, canPass, false, false);
        }

        private void ShowFinal()
        {
            _finalShown = true;
            _controls.SetVisible(false);
            var s = _match.Setup;
            int human = s.HumanTeam;
            string title = _match.Winner < 0 ? "TIE" : (_match.Winner == human ? "YOU WIN" : "FINAL");
            string line = s.TeamA.abbreviation + "  " + _match.Score[0] + "  -  " + _match.Score[1] + "  " + s.TeamB.abbreviation;
            _hud.ShowFinal(title, line);
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
