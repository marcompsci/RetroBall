using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 35: Kit Studio (Pack/Unpack, palette, defaults, clash, unlocks, share codes) and Rewards.</summary>
    public class Phase35KitStudioTests
    {
        // ---------------------------------------------------------------- colour encoding

        [Test]
        public void Pack_Unpack_RoundTrip()
        {
            var original = RgbColor.FromHex("#E8403C");
            var snapped = Kits.Snap(original); // snap to grid first
            Assert.AreEqual(snapped, Kits.Unpack(Kits.Pack(snapped)));
        }

        [Test]
        public void Expand_MapsZeroAndMax()
        {
            Assert.AreEqual(0, Kits.Expand(0));
            Assert.AreEqual(255, Kits.Expand(31));
        }

        [Test]
        public void Snap_IsDeterministic_AndIdempotent()
        {
            var c = RgbColor.FromHex("#3A7BFF");
            var once = Kits.Snap(c);
            Assert.AreEqual(once, Kits.Snap(once), "snapping an already-snapped colour is a no-op");
        }

        [Test]
        public void PackUnpack_WhiteAndBlack()
        {
            var white = new RgbColor(255, 255, 255);
            var black = new RgbColor(0, 0, 0);
            Assert.AreEqual(white, Kits.Unpack(Kits.Pack(white)));
            Assert.AreEqual(black, Kits.Unpack(Kits.Pack(black)));
        }

        // ---------------------------------------------------------------- palette

        [Test]
        public void Palette64_HasCorrectSize()
        {
            Assert.AreEqual(64, Kits.Palette64.Length);
        }

        [Test]
        public void Palette64_FirstEightAreGreyscale()
        {
            for (int i = 0; i < 8; i++)
            {
                var c = Kits.Palette64[i];
                Assert.AreEqual(c.r, c.g, "row 0 entry " + i + ": R==G");
                Assert.AreEqual(c.g, c.b, "row 0 entry " + i + ": G==B");
            }
        }

        [Test]
        public void Palette64_AllEntriesAreSnapped()
        {
            foreach (var c in Kits.Palette64)
                Assert.AreEqual(c, Kits.Snap(c), "every palette entry is on the RGB555 grid");
        }

        // ---------------------------------------------------------------- defaults and Ensure

        [Test]
        public void Defaults_HasThreeSlots()
        {
            var kits = Kits.Defaults(RgbColor.FromHex("#E8403C"), RgbColor.FromHex("#F4F1DE"), RgbColor.FromHex("#FFB703"), null, null);
            Assert.AreEqual(3, kits.Count);
        }

        [Test]
        public void Defaults_SlotNamesAreHomeAwayAlt()
        {
            var kits = Kits.Defaults(RgbColor.FromHex("#E8403C"), RgbColor.FromHex("#F4F1DE"), RgbColor.FromHex("#FFB703"), null, null);
            Assert.AreEqual("HOME", kits[0].name);
            Assert.AreEqual("AWAY", kits[1].name);
            Assert.AreEqual("ALT",  kits[2].name);
        }

        [Test]
        public void Defaults_HomeJerseyMatchesInputColour()
        {
            var jersey = RgbColor.FromHex("#FF0000");
            var trim = new RgbColor(0xF4, 0xF1, 0xDE);
            var kits = Kits.Defaults(jersey, trim, trim, null, null);
            var packed = Kits.Unpack(kits[0].jersey);
            // Pack/Unpack at most loses the low 3 bits per channel
            Assert.AreEqual(Kits.Snap(jersey), packed, "home jersey colour matches snapped input");
        }

        [Test]
        public void Ensure_FillsMissingSlotsFromDefaults()
        {
            var save = new KitSaveData();
            var red = RgbColor.FromHex("#C0392B");
            var white = new RgbColor(0xF4, 0xF1, 0xDE);
            Kits.Ensure(save, red, white, white, null, null);
            Assert.AreEqual(Kits.SlotCount, save.slots.Count);
            Assert.IsFalse(string.IsNullOrEmpty(save.slots[0].name));
        }

        [Test]
        public void Ensure_ClampsWearOutOfRange()
        {
            var save = new KitSaveData { wear = 99 };
            var red = RgbColor.FromHex("#C0392B");
            var white = new RgbColor(0xF4, 0xF1, 0xDE);
            Kits.Ensure(save, red, white, white, null, null);
            Assert.GreaterOrEqual(save.wear, 0);
            Assert.Less(save.wear, Kits.SlotCount);
        }

        // ---------------------------------------------------------------- ForMatch (clash detection)

        [Test]
        public void ForMatch_ReturnsHome_WhenNoClash()
        {
            var save = new KitSaveData { designed = true, wear = 0, autoAway = true };
            var red = RgbColor.FromHex("#FF0000");
            var white = new RgbColor(0xF4, 0xF1, 0xDE);
            save.slots = Kits.Defaults(red, white, white, null, null);
            var kit = Kits.ForMatch(save, RgbColor.FromHex("#0000FF"), ColorFilter.Off, out bool switched);
            Assert.IsFalse(switched, "blue opponent → red home kit stays");
        }

        [Test]
        public void ForMatch_SwitchesToAway_OnClash()
        {
            var save = new KitSaveData { designed = true, wear = 0, autoAway = true };
            var red = RgbColor.FromHex("#FF0000");
            var white = new RgbColor(0xF4, 0xF1, 0xDE);
            save.slots = Kits.Defaults(red, white, white, null, null);
            // Opponent wearing the same red — home kit clashes; away (white) does not.
            var kit = Kits.ForMatch(save, red, ColorFilter.Off, out bool switched);
            Assert.IsTrue(switched, "same colour opponent jersey → switch to Away");
            Assert.AreEqual(save.slots[1], kit, "the Away slot is returned");
        }

        [Test]
        public void ForMatch_RespectsAutoAwayFalse()
        {
            var save = new KitSaveData { designed = true, wear = 0, autoAway = false };
            var red = RgbColor.FromHex("#FF0000");
            var white = new RgbColor(0xF4, 0xF1, 0xDE);
            save.slots = Kits.Defaults(red, white, white, null, null);
            Kits.ForMatch(save, red, ColorFilter.Off, out bool switched);
            Assert.IsFalse(switched, "autoAway=false: never switches even on a clash");
        }

        // ---------------------------------------------------------------- share codes

        [Test]
        public void Encode_Decode_RoundTrip()
        {
            var original = Kits.Randomize(42, null);
            string code = Kits.Encode(original);
            Assert.IsTrue(Kits.TryDecode(code, out var decoded));
            // Check every colour and a style field
            Assert.AreEqual(original.jersey, decoded.jersey);
            Assert.AreEqual(original.trim,   decoded.trim);
            Assert.AreEqual(original.cut,    decoded.cut);
            Assert.AreEqual(original.collar, decoded.collar);
        }

        [Test]
        public void Decode_WithFullLink_Works()
        {
            string code = Kits.Encode(Kits.Randomize(7, null));
            Assert.IsTrue(Kits.TryDecode(Kits.LinkPrefix + code, out _), "retrohoops:// link");
        }

        [Test]
        public void Decode_WithOldLink_Works()
        {
            string code = Kits.Encode(Kits.Randomize(7, null));
            Assert.IsTrue(Kits.TryDecode(Kits.OldLinkPrefix + code, out _), "old retroball:// link");
        }

        [Test]
        public void Decode_ToleratesI_And_O_Substitutions()
        {
            // Encode a kit, replace '1' with 'I' and '0' with 'O', then decode
            string code = Kits.Encode(Kits.Randomize(99, null));
            string with_typos = code.Replace('1', 'I').Replace('0', 'O');
            Assert.IsTrue(Kits.TryDecode(with_typos, out _), "I→1 and O→0 substitutions are accepted");
        }

        [Test]
        public void Decode_RejectsCorruptedCode()
        {
            string code = Kits.Encode(Kits.Randomize(5, null));
            // Flip first character to a different base-32 symbol
            char[] chars = code.ToCharArray();
            chars[0] = chars[0] == 'A' ? 'B' : 'A';
            Assert.IsFalse(Kits.TryDecode(new string(chars), out _), "one changed char invalidates the CRC");
        }

        [Test]
        public void Decode_RejectsEmptyString()
        {
            Assert.IsFalse(Kits.TryDecode("", out _));
            Assert.IsFalse(Kits.TryDecode(null, out _));
        }

        [Test]
        public void Pretty_InsertsHyphensEveryFive()
        {
            string code = "ABCDEFGHIJ";
            string pretty = Kits.Pretty(code);
            Assert.AreEqual("ABCDE-FGHIJ", pretty);
        }

        // ---------------------------------------------------------------- unlocks

        [Test]
        public void IsUnlocked_FreeStyle_IsAlwaysAvailable()
        {
            // Tank (cut=0) has no unlock condition
            Assert.IsTrue(Kits.IsUnlocked(Kits.PartCut, (int)JerseyCut.Tank, null));
        }

        [Test]
        public void IsUnlocked_LongSleeve_RequiresTenWins()
        {
            var career = Career.New(DefaultContent.Create());
            Assert.IsFalse(Kits.IsUnlocked(Kits.PartCut, (int)JerseyCut.LongSleeve, career), "locked before 10 wins");
            career.totals.wins = 10;
            Assert.IsTrue(Kits.IsUnlocked(Kits.PartCut, (int)JerseyCut.LongSleeve, career), "unlocked after 10 wins");
        }

        [Test]
        public void HintFor_ReturnsNull_ForFreeStyle()
        {
            Assert.IsNull(Kits.HintFor(Kits.PartCut, (int)JerseyCut.Tank));
        }

        [Test]
        public void HintFor_ReturnsNonEmpty_ForLockedStyle()
        {
            string hint = Kits.HintFor(Kits.PartCut, (int)JerseyCut.LongSleeve);
            Assert.IsNotNull(hint);
            Assert.IsNotEmpty(hint);
        }
    }

    public class Phase35RewardsTests
    {
        private static readonly RewardTuning T = RewardTuning.Default;

        private static MatchSummary Win(int pts = 10, int ast = 2, int reb = 3, int stl = 1, int blk = 0,
                                        GameMode mode = GameMode.Rise, bool isPlayoff = false, bool isFinal = false, int margin = 6, int greens = 0)
        {
            return new MatchSummary
            {
                mode = mode, humanTeam = 0, winner = 0,
                scoreA = 21, scoreB = 21 - margin,
                isPlayoff = isPlayoff, isFinal = isFinal,
                lines = new List<SummaryLine>
                {
                    new SummaryLine { isHuman = true, team = 0, stats = new PlayerStatLine { points = pts, assists = ast, rebounds = reb, steals = stl, blocks = blk, greenReleases = greens } },
                },
            };
        }

        private static MatchSummary Loss(int pts = 5, GameMode mode = GameMode.Rise)
        {
            return new MatchSummary
            {
                mode = mode, humanTeam = 0, winner = 1,
                scoreA = 15, scoreB = 21,
                lines = new List<SummaryLine>
                {
                    new SummaryLine { isHuman = true, team = 0, stats = new PlayerStatLine { points = pts } },
                },
            };
        }

        // ---------------------------------------------------------------- basic formula

        [Test]
        public void Win_EarnsMoreSP_ThanLoss_SameStats()
        {
            var win = Rewards.For(Win(pts: 10, ast: 0, reb: 0, stl: 0, blk: 0), T);
            var loss = Rewards.For(Loss(pts: 10), T);
            Assert.Greater(win.signalPoints, loss.signalPoints);
        }

        [Test]
        public void SP_IncludesStatBonus()
        {
            // Win with stats should earn more than a win with zero stats
            var withStats = Rewards.For(Win(pts: 10, ast: 3, reb: 4, stl: 2, blk: 0), T);
            var bare = Rewards.For(Win(pts: 0, ast: 0, reb: 0, stl: 0, blk: 0), T);
            Assert.Greater(withStats.signalPoints, bare.signalPoints);
        }

        [Test]
        public void Practice_EarnsNothing()
        {
            foreach (var mode in new[] { GameMode.Practice, GameMode.Tutorial, GameMode.Versus })
            {
                var g = Rewards.For(Win(mode: mode), T);
                Assert.AreEqual(0, g.signalPoints, mode + " should earn 0 SP");
                Assert.AreEqual(0, g.fans, mode + " should earn 0 fans");
            }
        }

        [Test]
        public void QuickCall_ScalesDownRelativeToRise()
        {
            // Same match stats in Rise vs. Quick Call — Quick Call earns less
            var rise = Rewards.For(Win(pts: 10, ast: 2, reb: 3, stl: 1, blk: 0, mode: GameMode.Rise), T);
            var quick = Rewards.For(Win(pts: 10, ast: 2, reb: 3, stl: 1, blk: 0, mode: GameMode.QuickCall), T);
            Assert.Less(quick.signalPoints, rise.signalPoints, "Quick Call earns less than Rise");
        }

        [Test]
        public void Championship_AddsBonus_OnFinalWin()
        {
            var regular = Rewards.For(Win(), T);
            var finals = Rewards.For(Win(isFinal: true), T);
            Assert.Greater(finals.signalPoints, regular.signalPoints, "final win adds a championship bonus");
        }

        [Test]
        public void Playoff_AddsBonus_OnPlayoffWin()
        {
            var regular = Rewards.For(Win(), T);
            var playoff = Rewards.For(Win(isPlayoff: true), T);
            Assert.Greater(playoff.signalPoints, regular.signalPoints, "playoff win adds a bonus");
        }

        [Test]
        public void SignalPoints_CapIsRespected()
        {
            // Astronomical stats — should be capped at maxPerGame
            var g = Rewards.For(Win(pts: 9999, ast: 9999, reb: 9999, stl: 9999, blk: 0), T);
            Assert.LessOrEqual(g.signalPoints, T.maxPerGame, "SP can't exceed the cap");
        }

        [Test]
        public void BlowoutWin_AddsFanBonus()
        {
            var close = Rewards.For(Win(margin: 2), T);
            var blowout = Rewards.For(Win(margin: T.blowoutMargin), T);
            Assert.Greater(blowout.fans, close.fans, "blowout margin adds extra fans");
        }

        [Test]
        public void Loss_EarnsFewFans_ButNonZero()
        {
            var g = Rewards.For(Loss(), T);
            Assert.Greater(g.fans, 0, "losing still earns some fans");
            Assert.Less(g.fans, T.fansPerWin, "but fewer than a win");
        }

        [Test]
        public void Greens_AddFans()
        {
            var noGreens = Rewards.For(Win(greens: 0), T);
            var withGreens = Rewards.For(Win(greens: 5), T);
            Assert.Greater(withGreens.fans, noGreens.fans, "green releases add fans");
        }
    }
}
