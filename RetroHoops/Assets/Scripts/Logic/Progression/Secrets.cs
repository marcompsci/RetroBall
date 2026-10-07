using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Secret codes, hidden content and the Arcade Ladder (saved in the career).</summary>
    [Serializable]
    public class SecretsSaveData
    {
        /// <summary>Code ids entered correctly at least once.</summary>
        public List<string> codesFound = new List<string>();
        /// <summary>Code ids whose hint has been revealed by an accomplishment.</summary>
        public List<string> hintsRevealed = new List<string>();
        /// <summary>Unlocked team / court ids (secret content).</summary>
        public List<string> unlocked = new List<string>();
        /// <summary>Toggles (only usable once their code has been found).</summary>
        public bool bigHeads;
        public bool rainbowBall;
        public bool alwaysHeat;
        public bool pocketGreen;
        public bool skyHigh;
        public ArcadeSaveData arcade = new ArcadeSaveData();
    }

    [Serializable]
    public class ArcadeSaveData
    {
        public bool active;
        /// <summary>0-based rung of the ladder you're on.</summary>
        public int rung;
        public int continues;
        public string homeTeamId;
        public int clears;
        /// <summary>Highest rung reached (1-based, 0 = never played).</summary>
        public int bestRung;
        public int runs;
    }

    /// <summary>The four symbols codes are made of (shown as buttons on the code screen).</summary>
    public enum CodeSymbol { Star = 0, Ball = 1, Bolt = 2, Heart = 3 }

    public sealed class SecretCode
    {
        public string Id;
        public string Name;
        public string Description;
        public CodeSymbol[] Sequence;
        /// <summary>How the hint for this code is earned.</summary>
        public string HintCondition;

        public string SequenceText
        {
            get
            {
                var parts = new string[Sequence.Length];
                for (int i = 0; i < Sequence.Length; i++) parts[i] = Secrets.SymbolName(Sequence[i]);
                return string.Join(" · ", parts);
            }
        }
    }

    public enum CodeResult { Wrong = 0, Unlocked = 1, Toggled = 2 }

    /// <summary>
    /// Old-school secret codes. Enter four symbols in Settings ► SECRET CODES. Some codes unlock
    /// hidden teams and courts; others switch on fun modes (big heads, rainbow ball, always hot).
    /// Hints for each code are earned by playing (they show in Locker Room ► TROPHY).
    /// </summary>
    public static class Secrets
    {
        public const int CodeLength = 4;
        public const string BigHeads = "code.big_heads";
        public const string RainbowBall = "code.rainbow_ball";
        public const string PixelVoid = "code.pixel_void";
        public const string CartridgeKids = "code.cartridge_kids";
        public const string AlwaysHeat = "code.always_heat";
        public const string PocketGreen = "code.pocket_green";
        public const string SkyHigh = "code.sky_high";

        public static readonly List<SecretCode> All = new List<SecretCode>
        {
            Code(BigHeads, "BIG HEADS", "Everyone gets a giant head.", "Win 3 straight in King of the Court.",
                 CodeSymbol.Star, CodeSymbol.Star, CodeSymbol.Bolt, CodeSymbol.Heart),
            Code(RainbowBall, "RAINBOW BALL", "The ball cycles through colours.", "Reach a 3-day Daily Challenge streak.",
                 CodeSymbol.Heart, CodeSymbol.Ball, CodeSymbol.Heart, CodeSymbol.Ball),
            Code(PixelVoid, "PIXEL VOID", "Unlocks the hidden Pixel Void court.", "Clear the Arcade Ladder.",
                 CodeSymbol.Bolt, CodeSymbol.Bolt, CodeSymbol.Star, CodeSymbol.Ball),
            Code(CartridgeKids, "CARTRIDGE KIDS", "Unlocks the secret Cartridge Kids crew.", "Win 10 games.",
                 CodeSymbol.Ball, CodeSymbol.Star, CodeSymbol.Heart, CodeSymbol.Bolt),
            Code(AlwaysHeat, "ALWAYS HOT", "Your player starts every game heated up.", "Hit 50 green releases.",
                 CodeSymbol.Heart, CodeSymbol.Heart, CodeSymbol.Heart, CodeSymbol.Star),
            Code(PocketGreen, "POCKET GREEN", "Four-shade green screen, like an old handheld.", "Win a game of H-O-R-S-E.",
                 CodeSymbol.Ball, CodeSymbol.Bolt, CodeSymbol.Ball, CodeSymbol.Bolt),
            Code(SkyHigh, "SKY HIGH", "Everybody jumps twice as high (looks only).", "Throw or finish 5 alley-oops.",
                 CodeSymbol.Star, CodeSymbol.Heart, CodeSymbol.Star, CodeSymbol.Heart),
        };

        private static SecretCode Code(string id, string name, string desc, string hint, params CodeSymbol[] seq) =>
            new SecretCode { Id = id, Name = name, Description = desc, HintCondition = hint, Sequence = seq };

        /// <summary>Plain-text name (the code screen draws pixel icons; text keeps hints font-safe).</summary>
        public static string SymbolName(CodeSymbol s)
        {
            switch (s)
            {
                case CodeSymbol.Star: return "STAR";
                case CodeSymbol.Ball: return "BALL";
                case CodeSymbol.Bolt: return "BOLT";
                default: return "HEART";
            }
        }

        /// <summary>8×8 pixel icon mask for a symbol (rows top to bottom, '#' = filled).</summary>
        public static string[] SymbolIcon(CodeSymbol s)
        {
            switch (s)
            {
                case CodeSymbol.Star: return new[] { "...##...", "...##...", "########", ".######.", "..####..", ".##..##.", ".#....#.", "........" };
                case CodeSymbol.Ball: return new[] { "..####..", ".#.##.#.", "#..##..#", "########", "#..##..#", ".#.##.#.", "..####..", "........" };
                case CodeSymbol.Bolt: return new[] { "....###.", "...###..", "..###...", ".######.", "...###..", "..###...", ".##.....", "#......." };
                default: return new[] { ".##..##.", "########", "########", "########", ".######.", "..####..", "...##...", "........" };
            }
        }

        public static SecretCode Find(string id) => All.Find(x => x.Id == id);

        public static SecretCode Match(IList<CodeSymbol> entered)
        {
            if (entered == null || entered.Count != CodeLength) return null;
            foreach (var code in All)
            {
                bool same = true;
                for (int i = 0; i < CodeLength && same; i++) same = code.Sequence[i] == entered[i];
                if (same) return code;
            }
            return null;
        }

        public static bool Found(SecretsSaveData s, string codeId) => s != null && s.codesFound.Contains(codeId);
        public static bool IsUnlocked(SecretsSaveData s, string contentId) => s != null && s.unlocked.Contains(contentId);

        /// <summary>Applies an entered code. Toggle codes flip their mode each time they're entered.</summary>
        public static CodeResult Enter(SecretsSaveData s, IList<CodeSymbol> entered, out SecretCode code)
        {
            code = Match(entered);
            if (code == null || s == null) return CodeResult.Wrong;
            bool first = !s.codesFound.Contains(code.Id);
            if (first) s.codesFound.Add(code.Id);
            switch (code.Id)
            {
                case BigHeads: s.bigHeads = first || !s.bigHeads; return CodeResult.Toggled;
                case RainbowBall: s.rainbowBall = first || !s.rainbowBall; return CodeResult.Toggled;
                case AlwaysHeat: s.alwaysHeat = first || !s.alwaysHeat; return CodeResult.Toggled;
                case PocketGreen: s.pocketGreen = first || !s.pocketGreen; return CodeResult.Toggled;
                case SkyHigh: s.skyHigh = first || !s.skyHigh; return CodeResult.Toggled;
                case PixelVoid: Unlock(s, DefaultContent.SecretCourtId); return CodeResult.Unlocked;
                case CartridgeKids: Unlock(s, DefaultContent.SecretCrewId); return CodeResult.Unlocked;
            }
            return CodeResult.Unlocked;
        }

        /// <summary>Is a toggle code currently on?</summary>
        public static bool IsOn(SecretsSaveData s, string codeId)
        {
            if (s == null || !Found(s, codeId)) return false;
            switch (codeId)
            {
                case BigHeads: return s.bigHeads;
                case RainbowBall: return s.rainbowBall;
                case AlwaysHeat: return s.alwaysHeat;
                case PocketGreen: return s.pocketGreen;
                case SkyHigh: return s.skyHigh;
                default: return true;
            }
        }

        public static void Unlock(SecretsSaveData s, string contentId)
        {
            if (s != null && !string.IsNullOrEmpty(contentId) && !s.unlocked.Contains(contentId)) s.unlocked.Add(contentId);
        }

        /// <summary>Reveals hints whose conditions the career now meets. Returns the newly revealed codes.</summary>
        public static List<SecretCode> RevealHints(CareerSaveData d)
        {
            var revealed = new List<SecretCode>();
            if (d == null) return revealed;
            if (d.secrets == null) d.secrets = new SecretsSaveData();
            var s = d.secrets;
            void Check(string id, bool met)
            {
                if (!met || s.hintsRevealed.Contains(id)) return;
                s.hintsRevealed.Add(id);
                revealed.Add(Find(id));
            }
            Check(BigHeads, d.king != null && d.king.best >= 3);
            Check(RainbowBall, d.daily != null && d.daily.bestStreak >= 3);
            Check(PixelVoid, s.arcade != null && s.arcade.clears > 0);
            Check(CartridgeKids, d.totals != null && d.totals.wins >= 10);
            Check(AlwaysHeat, d.totals != null && d.totals.greens >= 50);
            Check(PocketGreen, d.practice != null && d.practice.horseWins >= 1);
            Check(SkyHigh, d.totals != null && d.totals.alleyOops >= 5);
            return revealed;
        }

        /// <summary>Teams you can pick in Quick Call: the default league teams plus unlocked secret teams.</summary>
        public static List<TeamDef> PlayableTeams(ContentCatalog c, SecretsSaveData s)
        {
            var list = c.TeamsInTier(TeamTier.League).FindAll(t => t.unlockedByDefault);
            foreach (var t in c.TeamsInTier(TeamTier.Secret)) if (IsUnlocked(s, t.id)) list.Add(t);
            // Your created team (once it's been added to the catalog) goes first.
            var mine = c.Team(CustomTeams.TeamId);
            if (mine != null) list.Insert(0, mine);
            return list;
        }

        /// <summary>Opponents for Quick Call: every league team plus unlocked secret teams.</summary>
        public static List<TeamDef> OpponentTeams(ContentCatalog c, SecretsSaveData s)
        {
            var list = new List<TeamDef>(c.TeamsInTier(TeamTier.League));
            foreach (var t in c.TeamsInTier(TeamTier.Secret)) if (IsUnlocked(s, t.id)) list.Add(t);
            return list;
        }

        /// <summary>Court for a Quick Call: the Pixel Void (when unlocked and chosen) or the home court.</summary>
        public static string CourtFor(TeamDef home, SecretsSaveData s, bool preferSecretCourt)
        {
            if (preferSecretCourt && IsUnlocked(s, DefaultContent.SecretCourtId)) return DefaultContent.SecretCourtId;
            return home?.homeCourtId;
        }
    }

    public enum ArcadeOutcome { None = 0, Advanced = 1, Continue = 2, GameOver = 3, Cleared = 4 }

    /// <summary>
    /// The Arcade Ladder: six games against tougher and tougher teams, ending with the secret
    /// boss, The Glitch, on its hidden court. You have three continues; a loss with none left ends
    /// the run. Clearing it unlocks The Glitch and its court and reveals a secret code.
    /// </summary>
    public static class ArcadeEngine
    {
        public const string RulesId = "rules.arcade";
        public const int Rungs = 6;
        public const int Continues = 3;
        public const int FirstClearBonus = 500;
        public const int ClearBonus = 150;

        public static void Start(ArcadeSaveData a, string homeTeamId)
        {
            a.active = true;
            a.rung = 0;
            a.continues = Continues;
            a.homeTeamId = homeTeamId;
            a.runs++;
            a.bestRung = Math.Max(a.bestRung, 1);
        }

        /// <summary>The ladder for a home team: five league teams from weakest to strongest, then the boss.</summary>
        public static List<string> Ladder(ContentCatalog c, string homeTeamId)
        {
            var pool = c.TeamsInTier(TeamTier.League).FindAll(t => t.id != homeTeamId);
            pool.Sort((x, y) =>
            {
                int cmp = TeamOverall(c, x).CompareTo(TeamOverall(c, y));
                return cmp != 0 ? cmp : string.CompareOrdinal(x.id, y.id);
            });
            var ladder = new List<string>();
            // Spread five picks across the sorted pool so the climb is steady.
            for (int i = 0; i < Rungs - 1 && pool.Count > 0; i++)
            {
                int k = pool.Count == 1 ? 0 : (int)Math.Round(i * (pool.Count - 1) / (double)(Rungs - 2));
                string id = pool[Math.Min(k, pool.Count - 1)].id;
                if (!ladder.Contains(id)) ladder.Add(id);
                else
                {
                    foreach (var t in pool) if (!ladder.Contains(t.id)) { ladder.Add(t.id); break; }
                }
            }
            ladder.Add(DefaultContent.BossTeamId);
            return ladder;
        }

        public static int TeamOverall(ContentCatalog c, TeamDef t)
        {
            int sum = 0, n = 0;
            foreach (var id in t.rosterPlayerIds)
            {
                var p = c.Player(id);
                if (p == null || n == MatchSimulation.PlayersPerTeam) continue;
                sum += p.attributes.Overall;
                n++;
            }
            return n == 0 ? 0 : sum / n;
        }

        /// <summary>Difficulty climbs: two Rookie, two Caller, then Legend for the last league team and the boss.</summary>
        public static string DifficultyFor(int rung)
        {
            if (rung < 2) return "difficulty.rookie";
            if (rung < 4) return DefaultContent.DefaultDifficultyId;
            return "difficulty.legend";
        }

        public static bool IsBossRung(int rung) => rung >= Rungs - 1;

        public static MatchRequest NextMatch(ArcadeSaveData a, ContentCatalog c)
        {
            if (a == null || !a.active) return null;
            var home = c.Team(a.homeTeamId);
            if (home == null) return null;
            var ladder = Ladder(c, a.homeTeamId);
            if (a.rung < 0 || a.rung >= ladder.Count) return null;
            var opp = c.Team(ladder[a.rung]);
            if (opp == null) return null;
            return new MatchRequest
            {
                Mode = GameMode.Arcade,
                HomeTeamId = home.id,
                AwayTeamId = opp.id,
                CourtId = IsBossRung(a.rung) ? DefaultContent.BossCourtId : opp.homeCourtId,
                RulesId = RulesId,
                DifficultyId = DifficultyFor(a.rung),
                ContextId = "arcade:r" + a.runs + ":g" + a.rung + ":c" + a.continues,
            };
        }

        /// <summary>Applies a finished Arcade game. Clearing grants a Signal Point bonus.</summary>
        public static ArcadeOutcome ApplyResult(CareerSaveData d, MatchSummary s, out int bonus)
        {
            bonus = 0;
            if (d == null || s == null || s.mode != GameMode.Arcade) return ArcadeOutcome.None;
            if (d.secrets == null) d.secrets = new SecretsSaveData();
            var a = d.secrets.arcade;
            if (!a.active) return ArcadeOutcome.None;
            if (!s.HumanWon)
            {
                if (a.continues > 0)
                {
                    a.continues--;
                    return ArcadeOutcome.Continue;
                }
                a.active = false;
                return ArcadeOutcome.GameOver;
            }
            if (IsBossRung(a.rung))
            {
                a.active = false;
                a.bestRung = Rungs;
                bonus = a.clears == 0 ? FirstClearBonus : ClearBonus;
                a.clears++;
                d.signalPoints += bonus;
                Secrets.Unlock(d.secrets, DefaultContent.BossTeamId);
                Secrets.Unlock(d.secrets, DefaultContent.BossCourtId);
                return ArcadeOutcome.Cleared;
            }
            a.rung++;
            a.bestRung = Math.Max(a.bestRung, a.rung + 1);
            return ArcadeOutcome.Advanced;
        }
    }
}
