using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum HorseOpponent { Cpu = 0, Friend = 1 }

    /// <summary>
    /// H-O-R-S-E. The setter shoots from anywhere; make it and the other shooter must make the same
    /// shot (from the same spot) or take a letter. Miss it and the turn passes. Spell HORSE and you lose.
    /// Against the CPU its shots are simulated with the real shot model; against a friend you pass
    /// the phone (both of you shoot with the same player).
    /// </summary>
    public sealed class HorseSession
    {
        public const string Word = "HORSE";
        public const float MatchRadius = 1.3f;
        public const float ResetDelay = 0.7f;
        public const float CpuThinkSeconds = 1.4f;

        /// <summary>Letters each shooter has (0 = you / player 1, 1 = CPU / player 2).</summary>
        public readonly int[] Letters = new int[2];
        /// <summary>Whose shot it is.</summary>
        public int Shooter { get; private set; }
        /// <summary>True when the shooter must match <see cref="SpotToMatch"/>.</summary>
        public bool Matching { get; private set; }
        public Vec2 SpotToMatch { get; private set; }
        public bool Finished { get; private set; }
        public int Winner { get; private set; } = -1;
        /// <summary>Plain-text callout for the last thing that happened (shown as a toast).</summary>
        public string LastCall { get; private set; }
        /// <summary>Bumped every time something is resolved (the view uses it to show <see cref="LastCall"/> once).</summary>
        public int Version { get; private set; }

        public readonly HorseOpponent Opponent;
        public readonly List<Vec2> CpuSpots = new List<Vec2>();
        public Vec2 LastCpuSpot { get; private set; }

        private readonly PlayerDef _cpu;
        private readonly DifficultyDef _difficulty;
        private readonly ShotTuning _tuning;
        private readonly GameRulesDef _rules;
        private readonly CourtGeometry _court;
        private readonly SeededRandom _rng;
        private Vec2 _releasePos;
        private bool _released;
        private float _resumeAt = -1f;
        private float _cpuAt = -1f;

        public HorseSession(HorseOpponent opponent, CourtGeometry court, ShotTuning tuning, GameRulesDef rules,
                            PlayerDef cpu = null, DifficultyDef difficulty = null, uint seed = 1)
        {
            Opponent = opponent;
            _court = court;
            _tuning = tuning ?? ShotTuning.Default;
            _rules = rules ?? new GameRulesDef();
            _cpu = cpu;
            _difficulty = difficulty;
            _rng = new SeededRandom(seed == 0 ? 1u : seed);
            // Spots the CPU likes: corners, wings, top, elbows, short corners.
            foreach (var (deg, r) in new[] { (6f, 7.3f), (174f, 7.3f), (40f, 7.3f), (140f, 7.3f), (90f, 7.4f), (60f, 4.2f), (120f, 4.2f), (20f, 2.6f), (160f, 2.6f) })
            {
                double a = deg * Math.PI / 180.0;
                float radius = Math.Min(r, court.arcRadius + 0.6f);
                CpuSpots.Add(court.Clamp(new Vec2((float)Math.Cos(a) * radius, court.hoopY + (float)Math.Sin(a) * radius)));
            }
        }

        public bool IsCpuTurn => !Finished && Opponent == HorseOpponent.Cpu && Shooter == 1;
        public string LettersOf(int who) => Word.Substring(0, Math.Min(Word.Length, Letters[who]));

        /// <summary>The rules: one shot by the current shooter, made or missed, from <paramref name="spot"/>.</summary>
        public void Resolve(bool made, Vec2 spot)
        {
            if (Finished) return;
            int me = Shooter, other = 1 - Shooter;
            Version++;
            if (Matching)
            {
                bool matched = made && Vec2.Distance(spot, SpotToMatch) <= MatchRadius;
                if (!matched)
                {
                    Letters[me]++;
                    LastCall = made ? "WRONG SPOT: " + Name(me) + " GETS " + Word[Letters[me] - 1] : Name(me) + " GETS " + Word[Letters[me] - 1];
                }
                else LastCall = Name(me) + " MATCHES IT";
                Matching = false;
                // The setter goes again.
                Shooter = other;
                if (Letters[me] >= Word.Length)
                {
                    Finished = true;
                    Winner = other;
                    LastCall = Name(other) + " WINS H-O-R-S-E";
                }
                return;
            }
            if (made)
            {
                Matching = true;
                SpotToMatch = spot;
                Shooter = other;
                LastCall = Name(me) + " SETS IT: MATCH THAT";
            }
            else
            {
                Shooter = other;
                LastCall = Name(me) + " MISSES: " + Name(other) + "'S SHOT";
            }
        }

        public string Name(int who) =>
            Opponent == HorseOpponent.Friend ? (who == 0 ? "P1" : "P2") : (who == 0 ? "YOU" : (_cpu != null ? _cpu.lastName.ToUpperInvariant() : "CPU"));

        /// <summary>Make chance for a CPU shot from <paramref name="spot"/> (shot model, open look).</summary>
        public float CpuMakeChance(Vec2 spot)
        {
            if (_cpu == null) return 0.4f;
            float dist = _court.DistanceToHoop(spot);
            var zone = _court.ZoneOf(spot);
            var type = ShotModel.Classify(dist, zone, _cpu.attributes.finishing, false, _tuning);
            float accuracy = _difficulty != null ? _difficulty.releaseAccuracy : 0.55f;
            float meter = _tuning.greenCenter + (_rng.NextFloat() * 2f - 1f) * (1f - accuracy) * 0.3f;
            var ctx = new ShotContext
            {
                Type = type, Distance = dist, Meter = meter, Shooter = _cpu.attributes,
                NearestDefenderDistance = float.MaxValue, NearestDefenderDefense = 50, Stamina01 = 1f,
            };
            return ShotModel.Evaluate(ctx, _tuning).MakeChance;
        }

        /// <summary>The CPU takes its shot (sets one, or matches yours).</summary>
        public bool CpuShoot()
        {
            var spot = Matching ? SpotToMatch : CpuSpots[_rng.Range(0, CpuSpots.Count)];
            LastCpuSpot = spot;
            bool made = _rng.NextFloat() < CpuMakeChance(spot);
            Resolve(made, spot);
            return made;
        }

        /// <summary>Call after every simulation step: watches the person's shots and runs the CPU's turns.</summary>
        public void Update(MatchSimulation m)
        {
            if (Finished) return;
            int me = m.ControlledIndex;
            if (IsCpuTurn)
            {
                // Hold the ball while the CPU "shoots".
                if (_cpuAt < 0f) _cpuAt = m.Time + CpuThinkSeconds;
                if (m.Time >= _cpuAt)
                {
                    _cpuAt = -1f;
                    CpuShoot();
                    if (!Finished) m.ResumeWithBall(me);
                }
                return;
            }
            foreach (var e in m.Events)
            {
                if (e.PlayerIndex != me) continue;
                if (e.Type == MatchEventType.ShotReleased)
                {
                    _released = true;
                    _releasePos = m.Controlled.Position;
                }
                else if (_released && (e.Type == MatchEventType.ShotMade || e.Type == MatchEventType.ShotMissed))
                {
                    _released = false;
                    Resolve(e.Type == MatchEventType.ShotMade, _releasePos);
                    _resumeAt = m.Time + ResetDelay;
                }
            }
            if (_resumeAt >= 0f && m.Time >= _resumeAt)
            {
                _resumeAt = -1f;
                if (!Finished) m.ResumeWithBall(me);
            }
        }
    }
}
