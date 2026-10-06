using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 34: shot zones, hot/cold spot tracking, and archetype shot tendencies.</summary>
    public class Phase34ShotZoneTests
    {
        private static readonly CourtGeometry Court = CourtGeometry.Default;

        // ---------------------------------------------------------------- SpotOf: zone classification

        [Test]
        public void SpotOf_UnderTheRim_IsPaint()
        {
            Assert.AreEqual(ShotSpot.Paint, ShotZones.SpotOf(Court.Hoop, Court));
        }

        [Test]
        public void SpotOf_NearBaseline_IsPaint()
        {
            // 1.5 m out from the hoop — within PaintDistance
            Assert.AreEqual(ShotSpot.Paint, ShotZones.SpotOf(new Vec2(0f, Court.hoopY + 1.5f), Court));
        }

        [Test]
        public void SpotOf_CentreMidRange_IsFoulLine()
        {
            var p = new Vec2(0f, Court.hoopY + 4f);
            Assert.AreEqual(ShotZone.InsideArc, Court.ZoneOf(p), "precondition: inside arc");
            Assert.AreEqual(ShotSpot.FoulLine, ShotZones.SpotOf(p, Court));
        }

        [Test]
        public void SpotOf_LeftMidRange_IsElbowLeft()
        {
            var p = new Vec2(-3f, Court.hoopY + 3f);
            Assert.AreEqual(ShotZone.InsideArc, Court.ZoneOf(p), "precondition: inside arc");
            Assert.AreEqual(ShotSpot.ElbowLeft, ShotZones.SpotOf(p, Court));
        }

        [Test]
        public void SpotOf_RightMidRange_IsElbowRight()
        {
            var p = new Vec2(3f, Court.hoopY + 3f);
            Assert.AreEqual(ShotZone.InsideArc, Court.ZoneOf(p), "precondition: inside arc");
            Assert.AreEqual(ShotSpot.ElbowRight, ShotZones.SpotOf(p, Court));
        }

        [Test]
        public void SpotOf_LeftCorner_IsCornerLeft()
        {
            // Straight corner-line zone: y below CornerLineTopY, x near sideline
            var p = new Vec2(-6.8f, Court.hoopY + 0.5f);
            Assert.AreEqual(ShotZone.BeyondArc, Court.ZoneOf(p), "precondition: beyond arc");
            Assert.IsTrue(p.y <= Court.CornerLineTopY, "precondition: in corner band");
            Assert.AreEqual(ShotSpot.CornerLeft, ShotZones.SpotOf(p, Court));
        }

        [Test]
        public void SpotOf_RightCorner_IsCornerRight()
        {
            var p = new Vec2(6.8f, Court.hoopY + 0.5f);
            Assert.AreEqual(ShotZone.BeyondArc, Court.ZoneOf(p), "precondition: beyond arc");
            Assert.AreEqual(ShotSpot.CornerRight, ShotZones.SpotOf(p, Court));
        }

        [Test]
        public void SpotOf_LeftWing_IsWingLeft()
        {
            var p = new Vec2(-4.5f, Court.hoopY + 6.5f);
            Assert.AreEqual(ShotZone.BeyondArc, Court.ZoneOf(p), "precondition: beyond arc");
            Assert.IsTrue(p.y > Court.CornerLineTopY, "precondition: above corner line");
            Assert.AreEqual(ShotSpot.WingLeft, ShotZones.SpotOf(p, Court));
        }

        [Test]
        public void SpotOf_RightWing_IsWingRight()
        {
            var p = new Vec2(4.5f, Court.hoopY + 6.5f);
            Assert.AreEqual(ShotSpot.WingRight, ShotZones.SpotOf(p, Court));
        }

        [Test]
        public void SpotOf_TopOfKey_IsTopOfKey()
        {
            // Just beyond the arc, centred
            var p = Court.ArcTop + new Vec2(0f, 0.3f);
            Assert.AreEqual(ShotZone.BeyondArc, Court.ZoneOf(p), "precondition: beyond arc");
            Assert.AreEqual(ShotSpot.TopOfKey, ShotZones.SpotOf(p, Court));
        }

        // ---------------------------------------------------------------- Record and Percentage

        [Test]
        public void Record_IncreasesAttemptedAndMade()
        {
            var d = new ShotChartData();
            ShotZones.Record(d, ShotSpot.Paint, made: true);
            ShotZones.Record(d, ShotSpot.Paint, made: false);
            Assert.AreEqual(2, d[ShotSpot.Paint].attempted);
            Assert.AreEqual(1, d[ShotSpot.Paint].made);
        }

        [Test]
        public void Percentage_IsAccurate()
        {
            var d = new ShotChartData();
            for (int i = 0; i < 4; i++) ShotZones.Record(d, ShotSpot.TopOfKey, i < 3); // 3/4
            Assert.AreEqual(0.75f, d[ShotSpot.TopOfKey].Percentage, 0.001f);
        }

        [Test]
        public void Percentage_IsZero_WhenNoAttempts()
        {
            Assert.AreEqual(0f, new ShotSpotRecord().Percentage);
        }

        // ---------------------------------------------------------------- HotSpot / ColdSpot

        [Test]
        public void HotSpot_ReturnsBestPercentageSpot()
        {
            var d = new ShotChartData();
            for (int i = 0; i < 3; i++) ShotZones.Record(d, ShotSpot.Paint, i < 2);     // 2/3 ≈ 67 %
            for (int i = 0; i < 3; i++) ShotZones.Record(d, ShotSpot.WingRight, true);  // 3/3 = 100 %
            Assert.AreEqual(ShotSpot.WingRight, ShotZones.HotSpot(d));
        }

        [Test]
        public void ColdSpot_ReturnsWorstPercentageSpot()
        {
            var d = new ShotChartData();
            for (int i = 0; i < 3; i++) ShotZones.Record(d, ShotSpot.ElbowLeft, false); // 0 %
            for (int i = 0; i < 3; i++) ShotZones.Record(d, ShotSpot.Paint, true);      // 100 %
            Assert.AreEqual(ShotSpot.ElbowLeft, ShotZones.ColdSpot(d));
        }

        [Test]
        public void HotSpot_ReturnsNull_WhenNoSpotMeetsMinAttempts()
        {
            var d = new ShotChartData();
            // Only 2 attempts — below MinAttempts of 3
            ShotZones.Record(d, ShotSpot.FoulLine, true);
            ShotZones.Record(d, ShotSpot.FoulLine, true);
            Assert.IsNull(ShotZones.HotSpot(d));
        }

        [Test]
        public void ColdSpot_ReturnsNull_OnEmptyChart()
        {
            Assert.IsNull(ShotZones.ColdSpot(new ShotChartData()));
        }

        // ---------------------------------------------------------------- Merge

        [Test]
        public void Merge_CombinesMadeAndAttempted()
        {
            var a = new ShotChartData();
            var b = new ShotChartData();
            for (int i = 0; i < 4; i++) ShotZones.Record(a, ShotSpot.Paint, i < 3); // 3/4
            for (int i = 0; i < 2; i++) ShotZones.Record(b, ShotSpot.Paint, true);  // 2/2
            ShotZones.Merge(a, b);
            Assert.AreEqual(6, a[ShotSpot.Paint].attempted);
            Assert.AreEqual(5, a[ShotSpot.Paint].made);
        }

        [Test]
        public void Merge_NullSource_IsNoOp()
        {
            var a = new ShotChartData();
            ShotZones.Record(a, ShotSpot.Paint, true);
            Assert.DoesNotThrow(() => ShotZones.Merge(a, null));
            Assert.AreEqual(1, a[ShotSpot.Paint].attempted);
        }

        // ---------------------------------------------------------------- PreferredSpots

        [Test]
        public void PreferredSpots_AreNonEmptyForEveryArchetype()
        {
            foreach (Archetype arch in Enum.GetValues(typeof(Archetype)))
                Assert.IsNotEmpty(ShotZones.PreferredSpots(arch), arch + " has preferred spots");
        }

        [Test]
        public void PreferredSpots_DeepShooter_FavoursBeyondArc()
        {
            var spots = ShotZones.PreferredSpots(Archetype.DeepShooter);
            bool anyArc = Array.Exists(spots, s =>
                s == ShotSpot.TopOfKey || s == ShotSpot.WingLeft || s == ShotSpot.WingRight
                || s == ShotSpot.CornerLeft || s == ShotSpot.CornerRight);
            Assert.IsTrue(anyArc, "DeepShooter should prefer arc spots");
        }

        [Test]
        public void PreferredSpots_RimRunner_FavoursPaint()
        {
            Assert.Contains(ShotSpot.Paint, ShotZones.PreferredSpots(Archetype.RimRunner));
        }

        [Test]
        public void PreferredSpots_StretchForward_ContainsOnlyArcSpots()
        {
            var spots = ShotZones.PreferredSpots(Archetype.StretchForward);
            foreach (var s in spots)
            {
                bool isArc = s == ShotSpot.CornerLeft || s == ShotSpot.CornerRight
                          || s == ShotSpot.WingLeft   || s == ShotSpot.WingRight
                          || s == ShotSpot.TopOfKey;
                Assert.IsTrue(isArc, "StretchForward spot " + s + " should be beyond the arc");
            }
        }

        // ---------------------------------------------------------------- SpotName

        [Test]
        public void SpotName_CoversAllNineSpots()
        {
            foreach (ShotSpot spot in Enum.GetValues(typeof(ShotSpot)))
            {
                string name = ShotZones.SpotName(spot);
                Assert.IsNotNull(name);
                Assert.IsNotEmpty(name);
                Assert.AreNotEqual("UNKNOWN", name, spot + " should have a real name");
            }
        }
    }
}
