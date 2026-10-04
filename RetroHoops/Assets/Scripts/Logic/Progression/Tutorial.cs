using System;

namespace CallerRetroBall.Logic
{
    public enum TutorialStep
    {
        Move = 0,
        Shoot = 1,
        Green = 2,
        Pass = 3,
        Ask = 4,
        Call = 5,
        Steal = 6,
        Jump = 7,
        Done = 8,
    }

    /// <summary>
    /// How-to-play tutorial. Runs on a practice-rules match (passive opponents) and advances one
    /// step at a time when the player actually does the thing. Defense steps hand the ball to the
    /// other side. Pure logic: the GameScene shows <see cref="Title"/> and the hint for the
    /// player's controls.
    /// </summary>
    public sealed class TutorialSession
    {
        public const float MoveMeters = 3f;
        /// <summary>After this many shots without a green, the green step is passed anyway.</summary>
        public const int GreenAttemptsAllowed = 3;

        public TutorialStep Step { get; private set; } = TutorialStep.Move;
        public bool Finished => Step == TutorialStep.Done;
        /// <summary>Set for one update when a step completes (for a "NICE!" callout).</summary>
        public bool JustAdvanced { get; private set; }

        private float _moved;
        private Vec2 _lastPos;
        private bool _hasLast;
        private int _greenTries;

        public static int StepCount => (int)TutorialStep.Done;

        public string Title => TitleOf(Step);
        public string TouchHint => TouchHintOf(Step);
        public string KeyHint => KeyHintOf(Step);

        public static string TitleOf(TutorialStep s)
        {
            switch (s)
            {
                case TutorialStep.Move: return "MOVE";
                case TutorialStep.Shoot: return "SHOOT";
                case TutorialStep.Green: return "HIT THE GREEN";
                case TutorialStep.Pass: return "PASS";
                case TutorialStep.Ask: return "ASK FOR IT";
                case TutorialStep.Call: return "CALL A PLAY";
                case TutorialStep.Steal: return "STEAL";
                case TutorialStep.Jump: return "JUMP TO CONTEST";
                default: return "YOU'RE READY";
            }
        }

        public static string TouchHintOf(TutorialStep s)
        {
            switch (s)
            {
                case TutorialStep.Move: return "Drag the stick on the left to move";
                case TutorialStep.Shoot: return "Hold SHOOT, then let go to release";
                case TutorialStep.Green: return "Let go when the meter is in the green band";
                case TutorialStep.Pass: return "Tap PASS. Aim with the stick";
                case TutorialStep.Ask: return "A teammate has it. Tap ASK";
                case TutorialStep.Call: return "Tap CALL and pick a play";
                case TutorialStep.Steal: return "Get close to the ball and tap STEAL";
                case TutorialStep.Jump: return "Tap BLOCK to contest a shot";
                default: return "Tutorial complete";
            }
        }

        public static string KeyHintOf(TutorialStep s)
        {
            switch (s)
            {
                case TutorialStep.Move: return "WASD or arrow keys";
                case TutorialStep.Shoot:
                case TutorialStep.Green: return "Hold K, then let go";
                case TutorialStep.Pass:
                case TutorialStep.Ask: return "J";
                case TutorialStep.Call: return "C";
                case TutorialStep.Steal: return "L";
                case TutorialStep.Jump: return "K";
                default: return "";
            }
        }

        /// <summary>Call after every simulation step.</summary>
        public void Update(MatchSimulation m)
        {
            JustAdvanced = false;
            if (Finished) return;
            int me = m.ControlledIndex;
            int myTeam = m.Setup.HumanTeam;

            var pos = m.Controlled.Position;
            if (_hasLast) _moved += Vec2.Distance(pos, _lastPos);
            _lastPos = pos;
            _hasLast = true;

            switch (Step)
            {
                case TutorialStep.Move:
                    if (_moved >= MoveMeters) Advance(m);
                    break;
                case TutorialStep.Ask:
                    // Make sure a teammate is holding it so ASK has something to do.
                    if (m.HumanHasBall && m.Phase == MatchPhase.Live && m.ChargingIndex < 0 && _askHandOff == 0)
                    {
                        _askHandOff = 1;
                        m.HandBallTo(MatchSimulation.IndexOf(myTeam, 1));
                    }
                    break;
                case TutorialStep.Steal:
                case TutorialStep.Jump:
                    // Defense steps: the other side must have the ball.
                    if (m.OffenseTeam == myTeam && m.Phase != MatchPhase.DeadBall) m.RestartWithBall(1 - myTeam);
                    break;
            }

            // Offense steps: keep the ball flowing back to the player (they haven't learned ASK yet).
            if (Step != TutorialStep.Ask && Step != TutorialStep.Steal && Step != TutorialStep.Jump)
            {
                if (m.OffenseTeam != myTeam && m.Phase != MatchPhase.DeadBall) m.RestartWithBall(myTeam);
                if (m.HumanTeamHasBall && !m.HumanHasBall && m.Phase == MatchPhase.Live)
                {
                    if (_teammateSince < 0f) _teammateSince = m.Time;
                    else if (m.Time - _teammateSince >= HandBackDelay && m.PassToHuman()) _teammateSince = -1f;
                }
                else
                {
                    _teammateSince = -1f;
                }
            }

            foreach (var e in m.Events)
            {
                if (Finished) break;
                switch (Step)
                {
                    case TutorialStep.Shoot when e.Type == MatchEventType.ShotReleased && e.PlayerIndex == me:
                        Advance(m);
                        break;
                    case TutorialStep.Green when e.Type == MatchEventType.ShotReleased && e.PlayerIndex == me:
                        _greenTries++;
                        if (e.Value == (int)ShotFeedback.Green || _greenTries >= GreenAttemptsAllowed) Advance(m);
                        break;
                    case TutorialStep.Pass when e.Type == MatchEventType.PassThrown && e.PlayerIndex == me:
                        Advance(m);
                        break;
                    case TutorialStep.Ask when e.Type == MatchEventType.PassCaught && e.PlayerIndex == me && _askHandOff == 1:
                        Advance(m);
                        break;
                    case TutorialStep.Call when e.Type == MatchEventType.PlayCalled && e.Team == myTeam:
                        Advance(m);
                        break;
                    case TutorialStep.Steal when e.Type == MatchEventType.StealAttempt && e.PlayerIndex == me:
                        Advance(m);
                        break;
                    case TutorialStep.Jump when e.Type == MatchEventType.Jump && e.PlayerIndex == me:
                        Advance(m);
                        break;
                }
            }
        }

        private int _askHandOff;
        private float _teammateSince = -1f;
        public const float HandBackDelay = 0.6f;

        private void Advance(MatchSimulation m)
        {
            Step = (TutorialStep)Math.Min((int)TutorialStep.Done, (int)Step + 1);
            JustAdvanced = true;
            _askHandOff = 0;
        }
    }
}
