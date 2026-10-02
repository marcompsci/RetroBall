using System;

namespace CallerRetroBall.Logic.PixelArt
{
    public enum CharacterView
    {
        /// <summary>Facing the camera (moving toward half-court, screen-down).</summary>
        Front = 0,
        /// <summary>Facing the hoop (screen-up).</summary>
        Back = 1,
        /// <summary>Facing screen-right; flipped horizontally for left.</summary>
        Side = 2,
    }

    /// <summary>
    /// Builds an original 16x24 pixel player sprite sheet from an <see cref="AppearanceDef"/>
    /// and team colours. Layout: one row per <see cref="CharacterView"/>, columns
    /// 0-1 = idle, 2-5 = run. Every frame gets a dark 1px outline for court readability.
    /// </summary>
    public static class CharacterSpriteGenerator
    {
        public const int FrameWidth = 16;
        public const int FrameHeight = 24;
        public const int IdleFrames = 2;
        public const int RunFrames = 4;
        public const int ShootFrames = 1;
        public const int FramesPerView = IdleFrames + RunFrames + ShootFrames;
        /// <summary>Arms-up jump-shot pose.</summary>
        public const int ShootFrame = IdleFrames + RunFrames;
        public const int ViewCount = 3;

        public static readonly RgbColor Outline = new RgbColor(0x14, 0x14, 0x20);

        public static readonly RgbColor[] SkinTones =
        {
            RgbColor.FromHex("#F5D0B5"), RgbColor.FromHex("#E0AC89"), RgbColor.FromHex("#C68A62"),
            RgbColor.FromHex("#A0674A"), RgbColor.FromHex("#7A4A33"), RgbColor.FromHex("#4E2E1F"),
        };

        public static readonly RgbColor[] HairColors =
        {
            RgbColor.FromHex("#1B1B1B"), RgbColor.FromHex("#4A2E1A"), RgbColor.FromHex("#8B5A2B"),
            RgbColor.FromHex("#D9B45A"), RgbColor.FromHex("#B23A2E"),
        };

        public const int HairStyleCount = 6;

        /// <summary>Bottom-left pixel of a frame within the sheet.</summary>
        public static void FrameOrigin(CharacterView view, int frame, out int x, out int y)
        {
            x = frame * FrameWidth;
            y = (int)view * FrameHeight;
        }

        /// <summary>Maps an eight-way court facing to a sprite view and horizontal flip.</summary>
        public static CharacterView ViewFor(Facing8 facing, out bool flipX)
        {
            switch (facing)
            {
                case Facing8.N: flipX = false; return CharacterView.Front;
                case Facing8.NE: flipX = false; return CharacterView.Front;
                case Facing8.NW: flipX = true; return CharacterView.Front;
                case Facing8.E: flipX = false; return CharacterView.Side;
                case Facing8.W: flipX = true; return CharacterView.Side;
                case Facing8.SE: flipX = false; return CharacterView.Back;
                case Facing8.SW: flipX = true; return CharacterView.Back;
                default: flipX = false; return CharacterView.Back; // S
            }
        }

        public static PixelCanvas GenerateSheet(AppearanceDef look, RgbColor jersey, RgbColor trim, RgbColor accent)
            => GenerateSheet(look, jersey, trim, accent, null, TeamPattern.Solid);

        /// <param name="shoes">Shoe colour (cosmetic); null = classic white.</param>
        /// <param name="pattern">Jersey pattern drawn in colourblind contrast mode (Solid = none).</param>
        public static PixelCanvas GenerateSheet(AppearanceDef look, RgbColor jersey, RgbColor trim, RgbColor accent,
                                                RgbColor? shoes, TeamPattern pattern) => GenerateSheet(look, jersey, trim, accent, shoes, pattern, null);

        /// <param name="shorts">Shorts colour (custom kits); null = a shade darker than the jersey.</param>
        public static PixelCanvas GenerateSheet(AppearanceDef look, RgbColor jersey, RgbColor trim, RgbColor accent,
                                                RgbColor? shoes, TeamPattern pattern, RgbColor? shorts)
        {
            var sheet = new PixelCanvas(FrameWidth * FramesPerView, FrameHeight * ViewCount);
            var p = new Palette(look, jersey, trim, accent);
            if (shoes.HasValue) p.Shoe = shoes.Value;
            if (shorts.HasValue) p.Shorts = shorts.Value;
            p.Pattern = pattern;
            for (int v = 0; v < ViewCount; v++)
                for (int f = 0; f < FramesPerView; f++)
                {
                    var frame = new PixelCanvas(FrameWidth, FrameHeight);
                    DrawFrame(frame, (CharacterView)v, f, look, p);
                    AddOutline(frame);
                    FrameOrigin((CharacterView)v, f, out int ox, out int oy);
                    for (int y = 0; y < FrameHeight; y++)
                        for (int x = 0; x < FrameWidth; x++)
                            sheet.Set(ox + x, oy + y, frame.Get(x, y));
                }
            return sheet;
        }

