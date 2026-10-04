using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 17: party games (H-O-R-S-E, 21, Around the World), Dynasty mode, highlight GIFs.</summary>
    public class HorseTests
    {
        private static HorseSession Friendly()
        {
            var c = DefaultContent.Create();
            return new HorseSession(HorseOpponent.Friend, CourtGeometry.Default, ShotTuning.Default, c.Rules[0]);
        }

        [Test]
        public void MakeSetsTheShot_MissPassesTheTurn()
        {
            var h = Friendly();
            var spot = new Vec2(3f, 6f);
            Assert.AreEqual(0, h.Shooter);
            h.Resolve(false, spot);
            Assert.AreEqual(1, h.Shooter, "a miss passes the turn");
            Assert.IsFalse(h.Matching);
            h.Resolve(true, spot);
            Assert.IsTrue(h.Matching, "a make must be matched");
            Assert.AreEqual(0, h.Shooter);
            Assert.AreEqual(spot, h.SpotToMatch);
        }

        [Test]
        public void MissingTheMatch_OrShootingFromTheWrongSpot_IsALetter()
        {
            var h = Friendly();
            var spot = new Vec2(-4f, 5f);
            h.Resolve(true, spot);               // P1 sets
            h.Resolve(false, spot);              // P2 misses the match
            Assert.AreEqual("H", h.LettersOf(1));
            Assert.AreEqual(0, h.Shooter, "the setter goes again");
            h.Resolve(true, spot);               // P1 sets again
            h.Resolve(true, spot + new Vec2(3f, 0f)); // P2 makes it, but from somewhere else
            Assert.AreEqual("HO", h.LettersOf(1));
            h.Resolve(true, spot);
            h.Resolve(true, spot + new Vec2(0.5f, 0f)); // close enough
            Assert.AreEqual("HO", h.LettersOf(1));
        }

        [Test]
        public void FiveLetters_EndsTheGame()
        {
            var h = Friendly();
            var spot = new Vec2(0f, 7f);
            for (int i = 0; i < 5; i++)
            {
                h.Resolve(true, spot);
                h.Resolve(false, spot);
            }
            Assert.IsTrue(h.Finished);
            Assert.AreEqual(0, h.Winner);
            Assert.AreEqual("HORSE", h.LettersOf(1));
            int v = h.Version;
            h.Resolve(true, spot);
            Assert.AreEqual(v, h.Version, "nothing happens after the game ends");
        }

        [Test]
        public void CpuTurns_AreDeterministic_AndBetterShootersMakeMore()
        {
            var c = DefaultContent.Create();
            var star = new PlayerDef { lastName = "Star", attributes = new AttributeSet { shooting = 95, finishing = 90 } };
            var brick = new PlayerDef { lastName = "Brick", attributes = new AttributeSet { shooting = 15, finishing = 15 } };
            int makesStar = 0, makesBrick = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                var a = new HorseSession(HorseOpponent.Cpu, CourtGeometry.Default, ShotTuning.Default, c.Rules[0], star, c.Difficulty("difficulty.legend"), seed);
                var b = new HorseSession(HorseOpponent.Cpu, CourtGeometry.Default, ShotTuning.Default, c.Rules[0], brick, c.Difficulty("difficulty.legend"), seed);
                a.Resolve(false, Vec2.Zero);
                b.Resolve(false, Vec2.Zero);
                Assert.IsTrue(a.IsCpuTurn);
                if (a.CpuShoot()) makesStar++;
                if (b.CpuShoot()) makesBrick++;
            }
            Assert.Greater(makesStar, makesBrick);
            Assert.AreEqual(9, new HorseSession(HorseOpponent.Cpu, CourtGeometry.Default, ShotTuning.Default, c.Rules[0], star).CpuSpots.Count);
        }

        [Test]
        public void ResumeWithBall_HandsItBackWhereYouStand()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.PracticeDefault();
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            for (int i = 0; i < 60; i++) m.Step(1f / 60f, new PlayerInput { Move = new Vec2(1f, 0f) });
            var where = m.Controlled.Position;
            m.KnockLoose(new Vec2(2f, 2f));
            m.ResumeWithBall(m.ControlledIndex);
            Assert.IsTrue(m.HumanHasBall);
            Assert.AreEqual(MatchPhase.Live, m.Phase);
            Assert.Less(Vec2.Distance(where, m.Controlled.Position), 0.01f, "no reset to the check spot");
        }
    }

    public class TwentyOneAndWorldTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void TwentyOne_BustsBackTo13_AndOnlyExactly21Wins()
        {
            var c = DefaultContent.Create();
            var rules = c.Find(c.Rules, "rules.21");
            Assert.IsTrue(rules.bustRule && rules.makeItTakeIt && !rules.useGameClock);
            var r = new MatchRequest { Mode = GameMode.OneOnOne, HomeTeamId = "team.metro_comets", AwayTeamId = "team.harbor_hounds", RulesId = "rules.21", Seed = 4 };
            var setup = MatchSetup.FromRequest(r, c);
            setup.Shot.debugGreenAlwaysMakes = true;
            setup.PassiveOpponents = true;
            var m = new MatchSimulation(setup);
            m.Score[0] = 20;
            var log = new List<MatchEvent>();
            // A two-pointer from 20 goes over: bust.
            for (int i = 0; i < 600 && !(m.HumanHasBall && m.Phase != MatchPhase.DeadBall && m.ChargingIndex < 0); i++) m.Step(Dt, default);
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            for (int i = 0; i < 120 && m.ChargingIndex >= 0 && m.ChargeMeter < m.Setup.Shot.greenCenter - 0.01f; i++) m.Step(Dt, new PlayerInput { ShootHeld = true });
            m.Step(Dt, default);
            for (int i = 0; i < 600 && m.Phase != MatchPhase.CheckBall; i++) { m.Step(Dt, default); log.AddRange(m.Events); }
            bool madeTwo = log.Exists(e => e.Type == MatchEventType.ShotMade && e.Value == 2);
            if (madeTwo)
            {
                Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.Bust), "20 + 2 busts");
                Assert.AreEqual(13, m.Score[0]);
                Assert.IsFalse(m.IsOver);
            }
            else
            {
                Assert.AreEqual(21, m.Score[0]);
                Assert.IsTrue(m.IsOver || m.Phase == MatchPhase.DeadBall);
            }
            Assert.AreEqual(0, m.OffenseTeam, "make it, take it");
        }

        [Test]
        public void AroundTheWorld_HasSevenSpots_AndAdvancesOnMakesFromTheSpot()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.PracticeDefault();
            r.Drill = (int)DrillKind.AroundTheWorld;
            var setup = MatchSetup.FromRequest(r, c);
            setup.Shot.debugGreenAlwaysMakes = true;
            var m = new MatchSimulation(setup);
            var s = new PracticeSession(DrillKind.AroundTheWorld, m);
            Assert.AreEqual(7, s.WorldSpots.Count);
            foreach (var p in s.WorldSpots) Assert.Less(m.Setup.Court.DistanceToHoop(p), m.Setup.Court.arcRadius, "mid-range spots");
            int hold = 0;
            for (int i = 0; i < 60 * 240 && !s.Finished; i++)
            {
                var input = new PlayerInput();
                var to = s.WorldSpots[Math.Min(s.WorldSpot, 6)] - m.Controlled.Position;
                if (m.HumanHasBall && m.ChargingIndex < 0 && to.Magnitude > 0.35f) input.Move = to.Normalized;
                else if (m.HumanHasBall && m.ChargingIndex < 0 && hold == 0) { input.ShootPressed = true; input.ShootHeld = true; hold = 1; }
                else if (hold > 0 && m.ChargingIndex >= 0 && m.ChargeMeter < m.Setup.Shot.greenCenter - 0.01f) { input.ShootHeld = true; hold++; }
                else hold = 0;
                m.Step(Dt, input);
                s.Update(m, Dt);
            }
            Assert.Greater(s.WorldSpot, 2, "makes from the spot move you round the arc");
            if (s.Finished) Assert.Greater(s.CourseTime, 0f);
            var d = Career.New(c);
            Assert.IsTrue(Career.RecordPractice(d, 0, 0, 0, 0f, 0, 0, false, 31.5f, true));
            Assert.AreEqual(31.5f, d.practice.aroundWorldTime, 1e-4);
            Assert.AreEqual(1, d.practice.horseWins);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual(31.5f, back.practice.aroundWorldTime, 1e-3);
            Assert.AreEqual(1, back.practice.horseWins);
        }
    }

    public class DynastyTests
    {
        private static CareerSaveData FinishedSeason(ContentCatalog c, int season = 1)
        {
            var d = Career.New(c);
            d.rise.stage = RiseStage.Complete;
            d.rise.season = new SeasonSaveData { seasonNumber = season, championId = "team.metro_comets" };
            return d;
        }

        [Test]
        public void OffSeason_AgesPlayers_RecordsTheChampion_AndOffersThreeProspects()
        {
            var c = DefaultContent.Create();
            var d = FinishedSeason(c);
            var someone = c.TeamsInTier(TeamTier.League)[0].rosterPlayerIds[0];
            int ageBefore = DynastyEngine.AgeOf(d.dynasty, someone);
            Assert.IsTrue(DynastyEngine.OffSeasonDue(d));
            var report = DynastyEngine.OffSeason(d, c);
            Assert.IsNotNull(report);
            Assert.AreEqual(1, report.Year);
            Assert.AreEqual(ageBefore + 1, DynastyEngine.AgeOf(d.dynasty, someone));
            Assert.AreEqual("team.metro_comets", d.dynasty.champions[0].teamId);
            Assert.AreEqual(DynastyEngine.ProspectCount, d.dynasty.draftPool.Count);
            Assert.IsFalse(DynastyEngine.OffSeasonDue(d), "only once per season");
            Assert.IsNull(DynastyEngine.OffSeason(d, c));
        }

        [Test]
        public void Veterans_Retire_AndRookiesTakeTheirSpot_ValidatingContent()
        {
            var c = DefaultContent.Create();
            var d = FinishedSeason(c);
            var team = c.TeamsInTier(TeamTier.League)[1];
            string vet = team.rosterPlayerIds[0];
            DynastyEngine.Track(d.dynasty, vet).age = DynastyEngine.RetireAge - 1;
            DynastyEngine.Track(d.dynasty, vet).peak = 90;
            DynastyEngine.Track(d.dynasty, vet).seasons = 9;
            var report = DynastyEngine.OffSeason(d, c);
            Assert.IsFalse(team.rosterPlayerIds.Contains(vet), "the veteran retired");
            Assert.IsTrue(d.dynasty.swaps.Exists(s => s.oldId == vet));
            Assert.IsTrue(report.HallOfFame.Contains(c.Player(vet).DisplayName), "a 90-peak, 10-season career makes the Hall");
            Assert.AreEqual(1, d.dynasty.hall.FindAll(h => h.name == c.Player(vet).DisplayName).Count);
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());
        }

        [Test]
        public void YoungPlayersImprove_VeteransDecline()
        {
            Assert.Greater(DynastyEngine.Progression(21), 0);
            Assert.AreEqual(0, DynastyEngine.Progression(28));
            Assert.Less(DynastyEngine.Progression(34), 0);
        }

        [Test]
        public void Draft_AddsTheProspectToYourCrew_ForFree()
        {
            var c = DefaultContent.Create();
            var d = FinishedSeason(c);
            DynastyEngine.OffSeason(d, c);
            string pick = d.dynasty.draftPool[1];
            Assert.IsFalse(DynastyEngine.Draft(d, c, "player.nobody"));
            Assert.IsTrue(DynastyEngine.Draft(d, c, pick));
            Assert.IsTrue(d.rise.signed.Contains(pick));
            Assert.AreEqual(0, d.dynasty.draftPool.Count);
            Assert.IsTrue(CrewEngine.IsFree(d, c, pick));
            Assert.AreEqual(RecruitCheck.Ok, CrewEngine.Recruit(d, c, pick, 0));
        }

        [Test]
        public void Dynasty_SurvivesSaveAndLoad_AndRebuildsTheLeague()
        {
            var c = DefaultContent.Create();
            var d = FinishedSeason(c);
            var team = c.TeamsInTier(TeamTier.League)[2];
            string vet = team.rosterPlayerIds[1];
            DynastyEngine.Track(d.dynasty, vet).age = 40;
            DynastyEngine.OffSeason(d, c);
            var rosterAfter = new List<string>(team.rosterPlayerIds);
            var ratingsAfter = new Dictionary<string, int>();
            foreach (var id in rosterAfter) ratingsAfter[id] = c.Player(id).attributes.Overall;

            var json = SaveCodec.Encode(d);
            var fresh = DefaultContent.Create();
            var back = SaveCodec.Decode(json, fresh, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            DynastyEngine.Apply(fresh, back.dynasty);
            var freshTeam = fresh.Team(team.id);
            CollectionsEqual(rosterAfter, freshTeam.rosterPlayerIds);
            foreach (var id in rosterAfter)
                Assert.AreEqual(ratingsAfter[id], fresh.Player(id).attributes.Overall, 1.0, id);
            Assert.AreEqual(d.dynasty.year, back.dynasty.year);
            Assert.AreEqual(d.dynasty.hall.Count, back.dynasty.hall.Count);
            Assert.AreEqual(d.dynasty.rookies.Count, back.dynasty.rookies.Count);
            Assert.AreEqual(1, DynastyEngine.TitlesByTeam(back.dynasty)[0].Value);
        }

        private static void CollectionsEqual(List<string> a, List<string> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i], "slot " + i);
        }
    }

    public class GifTests
    {
        [Test]
        public void Palette_RoundTripsItsOwnColours()
        {
            for (int i = 0; i < GifEncoder.PaletteSize; i++)
            {
                GifEncoder.PaletteColor(i, out byte r, out byte g, out byte b);
                Assert.AreEqual(i, (int)GifEncoder.Index(r, g, b));
            }
        }

        [Test]
        public void Encode_WritesAValidLoopingGif_ThatDecodesBack()
        {
            const int w = 37, h = 23;
            var frames = new List<byte[]>();
            var rng = new SeededRandom(5);
            for (int f = 0; f < 3; f++)
            {
                var px = new byte[w * h * 3];
                for (int i = 0; i < w * h; i++)
                {
                    // Blocky pixel art plus some noise so the LZW table fills and resets.
                    int block = ((i % w) / 4 + (i / w) / 4 + f) % 5;
                    GifEncoder.PaletteColor(rng.Range(0, 3) == 0 ? rng.Range(0, GifEncoder.PaletteSize) : block * 40, out px[i * 3], out px[i * 3 + 1], out px[i * 3 + 2]);
                }
                frames.Add(px);
            }
            var gif = GifEncoder.Encode(w, h, frames, 7);
            Assert.AreEqual("GIF89a", System.Text.Encoding.ASCII.GetString(gif, 0, 6));
            Assert.AreEqual(w, gif[6] | gif[7] << 8);
            Assert.AreEqual(h, gif[8] | gif[9] << 8);
            Assert.AreEqual(0x3B, gif[gif.Length - 1]);
            var decoded = Decode(gif, w, h);
            Assert.AreEqual(3, decoded.Count);
            for (int f = 0; f < 3; f++)
                for (int i = 0; i < w * h; i++)
                {
                    byte expected = GifEncoder.Index(frames[f][i * 3], frames[f][i * 3 + 1], frames[f][i * 3 + 2]);
                    if (decoded[f][i] != expected) Assert.Fail("frame " + f + " pixel " + i + ": " + decoded[f][i] + " != " + expected);
                }
        }

        [Test]
        public void BigFlatFrames_CompressWell()
        {
            const int w = 270, h = 480;
            var frame = new byte[w * h * 3];
            var gif = GifEncoder.Encode(w, h, new List<byte[]> { frame, frame, frame, frame }, 7);
            Assert.Less(gif.Length, w * h, "four flat frames should be far smaller than one raw frame");
            Assert.AreEqual(480, HighlightClip.HeightFor(1080, 1920));
            Assert.IsTrue(HighlightClip.FileName(new DateTime(2026, 10, 2, 9, 5, 0)).EndsWith(".gif"));
        }

        /// <summary>Minimal GIF decoder (for the test only): returns each frame's palette indices.</summary>
        private static List<byte[]> Decode(byte[] gif, int w, int h)
        {
            var frames = new List<byte[]>();
            int p = 13 + 256 * 3;
            while (p < gif.Length)
            {
                byte b = gif[p++];
                if (b == 0x3B) break;
                if (b == 0x21)
                {
                    p++; // label
                    while (gif[p] != 0) p += gif[p] + 1;
                    p++;
                    continue;
                }
                Assert.AreEqual(0x2C, b, "image descriptor");
                p += 9;
                int min = gif[p++];
                var data = new List<byte>();
                while (gif[p] != 0)
                {
                    int n = gif[p++];
                    for (int i = 0; i < n; i++) data.Add(gif[p++]);
                }
                p++;
                frames.Add(Lzw(data.ToArray(), min, w * h));
            }
            return frames;
        }

        private static byte[] Lzw(byte[] data, int minCode, int count)
        {
            int clear = 1 << minCode, end = clear + 1;
            var dict = new List<List<byte>>();
            void Reset()
            {
                dict.Clear();
                for (int i = 0; i < clear; i++) dict.Add(new List<byte> { (byte)i });
                dict.Add(null);
                dict.Add(null);
            }
            Reset();
            int size = minCode + 1, bitPos = 0;
            var output = new List<byte>(count);
            List<byte> prev = null;
            while (true)
            {
                int code = 0;
                for (int i = 0; i < size; i++, bitPos++)
                {
                    if (bitPos / 8 >= data.Length) return output.ToArray();
                    if ((data[bitPos / 8] >> (bitPos % 8) & 1) != 0) code |= 1 << i;
                }
                if (code == clear) { Reset(); size = minCode + 1; prev = null; continue; }
                if (code == end) break;
                List<byte> entry;
                if (code < dict.Count && dict[code] != null) entry = dict[code];
                else if (code == dict.Count && prev != null) { entry = new List<byte>(prev) { prev[0] }; }
                else throw new Exception("bad code " + code + " at " + dict.Count);
                output.AddRange(entry);
                if (prev != null && dict.Count < 4096)
                {
                    dict.Add(new List<byte>(prev) { entry[0] });
                    if (dict.Count == (1 << size) && size < 12) size++;
                }
                prev = entry;
            }
            return output.ToArray();
        }
    }
}
