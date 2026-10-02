using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 13: recruit your crew, rivals, badges, story.</summary>
    public class CrewTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Default_TeammatesAreTheOriginalFirstCallers()
        {
            var d = Career.New(_c);
            var mates = CrewEngine.Teammates(d, _c);
            Assert.AreEqual(2, mates.Count);
            Assert.IsFalse(mates.Contains(DefaultContent.RookPlayerId));
            foreach (var id in mates) Assert.IsTrue(_c.Team(DefaultContent.PlayerCrewId).rosterPlayerIds.Contains(id));
        }

        [Test]
        public void BeatingACrew_UnlocksAllThree_BeatingALeagueTeam_UnlocksOnlyTheBench()
        {
            var r = new RiseSaveData();
            var crew = _c.Team("crew.cage_regulars");
            Assert.AreEqual(3, CrewEngine.UnlockFrom(r, _c, crew.id).Count);
            var league = _c.TeamsInTier(TeamTier.League)[0];
            var added = CrewEngine.UnlockFrom(r, _c, league.id);
            Assert.AreEqual(league.rosterPlayerIds.Count - MatchSimulation.PlayersPerTeam, added.Count);
            foreach (var id in added) Assert.GreaterOrEqual(league.rosterPlayerIds.IndexOf(id), MatchSimulation.PlayersPerTeam, "bench only");
            Assert.AreEqual(0, CrewEngine.UnlockFrom(r, _c, crew.id).Count, "no duplicates");
            Assert.AreEqual(0, CrewEngine.UnlockFrom(r, _c, DefaultContent.RivalCrewId).Count, "rivals never join");
        }

        [Test]
        public void RiseWin_UnlocksTheBeatenCrew()
        {
            var d = Career.New(_c);
            var s = Fake.Summary(GameMode.Rise, true, "x", opponent: "crew.cage_regulars");
            RiseEngine.ApplyResult(d.rise, _c, s, d);
            Assert.AreEqual(3, d.rise.recruitable.Count);
        }

        [Test]
        public void Recruit_ChargesOnce_SwapsAreFree()
        {
            var d = Career.New(_c);
            CrewEngine.UnlockFrom(d.rise, _c, "crew.pier_pressure");
            string target = d.rise.recruitable[0];
            int cost = CrewEngine.Cost(_c.Player(target));
            Assert.Greater(cost, 0);
            Assert.AreEqual(RecruitCheck.NotEnoughPoints, CrewEngine.Recruit(d, _c, target, 0));
            d.signalPoints = cost + 5;
            Assert.AreEqual(RecruitCheck.Ok, CrewEngine.Recruit(d, _c, target, 0));
            Assert.AreEqual(5, d.signalPoints);
            Assert.AreEqual(target, CrewEngine.Teammates(d, _c)[0]);
            Assert.AreEqual(RecruitCheck.AlreadyOnCrew, CrewEngine.CanRecruit(d, _c, target));

            string original = CrewEngine.DefaultTeammates(_c)[0];
            Assert.AreEqual(RecruitCheck.Ok, CrewEngine.Recruit(d, _c, original, 0), "originals are free");
            Assert.AreEqual(RecruitCheck.Ok, CrewEngine.Recruit(d, _c, target, 1), "signed players are free to bring back");
            Assert.AreEqual(5, d.signalPoints);
        }

        [Test]
        public void LockedPlayers_CantBeRecruited()
        {
            var d = Career.New(_c);
            d.signalPoints = 99999;
            var stranger = _c.Team("crew.boardwalk_bandits").rosterPlayerIds[0];
            Assert.AreEqual(RecruitCheck.Locked, CrewEngine.CanRecruit(d, _c, stranger));
        }

        [Test]
        public void BetterPlayers_CostMore()
        {
            var weak = new PlayerDef { attributes = new AttributeSet(40, 40, 40, 40, 40, 40, 40, 40) };
            var strong = new PlayerDef { attributes = new AttributeSet(75, 75, 75, 75, 75, 75, 75, 75) };
            Assert.Greater(CrewEngine.Cost(strong), CrewEngine.Cost(weak));
            Assert.AreEqual(0, CrewEngine.Cost(strong) % 10);
        }

        [Test]
        public void Match_UsesYourRecruits()
        {
            var d = Career.New(_c);
            CrewEngine.UnlockFrom(d.rise, _c, "crew.pier_pressure");
            d.signalPoints = 99999;
            CrewEngine.Recruit(d, _c, d.rise.recruitable[1], 1);
            var r = MatchRequest.PracticeDefault();
            r.HumanTeammates = CrewEngine.TeammateDefs(d, _c);
            var m = new MatchSimulation(MatchSetup.FromRequest(r, _c));
            Assert.AreEqual(d.rise.recruitable[1], m.Players[2].Def.id);
        }

        [Test]
        public void Crew_RoundTripsThroughTheSaveFile()
        {
            var d = Career.New(_c);
            CrewEngine.UnlockFrom(d.rise, _c, "crew.cage_regulars");
            d.signalPoints = 99999;
            CrewEngine.Recruit(d, _c, d.rise.recruitable[0], 0);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(d.rise.teammates, back.rise.teammates);
            Assert.AreEqual(d.rise.recruitable, back.rise.recruitable);
            Assert.AreEqual(d.rise.signed, back.rise.signed);
        }
    }

    public class RivalTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        private CareerSaveData InSeason(int week)
        {
            var d = Career.New(_c);
            RiseEngine.StartSeason(d.rise, _c);
            d.rise.season.currentWeek = week;
            return d;
        }

        [Test]
        public void RivalTeam_IsValidContent()
        {
            var t = _c.Team(DefaultContent.RivalCrewId);
            Assert.AreEqual(TeamTier.Rival, t.tier);
            Assert.AreEqual(3, t.rosterPlayerIds.Count);
            Assert.IsNotNull(_c.Player(DefaultContent.RivalLeaderId));
            Assert.IsNotNull(_c.Court(t.homeCourtId));
            Assert.IsTrue(ContentValidator.Validate(_c).IsValid, ContentValidator.Validate(_c).ToString());
            Assert.IsFalse(_c.Seasons[0].teamIds.Contains(t.id), "not in the league");
        }

        [Test]
        public void Challenge_AppearsFromWeekFive_OncePerSeason()
        {
            Assert.IsFalse(RivalEngine.ChallengeAvailable(Career.New(_c)), "not during the circuit");
            Assert.IsFalse(RivalEngine.ChallengeAvailable(InSeason(4)));
            var d = InSeason(5);
            Assert.IsTrue(RivalEngine.ChallengeAvailable(d));
            var req = RivalEngine.Challenge(d, _c, "difficulty.caller");
            Assert.AreEqual(GameMode.Rival, req.Mode);
            Assert.AreEqual(DefaultContent.RivalCrewId, req.AwayTeamId);
            RivalEngine.ApplyResult(d, Fake.Summary(GameMode.Rival, false, "r1"));
            Assert.IsFalse(RivalEngine.ChallengeAvailable(d), "once per season");
            Assert.AreEqual(1, d.rival.losses);
        }

        [Test]
        public void Win_PaysBonus_AndDoesntTouchStandings()
        {
            var d = InSeason(6);
            int sp = d.signalPoints;
            int played = d.rise.season.games.FindAll(g => g.played).Count;
            Assert.AreEqual(RivalOutcome.Won, RivalEngine.ApplyResult(d, Fake.Summary(GameMode.Rival, true, "r2")));
            Assert.AreEqual(sp + RivalEngine.WinBonus, d.signalPoints);
            Assert.AreEqual(played, d.rise.season.games.FindAll(g => g.played).Count);
        }

        [Test]
        public void Rival_PaysRiseRates()
        {
            var t = RewardTuning.Default;
            Assert.AreEqual(Rewards.For(Fake.Summary(GameMode.Rise, true, "a"), t).signalPoints,
                            Rewards.For(Fake.Summary(GameMode.Rival, true, "b"), t).signalPoints);
        }
    }

    public class BadgeAndStoryTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Badges_HaveUniqueIdsAndText()
        {
            var ids = new HashSet<string>();
            foreach (var b in Badges.All)
            {
                Assert.IsTrue(ids.Add(b.Id), b.Id);
                Assert.IsNotEmpty(b.Title);
                Assert.IsNotEmpty(b.Description);
            }
            Assert.GreaterOrEqual(Badges.All.Count, 12);
        }

        [Test]
        public void FreshCareer_HasNoBadges_AndNewOnesAreAnnouncedOnce()
        {
            var d = Career.New(_c);
            Assert.AreEqual(0, Badges.EarnedCount(d));
            d.totals.wins = 1;
            d.records.greens = 5;
            var first = Badges.TakeNew(d);
            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(0, Badges.TakeNew(d).Count, "only announced once");
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(0, Badges.TakeNew(back).Count, "seen list is saved");
        }

        [Test]
        public void EveryStoryScene_HasLines()
        {
            foreach (var id in Story.AllIds)
            {
                var beat = Story.Beat(id, "Ace");
                Assert.IsNotNull(beat, id);
                Assert.GreaterOrEqual(beat.Lines.Count, 2);
                foreach (var l in beat.Lines) Assert.IsNotEmpty(l.Text);
            }
            Assert.IsNull(Story.Beat("story.nope", "Ace"));
        }

        [Test]
        public void Story_PlaysInOrderAsTheRunProgresses()
        {
            var d = Career.New(_c);
            Assert.AreEqual(Story.Intro, Story.Pending(d));
            Story.MarkSeen(d, Story.Intro);
            Assert.IsNull(Story.Pending(d), "nothing until the circuit is cleared");
            RiseEngine.StartSeason(d.rise, _c);
            Assert.AreEqual(Story.CircuitCleared, Story.Pending(d));
            Story.MarkSeen(d, Story.CircuitCleared);
            d.rise.season.currentWeek = RivalEngine.ChallengeFromWeek;
            Assert.AreEqual(Story.RivalIntro, Story.Pending(d));
            Story.MarkSeen(d, Story.RivalIntro);
            RivalEngine.ApplyResult(d, Fake.Summary(GameMode.Rival, true, "w"));
            Assert.AreEqual(Story.RivalBeaten, Story.Pending(d));
            Story.MarkSeen(d, Story.RivalBeaten);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsNull(Story.Pending(back));
            Assert.AreEqual(1, back.rival.wins);
        }
    }
}
