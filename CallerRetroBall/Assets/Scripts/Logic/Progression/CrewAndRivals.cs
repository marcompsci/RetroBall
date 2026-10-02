using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum RecruitCheck { Ok = 0, AlreadyOnCrew = 1, Locked = 2, NotEnoughPoints = 3, Unknown = 4 }

    /// <summary>
    /// Rise Mode roster building. Beating a street crew makes its three players recruitable; beating
    /// a league team makes its bench player recruitable (the fourth player, who never plays for them,
    /// so a recruit is never also on the other side). Signing costs Signal Points once; signed
    /// players and the original First Callers swap in and out of your two teammate spots for free.
    /// </summary>
    public static class CrewEngine
    {
        public const int TeammateSlots = 2;

        public static List<string> DefaultTeammates(ContentCatalog c)
        {
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var list = new List<string>();
            if (crew != null)
                foreach (var id in crew.rosterPlayerIds)
                    if (id != DefaultContent.RookPlayerId && list.Count < TeammateSlots) list.Add(id);
            return list;
        }

        /// <summary>Called when the crew beats <paramref name="teamId"/> in Rise Mode.</summary>
        public static List<string> UnlockFrom(RiseSaveData r, ContentCatalog c, string teamId)
        {
            var added = new List<string>();
            var team = c.Team(teamId);
            if (r == null || team == null) return added;
            if (team.tier == TeamTier.Circuit)
            {
                foreach (var id in team.rosterPlayerIds) Add(r, id, added);
            }
            else if (team.tier == TeamTier.League && team.rosterPlayerIds.Count > MatchSimulation.PlayersPerTeam)
            {
                for (int i = MatchSimulation.PlayersPerTeam; i < team.rosterPlayerIds.Count; i++) Add(r, team.rosterPlayerIds[i], added);
            }
            return added;
        }

        private static void Add(RiseSaveData r, string id, List<string> added)
        {
            if (r.recruitable.Contains(id)) return;
            r.recruitable.Add(id);
            added.Add(id);
        }

        /// <summary>Signing fee: rises with overall rating, rounded to 10.</summary>
        public static int Cost(PlayerDef p)
        {
            if (p == null) return 0;
            int raw = 60 + Math.Max(0, p.attributes.Overall - 45) * 18;
            return (int)(Math.Round(raw / 10.0) * 10);
        }

        public static List<string> Teammates(CareerSaveData d, ContentCatalog c)
        {
            var r = d?.rise;
            if (r == null || r.teammates == null || r.teammates.Count != TeammateSlots) return DefaultTeammates(c);
            foreach (var id in r.teammates) if (c.Player(id) == null) return DefaultTeammates(c);
            return r.teammates;
        }

        public static bool IsFree(CareerSaveData d, ContentCatalog c, string playerId) =>
            DefaultTeammates(c).Contains(playerId) || (d.rise.signed != null && d.rise.signed.Contains(playerId));

        public static RecruitCheck CanRecruit(CareerSaveData d, ContentCatalog c, string playerId)
        {
            var p = c.Player(playerId);
            if (d == null || p == null) return RecruitCheck.Unknown;
            if (Teammates(d, c).Contains(playerId)) return RecruitCheck.AlreadyOnCrew;
            if (IsFree(d, c, playerId)) return RecruitCheck.Ok;
            if (!d.rise.recruitable.Contains(playerId)) return RecruitCheck.Locked;
            if (d.signalPoints < Cost(p)) return RecruitCheck.NotEnoughPoints;
            return RecruitCheck.Ok;
        }

        /// <summary>Puts <paramref name="playerId"/> in teammate <paramref name="slot"/> (paying the fee the first time).</summary>
        public static RecruitCheck Recruit(CareerSaveData d, ContentCatalog c, string playerId, int slot)
        {
            var check = CanRecruit(d, c, playerId);
            if (check != RecruitCheck.Ok) return check;
            if (slot < 0 || slot >= TeammateSlots) return RecruitCheck.Unknown;
            if (!IsFree(d, c, playerId))
            {
                d.signalPoints -= Cost(c.Player(playerId));
                d.rise.signed.Add(playerId);
            }
            var team = new List<string>(Teammates(d, c));
            team[slot] = playerId;
            d.rise.teammates = team;
            return RecruitCheck.Ok;
        }

        /// <summary>Everyone you could put on the floor: originals, signed players, and unlocked recruits.</summary>
        public static List<string> Pool(CareerSaveData d, ContentCatalog c)
        {
            var pool = new List<string>(DefaultTeammates(c));
            foreach (var id in d.rise.signed) if (!pool.Contains(id)) pool.Add(id);
            foreach (var id in d.rise.recruitable) if (!pool.Contains(id)) pool.Add(id);
            pool.RemoveAll(id => c.Player(id) == null);
            return pool;
        }

        public static List<PlayerDef> TeammateDefs(CareerSaveData d, ContentCatalog c) =>
            Teammates(d, c).ConvertAll(id => c.Player(id));
    }

    [Serializable]
    public class RivalSaveData
    {
        public int wins;
        public int losses;
        /// <summary>Rise season of the last Rival Challenge played (0 = none yet).</summary>
        public int lastSeason;
    }

    public enum RivalOutcome { None = 0, Won = 1, Lost = 2 }

    /// <summary>
    /// Neon Static, the rival crew. Once a Rise season, from week 5 of the regular season, a Rival
    /// Challenge appears in the hub. It doesn't count in the standings; winning pays a bonus.
    /// </summary>
    public static class RivalEngine
    {
        public const int ChallengeFromWeek = 5;
        public const int WinBonus = 150;
        public const int WinFans = 50;

        public static bool ChallengeAvailable(CareerSaveData d)
        {
            var r = d?.rise;
            if (r == null || r.season == null || r.stage != RiseStage.Season) return false;
            if (r.season.currentWeek < ChallengeFromWeek) return false;
            return d.rival.lastSeason != r.season.seasonNumber;
        }

        public static MatchRequest Challenge(CareerSaveData d, ContentCatalog c, string difficultyId)
        {
            if (!ChallengeAvailable(d)) return null;
            var rival = c.Team(DefaultContent.RivalCrewId);
            if (rival == null) return null;
            return new MatchRequest
            {
                Mode = GameMode.Rival,
                HomeTeamId = DefaultContent.PlayerCrewId,
                AwayTeamId = rival.id,
                CourtId = rival.homeCourtId,
                DifficultyId = difficultyId,
                ContextId = "rival:s" + d.rise.season.seasonNumber,
            };
        }

        public static RivalOutcome ApplyResult(CareerSaveData d, MatchSummary s)
        {
            if (d == null || s == null || s.mode != GameMode.Rival) return RivalOutcome.None;
            d.rival.lastSeason = d.rise.season != null ? d.rise.season.seasonNumber : d.rival.lastSeason + 1;
            if (s.HumanWon)
            {
                d.rival.wins++;
                d.signalPoints += WinBonus;
                d.fans += WinFans;
                return RivalOutcome.Won;
            }
            d.rival.losses++;
            return RivalOutcome.Lost;
        }
    }

    public sealed class BadgeDef
    {
        public string Id;
        public string Title;
        public string Description;
        public Func<CareerSaveData, bool> Earned;
    }

    /// <summary>In-game badges (no Game Center needed). New ones are announced once after a game.</summary>
    public static class Badges
    {
        public static readonly List<BadgeDef> All = new List<BadgeDef>
        {
            B("badge.first_win", "FIRST W", "Win a game.", d => d.totals.wins >= 1),
            B("badge.green_light", "GREEN LIGHT", "Hit a GREEN release.", d => d.totals.greens >= 1),
            B("badge.sharpshooter", "SHARPSHOOTER", "5 GREEN releases in one game.", d => d.records.greens >= 5),
            B("badge.bucket_getter", "BUCKET GETTER", "Score 12 points in one game.", d => d.records.points >= 12),
            B("badge.floor_general", "FLOOR GENERAL", "5 assists in one game.", d => d.records.assists >= 5),
            B("badge.lockdown", "LOCKDOWN", "3 steals in one game.", d => d.records.steals >= 3),
            B("badge.rim_protector", "RIM PROTECTOR", "2 blocks in one game.", d => d.records.blocks >= 2),
            B("badge.on_a_run", "ON A RUN", "Win 5 games in a row.", d => d.records.bestWinStreak >= 5),
            B("badge.circuit", "OFF THE BLACKTOP", "Clear The Blacktop Circuit.", d => d.rise.stage != RiseStage.Circuit || d.rise.seasonsPlayed > 0),
            B("badge.gold_signal", "GOLD SIGNAL", "Win The Gold Signal Cup.", d => d.totals.championships >= 1),
            B("badge.first_call", "FIRST CALL", "Win the First Call Classic.", d => d.classic.titles >= 1),
            B("badge.static_killer", "STATIC KILLER", "Beat Neon Static.", d => d.rival.wins >= 1),
            B("badge.recruiter", "RECRUITER", "Sign a player to your crew.", d => d.rise.signed.Count >= 1),
            B("badge.every_day", "EVERY DAY", "7-day Daily Challenge streak.", d => d.daily.bestStreak >= 7),
            B("badge.self_made", "SELF MADE", "Create your own player.", d => d.customPlayer != null && d.customPlayer.created),
            B("badge.ready", "READY TO CALL", "Finish How to Play.", d => d.tutorialDone),
        };

        private static BadgeDef B(string id, string title, string description, Func<CareerSaveData, bool> earned) =>
            new BadgeDef { Id = id, Title = title, Description = description, Earned = earned };

        public static bool IsEarned(BadgeDef b, CareerSaveData d)
        {
            try { return d != null && b.Earned(d); }
            catch (NullReferenceException) { return false; }
        }

        /// <summary>Badges earned but not yet announced; marks them announced.</summary>
        public static List<BadgeDef> TakeNew(CareerSaveData d)
        {
            var list = new List<BadgeDef>();
            if (d == null) return list;
            foreach (var b in All)
                if (IsEarned(b, d) && !d.badgesSeen.Contains(b.Id))
                {
                    d.badgesSeen.Add(b.Id);
                    list.Add(b);
                }
            return list;
        }

        public static int EarnedCount(CareerSaveData d)
        {
            int n = 0;
            foreach (var b in All) if (IsEarned(b, d)) n++;
            return n;
        }
    }

    public enum StorySpeaker { Coach = 0, Rival = 1, You = 2 }

    public struct StoryLine
    {
        public StorySpeaker Speaker;
        public string Text;
        public StoryLine(StorySpeaker speaker, string text) { Speaker = speaker; Text = text; }
    }

    public sealed class StoryBeat
    {
        public string Id;
        public List<StoryLine> Lines = new List<StoryLine>();
    }

    /// <summary>
    /// Short story scenes between Rise Mode stages, with Coach Dee (your crew's coach) and Vex of
    /// Neon Static. All dialogue is original. Each scene plays once, in order, when its moment arrives.
    /// </summary>
    public static class Story
    {
        public const string CoachName = "COACH DEE";
        public const string RivalName = "VEX";

        public const string Intro = "story.intro";
        public const string CircuitCleared = "story.circuit_cleared";
        public const string RivalIntro = "story.rival_intro";
        public const string RivalBeaten = "story.rival_beaten";
        public const string RivalLost = "story.rival_lost";
        public const string Playoffs = "story.playoffs";
        public const string Champions = "story.champions";

        public static StoryBeat Beat(string id, string nickname)
        {
            string me = string.IsNullOrEmpty(nickname) ? "Rook" : nickname;
            var b = new StoryBeat { Id = id };
            void C(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Coach, t));
            void V(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival, t));
            void Y(string t) => b.Lines.Add(new StoryLine(StorySpeaker.You, t));
            switch (id)
            {
                case Intro:
                    C("So you're " + me + ". Heard you call your shot before you take it.");
                    C("Five street crews run the Blacktop Circuit. Beat all five and the Caller League has to let us in.");
                    Y("Then let's start knocking.");
                    C("That's the spirit. Cage Regulars first. They don't like visitors.");
                    break;
                case CircuitCleared:
                    C("Five for five. The whole blacktop is talking about the First Callers.");
                    C("The Caller League is a different animal. Ten games, top four make the playoffs.");
                    Y("And the Gold Signal Cup?");
                    C("Win two playoff games and it's ours. One step at a time.");
                    break;
                case RivalIntro:
                    V("So these are the famous First Callers. Cute.");
                    V("Neon Static ran this city before you learned to dribble. Come to the Static Lot and prove it.");
                    Y("Name the time.");
                    C("Ignore the noise, " + me + ". But... it would feel good to shut them up.");
                    break;
                case RivalBeaten:
                    V("...Lucky night. Don't get used to it.");
                    C("Lucky? You heard that crowd. That was all you.");
                    break;
                case RivalLost:
                    V("Static wins. Static always wins.");
                    C("Shake it off. They'll be back next season, and so will we.");
                    break;
                case Playoffs:
                    C("Playoffs. Everything we've built comes down to two games.");
                    Y("Then we call our shot.");
                    break;
                case Champions:
                    C("Gold Signal Cup champions. From the cage to the top of the league.");
                    V("Enjoy it while it lasts, " + me + ". Next season, it's ours.");
                    Y("We'll be waiting.");
                    break;
                default:
                    return null;
            }
            return b;
        }

        /// <summary>The next scene to play in the Rise hub, or null.</summary>
        public static string Pending(CareerSaveData d)
        {
            if (d == null) return null;
            bool Seen(string id) => d.storySeen.Contains(id);
            var r = d.rise;
            if (!Seen(Intro)) return Intro;
            if (r.stage != RiseStage.Circuit && !Seen(CircuitCleared)) return CircuitCleared;
            if (RivalEngine.ChallengeAvailable(d) && !Seen(RivalIntro)) return RivalIntro;
            if (d.rival.wins >= 1 && !Seen(RivalBeaten)) return RivalBeaten;
            if (d.rival.losses >= 1 && d.rival.wins == 0 && !Seen(RivalLost)) return RivalLost;
            if (r.stage == RiseStage.Playoffs && !Seen(Playoffs)) return Playoffs;
            if (d.totals.championships >= 1 && !Seen(Champions)) return Champions;
            return null;
        }

        public static void MarkSeen(CareerSaveData d, string id)
        {
            if (d != null && !string.IsNullOrEmpty(id) && !d.storySeen.Contains(id)) d.storySeen.Add(id);
        }

        public static readonly string[] AllIds = { Intro, CircuitCleared, RivalIntro, RivalBeaten, RivalLost, Playoffs, Champions };
    }
}
