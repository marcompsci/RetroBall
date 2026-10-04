using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What one player looked like in one recorded frame.</summary>
    public struct ReplayPlayer
    {
        public Vec2 Position;
        public Facing8 Facing;
        public bool Moving;
        public float Jump01;
        public bool ArmsUp;
    }

    /// <summary>One recorded simulation step (players + ball), enough to redraw the court.</summary>
    public sealed class ReplayFrame
    {
        public float Time;
        public ReplayPlayer[] Players;
        public Vec2 BallPosition;
        public float BallHeight;
        public int BallHolder;
        public BallPhase BallPhase;

        public ReplayFrame(int players) => Players = new ReplayPlayer[players];

        public void CopyFrom(ReplayFrame o)
        {
            Time = o.Time;
            Array.Copy(o.Players, Players, Players.Length);
            BallPosition = o.BallPosition;
            BallHeight = o.BallHeight;
            BallHolder = o.BallHolder;
            BallPhase = o.BallPhase;
        }
    }

    /// <summary>A saved stretch of frames plus what made it worth keeping.</summary>
    public sealed class ReplayClip
    {
        public readonly List<ReplayFrame> Frames = new List<ReplayFrame>();
        public string Label;
        public int Score;
        public float Duration => Frames.Count < 2 ? 0f : Frames[Frames.Count - 1].Time - Frames[0].Time;
    }

    /// <summary>
    /// Records the last few seconds of a match into a ring buffer (no allocations after start) so
    /// big plays can be replayed. Keeps the best play of the game for the post-game screen.
    /// Presentation only: reads the simulation, never changes it.
    /// </summary>
    public sealed class ReplayRecorder
    {
        public const float DefaultSeconds = 5f;
        public const int StepsPerSecond = 60;

        private readonly ReplayFrame[] _ring;
        private readonly int _stepsPerSecond;
        private int _next;
        private int _count;

        public ReplayClip BestPlay { get; private set; }

        /// <param name="stepsPerSecond">Simulation rate (60, or 120 with high frame rate on).</param>
        public ReplayRecorder(int players, float seconds = DefaultSeconds, int stepsPerSecond = StepsPerSecond)
        {
            _stepsPerSecond = Math.Max(1, stepsPerSecond);
            int n = Math.Max(2, (int)(seconds * _stepsPerSecond));
            _ring = new ReplayFrame[n];
            for (int i = 0; i < n; i++) _ring[i] = new ReplayFrame(players);
        }

        public int Count => _count;
        public int Capacity => _ring.Length;

        /// <summary>Call after every simulation step.</summary>
        public void Capture(MatchSimulation m)
        {
            var f = _ring[_next];
            _next = (_next + 1) % _ring.Length;
            if (_count < _ring.Length) _count++;
            f.Time = m.Time;
            for (int i = 0; i < f.Players.Length && i < m.Players.Length; i++)
            {
                var p = m.Players[i];
                // Stored as drawn on the fixed court (Full Court turns the simulation frame).
                f.Players[i] = new ReplayPlayer
                {
                    Position = m.ToWorldCourt(p.Position),
                    Facing = m.ToWorldFacing(p.Motion.facing),
                    Moving = p.Motion.IsMoving,
                    Jump01 = m.JumpHeight01(i),
                    ArmsUp = i == m.ChargingIndex,
                };
            }
            f.BallPosition = m.ToWorldCourt(m.Ball.Position);
            f.BallHeight = m.Ball.Height;
            f.BallHolder = m.Ball.IsHeld ? m.Ball.HolderIndex : -1;
            f.BallPhase = m.Ball.Phase;
        }

        /// <summary>Copies the last <paramref name="seconds"/> into a new clip (oldest first).</summary>
        public ReplayClip Snapshot(float seconds, string label = null, int score = 0)
        {
            var clip = new ReplayClip { Label = label, Score = score };
            int want = Math.Min(_count, Math.Max(1, (int)(seconds * _stepsPerSecond)));
            int start = (_next - want + _ring.Length * 2) % _ring.Length;
            for (int i = 0; i < want; i++)
            {
                var src = _ring[(start + i) % _ring.Length];
                var copy = new ReplayFrame(src.Players.Length);
                copy.CopyFrom(src);
                clip.Frames.Add(copy);
            }
            return clip;
        }

        /// <summary>Saves the moment as the play of the game if it beats the current one.</summary>
        public bool OfferBestPlay(string label, int score, float seconds = 3.5f)
        {
            if (score <= 0 || (BestPlay != null && BestPlay.Score >= score)) return false;
            BestPlay = Snapshot(seconds, label, score);
            return true;
        }

        /// <summary>How highlight-worthy a play is (0 = not a highlight).</summary>
        public static int PlayScore(MatchEventType type, ShotType shot, TimingGrade grade, int points)
        {
            switch (type)
            {
                case MatchEventType.ShotMade:
                    int s = shot == ShotType.Dunk ? 80 : (grade == TimingGrade.Green ? 60 : 30);
                    return s + points * 5;
                case MatchEventType.Block: return 70;
                case MatchEventType.Steal: return 50;
                default: return 0;
            }
        }

        /// <summary>Frame to show at <paramref name="t"/> seconds into a clip (nearest earlier frame).</summary>
        public static ReplayFrame FrameAt(ReplayClip clip, float t)
        {
            if (clip == null || clip.Frames.Count == 0) return null;
            float start = clip.Frames[0].Time;
            int lo = 0, hi = clip.Frames.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (clip.Frames[mid].Time - start <= t) lo = mid; else hi = mid - 1;
            }
            return clip.Frames[lo];
        }
    }
}
