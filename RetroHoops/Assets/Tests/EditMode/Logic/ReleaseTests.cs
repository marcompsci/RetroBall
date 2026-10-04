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

namespace CallerRetroBall.Tests
{
    /// <summary>Console-style button skin.</summary>
    public class UiSkinTests
    {
        private static readonly CallerRetroBall.Logic.RgbColor Red = CallerRetroBall.Logic.RgbColor.FromHex("#D82800");

        [Test]
        public void Button_HasOutlineCutCornersAndGlint()
        {
            var b = UiSkinGenerator.Button(Red);
            Assert.AreEqual(UiSkinGenerator.ButtonSize, b.Width);
            Assert.AreEqual(0, b.Get(0, 0).a, "cut corner");
            Assert.AreEqual(0, b.Get(b.Width - 1, b.Height - 1).a, "cut corner");
            Assert.AreEqual(UiSkinGenerator.Outline, b.Get(b.Width / 2, 0), "outline bottom");
            Assert.AreEqual(UiSkinGenerator.Outline, b.Get(0, b.Height / 2), "outline left");
            Assert.AreEqual(UiSkinGenerator.Glint, b.Get(2, b.Height - 3), "glint");
        }

        [Test]
        public void Button_ShineIsLighterThanFace_LipIsDarker()
        {
            var b = UiSkinGenerator.Button(Red);
            int mid = b.Width / 2;
            var shine = b.Get(mid, b.Height - 3);
            var face = b.Get(mid, b.Height / 2);
            var lip = b.Get(mid, 1);
            Assert.Greater(shine.r + shine.g + shine.b, face.r + face.g + face.b);
            Assert.Less(lip.r + lip.g + lip.b, face.r + face.g + face.b);
        }

        [Test]
        public void PressedButton_HasNoLip()
        {
            var up = UiSkinGenerator.Button(Red);
            var down = UiSkinGenerator.Button(Red, pressed: true);
            Assert.AreNotEqual(up.Get(8, 1), down.Get(8, 1));
        }

        [Test]
        public void Disc_IsRoundWithGlint()
        {
            var d = UiSkinGenerator.Disc(Red);
            Assert.AreEqual(0, d.Get(0, 0).a);
            Assert.AreEqual(0, d.Get(d.Width - 1, d.Height - 1).a);
            bool glint = false;
            foreach (var p in d.Pixels) if (p.Equals(UiSkinGenerator.Glint)) glint = true;
            Assert.IsTrue(glint);
        }
    }
}

namespace CallerRetroBall.Tests
{
    public class TitleLogoTests
    {
        [Test]
        public void Logo_IsWideDeterministicAndUsesBothWordColours()
        {
            var a = TitleLogoGenerator.Generate();
            var b = TitleLogoGenerator.Generate();
            Assert.AreEqual(a.Pixels, b.Pixels);
            Assert.Greater(a.Width, a.Height * 3, "one-line wordmark");
            var colours = new System.Collections.Generic.HashSet<CallerRetroBall.Logic.RgbColor>(a.Pixels);
            Assert.IsTrue(colours.Contains(TitleLogoGenerator.Outline));
            Assert.IsTrue(colours.Contains(TitleLogoGenerator.ShadowColor));
            Assert.IsTrue(colours.Contains(TitleLogoGenerator.StripeCyan));
            Assert.IsTrue(colours.Contains(TitleLogoGenerator.OrangeTop) || colours.Contains(TitleLogoGenerator.OrangeBottom), "BALL colour");
        }

        [Test]
        public void Logo_HasTransparentBackground()
        {
            var l = TitleLogoGenerator.Generate();
            Assert.AreEqual(0, l.Get(0, l.Height - 1).a);
            Assert.AreEqual(0, l.Get(l.Width - 1, l.Height - 1).a);
        }
    }
}
