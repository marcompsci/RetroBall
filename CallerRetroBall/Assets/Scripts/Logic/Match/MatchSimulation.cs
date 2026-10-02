using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Match-flow timing and ball-flight feel.</summary>
    [Serializable]
    public class MatchTuning
    {
        /// <summary>Freeze before live play after a check (ends early when the human moves or acts).</summary>
        public float checkBallSeconds = 0.8f;
        public float deadBallAfterMake = 1.0f;
        public float deadBallAfterTurnover = 0.9f;
        /// <summary>Clutch rating matters when the game clock is at or under this.</summary>
        public float lateGameSeconds = 15f;
        /// <summary>A shot that hits the rim resets the shot clock (3-on-3 style).</summary>
        public bool resetShotClockOnRim = true;
        public float releaseHeight = 2.2f;
        public float dunkReleaseHeight = 2.9f;
        public float shotFlightBase = 0.5f;
        public float shotFlightPerMeter = 0.06f;
        public float dunkFlightSeconds = 0.22f;
        public float shotArcBase = 1.2f;
        public float shotArcPerMeter = 0.13f;
        public float missSpeedMin = 2.2f;
        public float missSpeedMax = 4.6f;
        public float missUpSpeed = 2.8f;
        public float passHeight = 1.2f;
        public float maxPassSeconds = 2f;
        public float assistWindowSeconds = 4f;
        public float giveAndGoSeconds = 1.2f;

        public static MatchTuning Default => new MatchTuning();
    }

    /// <summary>Everything needed to start a match. Built from a MatchRequest + content.</summary>
    public sealed class MatchSetup
    {
        public TeamDef TeamA;
        public TeamDef TeamB;
        public List<PlayerDef> RosterA = new List<PlayerDef>();
        public List<PlayerDef> RosterB = new List<PlayerDef>();
        public List<ArchetypeDef> ArchetypesA = new List<ArchetypeDef>();
        public List<ArchetypeDef> ArchetypesB = new List<ArchetypeDef>();
        public GameRulesDef Rules = new GameRulesDef();
        public DifficultyDef Difficulty;
        public CourtGeometry Court = CourtGeometry.Default;
        public MovementTuning Movement = MovementTuning.Default;
        public BallTuning Ball = BallTuning.Default;
        public ShotTuning Shot = ShotTuning.Default;
        public PassTuning Pass = PassTuning.Default;
        public MatchTuning Flow = MatchTuning.Default;
        public DefenseTuning Defense = DefenseTuning.Default;
        public int HumanTeam = 0;
        public int StartingOffense = 0;
        public uint Seed = 1;
        /// <summary>Practice: opponents stand still and never take the ball.</summary>
        public bool PassiveOpponents;
        /// <summary>Local 2-player: slot 0 of the other team is also human-controlled (second input).</summary>
        public bool SecondHuman;
        /// <summary>Practice: the human's team keeps the ball after scoring.</summary>
        public bool KeepPossessionAfterScore;
        public float HumanTeamStartingStamina = 1f;
        /// <summary>Practice: AI teammates never shoot or drive; drills hand the ball back.</summary>
        public bool TeammatesOnlyPass;
        public float ChemistryBonus;

        /// <summary>Builds a setup from a request, taking the first three players of each roster.</summary>
        public static MatchSetup FromRequest(MatchRequest request, ContentCatalog c)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (c == null) throw new ArgumentNullException(nameof(c));

            var setup = new MatchSetup
            {
                TeamA = c.Team(request.HomeTeamId),
                TeamB = c.Team(request.AwayTeamId),
                Rules = c.Find(c.Rules, request.RulesId) ?? new GameRulesDef(),
                Difficulty = c.Difficulty(request.DifficultyId),
                Seed = request.Seed != 0 ? request.Seed : 1,
                PassiveOpponents = request.Mode == GameMode.Practice || request.Mode == GameMode.Tutorial,
                SecondHuman = request.Mode == GameMode.Versus,
                KeepPossessionAfterScore = request.Mode == GameMode.Practice || request.Mode == GameMode.Tutorial,
                TeammatesOnlyPass = request.Mode == GameMode.Practice || request.Mode == GameMode.Tutorial,
                HumanTeamStartingStamina = request.StartingStamina,
                ChemistryBonus = request.ChemistryBonus,
            };
            // Practice has no opponent: mirror the player crew so the court still has bodies.
            if (setup.TeamB == null) setup.TeamB = setup.TeamA;
            if (setup.TeamA == null) throw new InvalidOperationException("Match request has no valid home team.");

            FillRoster(setup.RosterA, setup.ArchetypesA, setup.TeamA, c);
            FillRoster(setup.RosterB, setup.ArchetypesB, setup.TeamB, c);
            if (request.HumanPlayer != null)
            {
                setup.RosterA[0] = request.HumanPlayer;
                var archetype = c.ArchetypeById(request.HumanPlayer.archetypeId);
                if (archetype != null && setup.ArchetypesA.Count > 0) setup.ArchetypesA[0] = archetype;
            }
            else if (request.HumanAttributes.HasValue)
            {
                // Copy so the shared content definition is never modified.
                var original = setup.RosterA[0];
                setup.RosterA[0] = new PlayerDef
                {
                    id = original.id, firstName = original.firstName, lastName = original.lastName,
                    jerseyNumber = original.jerseyNumber, archetypeId = original.archetypeId,
                    attributes = request.HumanAttributes.Value, appearance = original.appearance,
                };
            }
            return setup;
        }

        private static void FillRoster(List<PlayerDef> roster, List<ArchetypeDef> archetypes, TeamDef team, ContentCatalog c)
        {
            foreach (var id in team.rosterPlayerIds)
            {
                if (roster.Count == MatchSimulation.PlayersPerTeam) break;
                var p = c.Player(id);
                if (p == null) continue;
                roster.Add(p);
                archetypes.Add(c.ArchetypeById(p.archetypeId));
            }
            if (roster.Count < MatchSimulation.PlayersPerTeam)
                throw new InvalidOperationException("Team '" + team.id + "' needs " + MatchSimulation.PlayersPerTeam + " valid players.");
        }
    }

    /// <summary>Per-player live state (separate from the static PlayerDef).</summary>
    public sealed class PlayerRuntimeState
    {
        public int Index;
        public int Team;
        /// <summary>0..2 within the team; also the defensive matchup (slot guards slot).</summary>
        public int Slot;
        public PlayerDef Def;
        public ArchetypeDef Archetype;
        public MotionState Motion;
        public bool IsHuman;
        public int HotStreak;
        /// <summary>1 = fresh, 0 = gassed. Drains while sprinting, recovers when easing off.</summary>
        public float Stamina = 1f;
        public float StunnedUntil = -99f;
        public float ScreenedUntil = -99f;
        public float JumpStart = -99f;
        public float StealReadyAt;

        public Vec2 Position => Motion.position;
        public AiTendencies Tendencies => Archetype != null ? Archetype.ai : new AiTendencies(0.5f, 0.5f, 0.5f, 0.5f, 0.3f, 0.5f, 0.5f, 0.3f, 0.5f);
    }

    public enum MatchPhase
    {
        /// <summary>Players reset; ball with the offense at the check spot. Clocks stopped.</summary>
        CheckBall = 0,
        Live = 1,
        /// <summary>Brief pause after a score or turnover. Clocks stopped.</summary>
        DeadBall = 2,
        Final = 3,
    }

    public enum MatchEventType
    {
        PossessionChanged = 0,
        BallPickedUp = 1,
        BallLoose = 2,
        CheckBall = 3,
        ShotStarted = 4,
        ShotReleased = 5,
        ShotMade = 6,
        ShotMissed = 7,
        PassThrown = 8,
        PassCaught = 9,
        Interception = 10,
        ShotClockViolation = 11,
        BallCleared = 12,
        LiveBall = 13,
        GameOver = 14,
        Rebound = 15,
        StealAttempt = 16,
        Steal = 17,
        Jump = 18,
        Block = 19,
        Switch = 20,
        Screen = 21,
        PlayCalled = 22,
    }

    public struct MatchEvent
    {
        public MatchEventType Type;
        public int PlayerIndex;
        public int Team;
        /// <summary>Points (ShotMade), feedback (ShotReleased), or pass type — depends on Type.</summary>
        public int Value;
        public float Chance;

        public MatchEvent(MatchEventType type, int playerIndex, int team, int value = 0, float chance = 0f)
        {
            Type = type;
            PlayerIndex = playerIndex;
            Team = team;
            Value = value;
            Chance = chance;
        }
    }

    /// <summary>
    /// The deterministic match core: players, ball, possession, shots, passes, scoring,
    /// clocks, and AI. Unity views read this state each frame; nothing here touches the engine.
    /// The human always controls slot 0 of their team (their "caller"); teammates are AI.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        public const int PlayersPerTeam = 3;

        public readonly MatchSetup Setup;
        public readonly PlayerRuntimeState[] Players;
        public readonly BallState Ball = new BallState();
        public readonly int[] Score = new int[2];
        public readonly MatchStats Stats;
        /// <summary>Events raised during the last <see cref="Step"/>; views may react to them.</summary>
        public readonly List<MatchEvent> Events = new List<MatchEvent>();

        public MatchPhase Phase { get; private set; }
        public float PhaseTimer { get; private set; }
        public int OffenseTeam { get; private set; }
        public int ControlledIndex { get; private set; }
        public float Time { get; private set; }
        public float GameClock { get; private set; }
        public float ShotClock { get; private set; }
        /// <summary>After a steal or defensive rebound the ball must go beyond the arc before a shot.</summary>
        public bool MustClear { get; private set; }
        /// <summary>Clocks run during live play. Tests may freeze them.</summary>
        public bool ClocksRunning { get; set; } = true;

        /// <summary>Player currently charging the shot meter, or -1.</summary>
        public int ChargingIndex { get; private set; } = -1;
        public float ChargeTime { get; private set; }
        public ShotType ChargeType { get; private set; }
        public float ChargeMeter => ChargingIndex < 0 ? 0f : ChargeTime / ShotModel.FillSeconds(ChargeType, Setup.Shot);
        /// <summary>Meter value of the most recent release (for the meter display).</summary>
        public float LastReleaseMeter { get; private set; }

        /// <summary>Winning team once <see cref="Phase"/> is Final (-1 before).</summary>
        public int Winner { get; private set; } = -1;
        public GameOverReason EndReason { get; private set; }

        private readonly SeededRandom _rng;
        private readonly Vec2[] _scratch;
        private readonly Vec2[] _desired;
        private readonly List<PassCandidate> _candidates = new List<PassCandidate>(PlayersPerTeam);
        private readonly List<Vec2> _defenders = new List<Vec2>(PlayersPerTeam);

        private int _nextOffense;
        private bool _finalPending;
        private float _aiReleaseMeter = -1f;
        private bool _reboundable;
        private int _lastPasser = -1;
        private int _lastCatcher = -1;
        private float _lastCatchTime = -99f;
        private bool _lastShotWasMiss;

        public MatchSimulation(MatchSetup setup)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _rng = new SeededRandom(setup.Seed);
            Players = new PlayerRuntimeState[PlayersPerTeam * 2];
            for (int team = 0; team < 2; team++)
            {
                var roster = team == 0 ? setup.RosterA : setup.RosterB;
                var archetypes = team == 0 ? setup.ArchetypesA : setup.ArchetypesB;
                for (int slot = 0; slot < PlayersPerTeam; slot++)
                {
                    int index = team * PlayersPerTeam + slot;
                    Players[index] = new PlayerRuntimeState
                    {
                        Index = index,
                        Team = team,
                        Slot = slot,
                        Def = roster[slot],
                        Archetype = slot < archetypes.Count ? archetypes[slot] : null,
                        IsHuman = slot == 0 && (team == setup.HumanTeam || setup.SecondHuman),
                        Motion = new MotionState(Vec2.Zero),
                    };
                }
            }
            Stats = new MatchStats(Players.Length);
            _scratch = new Vec2[Players.Length];
            _desired = new Vec2[Players.Length];
            InitAi();
            InitDefense();
            GameClock = setup.Rules.useGameClock ? setup.Rules.gameClockSeconds : 0f;
            ShotClock = setup.Rules.shotClockSeconds;
            ControlledIndex = IndexOf(setup.HumanTeam, 0);
            SecondControlledIndex = setup.SecondHuman ? IndexOf(1 - setup.HumanTeam, 0) : -1;
            CheckBall(setup.StartingOffense);
        }

        public PlayerRuntimeState Controlled => Players[ControlledIndex];
        /// <summary>Player 2's player in local 2-player, otherwise -1.</summary>
        public int SecondControlledIndex { get; private set; } = -1;

        /// <summary>The human-controlled player on <paramref name="team"/>, or -1 if that team is all AI.</summary>
        public int HumanIndexOf(int team) =>
            team == Setup.HumanTeam ? ControlledIndex : (SecondControlledIndex >= 0 && Players[SecondControlledIndex].Team == team ? SecondControlledIndex : -1);

        /// <summary>True if <paramref name="index"/> is driven by a person (player 1 or player 2).</summary>
        public bool IsHumanControlled(int index) => index == ControlledIndex || (index >= 0 && index == SecondControlledIndex);
        public int HolderIndex => Ball.IsHeld ? Ball.HolderIndex : -1;
        public PlayerRuntimeState Holder => Ball.IsHeld ? Players[Ball.HolderIndex] : null;
        public int DefenseTeam => 1 - OffenseTeam;
        public bool IsOver => Phase == MatchPhase.Final;
        public bool HumanHasBall => Ball.IsHeld && Ball.HolderIndex == ControlledIndex;
        public bool HumanTeamHasBall => Ball.IsHeld && Players[Ball.HolderIndex].Team == Setup.HumanTeam;

        public static int IndexOf(int team, int slot) => team * PlayersPerTeam + slot;

        /// <summary>Puts the ball in a teammate's hands during live play (tutorial "ASK" step).</summary>
        public void HandBallTo(int playerIndex)
        {
            if (Phase != MatchPhase.Live || playerIndex < 0 || playerIndex >= Players.Length) return;
            if (Ball.IsHeld && Players[Ball.HolderIndex].Team != Players[playerIndex].Team) return;
            CancelCharge();
            GiveBall(playerIndex, announce: true, fromCheck: false);
        }

        /// <summary>Restarts play with a check ball for <paramref name="team"/> (tutorial steps, tools).</summary>
        public void RestartWithBall(int team)
        {
            if (Phase == MatchPhase.Final) return;
            CancelCharge();
            MustClear = false;
            CheckBall(team);
        }

        // ------------------------------------------------------------------ phases

        /// <summary>Resets positions for a check-ball: offense spread out, ball to slot 0 at the check spot.</summary>
        public void CheckBall(int offenseTeam)
        {
            OffenseTeam = offenseTeam;
            var court = Setup.Court;
            for (int slot = 0; slot < PlayersPerTeam; slot++)
            {
                var o = Players[IndexOf(offenseTeam, slot)];
                o.Motion = new MotionState(Formation.OffenseSpot(slot, court), Facing8.S);
            }
            for (int slot = 0; slot < PlayersPerTeam; slot++)
            {
                var man = Players[IndexOf(offenseTeam, slot)];
                var d = Players[IndexOf(1 - offenseTeam, slot)];
                d.Motion = new MotionState(Formation.GuardSpot(man.Position, slot == 0, court), Facing8.N);
            }
            CancelCharge();
            EndPlay();
            ResetMatchups();
            GiveBall(IndexOf(offenseTeam, 0), announce: false, fromCheck: true);
            MustClear = false;
            _reboundable = false;
            ShotClock = Setup.Rules.shotClockSeconds;
            Phase = MatchPhase.CheckBall;
            PhaseTimer = Setup.Flow.checkBallSeconds;
            ResetAiTimers();
            Events.Add(new MatchEvent(MatchEventType.CheckBall, Ball.HolderIndex, offenseTeam));
        }

        private void GoLive()
        {
            Phase = MatchPhase.Live;
            PhaseTimer = 0f;
            Events.Add(new MatchEvent(MatchEventType.LiveBall, Ball.HolderIndex, OffenseTeam));
        }

        private void GoDead(int nextOffense, float seconds)
        {
            CancelCharge();
            _nextOffense = nextOffense;
            Phase = MatchPhase.DeadBall;
            PhaseTimer = seconds;
        }

        private void EndGame(GameOverReason reason)
        {
            CancelCharge();
            Phase = MatchPhase.Final;
            EndReason = reason;
            int w = Scoring.Winner(Score[0], Score[1]);
            Winner = w;
            Events.Add(new MatchEvent(MatchEventType.GameOver, -1, w));
        }

        // ------------------------------------------------------------------ step

        private PlayerInput _input2;

        /// <summary>Advances the match by <paramref name="dt"/> seconds using the human's input.</summary>
        public void Step(float dt, PlayerInput input) => Step(dt, input, default);

        /// <summary>Two-player step: <paramref name="input2"/> drives player 2 (ignored unless SecondHuman).</summary>
        public void Step(float dt, PlayerInput input, PlayerInput input2)
        {
            _input2 = SecondControlledIndex >= 0 ? input2 : default;
            Events.Clear();
            if (dt <= 0f) return;
            Time += dt;

            switch (Phase)
            {
                case MatchPhase.Final:
                    Integrate(dt, freezeAll: true);
                    return;

                case MatchPhase.DeadBall:
                    PhaseTimer -= dt;
                    Integrate(dt, freezeAll: true);
                    StepBallDead(dt);
                    if (PhaseTimer <= 0f)
                    {
                        if (_finalPending) EndGame(EvaluateEnd());
                        else CheckBall(_nextOffense);
                    }
                    return;

                case MatchPhase.CheckBall:
                    PhaseTimer -= dt;
                    bool humanStarts = (HumanHasBall && (input.Move.SqrMagnitude > 0.04f || input.ShootPressed || input.PassPressed))
                        || (SecondControlledIndex >= 0 && Ball.IsHeld && Ball.HolderIndex == SecondControlledIndex
                            && (_input2.Move.SqrMagnitude > 0.04f || _input2.ShootPressed || _input2.PassPressed));
                    if (PhaseTimer <= 0f || humanStarts) GoLive();
                    else
                    {
                        StepHeldBall(dt);
                        return;
                    }
                    break;
            }

            StepLive(dt, input);
        }

        private void StepLive(float dt, PlayerInput input)
        {
            HandleHuman(ControlledIndex, input);
            if (SecondControlledIndex >= 0) HandleHuman(SecondControlledIndex, _input2);
            UpdatePlay();
            UpdateAi(dt);
            UpdateCharge(dt, input);

            // Desired movement.
            for (int i = 0; i < Players.Length; i++)
            {
                if (i == ChargingIndex) { _desired[i] = Vec2.Zero; continue; }
                if (i == ControlledIndex) { _desired[i] = input.Move; continue; }
                if (i == SecondControlledIndex) { _desired[i] = _input2.Move; continue; }
                _desired[i] = AiDesired(Players[i]);
            }
            Integrate(dt, freezeAll: false);

            StepBall(dt);

            if (MustClear && Ball.IsHeld && Players[Ball.HolderIndex].Team == OffenseTeam
                && Setup.Court.ZoneOf(Players[Ball.HolderIndex].Position) == ShotZone.BeyondArc)
            {
                MustClear = false;
                Events.Add(new MatchEvent(MatchEventType.BallCleared, Ball.HolderIndex, OffenseTeam));
            }

            if (Phase == MatchPhase.Live) TickClocks(dt);
        }

        private void Integrate(float dt, bool freezeAll)
        {
            var tuning = Setup.Movement;
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                var desired = freezeAll || Time < p.StunnedUntil ? Vec2.Zero : _desired[i];
                bool dribbling = Ball.IsHeld && Ball.HolderIndex == i;
                float scale = IsHumanControlled(i) || p.Team == Setup.HumanTeam ? 1f : AiProfile(p.Team).movementScale;
                if (Time < p.ScreenedUntil) scale *= Setup.Defense.screenSlow;
                float max = Movement.MaxSpeed(p.Def.attributes.speed, dribbling, tuning, scale) * StaminaSpeedFactor(p);
                p.Motion = Movement.Step(p.Motion, desired, max, dt, tuning);
                UpdateStamina(p, max, dt, freezeAll);
                _scratch[i] = p.Motion.position;
            }
            Movement.Separate(_scratch, tuning.bodyRadius, Setup.Court);
            for (int i = 0; i < Players.Length; i++) Players[i].Motion.position = _scratch[i];
        }

        private void TickClocks(float dt)
        {
            if (!ClocksRunning) return;
            var rules = Setup.Rules;
            bool shotInAir = Ball.Phase == BallPhase.Shot;

            if (rules.useGameClock && GameClock > 0f) GameClock = Math.Max(0f, GameClock - dt);
            if (!shotInAir && Ball.Phase != BallPhase.Loose) ShotClock = Math.Max(0f, ShotClock - dt);

            if (!shotInAir && ShotClock <= 0f && rules.shotClockSeconds < 99f)
            {
                int holder = Ball.IsHeld ? Ball.HolderIndex : -1;
                if (holder >= 0) Stats[holder].turnovers++;
                Events.Add(new MatchEvent(MatchEventType.ShotClockViolation, holder, OffenseTeam));
                GoDead(DefenseTeam, Setup.Flow.deadBallAfterTurnover);
                return;
            }

            if (rules.useGameClock && GameClock <= 0f && !shotInAir)
            {
                var reason = Scoring.Evaluate(Score[0], Score[1], GameClock, rules);
                if (reason != GameOverReason.None) EndGame(reason);
            }
        }

        private GameOverReason EvaluateEnd()
        {
            var r = Scoring.Evaluate(Score[0], Score[1], GameClock, Setup.Rules);
            return r == GameOverReason.None ? GameOverReason.TargetScore : r;
        }

        // ------------------------------------------------------------------ human

        /// <summary>Applies one person's buttons to their player (offense or defense).</summary>
        private void HandleHuman(int me, PlayerInput input)
        {
            int team = Players[me].Team;
            if (OffenseTeam != team)
            {
                HandleHumanDefense(me, input);
                return;
            }
            if (input.CallPlay != PlayCall.None) StartPlay(team, input.CallPlay, me);
            bool hasBall = Ball.IsHeld && Ball.HolderIndex == me;
            bool teamHasBall = Ball.IsHeld && Players[Ball.HolderIndex].Team == team;
            if (hasBall && ChargingIndex < 0)
            {
                if (input.ShootPressed && CanShoot(me)) BeginCharge(me, -1f);
                else if (input.PassPressed) PassFrom(me, input.Move, -1);
            }
            else if (input.PassPressed && teamHasBall && !IsHumanControlled(Ball.HolderIndex) && ChargingIndex != Ball.HolderIndex)
            {
                // Call for the ball: the AI teammate with it passes to this person.
                PassFrom(Ball.HolderIndex, Vec2.Zero, me);
            }
        }

        public bool CanShoot(int playerIndex)
        {
            return Phase == MatchPhase.Live && Ball.IsHeld && Ball.HolderIndex == playerIndex
                   && ChargingIndex < 0 && !MustClear;
        }

        /// <summary>The teammate holding the ball passes it to the human (practice drills, ASK).</summary>
        public bool PassToHuman()
        {
            if (!HumanTeamHasBall || HumanHasBall || ChargingIndex == Ball.HolderIndex) return false;
            return PassFrom(Ball.HolderIndex, Vec2.Zero, ControlledIndex);
        }

        /// <summary>Who a pass from the human would go to right now (for the receiver marker), or -1.</summary>
        public int PreviewPassTarget(Vec2 aim)
        {
            if (!HumanHasBall) return -1;
            return SelectPassTarget(ControlledIndex, aim);
        }

        // ------------------------------------------------------------------ shooting

        private void BeginCharge(int shooter, float aiReleaseMeter)
        {
            var p = Players[shooter];
            var court = Setup.Court;
            float dist = court.DistanceToHoop(p.Position);
            var zone = court.ZoneOf(p.Position);
            var toHoop = (court.Hoop - p.Position).Normalized;
            bool attacking = Vec2.Dot(p.Motion.velocity, toHoop) > 1.5f;
            ChargeType = ShotModel.Classify(dist, zone, p.Def.attributes.finishing, attacking, Setup.Shot);
            ChargingIndex = shooter;
            ChargeTime = 0f;
            _aiReleaseMeter = aiReleaseMeter;
            p.Motion.velocity = p.Motion.velocity * 0.3f; // plant for the shot
            p.Motion.facing = Movement.FacingOf(toHoop);
            Events.Add(new MatchEvent(MatchEventType.ShotStarted, shooter, p.Team, (int)ChargeType));
        }

        private void UpdateCharge(float dt, PlayerInput input)
        {
            if (ChargingIndex < 0) return;
            if (!Ball.IsHeld || Ball.HolderIndex != ChargingIndex || Phase != MatchPhase.Live)
            {
                CancelCharge();
                return;
            }
            ChargeTime += dt;
            float fill = ShotModel.FillSeconds(ChargeType, Setup.Shot);
            float meter = ChargeTime / fill;
            float overHold = 1f + Setup.Shot.overHoldSeconds / fill;

            bool release = ChargingIndex == ControlledIndex ? !input.ShootHeld
                         : ChargingIndex == SecondControlledIndex ? !_input2.ShootHeld
                         : meter >= _aiReleaseMeter;
            if (release || meter >= overHold) ReleaseShot(ChargingIndex, meter);
        }

        private void CancelCharge()
        {
            ChargingIndex = -1;
            ChargeTime = 0f;
            _aiReleaseMeter = -1f;
        }

        /// <summary>Builds the context for a shot from <paramref name="shooter"/> at <paramref name="meter"/>.</summary>
        public ShotContext ShotContextFor(int shooter, ShotType type, float meter)
        {
            var p = Players[shooter];
            var court = Setup.Court;
            ContestFor(p, out float nearestDist, out int nearestDefense);
            return new ShotContext
            {
                Type = type,
                Distance = court.DistanceToHoop(p.Position),
                Meter = meter,
                Shooter = p.Def.attributes,
                NearestDefenderDistance = nearestDist,
                NearestDefenderDefense = nearestDefense,
                HotStreak = p.HotStreak,
                Stamina01 = p.Stamina,
                LateGame = Setup.Rules.useGameClock && GameClock <= Setup.Flow.lateGameSeconds,
            };
        }

        private void NearestOpponent(PlayerRuntimeState p, out float distance, out int defense)
        {
            distance = float.MaxValue;
            defense = 50;
            if (Setup.PassiveOpponents) return;
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team == p.Team) continue;
                float d = Vec2.Distance(o.Position, p.Position);
                if (d < distance)
                {
                    distance = d;
                    defense = o.Def.attributes.defense;
                }
            }
        }

        private void ReleaseShot(int shooter, float meter)
        {
            var p = Players[shooter];
            var court = Setup.Court;
            var flow = Setup.Flow;
            var type = ChargeType;
            int blocker = TryBlock(p, type);
            if (blocker >= 0)
            {
                LastReleaseMeter = meter;
                Events.Add(new MatchEvent(MatchEventType.ShotReleased, shooter, p.Team,
                    (int)ShotModel.Evaluate(ShotContextFor(shooter, type, meter), Setup.Shot).Feedback, 0f));
                ResolveBlock(shooter, blocker);
                return;
            }
            var ctx = ShotContextFor(shooter, type, meter);
            LastReleaseMeter = meter;
            var eval = ShotModel.Evaluate(ctx, Setup.Shot);
            bool makes = eval.GuaranteedMake || _rng.NextFloat() < eval.MakeChance;
            var zone = court.ZoneOf(p.Position);
            int points = Scoring.PointsFor(zone, Setup.Rules);

            var line = Stats[shooter];
            if (eval.Grade == TimingGrade.Green) line.greenReleases++;
            line.fieldGoalsAttempted++;
            if (zone == ShotZone.BeyondArc) line.arcAttempted++;

            CancelCharge();
            Ball.Phase = BallPhase.Shot;
            Ball.HolderIndex = -1;
            Ball.ShooterIndex = shooter;
            Ball.ShotWillScore = makes;
            Ball.ShotPoints = points;
            Ball.ShotGrade = eval.Grade;
            Ball.ShotType = type;
            Ball.FlightFrom = p.Position;
            Ball.FlightTo = court.Hoop;
            Ball.FlightTime = 0f;
            float dist = ctx.Distance;
            bool dunk = type == ShotType.Dunk;
            Ball.FlightDuration = dunk ? flow.dunkFlightSeconds : flow.shotFlightBase + dist * flow.shotFlightPerMeter;
            Ball.FlightStartHeight = dunk ? flow.dunkReleaseHeight : flow.releaseHeight;
            Ball.FlightEndHeight = RimHeight;
            Ball.FlightArc = dunk ? 0.2f : flow.shotArcBase + dist * flow.shotArcPerMeter;
            Ball.Position = p.Position;
            Ball.Height = Ball.FlightStartHeight;
            _reboundable = false;

            Events.Add(new MatchEvent(MatchEventType.ShotReleased, shooter, p.Team, (int)eval.Feedback, eval.MakeChance));
        }

        public const float RimHeight = 3.05f;

        private void ResolveShot()
        {
            int shooter = Ball.ShooterIndex;
            var p = Players[shooter];
            var line = Stats[shooter];
            var court = Setup.Court;

            if (Ball.ShotWillScore)
            {
                Score[p.Team] += Ball.ShotPoints;
                line.points += Ball.ShotPoints;
                line.fieldGoalsMade++;
                if (Ball.ShotPoints >= Setup.Rules.beyondArcPoints) line.arcMade++;
                p.HotStreak++;
                if (_lastCatcher == shooter && _lastPasser >= 0 && Players[_lastPasser].Team == p.Team
                    && Time - _lastCatchTime <= Setup.Flow.assistWindowSeconds)
                    Stats[_lastPasser].assists++;
                _lastPasser = -1;
                _lastCatcher = -1;

                Events.Add(new MatchEvent(MatchEventType.ShotMade, shooter, p.Team, Ball.ShotPoints));

                // Ball drops through the net and is dead.
                Ball.Phase = BallPhase.Loose;
                Ball.Position = court.Hoop;
                Ball.Height = RimHeight - 0.4f;
                Ball.Velocity = new Vec2(0f, 0.6f);
                Ball.VerticalVelocity = -1f;

                var end = Scoring.Evaluate(Score[0], Score[1], GameClock, Setup.Rules);
                _finalPending = end != GameOverReason.None;
                // Possession changes after a made basket (practice keeps it for more reps).
                GoDead(Setup.KeepPossessionAfterScore ? p.Team : 1 - p.Team, Setup.Flow.deadBallAfterMake);
                return;
            }

            // Miss: carom off the rim, away from the hoop, toward the shooter's side.
            p.HotStreak = 0;
            _lastShotWasMiss = true;
            Events.Add(new MatchEvent(MatchEventType.ShotMissed, shooter, p.Team, (int)Ball.ShotGrade));
            var away = (Ball.FlightFrom - court.Hoop).Normalized;
            if (away.SqrMagnitude < 0.01f) away = Vec2.Up;
            float angle = (_rng.NextFloat() * 2f - 1f) * 1.1f; // ±63°
            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            var dir = new Vec2(away.x * cos - away.y * sin, away.x * sin + away.y * cos);
            if (dir.y < 0.1f) dir = new Vec2(dir.x, 0.1f).Normalized; // never back through the baseline
            float speed = Setup.Flow.missSpeedMin + (Setup.Flow.missSpeedMax - Setup.Flow.missSpeedMin) * _rng.NextFloat();
            Ball.Phase = BallPhase.Loose;
            Ball.Position = court.Hoop;
            Ball.Height = RimHeight;
            Ball.Velocity = dir * speed;
            Ball.VerticalVelocity = Setup.Flow.missUpSpeed;
            _reboundable = true;
            if (Setup.Flow.resetShotClockOnRim) ShotClock = Setup.Rules.shotClockSeconds;
            Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));

            if (Setup.Rules.useGameClock && GameClock <= 0f)
            {
                var end = Scoring.Evaluate(Score[0], Score[1], GameClock, Setup.Rules);
                if (end != GameOverReason.None) EndGame(end);
            }
        }

        // ------------------------------------------------------------------ passing

        private int SelectPassTarget(int passer, Vec2 aim)
        {
            var p = Players[passer];
            _candidates.Clear();
            _defenders.Clear();
            for (int i = 0; i < Players.Length; i++)
            {
                if (i == passer) continue;
                if (Players[i].Team == p.Team) _candidates.Add(new PassCandidate { PlayerIndex = i, Position = Players[i].Position });
                else if (!Setup.PassiveOpponents) _defenders.Add(Players[i].Position);
            }
            int k = PassModel.SelectTarget(p.Position, aim, _candidates, _defenders, Setup.Pass);
            return k < 0 ? -1 : _candidates[k].PlayerIndex;
        }

        /// <summary>Throws a pass. <paramref name="forcedTarget"/> ≥ 0 skips target selection.</summary>
        private bool PassFrom(int passer, Vec2 aim, int forcedTarget)
        {
            if (Phase != MatchPhase.Live || !Ball.IsHeld || Ball.HolderIndex != passer) return false;
            int target = forcedTarget >= 0 ? forcedTarget : SelectPassTarget(passer, aim);
            if (target < 0 || target == passer) return false;

            var from = Players[passer];
            var to = Players[target];
            var t = Setup.Pass;

            // Lane risk and possible interception.
            float minClear = float.MaxValue;
            int threat = -1;
            if (!Setup.PassiveOpponents)
            {
                for (int i = 0; i < Players.Length; i++)
                {
                    if (Players[i].Team == from.Team) continue;
                    float c = PassModel.LaneClearance(from.Position, to.Position, Players[i].Position, out _);
                    if (c < minClear)
                    {
                        minClear = c;
                        threat = i;
                    }
                }
            }
            var type = PassModel.ChooseType(minClear, t);
            int receiver = target;
            var destination = to.Position;
            if (threat >= 0)
            {
                float chance = PassModel.InterceptChance(type, minClear, Players[threat].Def.attributes.defense,
                                                         from.Def.attributes.playmaking, t);
                if (chance > 0f && _rng.NextFloat() < chance)
                {
                    receiver = threat;
                    PassModel.LaneClearance(from.Position, to.Position, Players[threat].Position, out float along);
                    destination = from.Position + (to.Position - from.Position) * along;
                }
            }

            CancelCharge();
            Ball.Phase = BallPhase.Pass;
            Ball.HolderIndex = -1;
            Ball.PasserIndex = passer;
            Ball.TargetIndex = receiver;
            Ball.PassType = type;
            Ball.FlightFrom = from.Position;
            Ball.FlightTo = destination;
            Ball.FlightTime = 0f;
            Ball.Position = from.Position;
            Ball.Height = Setup.Flow.passHeight;
            _lastPasser = passer;
            OnPassForPlays(passer, receiver);
            Events.Add(new MatchEvent(MatchEventType.PassThrown, passer, from.Team, (int)type));
            OnAiPassed(passer);
            return true;
        }

        // ------------------------------------------------------------------ ball

        private void StepBall(float dt)
        {
            switch (Ball.Phase)
            {
                case BallPhase.Held: StepHeldBall(dt); break;
                case BallPhase.Pass: StepPass(dt); break;
                case BallPhase.Shot: StepShot(dt); break;
                default: StepLoose(dt, allowPickup: true); break;
            }
        }

        private void StepHeldBall(float dt)
        {
            if (!Ball.IsHeld) return;
            var holder = Players[Ball.HolderIndex];
            Ball.DribbleTime += dt;
            Ball.Position = holder.Position + DribbleOffset(holder.Motion.facing);
            Ball.Velocity = holder.Motion.velocity;
            Ball.Height = Ball.HolderIndex == ChargingIndex
                ? 1.6f + 0.6f * Math.Min(1f, ChargeMeter)
                : BallPhysics.DribbleHeight(Ball.DribbleTime, Setup.Ball);
        }

        private void StepPass(float dt)
        {
            var t = Setup.Pass;
            float speed = PassModel.Speed(Ball.PassType, t);
            Ball.FlightTime += dt;
            var receiver = Players[Ball.TargetIndex];
            bool intercepting = receiver.Team != Players[Ball.PasserIndex].Team;
            // Passes to teammates lead the receiver; interceptions fly to the lane point.
            var goal = intercepting ? Ball.FlightTo : receiver.Position;
            Ball.Position = Vec2.MoveTowards(Ball.Position, goal, speed * dt);

            float total = Math.Max(0.1f, Vec2.Distance(Ball.FlightFrom, goal));
            float progress = Math.Min(1f, Vec2.Distance(Ball.FlightFrom, Ball.Position) / total);
            Ball.Height = Ball.PassType == PassType.Bounce
                ? Setup.Flow.passHeight * Math.Abs(1f - 2f * progress) + 0.05f
                : Setup.Flow.passHeight;

            bool arrived = Vec2.Distance(Ball.Position, goal) <= 0.05f
                           || (!intercepting && Vec2.Distance(Ball.Position, receiver.Position) <= t.catchRadius);
            if (arrived)
            {
                if (intercepting)
                {
                    Stats[Ball.TargetIndex].steals++;
                    Stats[Ball.PasserIndex].turnovers++;
                    Events.Add(new MatchEvent(MatchEventType.Interception, Ball.TargetIndex, receiver.Team));
                }
                else
                {
                    _lastCatcher = Ball.TargetIndex;
                    _lastCatchTime = Time;
                    Events.Add(new MatchEvent(MatchEventType.PassCaught, Ball.TargetIndex, receiver.Team));
                }
                GiveBall(Ball.TargetIndex, announce: false, fromCheck: false);
                return;
            }

            if (Ball.FlightTime > Setup.Flow.maxPassSeconds)
            {
                // Safety: a pass that never connects becomes a loose ball.
                Ball.Phase = BallPhase.Loose;
                Ball.Velocity = (goal - Ball.Position).Normalized * 2f;
                Ball.VerticalVelocity = 0f;
                Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));
            }
        }

        private void StepShot(float dt)
        {
            Ball.FlightTime += dt;
            float u = Math.Min(1f, Ball.FlightTime / Ball.FlightDuration);
            Ball.Position = Vec2.Lerp(Ball.FlightFrom, Ball.FlightTo, u);
            Ball.Height = Ball.FlightStartHeight + (Ball.FlightEndHeight - Ball.FlightStartHeight) * u
                          + 4f * Ball.FlightArc * u * (1f - u);
            if (u >= 1f) ResolveShot();
        }

        private void StepBallDead(float dt)
        {
            if (Ball.Phase == BallPhase.Loose) BallPhysics.StepLoose(Ball, dt, Setup.Court, Setup.Ball);
            else if (Ball.Phase == BallPhase.Held) StepHeldBall(dt);
        }

        private void StepLoose(float dt, bool allowPickup)
        {
            var t = Setup.Ball;
            BallPhysics.StepLoose(Ball, dt, Setup.Court, t);
            if (!allowPickup) return;

            int best;
            if (_reboundable)
            {
                best = PickRebounder();
            }
            else
            {
                // Nearest eligible player picks it up (ties go to the lower index: deterministic).
                best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < Players.Length; i++)
                {
                    if (Setup.PassiveOpponents && Players[i].Team != Setup.HumanTeam) continue;
                    if (!BallPhysics.CanPickUp(Ball, Players[i].Position, t)) continue;
                    float d = Vec2.Distance(Ball.Position, Players[i].Position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
            }
            if (best < 0) return;

            if (_reboundable)
            {
                Stats[best].rebounds++;
                Events.Add(new MatchEvent(MatchEventType.Rebound, best, Players[best].Team));
                _reboundable = false;
            }
            GiveBall(best, announce: true, fromCheck: false);
        }

        private void GiveBall(int playerIndex, bool announce, bool fromCheck)
        {
            var p = Players[playerIndex];
            Ball.Phase = BallPhase.Held;
            Ball.HolderIndex = playerIndex;
            Ball.Position = p.Position;
            Ball.Velocity = Vec2.Zero;
            Ball.VerticalVelocity = 0f;
            Ball.DribbleTime = 0f;
            Ball.TargetIndex = -1;
            Ball.ShooterIndex = -1;

            if (announce) Events.Add(new MatchEvent(MatchEventType.BallPickedUp, playerIndex, p.Team));
            if (!fromCheck && p.Team != OffenseTeam)
            {
                OffenseTeam = p.Team;
                ShotClock = Setup.Rules.shotClockSeconds;
                // Change of possession on a live ball: take it back beyond the arc first.
                MustClear = Setup.Court.ZoneOf(p.Position) != ShotZone.BeyondArc;
                _lastPasser = -1;
                _lastCatcher = -1;
                EndPlay();
                ResetMatchups();
                Events.Add(new MatchEvent(MatchEventType.PossessionChanged, playerIndex, p.Team));
                ResetAiTimers();
            }
            else if (!fromCheck && _lastShotWasMiss)
            {
                // Offensive rebound keeps the possession; no clear needed.
                MustClear = false;
            }
            _lastShotWasMiss = false;
        }

        /// <summary>Knocks the ball loose from its holder (debug / future steals).</summary>
        public void KnockLoose(Vec2 velocity, float upwardSpeed = 2.5f)
        {
            CancelCharge();
            var from = Ball.IsHeld ? Players[Ball.HolderIndex].Position : Ball.Position;
            Ball.Phase = BallPhase.Loose;
            Ball.HolderIndex = -1;
            Ball.Position = from;
            Ball.Velocity = velocity;
            Ball.Height = Math.Max(Ball.Height, 0.8f);
            Ball.VerticalVelocity = upwardSpeed;
            Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));
        }

        /// <summary>Ball sits slightly to the side/front of the dribbler based on facing.</summary>
        public static Vec2 DribbleOffset(Facing8 facing)
        {
            switch (facing)
            {
                case Facing8.N: return new Vec2(0.25f, 0.15f);
                case Facing8.NE: return new Vec2(0.3f, 0.05f);
                case Facing8.E: return new Vec2(0.32f, -0.05f);
                case Facing8.SE: return new Vec2(0.25f, -0.2f);
                case Facing8.S: return new Vec2(0.25f, -0.2f);
                case Facing8.SW: return new Vec2(-0.25f, -0.2f);
                case Facing8.W: return new Vec2(-0.32f, -0.05f);
                default: return new Vec2(-0.3f, 0.05f);
            }
        }
    }
}
