using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 20: Kit Studio, share codes and QR, trading cards, Holiday Games, Full Court rules.</summary>
    public class KitStudioTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Colours_PackToFifteenBits_AndPaletteIsOnTheGrid()
        {
            Assert.AreEqual(RgbColor.White, Kits.Unpack(Kits.Pack(RgbColor.White)));
            Assert.AreEqual(RgbColor.Black, Kits.Unpack(Kits.Pack(RgbColor.Black)));
            for (int v = 0; v < 32768; v += 97) Assert.AreEqual(v, Kits.Pack(Kits.Unpack(v)));
            Assert.AreEqual(64, Kits.Palette64.Length);
            var unique = new HashSet<int>();
            foreach (var c in Kits.Palette64)
            {
                Assert.AreEqual(c, Kits.Snap(c), "palette colours are exact on the 32-step grid");
                unique.Add(Kits.Pack(c));
            }
            Assert.GreaterOrEqual(unique.Count, 60, "palette colours are (nearly all) distinct");
        }

        [Test]
        public void ClassicKit_DrawsExactlyTheOriginalSprites()
        {
            var look = new AppearanceDef(2, 3, 1, BodyType.Broad, 2);
            var jersey = RgbColor.FromHex("#3A5BD9");
            var trim = RgbColor.FromHex("#FFD166");
            var a = CharacterSpriteGenerator.GenerateSheet(look, jersey, trim, RgbColor.White, RgbColor.FromHex("#112233"), TeamPattern.Dots, RgbColor.FromHex("#445566"));
            var b = CharacterSpriteGenerator.GenerateSheet(look, KitLook.Classic(jersey, trim, RgbColor.White, RgbColor.FromHex("#112233"), TeamPattern.Dots, RgbColor.FromHex("#445566")));
            for (int i = 0; i < a.Pixels.Length; i++) Assert.AreEqual(a.Pixels[i], b.Pixels[i]);
        }

        [Test]
        public void Styles_ChangeTheSprite()
        {
            var look = new AppearanceDef(1, 1, 0, BodyType.Standard, 1);
            var basic = Kits.Look(Kits.FromColors("H", RgbColor.FromHex("#3A5BD9"), RgbColor.FromHex("#FFD166"), RgbColor.FromHex("#F72585"),
                                                  RgbColor.FromHex("#14213D"), RgbColor.FromHex("#F2F2F2")));
            var reference = CharacterSpriteGenerator.GenerateSheet(look, basic);
            int Count(PixelCanvas c, RgbColor col)
            {
                int n = 0;
                foreach (var p in c.Pixels) if (p.Equals(col)) n++;
                return n;
            }
            var sleeves = basic; sleeves.Cut = JerseyCut.LongSleeve;
            Assert.Greater(Count(CharacterSpriteGenerator.GenerateSheet(look, sleeves), basic.Jersey), Count(reference, basic.Jersey), "long sleeves add jersey pixels");
            var highs = basic; highs.Top = ShoeTop.High;
            Assert.Greater(Count(CharacterSpriteGenerator.GenerateSheet(look, highs), basic.Shoe), Count(reference, basic.Shoe), "high tops are taller");
            var longShorts = basic; longShorts.Length = ShortsLength.Long;
            Assert.Greater(Count(CharacterSpriteGenerator.GenerateSheet(look, longShorts), basic.Shorts), Count(reference, basic.Shorts));
            var band = basic; band.Chest = ChestMark.Band;
            Assert.Greater(Count(CharacterSpriteGenerator.GenerateSheet(look, band), basic.Accent), Count(reference, basic.Accent));
            var stripe = basic; stripe.ShoeStripeOn = true; stripe.ShoeStripe = RgbColor.FromHex("#E63946");
            Assert.Greater(Count(CharacterSpriteGenerator.GenerateSheet(look, stripe), stripe.ShoeStripe), 0);
        }

        [Test]
        public void ShareCode_RoundTrips_AndRejectsTypos()
        {
            for (uint seed = 1; seed <= 40; seed++)
            {
                var kit = Kits.Randomize(seed, null);
                string code = Kits.Encode(kit);
                Assert.AreEqual(33, code.Length);
                Assert.IsTrue(Kits.TryDecode(code, out var back), code);
                Assert.IsTrue(kit.SameLook(back), "seed " + seed);
                Assert.IsTrue(Kits.TryDecode(Kits.Pretty(code).ToLowerInvariant(), out _), "case and dashes don't matter");
                Assert.IsTrue(Kits.TryDecode("Look at this: " + Kits.LinkPrefix + code, out var linked));
                Assert.IsTrue(kit.SameLook(linked));
                // Change one character: the checksum notices.
                char[] typo = code.ToCharArray();
                typo[10] = typo[10] == 'A' ? 'B' : 'A';
                Assert.IsFalse(Kits.TryDecode(new string(typo), out _), "typo in seed " + seed);
            }
            Assert.IsFalse(Kits.TryDecode("", out _));
            Assert.IsFalse(Kits.TryDecode("HELLO", out _));
            Assert.IsFalse(Kits.TryDecode("!!!", out _));
        }

        [Test]
        public void Kits_SaveAsCodes_AndOldSavesKeepTheOldLook()
        {
            var d = Career.New(_c);
            Assert.IsFalse(d.kits.designed);
            Kits.Ensure(d.kits, RgbColor.FromHex("#1FB5A6"), RgbColor.FromHex("#FF6F59"), RgbColor.White, null, null);
            Assert.AreEqual(3, d.kits.slots.Count);
            d.kits.slots[2] = Kits.Randomize(9, d, "NIGHT");
            d.kits.designed = true;
            d.kits.wear = 2;
            d.kits.autoAway = false;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsTrue(back.kits.designed);
            Assert.AreEqual(2, back.kits.wear);
            Assert.IsFalse(back.kits.autoAway);
            Assert.AreEqual("NIGHT", back.kits.slots[2].name);
            Assert.IsTrue(d.kits.slots[2].SameLook(back.kits.slots[2]));
            var fresh = SaveCodec.Decode(SaveCodec.Encode(Career.New(_c)), _c, out _);
            Assert.IsFalse(fresh.kits.designed);
            Assert.AreEqual(0, fresh.kits.slots.Count);
        }

        [Test]
        public void AwayKit_IsWornWhenColoursClash()
        {
            var s = new KitSaveData { designed = true };
            Kits.Ensure(s, RgbColor.FromHex("#C1121F"), RgbColor.FromHex("#F4F1DE"), RgbColor.FromHex("#FFD166"), null, null);
            var red = RgbColor.FromHex("#C8102E");
            var kit = Kits.ForMatch(s, red, ColorFilter.Off, out bool away);
            Assert.IsTrue(away);
            Assert.IsTrue(ReferenceEquals(s.slots[1], kit));
            kit = Kits.ForMatch(s, RgbColor.FromHex("#1D4ED8"), ColorFilter.Off, out away);
            Assert.IsFalse(away);
            Assert.IsTrue(ReferenceEquals(s.slots[0], kit));
            s.autoAway = false;
            Kits.ForMatch(s, red, ColorFilter.Off, out away);
            Assert.IsFalse(away, "auto-switch can be turned off");
            Assert.IsNull(Kits.ForMatch(new KitSaveData(), red, ColorFilter.Off, out _), "no kit until you design one");
        }

        [Test]
        public void Unlocks_ComeFromPlaying_AndRandomOnlyUsesUnlockedStyles()
        {
            var d = Career.New(_c);
            Assert.IsTrue(Kits.IsUnlocked(Kits.PartCut, (int)JerseyCut.Tee, d));
            Assert.IsFalse(Kits.IsUnlocked(Kits.PartCut, (int)JerseyCut.LongSleeve, d));
            Assert.IsNotNull(Kits.HintFor(Kits.PartCut, (int)JerseyCut.LongSleeve));
            for (uint seed = 1; seed < 200; seed++)
            {
                var k = Kits.Randomize(seed, d);
                Assert.AreNotEqual((int)JerseyCut.LongSleeve, k.cut);
                Assert.AreNotEqual((int)ShoeTop.High, k.top);
                Assert.IsTrue(Kits.IsUnlocked(Kits.PartPattern, k.pattern, d));
            }
            d.totals.wins = 10;
            Assert.IsTrue(Kits.IsUnlocked(Kits.PartCut, (int)JerseyCut.LongSleeve, d));
            foreach (var u in Kits.Unlocks) Assert.IsTrue(Loc.Has(u.Hint), u.Hint);
        }
    }

    public class QrAndCardTests
    {
        private static int FormatBits(QrCode q, bool firstCopy)
        {
            int bits = 0;
            if (firstCopy)
            {
                for (int i = 0; i <= 5; i++) bits |= (q.Get(8, i) ? 1 : 0) << i;
                bits |= (q.Get(8, 7) ? 1 : 0) << 6;
                bits |= (q.Get(8, 8) ? 1 : 0) << 7;
                bits |= (q.Get(7, 8) ? 1 : 0) << 8;
                for (int i = 9; i < 15; i++) bits |= (q.Get(14 - i, 8) ? 1 : 0) << i;
            }
            else
            {
                for (int i = 0; i < 8; i++) bits |= (q.Get(q.Size - 1 - i, 8) ? 1 : 0) << i;
                for (int i = 8; i < 15; i++) bits |= (q.Get(8, q.Size - 15 + i) ? 1 : 0) << i;
            }
            return bits;
        }

        [Test]
        public void Qr_HasFindersTimingAndValidFormatInfo()
        {
            foreach (int len in new[] { 5, 49, 100, 150, 213 })
            {
                var q = QrCode.EncodeText(new string('k', len));
                Assert.AreEqual(q.Version * 4 + 17, q.Size);
                // Finder corners are dark, their separators light.
                foreach (var (x, y) in new[] { (0, 0), (q.Size - 1, 0), (0, q.Size - 1) }) Assert.IsTrue(q.Get(x, y));
                Assert.IsFalse(q.Get(7, 7));
                Assert.IsTrue(q.Get(8, q.Size - 8), "dark module");
                for (int i = 8; i < q.Size - 8; i++) Assert.AreEqual(i % 2 == 0, q.Get(i, 6), "timing row");
                int f1 = FormatBits(q, true), f2 = FormatBits(q, false);
                Assert.AreEqual(f1, f2, "both format copies agree");
                int data = (f1 ^ 0x5412) >> 10;
                Assert.AreEqual(0, data >> 3, "error correction level M");
                Assert.AreEqual(q.Mask, data & 7);
            }
            Assert.Throws<System.ArgumentException>(() => QrCode.EncodeText(new string('k', 214)));
        }

        [Test]
        public void KitLink_FitsInAQr_AndIsDeterministic()
        {
            string link = Kits.LinkPrefix + Kits.Encode(Kits.Randomize(3, null));
            var a = QrCode.EncodeText(link);
            var b = QrCode.EncodeText(link);
            Assert.LessOrEqual(a.Version, 4);
            for (int y = 0; y < a.Size; y++)
                for (int x = 0; x < a.Size; x++) Assert.AreEqual(a.Get(x, y), b.Get(x, y));
            var canvas = a.ToCanvas(4, RgbColor.Black, RgbColor.White);
            Assert.AreEqual((a.Size + 8) * 4, canvas.Width);
        }

        [Test]
        public void TradingCard_DrawsYourKitAndText()
        {
            var kit = Kits.Look(Kits.Randomize(11, null));
            var card = TradingCard.Generate(new CardInfo
            {
                Look = new AppearanceDef(3, 2, 0, BodyType.Standard, 1), Kit = kit, Name = "Rook", Number = 7, Team = "First Callers",
                Role = "Floor General", Overall = 77, Stats = new[] { ("WINS", "12"), ("PTS", "340"), ("GREEN", "51") },
            });
            Assert.AreEqual(TradingCard.Width, card.Width);
            Assert.AreEqual(TradingCard.Height, card.Height);
            int jersey = 0, trim = 0, white = 0;
            foreach (var p in card.Pixels)
            {
                if (p.Equals(kit.Jersey)) jersey++;
                if (p.Equals(kit.Trim)) trim++;
                if (p.Equals(RgbColor.White)) white++;
            }
            Assert.Greater(jersey, 50, "your jersey is on the card");
            Assert.Greater(trim, 300, "the frame is your trim colour");
            Assert.Greater(white, 40, "the name and stats are written");
            Assert.AreEqual(4 * TradingCard.Width, TradingCard.Scale(card, 4).Width);
            foreach (char ch in "ROOK #77 FIRST CALLERS 0123456789") Assert.IsTrue(ch == ' ' || PixelFont.Has(ch), ch.ToString());
        }
    }

    public class HolidayTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void FourHolidayCourts_Exist_AndDraw()
        {
            Assert.AreEqual(4, Holidays.All.Count);
            foreach (var h in Holidays.All)
            {
                var court = _c.Court(h.CourtId);
                Assert.IsNotNull(court, h.CourtId);
                Assert.AreEqual(CourtCircuit.Holiday, court.circuit);
                Assert.AreEqual(h.Theme, court.theme);
                var plain = CourtGenerator.Generate(new CourtDef
                {
                    id = court.id, circuit = CourtCircuit.Blacktop, floor = court.floor, lines = court.lines, paint = court.paint,
                    skyTop = court.skyTop, skyBottom = court.skyBottom, crowdDensity = court.crowdDensity,
                }, CourtGeometry.Default, 3);
                var dressed = CourtGenerator.Generate(court, CourtGeometry.Default, 3);
                int diff = 0;
                for (int i = 0; i < plain.Pixels.Length; i++) if (!plain.Pixels[i].Equals(dressed.Pixels[i])) diff++;
                Assert.Greater(diff, 150, h.Name + " has decorations");
                Assert.IsTrue(Loc.Has(h.Name) && Loc.Has(h.Blurb), h.Name);
            }
            Assert.IsTrue(ContentValidator.Validate(_c).IsValid);
        }

        [Test]
        public void Seasons_AndEasterDates()
        {
            Assert.AreEqual(new System.DateTime(2026, 4, 5), Holidays.EasterSunday(2026));
            Assert.AreEqual(new System.DateTime(2027, 3, 28), Holidays.EasterSunday(2027));
            Assert.AreEqual(new System.DateTime(2024, 3, 31), Holidays.EasterSunday(2024));
            Assert.AreEqual(HolidayTheme.Halloween, Holidays.InSeason(new System.DateTime(2026, 10, 3)));
            Assert.AreEqual(HolidayTheme.Christmas, Holidays.InSeason(new System.DateTime(2026, 12, 24)));
            Assert.AreEqual(HolidayTheme.None, Holidays.InSeason(new System.DateTime(2026, 12, 29)));
            Assert.AreEqual(HolidayTheme.Easter, Holidays.InSeason(new System.DateTime(2026, 4, 2)));
            Assert.AreEqual(HolidayTheme.FourthOfJuly, Holidays.InSeason(new System.DateTime(2026, 7, 4)));
            Assert.AreEqual(HolidayTheme.None, Holidays.InSeason(new System.DateTime(2026, 8, 15)));
            var req = Holidays.Request(HolidayTheme.Christmas, DefaultContent.PlayerCrewId, _c.TeamsInTier(TeamTier.League)[0].id, "difficulty.caller");
            Assert.AreEqual(Holidays.ChristmasCourtId, req.CourtId);
            var m = new MatchSimulation(MatchSetup.FromRequest(req, _c));
            Assert.AreEqual(6, m.Players.Length, "Holiday Games are half-court 3-on-3");
        }
    }

    public class FullCourtRulesTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        private MatchSimulation Game(bool demo = true, uint seed = 3)
        {
            var league = _c.TeamsInTier(TeamTier.League);
            var setup = MatchSetup.FromRequest(new MatchRequest { Mode = GameMode.FullCourt, HomeTeamId = league[0].id, AwayTeamId = league[1].id, Seed = seed }, _c);
            setup.Demo = demo;
            return new MatchSimulation(setup);
        }

        [Test]
        public void Benches_HaveTwoFreshPlayers()
        {
            var m = Game();
            Assert.AreEqual(4, m.Bench.Count);
            var ids = new HashSet<string>();
            foreach (var p in m.Players) ids.Add(p.Def.id);
            foreach (var b in m.Bench) Assert.IsTrue(ids.Add(b.Def.id), "bench player " + b.Def.id + " isn't already on the court");
        }

        [Test]
        public void AfterABasket_TheBallIsInboundedFromTheBaseline()
        {
            var m = Game();
            bool checkedInbound = false;
            for (int i = 0; i < 60 * 240 && !m.IsOver && !checkedInbound; i++)
            {
                m.Step(Dt, default);
                if (m.Phase == MatchPhase.CheckBall && m.Time > 1f)
                {
                    Assert.IsTrue(m.MustInbound);
                    Assert.Greater(m.Holder.Position.y, m.Setup.Court.depth - 0.5f, "inbounder on the far baseline");
                    Assert.IsFalse(m.CanShoot(m.HolderIndex), "no shooting before the inbound pass");
                    // The AI gets it in quickly.
                    for (int j = 0; j < 60 * 4 && m.MustInbound; j++) m.Step(Dt, default);
                    Assert.IsFalse(m.MustInbound, "inbound pass thrown");
                    checkedInbound = true;
                }
            }
            Assert.IsTrue(checkedInbound);
        }

        [Test]
        public void HoldingTheBallInTheBackcourt_IsAnEightSecondViolation()
        {
            var m = Game(demo: false);
            m.Setup.PassiveOpponents = true;
            // Tip-off: the human has the ball just inside the backcourt. Stand still for more than 8 seconds.
            Assert.AreEqual(m.ControlledIndex, m.HolderIndex);
            var log = new List<MatchEvent>();
            for (int i = 0; i < 60 * 11 && m.Phase != MatchPhase.DeadBall; i++)
            {
                m.Step(Dt, new PlayerInput { Move = i < 2 ? new Vec2(0f, 0.05f) : Vec2.Zero });
                log.AddRange(m.Events);
            }
            var v = log.Find(e => e.Type == MatchEventType.Violation);
            Assert.AreEqual(MatchEventType.Violation, v.Type);
            Assert.AreEqual((int)ViolationKind.EightSeconds, v.Value);
        }

        [Test]
        public void GoingBackOverHalfCourt_IsABackcourtViolation()
        {
            var m = Game(demo: false);
            m.Setup.PassiveOpponents = true;
            var log = new List<MatchEvent>();
            // Up the floor (court "up" is -y in the stick)...
            for (int i = 0; i < 60 * 2; i++) { m.Step(Dt, new PlayerInput { Move = new Vec2(0f, -1f) }); log.AddRange(m.Events); }
            Assert.IsTrue(m.CrossedHalf);
            // ...then back down.
            for (int i = 0; i < 60 * 3 && m.Phase == MatchPhase.Live; i++) { m.Step(Dt, new PlayerInput { Move = new Vec2(0f, 1f) }); log.AddRange(m.Events); }
            var v = log.Find(e => e.Type == MatchEventType.Violation);
            Assert.AreEqual(MatchEventType.Violation, v.Type);
            Assert.AreEqual((int)ViolationKind.Backcourt, v.Value);
            Assert.AreEqual(1, m.Stats[m.ControlledIndex].turnovers);
        }

        [Test]
        public void TiredAiPlayers_SubOut_AndKeepTheirStats()
        {
            var m = Game(seed: 2);
            int subs = 0;
            for (int i = 0; i < 60 * 600 && !m.IsOver; i++)
            {
                if (m.Phase == MatchPhase.DeadBall)
                    foreach (var p in m.Players) if (!p.IsHuman) p.Stamina = System.Math.Min(p.Stamina, 0.3f); // force fatigue
                m.Step(Dt, default);
                foreach (var e in m.Events) if (e.Type == MatchEventType.Substitution) subs++;
            }
            Assert.Greater(subs, 0);
            var summary = MatchSummary.From(m, GameMode.FullCourt, "fc");
            Assert.Greater(summary.lines.Count, 10, "subs get their own box score lines");
            Assert.IsTrue(summary.fullCourt);
            int points = 0;
            foreach (var l in summary.lines) points += l.stats.points;
            Assert.AreEqual(m.Score[0] + m.Score[1], points);
        }

        [Test]
        public void RiseFinal_IsFullCourt()
        {
            var d = Career.New(_c);
            var r = d.rise;
            r.stage = RiseStage.Playoffs;
            r.season = SeasonEngine.Create(_c, DefaultContent.PlayerCrewId, 99);
            foreach (var g in r.season.games) if (g.round == 0) { g.played = true; g.homeScore = 21; g.awayScore = 10; }
            r.season.games.Add(new ScheduledGame { week = 99, round = 2, homeId = DefaultContent.PlayerCrewId, awayId = r.season.teamIds.Find(t => t != DefaultContent.PlayerCrewId) });
            r.season.games.RemoveAll(g => g.round == 1);
            var req = RiseEngine.NextMatch(r, _c, "difficulty.caller");
            Assert.IsNotNull(req);
            Assert.AreEqual(2, req.Round);
            Assert.IsTrue(req.FullCourt);
            var setup = MatchSetup.FromRequest(req, _c);
            Assert.IsTrue(setup.FullCourt);
            Assert.AreEqual(5, setup.TeamSize);
        }
    }
}
