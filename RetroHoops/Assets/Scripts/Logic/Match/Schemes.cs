using System;

namespace CallerRetroBall.Logic
{
    /// <summary>How a team defends. Every AI team has a favourite; smart teams adjust during the game.</summary>
    public enum DefenseScheme
    {
        /// <summary>Stay with your man, help when the ball gets deep.</summary>
        ManToMan = 0,
        /// <summary>Crowd the ball, deny passing lanes, gamble for steals.</summary>
        Pressure = 1,
        /// <summary>Sag off toward the rim: wall off drives, give up jumpers.</summary>
        PackLine = 2,
        /// <summary>Guard areas, not players: protect the paint, shade to the ball.</summary>
        Zone = 3,
    }

    /// <summary>
    /// AI defensive schemes and in-game adjustments. If the other team keeps scoring the way a scheme
    /// allows (threes against a sagging defense, layups against pressure), a smart AI switches.
    /// Only the AI side adjusts; your AI teammates always play man-to-man.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        private readonly DefenseScheme[] _scheme = new DefenseScheme[2];
        private readonly int[] _arcAgainst = new int[2];
        private readonly int[] _rimAgainst = new int[2];

        /// <summary>The scheme <paramref name="team"/> is defending in right now.</summary>
        public DefenseScheme SchemeOf(int team) => _scheme[team];

        private void InitSchemes()
        {
            for (int team = 0; team < 2; team++)
            {
                var def = team == 0 ? Setup.TeamA : Setup.TeamB;
                bool aiTeam = Setup.Demo || (team != Setup.HumanTeam && !Setup.SecondHuman);
                _scheme[team] = aiTeam && def != null ? def.scheme : DefenseScheme.ManToMan;
            }
        }

        /// <summary>After a basket: the defending AI may change scheme if it keeps getting beaten the same way.</summary>
        private void AdjustScheme(int scoringTeam, int points, ShotType shot)
        {
            int defense = 1 - scoringTeam;
            if (defense == Setup.HumanTeam && !Setup.Demo) return;
            if (Setup.SecondHuman || Setup.PassiveOpponents) return;
            var profile = AiProfile(defense);
            if (profile.decisionQuality < Setup.Defense.schemeAdjustMinQuality) return;

            bool arc = points >= Setup.Rules.beyondArcPoints;
            bool rim = shot == ShotType.Layup || shot == ShotType.Dunk;
            if (arc) { _arcAgainst[defense]++; _rimAgainst[defense] = 0; }
            else if (rim) { _rimAgainst[defense]++; _arcAgainst[defense] = 0; }

            var current = _scheme[defense];
            var next = current;
            int trigger = Setup.Defense.schemeAdjustAfter;
            // Sagging and zones give up threes: get out on shooters.
            if ((current == DefenseScheme.PackLine || current == DefenseScheme.Zone) && _arcAgainst[defense] >= trigger)
                next = DefenseScheme.ManToMan;
            // Pressure and man get beaten at the rim: pack the paint.
            else if ((current == DefenseScheme.Pressure || current == DefenseScheme.ManToMan) && _rimAgainst[defense] >= trigger)
                next = current == DefenseScheme.Pressure ? DefenseScheme.ManToMan : DefenseScheme.PackLine;
            if (next == current) return;
            _scheme[defense] = next;
            _arcAgainst[defense] = 0;
            _rimAgainst[defense] = 0;
            Events.Add(new MatchEvent(MatchEventType.SchemeChanged, -1, defense, (int)next));
        }

        /// <summary>Off-ball target for a defender under the team's scheme (on-ball defence is shared).</summary>
        private Vec2 SchemeGuardSpot(PlayerRuntimeState p, PlayerRuntimeState man, PlayerRuntimeState holder)
        {
            var court = Setup.Court;
            switch (_scheme[p.Team])
            {
                case DefenseScheme.Pressure:
                    // Deny: stand in the lane between the ball and your man.
                    if (holder != null && holder.Team != p.Team)
                        return court.Clamp(man.Position + (holder.Position - man.Position) * 0.3f);
                    break;
                case DefenseScheme.PackLine:
                    // Sag: halfway between your man and the rim.
                    return court.Clamp(Vec2.Lerp(man.Position, court.Hoop, 0.45f));
                case DefenseScheme.Zone:
                    // Zone spots in front of the rim, shaded toward the ball (Full Court adds two up top).
                    float shade = holder != null ? Math.Max(-0.8f, Math.Min(0.8f, holder.Position.x * 0.25f)) : 0f;
                    if (p.Slot >= 3)
                        return court.Clamp(new Vec2((p.Slot == 3 ? -1f : 1f) * 3.4f + shade, court.hoopY + 4.8f));
                    float side = p.Slot == 1 ? -1f : 1f;
                    return court.Clamp(new Vec2(side * 1.6f + shade, court.hoopY + 1.8f));
            }
            return Formation.GuardSpot(man.Position, false, court);
        }

        /// <summary>On-ball gap for the scheme: pressure crowds, pack-line gives a cushion.</summary>
        private float SchemeOnBallGap(int team)
        {
            switch (_scheme[team])
            {
                case DefenseScheme.Pressure: return 0.85f;
                case DefenseScheme.PackLine: return 1.6f;
                case DefenseScheme.Zone: return 1.4f;
                default: return 1.2f;
            }
        }

        public static string SchemeName(DefenseScheme s)
        {
            switch (s)
            {
                case DefenseScheme.Pressure: return "FULL PRESSURE";
                case DefenseScheme.PackLine: return "PACK THE PAINT";
                case DefenseScheme.Zone: return "ZONE";
                default: return "MAN-TO-MAN";
            }
        }

        // ------------------------------------------------------------------ 1v1

        /// <summary>1-on-1: slots 1 and 2 of both teams sit out at the far corners and never touch the ball.</summary>
        public bool IsBenched(int player) => Setup.OneOnOne && player >= 0 && player < Players.Length && Players[player].Slot != 0;

        /// <summary>Where a benched player waits (far top corners, away from the play).</summary>
        private Vec2 BenchSpot(PlayerRuntimeState p)
        {
            var c = Setup.Court;
            float x = (p.Team == 0 ? -1f : 1f) * (c.HalfWidth - 0.4f - 0.9f * (p.Slot - 1));
            return new Vec2(x, c.depth - 0.4f);
        }
    }
}
