using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Legacy career mode.</summary>
    public class LegacyTests
    {
        private ContentCatalog _c;
        private CareerSaveData _career;
        private LegacySaveData _s;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            _career = Career.New(_c);
            _s = Legacy.Start(_c, 41);
            Legacy.Register(_c, _s, _career);
        }

        private PlayerStatLine Line(int pts, int ast = 2, int reb = 3) => new PlayerStatLine { points = pts, assists = ast, rebounds = reb };

        /// <summary>Plays every game in the current stage: answers story moments, wins big.</summary>
        private void PlayStage(int pts = 14, bool win = true)
        {
            for (int guard = 0; guard < 40; guard++)
            {
                if (Legacy.PendingEvent(_s) != null) { Legacy.Choose(_s, 0); continue; }
                var req = Legacy.NextMatch(_s, _c, _career, DefaultContent.DefaultDifficultyId);
                if (req == null) return;
                Legacy.RecordGame(_s, _c, win ? 30 : 10, win ? 18 : 25, Line(pts));
            }
        }

        [Test]
        public void Start_SeniorYear_SevenGames_AtYourSchool()
        {
            Assert.IsTrue(_s.active);
            Assert.AreEqual(LegacyStage.HighSchool, _s.stage);
            Assert.AreEqual(17, _s.age);
            Assert.AreEqual(Legacy.HighSchoolWeeks, _s.season.games.FindAll(g => g.Involves(Legacy.SchoolId)).Count);
            Assert.AreEqual("Central High", _c.Team(Legacy.SchoolId).city);
            var req = Legacy.NextMatch(_s, _c, _career, DefaultContent.DefaultDifficultyId);
            Assert.AreEqual(GameMode.Legacy, req.Mode);
            Assert.IsTrue(req.FullCourt);
            var setup = MatchSetup.FromRequest(req, _c);
            Assert.AreEqual(Legacy.YouId, setup.RosterA[0].id, "you start");
            Assert.AreEqual(5, setup.RosterB.Count);
            Assert.AreEqual(2, setup.BenchA.Count);
        }

        [Test]
        public void YouStartBelowYourBase_AndGrowWithAgeAndSkills()
        {
            var b = PlayerCreator.BasePlayer(_career, _c);
            var start = Legacy.Ratings(b, _s);
            Assert.AreEqual(Legacy.StartOverall, start.Overall, 1.01, "a senior starts around 52 whatever the base");
            _s.growth = 12;
            Assert.Greater(Legacy.Ratings(b, _s).Overall, start.Overall);
            _s.skillPoints = 10;
            int shooting = Legacy.Ratings(b, _s).shooting;
            Assert.IsNotNull(Legacy.CannotLearn(_s, "sc2b"), "tier 2 needs tier 1 first");
            Assert.IsTrue(Legacy.Learn(_s, "sc1b"));
            Assert.IsTrue(Legacy.Learn(_s, "sc2b"));
            Assert.AreEqual(shooting + 7, Legacy.Ratings(b, _s).shooting);
            Assert.AreEqual(7, _s.skillPoints);
            Assert.IsFalse(Legacy.Learn(_s, "sc1b"), "only once");
            _s.growth = 200;
            var maxed = Legacy.Ratings(b, _s);
            for (int i = 0; i < RatingScale.AttributeCount; i++) Assert.LessOrEqual(maxed.Get((AttributeType)i), Legacy.MaxRating);
        }

        [Test]
        public void Grades_AndXp_LevelUpGivesSkillPoints()
        {
            Assert.AreEqual("A+", Legacy.Grade(Legacy.GameScore(20, 4, 5, 1, 1, 0, true)));
            Assert.AreEqual("F", Legacy.Grade(Legacy.GameScore(0, 0, 1, 0, 0, 2, false)));
            int sp = _s.skillPoints;
            for (int i = 0; i < 3; i++) Legacy.RecordGame(_s, _c, 30, 20, Line(20, 4, 6));
            Assert.GreaterOrEqual(_s.xp, 3 * 100);
            Assert.Greater(_s.skillPoints, sp);
            Assert.AreEqual(3, _s.games.Count);
            Assert.AreEqual("A+", _s.games[0].grade);
        }

        [Test]
        public void HighSchool_ToCollegeOffers_StarsFromYourGames()
        {
            PlayStage(18);
            Assert.AreEqual(LegacyStage.College, _s.stage);
            Assert.AreEqual(18, _s.age);
            Assert.GreaterOrEqual(_s.stars, 4, "a dominant senior year is a 4-5 star recruit");
            Assert.GreaterOrEqual(_s.offers.Count, 2);
            Assert.AreEqual(-1, _s.offers[_s.offers.Count - 1].value, "the last option skips college");
            Assert.AreEqual(1, _s.history.Count);
            Assert.AreEqual("HIGH SCHOOL", _s.history[0].label);
            Assert.IsTrue(Legacy.ChooseCollege(_s, _c, 0));
            Assert.AreEqual(LegacyStage.College, _s.stage);
            Assert.IsNotNull(_s.season);
            Legacy.Register(_c, _s, _career);
            Assert.AreEqual(_s.collegeName.Split(' ')[0], _c.Team(Legacy.SchoolId).city.Split(' ')[0]);
        }

        [Test]
        public void FullCareer_Draft_Pro_Contracts_Sponsors_Retire()
        {
            PlayStage(16);
            Legacy.ChooseCollege(_s, _c, 0);
            PlayStage(16);
            Assert.AreEqual(LegacyStage.Draft, _s.stage);
            Assert.Greater(_s.stock, 40);
            int projected = Legacy.ProjectedPick(_s);
            var team = Legacy.Draft(_s, _c);
            Assert.IsNotNull(team);
            Assert.AreEqual(LegacyStage.Pro, _s.stage);
            Assert.AreEqual(projected, _s.draftPick);
            Assert.AreEqual(team.id, _s.teamId);
            Assert.AreEqual(3, _s.contractYears);
            Assert.AreEqual(1, _s.proSeason);
            Assert.AreEqual(TeamTier.League, _c.Team(_s.teamId).tier);
            var req = Legacy.NextMatch(_s, _c, _career, DefaultContent.DefaultDifficultyId);
            Assert.AreEqual(_s.teamId, req.HomeTeamId);
            Assert.AreEqual(Legacy.YouId, MatchSetup.FromRequest(req, _c).RosterA[0].id);
            Assert.IsNotNull(Legacy.Role(_s, _c));

            _s.fans = 1000;
            var brands = Legacy.BrandOffers(_s);
            Assert.Greater(brands.Count, Legacy.MaxEndorsements - 1);
            foreach (var b in brands) Legacy.SignBrand(_s, b.Id);
            Assert.AreEqual(Legacy.MaxEndorsements, _s.endorsements.Count);
            Assert.AreEqual(0, Legacy.BrandOffers(_s).Count, "three sponsors max");

            long cash = _s.cash;
            int seasons = 0;
            while (_s.stage == LegacyStage.Pro && seasons < 30)
            {
                PlayStage(14);
                Assert.IsNull(_s.season, "between seasons");
                seasons++;
                if (seasons == 1) Assert.Greater(_s.cash, cash, "paid salary + sponsors");
                if (_s.offers.Count > 0)
                {
                    Assert.GreaterOrEqual(_s.offers.Count, 2, "your team and others bid");
                    Assert.IsTrue(Legacy.NextSeason(_s, _c, 1));
                    Assert.AreEqual(_s.offers.Count, 0);
                }
                else if (Legacy.CanRetire(_s) && seasons >= 14) { Legacy.Retire(_s); break; }
                else if (_s.stage == LegacyStage.Pro) Assert.IsTrue(Legacy.NextSeason(_s, _c));
            }
            Assert.AreEqual(LegacyStage.Retired, _s.stage);
            var t = Legacy.Totals(_s);
            Assert.Greater(t.seasons, 10);
            Assert.AreEqual(_s.legacyPoints >= Legacy.HallOfFameLegacy, _s.hallOfFame);
            Assert.IsNull(Legacy.NextGame(_s));
        }

        [Test]
        public void SkipCollege_GoesStraightToTheDraft()
        {
            PlayStage(18);
            Assert.IsTrue(Legacy.ChooseCollege(_s, _c, _s.offers.Count - 1));
            Assert.AreEqual(LegacyStage.Draft, _s.stage);
            Assert.Greater(_s.stock, 0);
        }

        [Test]
        public void SimGame_Counts_WithLessXp()
        {
            var a = Legacy.SimGame(_s, _c, _career);
            Assert.IsNotNull(a);
            Assert.IsTrue(a.simmed);
            Assert.AreEqual(1, _s.season.currentWeek);
            Assert.GreaterOrEqual(a.pts, 0);
            Assert.LessOrEqual(a.pts, a.us);
        }

        [Test]
        public void StoryEvents_BlockTheNextGameUntilAnswered()
        {
            for (int i = 0; i < 7 && _s.pendingEvent == null && Legacy.NextGame(_s) != null; i++) Legacy.RecordGame(_s, _c, 30, 20, Line(10));
            if (_s.pendingEvent == null) Assert.Inconclusive("no event this seed");
            Assert.IsNull(Legacy.NextGame(_s));
            int fans = _s.fans;
            var e = Legacy.PendingEvent(_s);
            Assert.IsTrue(Legacy.Choose(_s, 0));
            Assert.AreEqual(fans + e.Choices[0].fans, _s.fans);
            Assert.IsTrue(_s.seenEvents.Contains(e.Id));
            Assert.IsNull(_s.pendingEvent);
        }

        [Test]
        public void Trainer_CostsMoreEachTime_AndOnlyAsAPro()
        {
            _s.cash = 1_000_000;
            Assert.IsFalse(Legacy.HireTrainer(_s), "amateurs can't");
            _s.stage = LegacyStage.Pro;
            long first = Legacy.TrainerCost(_s);
            Assert.IsTrue(Legacy.HireTrainer(_s));
            Assert.Greater(Legacy.TrainerCost(_s), first);
            Assert.AreEqual(1, _s.skillPoints);
        }

        [Test]
        public void Aging_GrowsYoung_DeclinesOld()
        {
            Assert.Greater(Legacy.Growth(19), 0);
            Assert.AreEqual(0, Legacy.Growth(29));
            Assert.Less(Legacy.Growth(34), 0);
        }

        [Test]
        public void Save_RoundTrips_MidCareer()
        {
            PlayStage(16);
            Legacy.ChooseCollege(_s, _c, 0);
            Legacy.RecordGame(_s, _c, 30, 20, Line(12));
            _s.skillPoints = 3;
            Legacy.Learn(_s, "df1a");
            _career.legacy = _s;
            var back = SaveCodec.Decode(SaveCodec.Encode(_career), DefaultContent.Create(), out _).legacy;
            Assert.IsTrue(back.active);
            Assert.AreEqual(_s.stage, back.stage);
            Assert.AreEqual(_s.collegeName, back.collegeName);
            Assert.AreEqual(_s.xp, back.xp);
            Assert.AreEqual(_s.skills.Count, back.skills.Count);
            Assert.AreEqual(_s.games.Count, back.games.Count);
            Assert.AreEqual(_s.season.games.Count, back.season.games.Count);
            Assert.AreEqual(_s.season.currentWeek, back.season.currentWeek);
            Assert.AreEqual(_s.history.Count, back.history.Count);
            Assert.AreEqual(_s.history[0].awards.Count, back.history[0].awards.Count);
            Assert.AreEqual(_s.pendingEvent, back.pendingEvent);
            Assert.IsFalse(SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), _c, out _).legacy.active);
        }

        [Test]
        public void Names_AreOriginal_NoRealSchoolAbbreviations()
        {
            foreach (var bad in new[] { "LSU", "PSU", "UCLA", "UNC", "USC", "Duke", "Kentucky" })
            {
                Legacy.Register(_c, _s, _career);
                foreach (var t in _c.Teams)
                    if (t.id.StartsWith(Legacy.TeamPrefix)) { Assert.AreNotEqual(bad, t.abbreviation); StringAssert.DoesNotContain(bad, t.FullName); }
            }
            foreach (var b in Legacy.Brands) Assert.IsFalse(string.IsNullOrEmpty(b.Name));
        }
    }

    /// <summary>The Park (street rules, 2-on-2 to 4-on-4, ankle breakers) and the Tournament Builder.</summary>
    public class StreetTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        private MatchSimulation Demo(int size, bool street, uint seed)
        {
            var league = _c.TeamsInTier(TeamTier.League);
            var req = new MatchRequest
            {
                Mode = GameMode.Street, HomeTeamId = league[0].id, AwayTeamId = league[1].id, CourtId = league[0].homeCourtId,
                RulesId = Street.RulesId, TeamSize = size, StreetRules = street, Seed = seed,
            };
            var setup = MatchSetup.FromRequest(req, _c);
            setup.Demo = true;
            return new MatchSimulation(setup);
        }

        private static int Run(MatchSimulation m, out int ankles)
        {
            ankles = 0;
            int steps = 0;
            for (; steps < 60 * 60 * 6 && !m.IsOver; steps++)
            {
                m.Step(Dt, default);
                foreach (var e in m.Events) if (e.Type == MatchEventType.AnkleBreaker) ankles++;
                m.Events.Clear();
            }
            return steps;
        }

        [Test]
        public void TwoOnTwo_AndFourOnFour_HalfCourt_PlayToTheEnd()
        {
            foreach (int size in new[] { 2, 3, 4 })
            {
                var m = Demo(size, false, 11);
                Assert.AreEqual(size, m.Setup.TeamSize);
                Assert.AreEqual(size * 2, m.Players.Length);
                Assert.IsFalse(m.Setup.FullCourt);
                Run(m, out _);
                Assert.IsTrue(m.IsOver, size + " on " + size + " finishes");
                Assert.Greater(m.Score[0] + m.Score[1], 5, size + " on " + size + " has scoring");
            }
        }

        [Test]
        public void StreetRules_BreakAnkles_AndStumbledDefendersStop()
        {
            int total = 0;
            for (uint seed = 1; seed <= 4; seed++)
            {
                var m = Demo(2, true, seed);
                Run(m, out int ankles);
                total += ankles;
                int stat = 0;
                for (int i = 0; i < m.Players.Length; i++) stat += m.Stats[i].ankleBreakers;
                Assert.AreEqual(ankles, stat, "events and box score agree");
            }
            Assert.Greater(total, 0, "sharp crossovers do break ankles");
            var none = Demo(2, false, 1);
            Run(none, out int off);
            Assert.AreEqual(0, off, "no ankle breakers without street rules");
        }

        [Test]
        public void AnkleChance_FavoursHandles_AndIsBounded()
        {
            var good = new AttributeSet(60, 60, 90, 50, 50, 90, 70, 60);
            var slow = new AttributeSet(60, 60, 50, 50, 50, 50, 70, 60);
            var lock_ = new AttributeSet(60, 60, 50, 92, 50, 90, 70, 60);
            Assert.Greater(MatchSimulation.AnkleChance(good, slow), MatchSimulation.AnkleChance(slow, lock_));
            Assert.LessOrEqual(MatchSimulation.AnkleChance(good, slow), 0.55f);
            Assert.GreaterOrEqual(MatchSimulation.AnkleChance(slow, lock_), 0.08f);
        }

        [Test]
        public void Park_Challenges_RepUnlocksTougherCallers()
        {
            var career = Career.New(_c);
            var s = career.street;
            Assert.AreEqual(12, Street.Challengers.Length);
            var easy = Street.Challengers[0];
            var hard = System.Array.Find(Street.Challengers, x => x.Tier == 4);
            Assert.IsTrue(Street.Unlocked(s, easy));
            Assert.IsFalse(Street.Unlocked(s, hard));
            foreach (var ch in Street.Challengers)
            {
                var req = Street.Challenge(_c, career, ch);
                Assert.IsTrue(req.StreetRules);
                Assert.AreEqual("street:" + ch.Id, req.ContextId);
                Assert.IsNotNull(_c.Court(req.CourtId), ch.Id + " court");
                var setup = MatchSetup.FromRequest(req, _c);
                Assert.AreEqual(ch.Size == 1 ? 3 : ch.Size, setup.TeamSize);
                Assert.AreEqual(ch.Nickname, setup.RosterB[0].firstName);
                Assert.AreEqual(ch, Street.FromContext(req.ContextId));
            }
            int gained = Street.ApplyResult(s, easy, true, 2);
            Assert.AreEqual((25 + 0) * 2 + 10, gained, "first win pays double, plus ankles");
            Assert.AreEqual(25 + 10, Street.ApplyResult(s, easy, true, 2), "repeat win");
            Assert.AreEqual(-5, Street.ApplyResult(s, easy, false, 0));
            Assert.AreEqual(0, s.streak);
            s.rep = Street.RepNeeded[4];
            Assert.IsTrue(Street.Unlocked(s, hard));
            Assert.AreEqual("LEGEND", Street.RepNames[Street.RepLevel(s.rep)]);
            foreach (var ch in Street.Challengers) Street.ApplyResult(s, ch, true, 0);
            Assert.IsTrue(Street.KingOfThePark(s));
        }

        [Test]
        public void Bracket_Seeding_TopSeedsMeetLate()
        {
            var o = CustomCup.BracketOrder(8);
            Assert.AreEqual(8, o.Length);
            Assert.AreEqual(0, o[0]);
            Assert.AreEqual(7, o[1]);
            // Seeds 1 and 2 are in different halves.
            Assert.Less(System.Array.IndexOf(o, 0), 4);
            Assert.GreaterOrEqual(System.Array.IndexOf(o, 1), 4);
            CollectionAssert.AllItemsAreUnique(CustomCup.BracketOrder(16));
        }

        [Test]
        public void TournamentBuilder_AllSizesAndFormats_RunToAChampion()
        {
            var career = Career.New(_c);
            var pool = CustomCup.Pool(_c, career).ConvertAll(t => t.id);
            Assert.GreaterOrEqual(pool.Count, 16);
            foreach (int size in CustomCup.Sizes)
                foreach (int format in CustomCup.Formats)
                {
                    var cup = new CustomCupSaveData();
                    var teams = pool.GetRange(0, size);
                    Assert.IsNotNull(CustomCup.CannotStart(pool.GetRange(0, 3), teams[0]));
                    Assert.IsTrue(CustomCup.Start(cup, _c, "Test Cup", teams, teams[size - 1], format));
                    Assert.AreEqual(size / 2, cup.games.Count);
                    var req = CustomCup.NextMatch(cup, _c, DefaultContent.DefaultDifficultyId);
                    Assert.AreEqual(format == 5, req.FullCourt);
                    var setup = MatchSetup.FromRequest(req, _c);
                    Assert.AreEqual(format, setup.TeamSize);
                    // Win every game.
                    CustomCupOutcome last = CustomCupOutcome.None;
                    for (int g = 0; g < 6 && cup.Active; g++) last = CustomCup.ApplyResult(cup, _c, 21, 10);
                    Assert.AreEqual(CustomCupOutcome.Champion, last);
                    Assert.AreEqual(cup.yourTeam, cup.championId);
                    Assert.AreEqual(size - 1, cup.games.Count, "a knockout plays n-1 games");
                    Assert.AreEqual("FINAL", CustomCup.RoundName(cup, CustomCup.Rounds(cup)));
                }
            var lose = new CustomCupSaveData();
            CustomCup.Start(lose, _c, "x", pool.GetRange(0, 8), pool[0], 3);
            Assert.AreEqual(CustomCupOutcome.Eliminated, CustomCup.ApplyResult(lose, _c, 5, 21));
            Assert.IsTrue(lose.finished, "the rest is simulated");
            Assert.AreEqual(7, lose.games.Count);
        }

        [Test]
        public void StreetAndCup_SaveRoundTrip()
        {
            var career = Career.New(_c);
            career.street.rep = 321;
            career.street.beaten.Add("slim");
            var pool = CustomCup.Pool(_c, career).ConvertAll(t => t.id);
            CustomCup.Start(career.customCup, _c, "Summer Jam", pool.GetRange(0, 8), pool[2], 2);
            CustomCup.ApplyResult(career.customCup, _c, 15, 9);
            var back = SaveCodec.Decode(SaveCodec.Encode(career), _c, out _);
            Assert.AreEqual(321, back.street.rep);
            Assert.AreEqual(1, back.street.beaten.Count);
            Assert.AreEqual("Summer Jam", back.customCup.name);
            Assert.AreEqual(2, back.customCup.format);
            Assert.AreEqual(career.customCup.games.Count, back.customCup.games.Count);
            Assert.AreEqual(pool[2], back.customCup.yourTeam);
            Assert.IsTrue(back.customCup.Active);
        }
    }

    /// <summary>The rename to Retro Hoops.</summary>
    public class RenameTests
    {
        [Test]
        public void GameIsRetroHoops_AndOldKitLinksStillOpen()
        {
            Assert.AreEqual("Retro Hoops", DefaultContent.GameName);
            StringAssert.Contains("retrohoops://", Kits.LinkPrefix);
            string code = Kits.Encode(Kits.Randomize(5, null));
            Assert.IsTrue(Kits.TryDecode(Kits.LinkPrefix + code, out _));
            Assert.IsTrue(Kits.TryDecode("retroball://kit/" + code, out _), "links shared before the rename");
            Assert.Greater(CallerRetroBall.Logic.PixelArt.TitleLogoGenerator.Width, 90, "RETRO HOOPS is wider than RETROBALL");
        }
    }
}