        private struct Palette
        {
            public RgbColor Skin, SkinShade, Hair, Jersey, JerseyShade, Shorts, Trim, Accent, Shoe, Sole, Eye;
            public TeamPattern Pattern;

            public Palette(AppearanceDef look, RgbColor jersey, RgbColor trim, RgbColor accent)
            {
                Skin = SkinTones[Mod(look.skinTone, SkinTones.Length)];
                SkinShade = Skin.Darken(0.18f);
                Hair = HairColors[Mod(look.hairColor, HairColors.Length)];
                Jersey = jersey;
                JerseyShade = jersey.Darken(0.2f);
                Shorts = jersey.Darken(0.12f);
                Trim = trim;
                Accent = accent;
                Shoe = new RgbColor(0xF2, 0xF2, 0xF2);
                Sole = new RgbColor(0x9A, 0x9A, 0xA4);
                Eye = new RgbColor(0x1A, 0x1A, 0x1A);
                Pattern = TeamPattern.Solid;
            }
        }

        private static int Mod(int v, int n) => ((v % n) + n) % n;

        /// <summary>
        /// Row (from the frame bottom) where the head starts in a frame: used by the BIG HEADS
        /// secret, which redraws everything from this row up at a larger scale.
        /// Mirrors the layout in <see cref="DrawFrame"/>.
        /// </summary>
        public static int HeadBottomRow(AppearanceDef look, int frame)
        {
            bool shooting = frame == ShootFrame;
            bool running = frame >= IdleFrames && !shooting;
            int runPhase = running ? frame - IdleFrames : 0;
            int bob = shooting ? 1 : (running ? (runPhase % 2 == 1 ? 1 : 0) : (frame == 1 ? -1 : 0));
            int legLen = 6 + Clamp(look.heightTier, 0, 2) - 1;
            return 1 + legLen + 2 + bob + 5 + 1 + 1;
        }

