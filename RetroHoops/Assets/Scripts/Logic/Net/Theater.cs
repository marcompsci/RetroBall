using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A place on a tape worth jumping to.</summary>
    public sealed class TapeMark
    {
        /// <summary>The step the mark points at (the play starts a little before it).</summary>
        public int Tick;
        public string Label = "";
        /// <summary>Found by the game (a basket, a block, a steal) rather than added by you.</summary>
        public bool Auto;
    }

    /// <summary>What the replay theater camera looks at.</summary>
    public enum TheaterCamera
    {
        /// <summary>Follows the ball (the usual broadcast view).</summary>
        Ball = 0,
        /// <summary>Stays on one player (tap CAMERA again for the next one).</summary>
        Player = 1,
        /// <summary>Parks under the basket being attacked.</summary>
        Rim = 2,
        /// <summary>Follows the ball, one zoom step closer.</summary>
        CloseUp = 3,
    }

    /// <summary>
    /// Phase 35 REPLAY THEATER: the controls for watching a game tape: pause, slow motion (0.25x and 0.5x), fast (2x and 4x),
    /// scrubbing to any moment, camera choices, and marks (every basket, block and steal is marked for you; add your
    /// own). A tape is the game's inputs, so going forward means simulating ahead and going back means starting the
    /// game over and simulating up to that moment (fast: a whole game is a few hundred thousand cheap steps at most).
    /// Nothing here touches the simulation itself.
    /// </summary>
    public sealed class TapeTheater
    {
        public static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f, 4f };
        public const int NormalSpeed = 2;
        /// <summary>Jumping to a mark starts this many steps before it, so you see the play build.</summary>
        public const int LeadIn = 150;
        /// <summary>PREVIOUS from just after a mark goes to the one before it (not the same one again).</summary>
        public const int BackGrace = 90;
        public const int MaxUserMarks = Tapes.MaxMarks;

        public readonly GameTape Tape;
        public readonly List<TapeMark> Marks = new List<TapeMark>();
        public bool Indexed { get; private set; }
        public int SpeedIndex { get; private set; } = NormalSpeed;
        public bool Paused { get; set; }
        public TheaterCamera Camera { get; private set; } = TheaterCamera.Ball;
        /// <summary>The player the PLAYER camera follows.</summary>
        public int CameraPlayer { get; private set; }

        private float _carry;

        public TapeTheater(GameTape tape) { Tape = tape; }

        public float Speed => Speeds[SpeedIndex];
        public int Steps => Tape?.Steps ?? 0;

        public string SpeedLabel
        {
            get
            {
                float s = Speed;
                return s == 0.25f ? "0.25x" : s == 0.5f ? "0.5x" : ((int)s) + "x";
            }
        }

        public void Faster() { if (SpeedIndex < Speeds.Length - 1) SpeedIndex++; }
        public void Slower() { if (SpeedIndex > 0) SpeedIndex--; }
        public void CycleSpeed() => SpeedIndex = (SpeedIndex + 1) % Speeds.Length;
        public void SetSpeed(int index) => SpeedIndex = Math.Max(0, Math.Min(Speeds.Length - 1, index));

        /// <summary>Next camera; on the PLAYER camera, the next player first (through all <paramref name="players"/>).</summary>
        public void NextCamera(int players)
        {
            if (Camera == TheaterCamera.Player && players > 0 && CameraPlayer < players - 1)
            {
                CameraPlayer++;
                return;
            }
            Camera = (TheaterCamera)(((int)Camera + 1) % 4);
            CameraPlayer = 0;
        }

        public void SetCamera(TheaterCamera c, int player = 0)
        {
            Camera = c;
            CameraPlayer = Math.Max(0, player);
        }

        public static string CameraName(TheaterCamera c)
        {
            switch (c)
            {
                case TheaterCamera.Player: return "PLAYER CAM";
                case TheaterCamera.Rim: return "RIM CAM";
                case TheaterCamera.CloseUp: return "CLOSE-UP";
                default: return "BALL CAM";
            }
        }

        /// <summary>
        /// Steps to simulate this frame: real time × speed, carrying the fraction over (so ¼ speed is one step in four
        /// frames, evenly). Never more than <paramref name="maxSteps"/>, and none while paused.
        /// </summary>
        public int StepsFor(float dt, float fixedStep, int maxSteps)
        {
            if (Paused || fixedStep <= 0f || dt <= 0f) return 0;
            _carry += dt * Speed / fixedStep;
            int n = (int)Math.Floor(_carry);
            if (n > maxSteps)
            {
                n = maxSteps;
                _carry = 0f;
            }
            else _carry -= n;
            return Math.Max(0, n);
        }

        /// <summary>
        /// How to get from step <paramref name="current"/> to <paramref name="target"/>: simulate forward from here,
        /// or start over (going back) and simulate from the tip-off.
        /// </summary>
        public static int SeekPlan(int current, int target, int steps, out bool restart)
        {
            target = Math.Max(0, Math.Min(steps, target));
            restart = target < current;
            return restart ? target : target - current;
        }

        /// <summary>The step a scrub bar at <paramref name="fraction"/> (0..1) means.</summary>
        public int TickAt(float fraction) => (int)Math.Round(Math.Max(0f, Math.Min(1f, fraction)) * Steps);

        public float FractionOf(int tick) => Steps <= 0 ? 0f : Math.Max(0f, Math.Min(1f, tick / (float)Steps));

        /// <summary>"3:07" for a step count at 60 steps a second.</summary>
        public static string Clock(int tick)
        {
            int s = Math.Max(0, tick) / 60;
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ------------------------------------------------------------------ marks

        private MatchSimulation _indexSim;
        private int _indexTick;

        /// <summary>How far the marking pass has got (0..1).</summary>
        public float IndexProgress => Indexed ? 1f : Steps <= 0 ? 0f : _indexTick / (float)Steps;

        /// <summary>
        /// Marks every basket, block and steal by simulating the whole tape once with a fresh match built by
        /// <paramref name="freshMatch"/> (the same setup the screen plays it with). All at once: tests and tools.
        /// </summary>
        public void Index(Func<MatchSimulation> freshMatch, float fixedStep)
        {
            if (Indexed || Tape == null || freshMatch == null) return;
            StartIndex(freshMatch);
            while (!IndexSome(int.MaxValue, fixedStep)) { }
        }

        /// <summary>Phase 36: the marking pass a slice at a time (the screen does a few hundred steps a frame, so it never freezes).</summary>
        public void StartIndex(Func<MatchSimulation> freshMatch)
        {
            if (Indexed || _indexSim != null || Tape == null || freshMatch == null) return;
            _indexSim = freshMatch();
            _indexTick = 0;
            if (_indexSim == null) Indexed = true;
        }

        /// <summary>Simulates up to <paramref name="maxSteps"/> more steps of the marking pass. True once it's finished.</summary>
        public bool IndexSome(int maxSteps, float fixedStep)
        {
            if (Indexed) return true;
            if (_indexSim == null) return false;
            var m = _indexSim;
            for (int n = 0; n < maxSteps && _indexTick < Tape.Steps && !m.IsOver; n++)
            {
                m.Step(fixedStep, Tape.TeamA[_indexTick], Tape.TeamB[_indexTick]);
                _indexTick++;
                foreach (var e in m.Events)
                {
                    string label = LabelFor(m, e);
                    if (label != null) AddMark(_indexTick, label, true);
                }
            }
            if (_indexTick >= Tape.Steps || m.IsOver)
            {
                Indexed = true;
                _indexSim = null;
            }
            return Indexed;
        }

        /// <summary>What a mark for this event says, or null if it isn't worth one.</summary>
        public static string LabelFor(MatchSimulation m, MatchEvent e)
        {
            string who = e.PlayerIndex >= 0 && e.PlayerIndex < m.Players.Length ? m.Players[e.PlayerIndex].Def.DisplayName.ToUpperInvariant() : "";
            switch (e.Type)
            {
                case MatchEventType.ShotMade:
                    return who + " " + (e.Value >= 3 || (e.Value >= 2 && !m.Setup.FullCourt) ? "FROM DEEP" : "SCORES") + "  " + m.Score[0] + "-" + m.Score[1];
                case MatchEventType.Block: return who + " BLOCK";
                case MatchEventType.Steal: return who + " STEAL";
                default: return null;
            }
        }

        public TapeMark AddMark(int tick, string label, bool auto)
        {
            if (!auto && Marks.FindAll(x => !x.Auto).Count >= MaxUserMarks) return null;
            var mark = new TapeMark { Tick = Math.Max(0, Math.Min(Steps, tick)), Label = label ?? "", Auto = auto };
            int at = Marks.FindIndex(x => x.Tick > mark.Tick);
            if (at < 0) Marks.Add(mark);
            else Marks.Insert(at, mark);
            return mark;
        }

        /// <summary>Your own mark at <paramref name="tick"/> ("MARK 3 · 2:41").</summary>
        public TapeMark AddUserMark(int tick)
        {
            int n = Marks.FindAll(x => !x.Auto).Count + 1;
            return AddMark(tick, "MARK " + n + " · " + Clock(tick), false);
        }

        /// <summary>The first mark whose play starts after <paramref name="tick"/>.</summary>
        public TapeMark NextMark(int tick)
        {
            foreach (var m in Marks) if (StartOf(m) > tick) return m;
            return null;
        }

        /// <summary>The last mark whose play starts before <paramref name="tick"/> (allowing a moment, so repeated taps go further back).</summary>
        public TapeMark PreviousMark(int tick)
        {
            TapeMark best = null;
            foreach (var m in Marks) if (StartOf(m) < tick - BackGrace) best = m;
            return best;
        }

        /// <summary>Where playback starts for a mark (a little before it).</summary>
        public static int StartOf(TapeMark m) => m == null ? 0 : Math.Max(0, m.Tick - LeadIn);
    }
}
