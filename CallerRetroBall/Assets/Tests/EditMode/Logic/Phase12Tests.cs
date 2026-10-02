using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 12: create-a-player, accessibility, animation and sound, stats and records.</summary>
    public class CreatePlayerTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Default_IsRookWithYourNickname()
        {
            var d = Career.New(_c);
            d.nickname = "Ace";
            var p = PlayerCreator.BasePlayer(d, _c);
            var rook = _c.Player(DefaultContent.RookPlayerId);
            Assert.AreEqual("Ace", p.DisplayName);
            Assert.AreEqual(rook.attributes, p.attributes);
            Assert.AreEqual(rook.appearance.skinTone, p.appearance.skinTone);
        }

        [Test]
        public void Created_UsesYourLookArchetypeAndNumber()
        {
            var d = Career.New(_c);
            var arch = _c.Archetypes[3];
            d.customPlayer = new CustomPlayerData { created = true, skinTone = 4, hairStyle = 2, hairColor = 1, body = 2, heightTier = 0, jerseyNumber = 23, archetypeId = arch.id };
            var p = PlayerCreator.BasePlayer(d, _c);
            Assert.AreEqual(23, p.jerseyNumber);
            Assert.AreEqual(arch.id, p.archetypeId);
            Assert.AreEqual(4, p.appearance.skinTone);
            Assert.AreEqual(BodyType.Broad, p.appearance.body);
            Assert.AreEqual(arch.baseline.Offset(PlayerCreator.RatingOffset), p.attributes);
        }

        [Test]
        public void Clamp_FixesOutOfRangeValues()
        {
            var p = new CustomPlayerData { skinTone = 99, hairStyle = -1, hairColor = 50, body = 7, heightTier = -4, jerseyNumber = 300, archetypeId = "nope" };
            PlayerCreator.Clamp(p, _c);
            Assert.IsTrue(p.skinTone >= 0 && p.skinTone <= PlayerCreator.SkinToneCount - 1);
            Assert.IsTrue(p.hairStyle >= 0 && p.hairStyle <= PlayerCreator.HairStyleCount - 1);
            Assert.IsTrue(p.hairColor >= 0 && p.hairColor <= PlayerCreator.HairColorCount - 1);
            Assert.IsTrue(p.body >= 0 && p.body <= 2);
            Assert.IsTrue(p.heightTier >= 0 && p.heightTier <= 2);
            Assert.AreEqual(PlayerCreator.MaxJersey, p.jerseyNumber);
            Assert.IsNotNull(_c.ArchetypeById(p.archetypeId));
        }

        [Test]
        public void ForMatch_AppliesTrainingUpgrades_AndMatchUsesIt()
        {
            var d = Career.New(_c);
            d.customPlayer = PlayerCreator.FromRook(_c);
            d.customPlayer.created = true;
            d.customPlayer.jerseyNumber = 8;
            var u = _c.Upgrades[0];
            d.upgrades.Add(new UpgradeProgress { id = u.id, level = 2 });
            var p = PlayerCreator.ForMatch(d, _c);
            Assert.Greater(p.attributes.Get(u.attribute), PlayerCreator.BaseRatings(d, _c).Get(u.attribute));

            var r = MatchRequest.PracticeDefault();
            r.HumanPlayer = p;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, _c));
            Assert.AreEqual(8, m.Controlled.Def.jerseyNumber);
            Assert.AreEqual(p.attributes, m.Controlled.Def.attributes);
            Assert.IsFalse(ReferenceEquals(_c.Player(DefaultContent.RookPlayerId), m.Controlled.Def), "content must not be modified");
        }

        [Test]
        public void EveryCombination_RendersASprite()
        {
            for (int skin = 0; skin < PlayerCreator.SkinToneCount; skin++)
                for (int hair = 0; hair < PlayerCreator.HairStyleCount; hair++)
                {
                    var look = new AppearanceDef(skin, hair, hair % PlayerCreator.HairColorCount, (BodyType)(hair % 3), skin % 3);
                    var sheet = CharacterSpriteGenerator.GenerateSheet(look, RgbColor.FromHex("#F72585"), RgbColor.FromHex("#4CC9F0"), RgbColor.White);
                    Assert.Greater(sheet.OpaqueCount(), 100);
                }
        }
    }

    public class AccessibilityLogicTests
    {
        [Test]
        public void TapShoot_TapStartsTapReleases()
        {
            var t = new TapShoot();
            Assert.IsTrue(t.Update(tapped: true, charging: false), "first tap holds");
            Assert.IsTrue(t.Update(false, true), "keeps holding while the meter runs");
            Assert.IsTrue(t.Update(false, true));
            Assert.IsFalse(t.Update(true, true), "second tap releases");
            Assert.IsFalse(t.Update(false, false));
        }

        [Test]
        public void TapShoot_GivesUpIfTheMeterNeverStarts()
        {
            var t = new TapShoot();
            t.Update(true, false);
            for (int i = 0; i < TapShoot.StartGraceFrames; i++) Assert.IsTrue(t.Update(false, false));
            Assert.IsFalse(t.Update(false, false));
        }

        [Test]
        public void TapShoot_StopsWhenTheShotEndsAnotherWay()
        {
            var t = new TapShoot();
            t.Update(true, false);
            t.Update(false, true);
            Assert.IsFalse(t.Update(false, false), "blocked or stripped: stop holding");
        }

        [Test]
        public void Settings_RoundTrip()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.settings.leftHanded = d.settings.largeButtons = d.settings.tapToShoot = d.settings.reduceMotion = true;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.IsTrue(back.settings.leftHanded && back.settings.largeButtons && back.settings.tapToShoot && back.settings.reduceMotion);
        }
    }

    public class AnimationAndSoundTests
    {
        [Test]
        public void Leap_DunkRisesHigherThanLayup_JumpShotsDontLeap()
        {
            float dunk = 0f, layup = 0f;
            for (float t = 0f; t < 0.5f; t += 0.01f)
            {
                dunk = Math.Max(dunk, Flair.Leap(ShotType.Dunk, t, 0.5f));
                layup = Math.Max(layup, Flair.Leap(ShotType.Layup, t, 0.5f));
                Assert.AreEqual(0f, Flair.Leap(ShotType.Arc, t, 0.5f));
            }
            Assert.Greater(dunk, layup);
            Assert.Greater(layup, 0.5f);
            Assert.AreEqual(0f, Flair.Leap(ShotType.Dunk, 0.5f, 0.5f));
        }

        [Test]
        public void MusicTracks_AreDistinctLoopsOfWholeBeats()
        {
            var lengths = new HashSet<int>();
            for (int i = 0; i < AudioSynth.MusicTrackCount; i++)
            {
                var loop = AudioSynth.MusicLoop(i);
                Assert.Greater(AudioSynth.Duration(loop), 15f);
                float peak = 0f;
                foreach (var v in loop) peak = Math.Max(peak, Math.Abs(v));
                Assert.AreEqual(0.8f, peak, 0.001f);
                lengths.Add(loop.Length);
            }
            Assert.AreEqual(AudioSynth.MusicTrackCount, lengths.Count, "different tempos");
            Assert.AreEqual(AudioSynth.MusicLoop(0), AudioSynth.MusicLoop(), "track 0 is the original loop");
        }

        [Test]
        public void Stingers_AreShortAndAudible()
        {
            foreach (var id in new[] { SfxId.Stinger, SfxId.OnFire, SfxId.Fanfare })
            {
                var s = AudioSynth.Sfx(id);
                Assert.IsTrue(AudioSynth.Duration(s) >= 0.1f && AudioSynth.Duration(s) <= 1.5f, id.ToString());
                float peak = 0f;
                foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
                Assert.Greater(peak, 0.3f);
            }
        }

        [Test]
        public void Fans_ArmsUpDiffersAndBobsByMood()
        {
            var a = CrowdGenerator.Fan(RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#C68A62"), false);
            var b = CrowdGenerator.Fan(RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#C68A62"), true);
            Assert.Greater(b.OpaqueCount(), a.OpaqueCount());
            Assert.AreEqual(-1, CrowdGenerator.Bob(CrowdMood.Groan, 1f, 0));
            int ups = 0;
            for (int i = 0; i < 20; i++) ups += CrowdGenerator.Bob(CrowdMood.Cheer, i * 0.0625f, 0);
            Assert.IsTrue(ups >= 8 && ups <= 12, "cheering fans bounce every other beat");
        }
    }

    public class RecordsTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void FirstGame_SetsMarksWithoutAnnouncing_LaterBestsAreAnnounced()
        {
            var d = Career.New(_c);
            Career.ApplyMatch(d, Fake.Summary(GameMode.QuickCall, true, "1", humanPoints: 8), default);
            Assert.AreEqual(0, d.lastNewRecords.Count);
            Assert.AreEqual(8, d.records.points);
            Career.ApplyMatch(d, Fake.Summary(GameMode.QuickCall, true, "2", humanPoints: 12), default);
            Assert.Contains("POINTS", d.lastNewRecords);
            Assert.AreEqual(12, d.records.points);
            Career.ApplyMatch(d, Fake.Summary(GameMode.QuickCall, true, "3", humanPoints: 5), default);
            Assert.IsFalse(d.lastNewRecords.Contains("POINTS"));
        }

        [Test]
        public void WinStreak_GrowsAndResets()
        {
            var d = Career.New(_c);
            for (int i = 0; i < 4; i++) Career.ApplyMatch(d, Fake.Summary(GameMode.QuickCall, true, "w" + i), default);
            Assert.AreEqual(4, d.records.winStreak);
            Assert.AreEqual(4, d.records.bestWinStreak);
            Career.ApplyMatch(d, Fake.Summary(GameMode.QuickCall, false, "l"), default);
            Assert.AreEqual(0, d.records.winStreak);
            Assert.AreEqual(4, d.records.bestWinStreak);
        }

        [Test]
        public void History_KeepsTheLastTwentyNewestFirst()
        {
            var d = Career.New(_c);
            for (int i = 0; i < 25; i++)
            {
                var s = Fake.Summary(GameMode.QuickCall, i % 2 == 0, "h" + i, humanPoints: i);
                s.day = 1000 + i;
                Career.ApplyMatch(d, s, default);
            }
            Assert.AreEqual(Records.HistoryLength, d.history.Count);
            Assert.AreEqual(1024, d.history[0].day);
            Assert.AreEqual(24, d.history[0].points);
        }

        [Test]
        public void PracticeAndVersus_DontTouchRecords()
        {
            var d = Career.New(_c);
            Career.ApplyMatch(d, Fake.Summary(GameMode.Practice, true, "p", humanPoints: 30), default);
            Career.ApplyMatch(d, Fake.Summary(GameMode.Versus, true, "v", humanPoints: 30), default);
            Assert.AreEqual(0, d.records.points);
            Assert.AreEqual(0, d.history.Count);
        }

        [Test]
        public void RiseSeasons_AreTrackedWithTheirResult()
        {
            var d = Career.New(_c);
            var r = d.rise;
            for (int i = 0; i < RiseEngine.CircuitOrder.Length; i++)
            {
                var s = Fake.Summary(GameMode.Rise, true, "c" + i, opponent: RiseEngine.CircuitOrder[i]);
                Career.ApplyMatch(d, s, default);
                RiseEngine.ApplyResult(r, _c, s, d);
            }
            var circuit = Records.Season(d, 0);
            Assert.AreEqual(RiseEngine.CircuitOrder.Length, circuit.wins);
            Assert.AreEqual("Circuit cleared", circuit.result);

            var g = RiseEngine.NextMatch(r, _c, "x");
            var s1 = Fake.Summary(GameMode.Rise, false, "s1", opponent: g.AwayTeamId);
            Career.ApplyMatch(d, s1, default);
            RiseEngine.ApplyResult(r, _c, s1, d);
            var season1 = Records.Season(d, 1);
            Assert.AreEqual(1, season1.games);
            Assert.AreEqual(1, season1.losses);
        }

        [Test]
        public void Everything_RoundTripsThroughTheSaveFile()
        {
            var d = Career.New(_c);
            d.customPlayer = new CustomPlayerData { created = true, skinTone = 3, hairStyle = 4, hairColor = 2, body = 0, heightTier = 2, jerseyNumber = 42, archetypeId = _c.Archetypes[5].id };
            for (int i = 0; i < 3; i++) Career.ApplyMatch(d, Fake.Summary(GameMode.Rise, true, "r" + i, humanPoints: 10 + i), default);
            Records.SetSeasonResult(d, 0, "Circuit cleared");
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.IsTrue(back.customPlayer.created);
            Assert.AreEqual(42, back.customPlayer.jerseyNumber);
            Assert.AreEqual(_c.Archetypes[5].id, back.customPlayer.archetypeId);
            Assert.AreEqual(12, back.records.points);
            Assert.AreEqual(3, back.records.bestWinStreak);
            Assert.AreEqual(3, back.history.Count);
            Assert.AreEqual(GameMode.Rise, back.history[0].mode);
            Assert.AreEqual("Circuit cleared", back.seasons[0].result);
            Assert.AreEqual(3, back.seasons[0].wins);
        }
    }
}