        private static void DrawFrame(PixelCanvas c, CharacterView view, int frame, AppearanceDef look, Palette p)
        {
            bool shooting = frame == ShootFrame;
            bool running = frame >= IdleFrames && !shooting;
            int runPhase = running ? frame - IdleFrames : 0;
            // Body bob: idle breathes on frame 1; running lifts on the passing frames 1 and 3.
            int bob = shooting ? 1 : (running ? (runPhase % 2 == 1 ? 1 : 0) : (frame == 1 ? -1 : 0));

            int legLen = 6 + Clamp(look.heightTier, 0, 2) - 1;          // 5..7
            int torsoW = look.body == BodyType.Slim ? 6 : (look.body == BodyType.Broad ? 8 : 7);
            if (view == CharacterView.Side) torsoW = Math.Max(4, torsoW - 2);
            int cx = FrameWidth / 2;
            int torsoLeft = cx - torsoW / 2;

            int legTop = 1 + legLen;             // top row of legs
            int shortsBottom = legTop - 2;
            int torsoBottom = legTop + 2 + bob;  // shorts cover 4 rows: shortsBottom..torsoBottom-1
            int torsoTop = torsoBottom + 5;      // 6 rows of jersey
            int neck = torsoTop + 1;
            int headBottom = neck + 1;
            int headTop = headBottom + 4;        // 5 rows

            // ---- legs & shoes
            if (view == CharacterView.Side)
            {
                int stride = !running ? 0 : (runPhase == 0 ? 2 : (runPhase == 2 ? -2 : 0));
                DrawLeg(c, cx - 1 + stride, 0, legTop, p, liftRows: 0);
                DrawLeg(c, cx - 1 - stride, 0, legTop, p, liftRows: 0, shade: true);
            }
            else
            {
                int liftL = running && runPhase == 0 ? 2 : 0;
                int liftR = running && runPhase == 2 ? 2 : 0;
                int hipHalf = Math.Max(2, torsoW / 2 - 1);
                DrawLeg(c, cx - hipHalf - 1, 0, legTop, p, liftL);
                DrawLeg(c, cx + hipHalf - 1, 0, legTop, p, liftR);
            }

            // ---- shorts
            Rect(c, torsoLeft, shortsBottom, torsoW, torsoBottom - shortsBottom, p.Shorts);
            Rect(c, torsoLeft, shortsBottom, 1, torsoBottom - shortsBottom, p.Trim);
            Rect(c, torsoLeft + torsoW - 1, shortsBottom, 1, torsoBottom - shortsBottom, p.Trim);
            if (view != CharacterView.Side) Rect(c, cx, shortsBottom, 1, 2, p.JerseyShade); // leg split

            // ---- jersey
            Rect(c, torsoLeft, torsoBottom, torsoW, torsoTop - torsoBottom + 1, p.Jersey);
            Rect(c, torsoLeft, torsoBottom, torsoW, 1, p.JerseyShade);                   // waist shade
            if (p.Pattern != TeamPattern.Solid)
            {
                // Colourblind contrast: a distinct pattern per team, drawn in the trim colour.
                var ink = RgbColor.Distance(p.Trim, p.Jersey) > 90 ? p.Trim : (p.Jersey.Luminance > 0.4 ? RgbColor.Black : RgbColor.White);
                for (int y = torsoBottom + 1; y <= torsoTop; y++)
                    for (int x = torsoLeft; x < torsoLeft + torsoW; x++)
                        if (PatternHit(p.Pattern, x - torsoLeft, y - torsoBottom)) c.Set(x, y, ink);
            }
            if (view == CharacterView.Front)
            {
                c.Set(cx - 1, torsoTop, p.Trim);                                              // neckline V
                c.Set(cx, torsoTop, p.Trim);
                c.Set(cx, torsoTop - 1, p.Trim);
                Rect(c, cx - 1, torsoBottom + 2, 2, 2, p.Accent);                          // chest mark
            }
            else if (view == CharacterView.Back)
            {
                Rect(c, cx - 1, torsoBottom + 1, 3, 3, p.Trim);                            // number block
            }
            else
            {
                Rect(c, torsoLeft + torsoW - 1, torsoBottom + 1, 1, torsoTop - torsoBottom, p.Trim); // side seam
            }

            // ---- arms (sleeveless jerseys: skin), swing opposite to legs
            int armSwing = !running ? 0 : (runPhase == 0 ? 1 : (runPhase == 2 ? -1 : 0));
            int armTop = torsoTop;
            if (shooting)
            {
                // Arms are drawn after the head (raised above it).
            }
            else if (view == CharacterView.Side)
            {
                int armX = cx + (armSwing > 0 ? 0 : -1);
                Rect(c, armX, armTop - 4 + Math.Abs(armSwing), 2, 5 - Math.Abs(armSwing), p.Skin);
                c.Set(armX + (armSwing >= 0 ? 1 : 0), armTop - 4 + Math.Abs(armSwing), p.SkinShade);
            }
            else
            {
                Rect(c, torsoLeft - 2, armTop - 4 - armSwing, 2, 5, p.Skin);
                Rect(c, torsoLeft + torsoW, armTop - 4 + armSwing, 2, 5, p.Skin);
                c.Set(torsoLeft - 2, armTop - 4 - armSwing, p.SkinShade);                     // hands
                c.Set(torsoLeft + torsoW + 1, armTop - 4 + armSwing, p.SkinShade);
            }

            // ---- neck & head
            Rect(c, cx - 1, neck, 2, 1, p.SkinShade);
            int headW = view == CharacterView.Side ? 5 : 6;
            int headLeft = cx - 3 + (view == CharacterView.Side ? 1 : 0);
            Rect(c, headLeft, headBottom, headW, headTop - headBottom + 1, p.Skin);
            if (view == CharacterView.Front)
            {
                c.Set(cx - 2, headBottom + 2, p.Eye);
                c.Set(cx + 1, headBottom + 2, p.Eye);
                c.Set(cx - 1, headBottom, p.SkinShade); // chin shade
                c.Set(cx, headBottom, p.SkinShade);
            }
            else if (view == CharacterView.Side)
            {
                c.Set(headLeft + headW - 2, headBottom + 2, p.Eye);
                c.Set(headLeft + headW, headBottom + 1, p.Skin); // nose
            }

            DrawHair(c, view, Mod(look.hairStyle, HairStyleCount), headLeft, headW, headBottom, headTop, p);

            if (shooting)
            {
                int top = Math.Min(FrameHeight - 1, headTop + 2);
                if (view == CharacterView.Side)
                {
                    Rect(c, cx + 1, armTop - 1, 2, top - armTop + 2, p.Skin);
                    c.Set(cx + 2, top, p.SkinShade);
                }
                else
                {
                    Rect(c, torsoLeft - 1, armTop - 1, 2, top - armTop + 2, p.Skin);
                    Rect(c, torsoLeft + torsoW - 1, armTop - 1, 2, top - armTop + 2, p.Skin);
                    c.Set(torsoLeft - 1, top, p.SkinShade);
                    c.Set(torsoLeft + torsoW, top, p.SkinShade);
                }
            }
        }

