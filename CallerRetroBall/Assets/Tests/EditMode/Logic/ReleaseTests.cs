using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 8: the generated app icon and launch image meet App Store rules.</summary>
    public class ReleaseArtTests
    {
        private static int NonOpaque(PixelCanvas c)
        {
            int n = 0;
            foreach (var p in c.Pixels) if (p.a < 255) n++;
            return n;
        }

        [Test]
        public void StoreIcon_Is1024Square()
        {
            var icon = AppIconGenerator.StoreIcon();
            Assert.AreEqual(1024, icon.Width);
            Assert.AreEqual(1024, icon.Height);
        }

        [Test]
        public void StoreIcon_HasNoTransparency()
        {
            Assert.AreEqual(0, NonOpaque(AppIconGenerator.StoreIcon()), "App Store icons must be fully opaque");
        }

        [Test]
        public void Icon_IsDeterministic()
        {
            var a = AppIconGenerator.Icon();
            var b = AppIconGenerator.Icon();
            Assert.AreEqual(a.Pixels, b.Pixels);
        }

        [Test]
        public void Icon_UsesBallSeamAndGoldColours()
        {
            var colours = new HashSet<RgbColor>(AppIconGenerator.Icon().Pixels);
            Assert.IsTrue(colours.Contains(AppIconGenerator.BallMain), "ball");
            Assert.IsTrue(colours.Contains(AppIconGenerator.Seam), "seams");
            Assert.IsTrue(colours.Contains(AppIconGenerator.Gold), "signal arcs");
            Assert.GreaterOrEqual(colours.Count, 8, "should read as art, not a flat block");
        }

        [Test]
        public void Scale_IsNearestNeighbour()
        {
            var src = new PixelCanvas(2, 1);
            src.Set(0, 0, RgbColor.White);
            src.Set(1, 0, RgbColor.Black);
            var big = AppIconGenerator.Scale(src, 3);
            Assert.AreEqual(6, big.Width);
            Assert.AreEqual(3, big.Height);
            Assert.AreEqual(RgbColor.White, big.Get(2, 2));
            Assert.AreEqual(RgbColor.Black, big.Get(3, 0));
        }

        [Test]
        public void LaunchImage_IsPortraitOpaqueAndMatchesMenuBackdrop()
        {
            var court = DefaultContent.Create().Court("court.sunset_cage");
            var launch = AppIconGenerator.LaunchImage(court, 7);
            Assert.Greater(launch.Height, launch.Width);
            Assert.AreEqual(0, NonOpaque(launch));
            // Top-left corner is untouched backdrop (same court and seed as the main menu).
            var backdrop = BackdropGenerator.Generate(court, 7);
            int top = launch.Height - 1;
            Assert.AreEqual(backdrop.Get(0, top).WithAlpha(255), launch.Get(0, top));
        }
    }
}
