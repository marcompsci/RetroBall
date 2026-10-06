using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What a coach tells his team to look for on offence.</summary>
    public enum CoachFocus
    {
        /// <summary>Play it as it comes.</summary>
        Balanced = 0,
        /// <summary>Drive and finish: get to the rim, skip the long ones.</summary>
        AttackRim = 1,
        /// <summary>Spread out and shoot from deep.</summary>
        LetItFly = 2,
        /// <summary>Get the ball to whoever's scoring tonight.</summary>
        FeedHotHand = 3,
    }

    /// <summary>
    /// Phase 35 COACH MODE (Franchise): every one of your players is run by the AI and you coach from the sideline:
    /// call a set play, pick what the offence looks for, choose the defence, and make substitutions (they happen at the
    /// next dead ball). Nobody's stick input is read. Every call is applied between steps and uses no random draws of
    /// its own, so a coached game is just as repeatable as any other.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        /// <summary>Your team is coached, not played: no player of yours takes stick input.</summary>
        public bool Coaching => Setup.Coach;
        /// <summary>No person drives any player (the attract-mode demo, or a coached game).</summary>
        private bool NoHuman => Setup.Demo || Setup.Coach;

        public CoachFocus Focus { get; private set; }
        /// <summary>A play called while the other team had the ball: it runs as soon as yours bring it up.</summary>
        public PlayCall PendingPlay { get; private set; }
        /// <summary>Tired players come out by themselves at dead balls (off: only your own substitutions happen).</summary>
        public bool CoachAutoSubs { get; set; } = true;
        /// <summary>Plays the coach has called that actually ran.</summary>
        public int CoachPlaysRun { get; private set; }

        private readonly List<(int player, int seat)> _subQueue = new List<(int, int)>();
        public int PendingSubs => _subQueue.Count;

        public void CoachSetFocus(CoachFocus focus)
        {
            if (!Coaching) return;
            Focus = focus;
        }

        /// <summary>Calls a set play: now if your team has the ball, otherwise on the next trip.</summary>
        public bool CoachCallPlay(PlayCall play)
        {
            if (!Coaching || (play != PlayCall.PickAndRoll && play != PlayCall.Backdoor && play != PlayCall.PostUp)) return false;
            PendingPlay = play;
            TryRunPendingPlay();
            return true;
        }

        /// <summary>Your team's defence from now on (your AI never changes it by itself).</summary>
        public void CoachSetDefense(DefenseScheme scheme)
        {
            if (!Coaching) return;
            int team = Setup.HumanTeam;
            if (_scheme[team] == scheme) return;
            _scheme[team] = scheme;
            Events.Add(new MatchEvent(MatchEventType.SchemeChanged, -1, team, (int)scheme));
        }

        /// <summary>Your bench players in seat order (seat 0 first).</summary>
        public List<BenchPlayer> CoachBench()
        {
            var list = new List<BenchPlayer>();
            foreach (var b in _bench) if (b.Team == Setup.HumanTeam) list.Add(b);
            return list;
        }

        /// <summary>
        /// Sends bench seat <paramref name="seat"/> on for <paramref name="playerIndex"/> at the next dead ball. A second call
        /// for the same player or the same seat replaces the first. Returns false if that isn't a sub you can make.
        /// </summary>
        public bool CoachSub(int playerIndex, int seat)
        {
            if (!Coaching || playerIndex < 0 || playerIndex >= Players.Length || Players[playerIndex].Team != Setup.HumanTeam) return false;
            if (seat < 0 || seat >= CoachBench().Count) return false;
            _subQueue.RemoveAll(s => s.player == playerIndex || s.seat == seat);
            _subQueue.Add((playerIndex, seat));
            return true;
        }

        public void CoachCancelSubs() => _subQueue.Clear();

        /// <summary>Is <paramref name="playerIndex"/> waiting to come out?</summary>
        public bool SubPendingFor(int playerIndex) => _subQueue.Exists(s => s.player == playerIndex);

        /// <summary>At a dead ball: the coach's substitutions, in the order called.</summary>
        private void ApplyCoachSubs()
        {
            if (_subQueue.Count == 0) return;
            var bench = CoachBench();
            foreach (var (player, seat) in _subQueue)
                if (seat < bench.Count) Swap(Players[player], bench[seat]);
            _subQueue.Clear();
        }

        /// <summary>Called each live step: starts a waiting play once your team has the ball in the front court.</summary>
        private void TryRunPendingPlay()
        {
            if (PendingPlay == PlayCall.None || Phase != MatchPhase.Live || OffenseTeam != Setup.HumanTeam || _play != PlayCall.None) return;
            if (!Ball.IsHeld || Players[Ball.HolderIndex].Team != Setup.HumanTeam || MustInbound) return;
            if (Setup.FullCourt && !_crossedHalf) return;
            if (StartPlay(Setup.HumanTeam, PendingPlay, Ball.HolderIndex))
            {
                PendingPlay = PlayCall.None;
                CoachPlaysRun++;
            }
        }

        /// <summary>The coached team's handler: what the focus adds to shooting, passing and driving (no random draws).</summary>
        private void CoachBias(PlayerRuntimeState p, float dist, ref float uShoot, ref float uDrive, ref int passTarget, ref float uPass)
        {
            if (!Coaching || p.Team != Setup.HumanTeam) return;
            var spot = ShotZones.SpotOf(p.Position, Setup.Court);
            switch (Focus)
            {
                case CoachFocus.AttackRim:
                    if (uDrive > float.MinValue) uDrive += 0.2f;
                    if (uShoot > float.MinValue) uShoot += spot == ShotSpot.Paint ? 0.15f : -0.12f;
                    break;
                case CoachFocus.LetItFly:
                    if (uShoot > float.MinValue) uShoot += ShotCharts.IsDeep(spot) ? 0.18f : (spot == ShotSpot.Paint ? 0f : -0.08f);
                    if (uDrive > float.MinValue) uDrive -= 0.12f;
                    break;
                case CoachFocus.FeedHotHand:
                    int hot = HotHand(Setup.HumanTeam);
                    if (hot == p.Index) { if (uShoot > float.MinValue) uShoot += 0.12f; }
                    else if (hot >= 0 && OpennessOf(Players[hot]) > 1.2f)
                    {
                        passTarget = hot;
                        uPass = Math.Max(uPass == float.MinValue ? 0f : uPass, 0.42f);
                    }
                    break;
            }
        }

        /// <summary>The player on <paramref name="team"/> with the most baskets this game (at least two), or -1.</summary>
        public int HotHand(int team)
        {
            int best = -1, most = 1;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].Team != team || IsBenched(i)) continue;
                int made = Stats[i].fieldGoalsMade;
                if (made > most) { most = made; best = i; }
            }
            return best;
        }

        public static string FocusName(CoachFocus f)
        {
            switch (f)
            {
                case CoachFocus.AttackRim: return "ATTACK THE RIM";
                case CoachFocus.LetItFly: return "LET IT FLY";
                case CoachFocus.FeedHotHand: return "FEED THE HOT HAND";
                default: return "BALANCED";
            }
        }
    }
}
