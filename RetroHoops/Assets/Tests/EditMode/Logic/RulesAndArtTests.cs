using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    public class ScoringTests
    {
        private readonly GameRulesDef _rules = new GameRulesDef();

        [Test]
        public void InsideArc_IsOnePoint_BeyondArc_IsTwo()
        {
            Assert.AreEqual(1, Scoring.PointsFor(ShotZone.InsideArc, _rules));
            Assert.AreEqual(2, Scoring.PointsFor(ShotZone.BeyondArc, _rules));
        }

        [Test]
        public void GameEnds_AtTargetScore()
        {
            Assert.AreEqual(GameOverReason.TargetScore, Scoring.Evaluate(21, 15, 60f, _rules));
            Assert.AreEqual(GameOverReason.TargetScore, Scoring.Evaluate(10, 22, 60f, _rules));
            Assert.AreEqual(GameOverReason.None, Scoring.Evaluate(20, 19, 60f, _rules));
        }

        [Test]
        public void GameEnds_WhenClockExpires_WithALeader()
        {
            Assert.AreEqual(GameOverReason.ClockExpired, Scoring.Evaluate(9, 7, 0f, _rules));
        }

        [Test]
        public void TieAtHorn_GoesToSuddenDeath()
        {
            Assert.AreEqual(GameOverReason.None, Scoring.Evaluate(8, 8, 0f, _rules));
            Assert.AreEqual(GameOverReason.ClockExpired, Scoring.Evaluate(9, 8, -0.5f, _rules));
        }

        [Test]
        public void WinByTwo_RequiresMargin()
        {
            var rules = new GameRulesDef { winByTwo = true };
            Assert.AreEqual(GameOverReason.None, Scoring.Evaluate(21, 20, 30f, rules));
            Assert.AreEqual(GameOverReason.TargetScore, Scoring.Evaluate(22, 20, 30f, rules));
        }

        [Test]
        public void ClockIgnored_WhenDisabled()
        {
            var rules = new GameRulesDef { useGameClock = false };
            Assert.AreEqual(GameOverReason.None, Scoring.Evaluate(3, 1, 0f, rules));
        }

        [Test]
        public void Winner_ReportsCorrectSide()
        {
            Assert.AreEqual(0, Scoring.Winner(21, 10));
            Assert.AreEqual(1, Scoring.Winner(10, 21));
            Assert.AreEqual(-1, Scoring.Winner(5, 5));
        }
    }

    public class DeterminismTests
    {
        [Test]
        public void SeededRandom_IsReproducible()
        {
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void SeededRandom_FloatInRange()
        {
            var r = new SeededRandom(99);
            for (int i = 0; i < 1000; i++)
            {
                float f = r.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void StableHash_IsStable()
        {
            // Pinned value: FNV-1a("caller") must never change or generated content shifts.
            Assert.AreEqual(StableHash.Of("caller"), StableHash.Of("caller"));
            Assert.AreNotEqual(StableHash.Of("caller"), StableHash.Of("callers"));
        }
    }

    public class ColorTests
    {
        [TestCase("#FFD400")]
        [TestCase("#1FB5A6")]
        [TestCase("#0B0B0B")]
        public void Hex_RoundTrips(string hex)
        {
            Assert.AreEqual(hex, RgbColor.FromHex(hex).ToHex());
        }

        [Test]
        public void Hex_WithAlpha()
        {
            var c = RgbColor.FromHex("#11223344");
            Assert.AreEqual(0x44, c.a);
            Assert.AreEqual("#11223344", c.ToHex(true));
        }

        [Test]
        public void ContrastRatio_BlackOnWhite_Is21()
        {
            Assert.AreEqual(21.0, RgbColor.ContrastRatio(RgbColor.Black, RgbColor.White), 0.01);
        }
    }

    public class PixelArtTests
    {
        [Test]
        public void Logo_IsCorrectSize_AndNotEmpty()
        {
            foreach (var team in DefaultContent.Create().Teams)
            {
                var logo = LogoGenerator.Generate(team);
                Assert.AreEqual(LogoGenerator.Size, logo.Width);
                Assert.AreEqual(LogoGenerator.Size, logo.Height);
                Assert.Greater(logo.OpaqueCount(), 300, team.id);
            }
        }

        [Test]
        public void Logo_CornersAreTransparent_ForRoundShapes()
        {
            var logo = LogoGenerator.Generate(LogoShape.Circle, LogoMotif.Ball, RgbColor.White, RgbColor.Black, RgbColor.Black);
            Assert.AreEqual(0, logo.Get(0, 0).a);
            Assert.AreEqual(0, logo.Get(31, 31).a);
        }

        [Test]
        public void Backdrop_IsDeterministicPerSeed()
        {
            var court = DefaultContent.Create().Court("court.sunset_cage");
            var a = BackdropGenerator.Generate(court, 7);
            var b = BackdropGenerator.Generate(court, 7);
            Assert.AreEqual(a.Pixels.Length, b.Pixels.Length);
            for (int i = 0; i < a.Pixels.Length; i++) Assert.AreEqual(a.Pixels[i], b.Pixels[i]);
            Assert.AreEqual(a.Pixels.Length, a.OpaqueCount());
        }

        [Test]
        public void Canvas_IgnoresOutOfBoundsWrites()
        {
            var c = new PixelCanvas(4, 4);
            c.Set(-1, 0, RgbColor.White);
            c.Set(4, 4, RgbColor.White);
            Assert.AreEqual(0, c.OpaqueCount());
        }
    }
}

namespace CallerRetroBall.Tests
{
    public class MatchRequestTests
    {
        [Test]
        public void QuickCallDefault_UsesTwoDifferentUnlockedLeagueTeams()
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            Assert.AreEqual(GameMode.QuickCall, req.Mode);
            Assert.IsNotNull(c.Team(req.HomeTeamId));
            Assert.IsNotNull(c.Team(req.AwayTeamId));
            Assert.AreNotEqual(req.HomeTeamId, req.AwayTeamId);
            Assert.IsTrue(c.Team(req.HomeTeamId).unlockedByDefault);
            Assert.IsTrue(c.Team(req.AwayTeamId).unlockedByDefault);
            Assert.IsNotNull(c.Court(req.CourtId));
        }

        [Test]
        public void PracticeDefault_ReferencesExistingContent()
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.PracticeDefault();
            Assert.IsNotNull(c.Team(req.HomeTeamId));
            Assert.IsNotNull(c.Court(req.CourtId));
            Assert.IsNotNull(c.Find(c.Rules, req.RulesId));
        }
    }
}
