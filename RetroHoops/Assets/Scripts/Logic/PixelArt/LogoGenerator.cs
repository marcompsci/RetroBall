using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Builds original geometric team logos: an outlined badge shape in the team's
    /// primary colour with a 12x12 abstract motif in the secondary colour.
    /// All motif bitmaps below are original to this project.
    /// </summary>
    public static class LogoGenerator
    {
        public const int Size = 32;
        private const int MotifSize = 12;
        private const int MotifScale = 2;

        public static PixelCanvas Generate(TeamDef team)
        {
            if (team == null) throw new ArgumentNullException(nameof(team));
            return Generate(team.logoShape, team.logoMotif, team.primary, team.secondary, team.accent);
        }

        public static PixelCanvas Generate(LogoShape shape, LogoMotif motif, RgbColor primary, RgbColor secondary, RgbColor accent)
        {
            var canvas = new PixelCanvas(Size, Size);
            var outline = accent;
            // If accent and primary are too close, fall back to a dark outline for readability.
            if (RgbColor.Distance(outline, primary) < 60) outline = primary.Darken(0.6f);

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // Pixel centre in -1..1 space, y up.
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    if (Inside(shape, u, v, 0.94f))
                    {
                        bool inner = Inside(shape, u, v, 0.80f);
                        var fill = inner ? primary : outline;
                        // Subtle top-light band for a 16-bit look.
                        if (inner && !Inside(shape, u, v + 0.12f, 0.80f)) fill = primary.Lighten(0.25f);
                        canvas.Set(x, y, fill);
                    }
                }

            var motifColor = RgbColor.Distance(secondary, primary) < 60 ? outline : secondary;
            int span = MotifSize * MotifScale;
            int left = (Size - span) / 2;
            int top = (Size + span) / 2 - 1;
            canvas.Stamp(Motif(motif), left, top, motifColor, accent, MotifScale);
            return canvas;
        }

        /// <summary>Shape membership test in normalised space; <paramref name="scale"/> shrinks the shape.</summary>
        public static bool Inside(LogoShape shape, float u, float v, float scale)
        {
            u /= scale;
            v /= scale;
            float au = Math.Abs(u), av = Math.Abs(v);
            switch (shape)
            {
                case LogoShape.Circle:
                    return u * u + v * v <= 1f;
                case LogoShape.Diamond:
                    return au + av <= 1f;
                case LogoShape.Hexagon:
                    // Pointy-sided hexagon.
                    // Flat-top hexagon with vertices at (±1, 0).
                    return av <= 0.866f && (au * 0.866f + av * 0.5f) <= 0.866f;
                case LogoShape.Shield:
                    if (v >= 0f) return au <= 0.9f && v <= 0.95f;
                    return au <= 0.9f * (1f + v) + 0.02f && v >= -1f;
                case LogoShape.Badge:
                {
                    // Rounded square.
                    const float r = 0.35f;
                    float qx = Math.Max(au - (1f - r), 0f), qy = Math.Max(av - (1f - r), 0f);
                    return au <= 1f && av <= 1f && qx * qx + qy * qy <= r * r;
                }
                default:
                    return false;
            }
        }

        public static string[] Motif(LogoMotif motif)
        {
            switch (motif)
            {
                case LogoMotif.Bolt: return Bolt;
                case LogoMotif.Wave: return Wave;
                case LogoMotif.Paw: return Paw;
                case LogoMotif.Tree: return Tree;
                case LogoMotif.Comet: return Comet;
                case LogoMotif.Dune: return Dune;
                case LogoMotif.Owl: return Owl;
                case LogoMotif.Crown: return Crown;
                case LogoMotif.Ball: return Ball;
                case LogoMotif.Signal: return Signal;
                case LogoMotif.Crane: return Crane;
                case LogoMotif.Lighthouse: return Lighthouse;
                case LogoMotif.Lantern: return Lantern;
                case LogoMotif.Skate: return Skate;
                case LogoMotif.Bubbles: return Bubbles;
                case LogoMotif.Kite: return Kite;
                case LogoMotif.Flag: return Flag;
                default: return Ball;
            }
        }

        // 12x12 motifs. Rows top→bottom. '#' = motif colour, '+' = accent.
        private static readonly string[] Bolt =
        {
            "......####..",
            ".....####...",
            "....####....",
            "...####.....",
            "..#########.",
            "......####..",
            ".....####...",
            "....####....",
            "...###......",
            "..##........",
            ".#..........",
            "............",
        };

        // A golf flag in the hole: pennant, pole and the cup on a little green.
        private static readonly string[] Flag =
        {
            "...##.......",
            "...#+##.....",
            "...#+++##...",
            "...#++++##..",
            "...#+++##...",
            "...#+##.....",
            "...##.......",
            "...#........",
            "...#........",
            "..###.......",
            ".##+##+++##.",
            "..########..",
        };

        // A diamond kite with its cross spars and a bowed tail.
        private static readonly string[] Kite =
        {
            ".....##.....",
            "....#++#....",
            "...#++#+#...",
            "..#++##++#..",
            ".##########.",
            "..#++##++#..",
            "...#+#++#...",
            "....#++#....",
            ".....##.....",
            "......#.....",
            ".....#.##...",
            "....#....#..",
        };

        // Soap bubbles rising: one big, two medium, two small, each with a shine.
        private static readonly string[] Bubbles =
        {
            "......##....",
            ".....#++#...",
            "..##..##....",
            ".#++#....##.",
            ".#+##...#++#",
            "..##....#+##",
            "....####.##.",
            "...#+++##...",
            "..#++++++#..",
            "..#+++++##..",
            "...#+++##...",
            "....####....",
        };

        // A quad roller skate from the side: high boot with laces, plate and two big wheels.
        private static readonly string[] Skate =
        {
            "..####......",
            "..#++#......",
            "..#+##......",
            "..#++#......",
            "..#+##......",
            "..#++####...",
            "..#++++++#..",
            ".#++++++++#.",
            ".##########.",
            "..##....##..",
            ".#++#..#++#.",
            "..##....##..",
        };

        // A paper lantern on its string: cap, ribbed glowing body, tassel.
        private static readonly string[] Lantern =
        {
            ".....##.....",
            "....####....",
            "...######...",
            "..#+#++#+#..",
            ".#+#++++#+#.",
            ".#+#++++#+#.",
            ".#+#++++#+#.",
            "..#+#++#+#..",
            "...######...",
            "....####....",
            ".....##.....",
            ".....#.#....",
        };

        // A lighthouse: beams out to both sides from the lamp, striped tower, rocks at the base.
        private static readonly string[] Lighthouse =
        {
            ".....##.....",
            "+...####...+",
            ".++.#++#.++.",
            "....####....",
            ".....##.....",
            "....####....",
            "....#++#....",
            "....####....",
            "...#++++#...",
            "...######...",
            "..########..",
            ".##########.",
        };

        // A folded paper bird: two wings up, beak to the left.
        private static readonly string[] Crane =
        {
            "............",
            "...#.....#..",
            "...##...##..",
            "...###.###..",
            "....#####...",
            "#...#####...",
            ".##+######..",
            "..+++#####..",
            "....+++###..",
            "......+++#..",
            "........++..",
            "............",
        };

        private static readonly string[] Wave =
        {
            "............",
            "..##....##..",
            ".#..#..#..#.",
            "#....##....#",
            "............",
            "..##....##..",
            ".#..#..#..#.",
            "#....##....#",
            "............",
            "..##....##..",
            ".#..#..#..#.",
            "#....##....#",
        };

        private static readonly string[] Paw =
        {
            "............",
            "...##..##...",
            "...##..##...",
            ".##......##.",
            ".##......##.",
            "............",
            "....####....",
            "...######...",
            "..########..",
            "..########..",
            "...##..##...",
            "............",
        };

        private static readonly string[] Tree =
        {
            ".....##.....",
            "....####....",
            "...######...",
            "....####....",
            "..########..",
            "...######...",
            ".##########.",
            "..########..",
            "############",
            ".....++.....",
            ".....++.....",
            "....++++....",
        };

        private static readonly string[] Comet =
        {
            "...........#",
            "........#.#.",
            ".......#.#..",
            "..####..#...",
            ".######.....",
            "########....",
            "###++###....",
            "###++###....",
            "########....",
            ".######.....",
            "..####......",
            "............",
        };

        private static readonly string[] Dune =
        {
            "....++++....",
            "...++++++...",
            "...++++++...",
            "....++++....",
            "............",
            "........###.",
            "......######",
            "..###.######",
            ".###########",
            "############",
            "############",
            "............",
        };

        private static readonly string[] Owl =
        {
            ".#........#.",
            ".##......##.",
            ".##########.",
            "##..####..##",
            "#.++.##.++.#",
            "#.++.##.++.#",
            "##..#..#..##",
            ".####..####.",
            "..###..###..",
            "...######...",
            "....####....",
            "....#..#....",
        };

        private static readonly string[] Crown =
        {
            "............",
            "#....##....#",
            "##..####..##",
            "###.####.###",
            "############",
            "############",
            "#+##+##+##+#",
            "############",
            "############",
            "............",
            "############",
            "............",
        };

        private static readonly string[] Ball =
        {
            "...######...",
            "..#..##..#..",
            ".#...##...#.",
            "#.#..##..#.#",
            "#..#.##.#..#",
            "############",
            "############",
            "#..#.##.#..#",
            "#.#..##..#.#",
            ".#...##...#.",
            "..#..##..#..",
            "...######...",
        };

        private static readonly string[] Signal =
        {
            "............",
            ".#........#.",
            "#..#....#..#",
            "#.#......#.#",
            "#.#..++..#.#",
            "#.#.++++.#.#",
            "#.#.++++.#.#",
            "#.#..++..#.#",
            "#.#......#.#",
            "#..#....#..#",
            ".#........#.",
            "............",
        };
    }
}