        /// <summary>Whether a jersey pixel (local coords) is inked for a pattern.</summary>
        public static bool PatternHit(TeamPattern pattern, int x, int y)
        {
            switch (pattern)
            {
                case TeamPattern.Stripes: return x % 2 == 0;
                case TeamPattern.Dots: return x % 2 == 1 && y % 2 == 0;
                case TeamPattern.Chevrons: return (x + (y % 4 < 2 ? y % 4 : 4 - y % 4)) % 4 == 0;
                case TeamPattern.Checker: return (x / 2 + y / 2) % 2 == 0;
                case TeamPattern.Diagonal: return (x + y) % 3 == 0;
                case TeamPattern.Rings: return y % 3 == 0;
                case TeamPattern.Cross: return x == 2 || y == 3;
                default: return false;
            }
        }

        private static void DrawLeg(PixelCanvas c, int x, int floor, int legTop, Palette p, int liftRows, bool shade = false)
        {
            int shoeBottom = floor + liftRows;
            var skin = shade ? p.SkinShade : p.Skin;
            Rect(c, x, shoeBottom + 2, 2, legTop - (shoeBottom + 2) + 1, skin);
            Rect(c, x, shoeBottom, 2, 2, p.Shoe);
            Rect(c, x, shoeBottom, 2, 1, p.Sole);
            if (!shade) c.Set(x + 2, shoeBottom, p.Shoe); // toe
        }

        private static void DrawHair(PixelCanvas c, CharacterView view, int style, int left, int w, int bottom, int top, Palette p)
        {
            if (view == CharacterView.Back)
            {
                // From behind, hair covers the head except the nape.
                Rect(c, left, bottom + 1, w, top - bottom, p.Hair);
            }

            switch (style)
            {
                case 0: // buzz
                    Rect(c, left, top, w, 1, p.Hair);
                    break;
                case 1: // short
                    Rect(c, left, top - 1, w, 2, p.Hair);
                    Rect(c, left, top - 2, 1, 1, p.Hair);
                    Rect(c, left + w - 1, top - 2, 1, 1, p.Hair);
                    break;
                case 2: // high-top
                    Rect(c, left, top, w, 1, p.Hair);
                    Rect(c, left + 1, top + 1, w - 2, 2, p.Hair);
                    break;
                case 3: // puff
                    Rect(c, left - 1, top - 1, w + 2, 2, p.Hair);
                    Rect(c, left, top + 1, w, 1, p.Hair);
                    break;
                case 4: // headband
                    Rect(c, left, top, w, 1, p.Hair);
                    Rect(c, left, top - 1, w, 1, p.Accent);
                    break;
                default: // locs
                    Rect(c, left, top - 1, w, 2, p.Hair);
                    Rect(c, left - 1, bottom, 1, top - bottom, p.Hair);
                    Rect(c, left + w, bottom, 1, top - bottom, p.Hair);
                    break;
            }
        }

        private static void Rect(PixelCanvas c, int x, int y, int w, int h, RgbColor col)
        {
            if (w <= 0 || h <= 0) return;
            c.FillRect(x, y, w, h, col);
        }

        /// <summary>Adds a 1px dark outline around every opaque region (4-neighbourhood).</summary>
        public static void AddOutline(PixelCanvas c)
        {
            var mask = new bool[c.Width * c.Height];
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    mask[y * c.Width + x] = c.Get(x, y).a > 0;

            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    if (mask[y * c.Width + x]) continue;
                    bool edge = (x > 0 && mask[y * c.Width + x - 1]) || (x < c.Width - 1 && mask[y * c.Width + x + 1])
                                || (y > 0 && mask[(y - 1) * c.Width + x]) || (y < c.Height - 1 && mask[(y + 1) * c.Width + x]);
                    if (edge) c.Set(x, y, Outline);
                }
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }

    /// <summary>Small original props: ball, drop shadow, hoop, and the controlled-player ring.</summary>
    public static class PropSpriteGenerator
    {
        public static PixelCanvas Ball()
        {
            var c = new PixelCanvas(8, 8);
            c.Stamp(new[]
            {
                "..####..",
                ".#+#+##.",
                "#++#+###",
                "########",
                "###+####",
                "###+####",
                ".##+###.",
                "..####..",
            }, 0, 7, RgbColor.FromHex("#E8742A"), RgbColor.FromHex("#3A1A0A"));
            c.Set(2, 6, RgbColor.FromHex("#FFB070"));
            c.Set(1, 5, RgbColor.FromHex("#FFB070"));
            return c;
        }

        /// <summary>Soft ellipse shadow (black, partial alpha) for players and the ball.</summary>
        public static PixelCanvas Shadow(int width = 12, int height = 4)
        {
            var c = new PixelCanvas(width, height);
            float rx = width / 2f, ry = height / 2f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                    if (dx * dx + dy * dy <= 1f) c.Set(x, y, new RgbColor(0, 0, 0, 90));
                }
            return c;
        }

        /// <summary>White ellipse ring, tinted at runtime (controlled-player marker).</summary>
        public static PixelCanvas Ring(int width = 16, int height = 6)
        {
            var c = new PixelCanvas(width, height);
            float rx = width / 2f, ry = height / 2f;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                    float d = dx * dx + dy * dy;
                    if (d <= 1f && d >= 0.45f) c.Set(x, y, RgbColor.White);
                }
            return c;
        }

        /// <summary>Backboard, rim, and net seen from the 3/4 court camera. Pivot: rim centre = (16, 7).</summary>
        public static PixelCanvas Hoop()
        {
            const int w = 32, h = 30;
            var c = new PixelCanvas(w, h);
            var board = RgbColor.FromHex("#F4F4F8");
            var boardEdge = RgbColor.FromHex("#9AA0B4");
            var rim = RgbColor.FromHex("#E4572E");
            var net = RgbColor.FromHex("#FFFFFF").WithAlpha(210);

            // Board (upper part of the sprite) with a target square.
            c.FillRect(1, 14, 30, 14, board);
            for (int x = 1; x < 31; x++) { c.Set(x, 14, boardEdge); c.Set(x, 27, boardEdge); }
            for (int y = 14; y < 28; y++) { c.Set(1, y, boardEdge); c.Set(30, y, boardEdge); }
            for (int x = 11; x <= 20; x++) { c.Set(x, 15, rim); c.Set(x, 22, rim); }
            for (int y = 15; y <= 22; y++) { c.Set(11, y, rim); c.Set(20, y, rim); }

            // Net: tapered strands below the rim.
            for (int row = 0; row < 6; row++)
            {
                int half = 5 - row / 2;
                for (int x = 16 - half; x <= 15 + half; x += 2) c.Set(x, 6 - row, net);
                c.Set(16 - half, 6 - row, net);
                c.Set(15 + half, 6 - row, net);
            }

            // Rim ellipse (12 x 5) centred on (16, 7).
            for (int x = 12; x <= 19; x++) { c.Set(x, 9, rim); c.Set(x, 5, rim); }
            foreach (int x in new[] { 10, 11, 20, 21 }) { c.Set(x, 8, rim); c.Set(x, 6, rim); }
            c.Set(10, 7, rim);
            c.Set(21, 7, rim);
            CharacterSpriteGenerator.AddOutline(c);
            return c;
        }

        /// <summary>Vertical shot-meter frame: 7 x 28 px with a 2px dark border (inner area 3 x 24).</summary>
        public static PixelCanvas MeterFrame()
        {
            var c = new PixelCanvas(MeterWidth, MeterHeight);
            c.Fill(new RgbColor(0x14, 0x14, 0x20));
            c.FillRect(1, 1, MeterWidth - 2, MeterHeight - 2, new RgbColor(0xF4, 0xF1, 0xDE));
            c.FillRect(2, 2, MeterWidth - 4, MeterHeight - 4, new RgbColor(0x2A, 0x2A, 0x45));
            return c;
        }

        public const int MeterWidth = 7;
        public const int MeterHeight = 28;
        /// <summary>Inner fillable area, bottom-left at (2, 2).</summary>
        public const int MeterInnerHeight = MeterHeight - 4;

        /// <summary>Small down-pointing arrow (white, dark outline) marking the pass receiver.</summary>
        public static PixelCanvas Arrow()
        {
            var c = new PixelCanvas(9, 7);
            c.Stamp(new[]
            {
                ".........",
                ".#######.",
                "..#####..",
                "...###...",
                "....#....",
                ".........",
                ".........",
            }, 0, 6, RgbColor.White, RgbColor.White);
            CharacterSpriteGenerator.AddOutline(c);
            return c;
        }

        /// <summary>1x1 white pixel, scaled and tinted at runtime (meter fill, bars).</summary>
        public static PixelCanvas WhitePixel()
        {
            var c = new PixelCanvas(1, 1);
            c.Set(0, 0, RgbColor.White);
            return c;
        }

        public const int HoopPivotX = 16;
        public const int HoopPivotY = 7;
    }
}
