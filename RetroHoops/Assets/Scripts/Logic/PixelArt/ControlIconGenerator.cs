using System.Collections.Generic;

namespace CallerRetroBall.Logic.PixelArt
{
    public enum ControlIcon { Shoot = 0, Pass = 1, Dunk = 2, Layup = 3, Call = 4, Block = 5, Steal = 6, Switch = 7, Oop = 8 }

    /// <summary>
    /// Original 13x13 pixel icons for the touch buttons (white line art, orange ball, dark outline),
    /// drawn from little text grids. Row 0 of each grid is the TOP row.
    /// </summary>
    public static class ControlIconGenerator
    {
        public const int Size = 13;
        public static readonly RgbColor Line = new RgbColor(0xFF, 0xFF, 0xF4);
        public static readonly RgbColor Ball = new RgbColor(0xFF, 0x8C, 0x42);
        public static readonly RgbColor Outline = new RgbColor(0x14, 0x14, 0x20, 230);

        private static readonly Dictionary<ControlIcon, string[]> Grids = new Dictionary<ControlIcon, string[]>
        {
            // Ball rising over a rim and net.
            [ControlIcon.Shoot] = new[]
            {
                "....ooo......",
                "...ooooo.....",
                "...ooooo.....",
                "....ooo......",
                ".............",
                "..#########..",
                "...#.#.#.#...",
                "...#.#.#.#...",
                "....#.#.#....",
                "....#.#.#....",
                ".....#.#.....",
                ".............",
                ".............",
            },
            // Ball and an arrow to the right.
            [ControlIcon.Pass] = new[]
            {
                ".............",
                ".............",
                ".........#...",
                ".ooo......#..",
                "ooooo######..",
                "ooooo.....#..",
                ".ooo.....#...",
                ".............",
                "..#..........",
                "..##.........",
                "..#.#........",
                "..##.........",
                ".............",
            },
            // Ball driven down into the rim.
            [ControlIcon.Dunk] = new[]
            {
                "......#......",
                "......#......",
                "....#####....",
                ".....###.....",
                ".....ooo.....",
                "....ooooo....",
                "....ooooo....",
                ".....ooo.....",
                "..#########..",
                "...#.#.#.#...",
                "....#.#.#....",
                ".....#.#.....",
                ".............",
            },
            // Ball laid up on an arc to the rim (upper right).
            [ControlIcon.Layup] = new[]
            {
                ".............",
                "......#######",
                ".......#.#.#.",
                "....ooo#.#.#.",
                "...ooooo.#...",
                "...ooooo.....",
                "..#.ooo......",
                ".#...........",
                ".#...........",
                "#............",
                "#............",
                ".............",
                ".............",
            },
            // Clipboard with a play drawn on it.
            [ControlIcon.Call] = new[]
            {
                "....#####....",
                "..#########..",
                "..#.......#..",
                "..#.#...#.#..",
                "..#..#.#..#..",
                "..#...#...#..",
                "..#..#.#..#..",
                "..#.#...#.#..",
                "..#.......#..",
                "..#..ooo..#..",
                "..#..ooo..#..",
                "..#########..",
                ".............",
            },
            // Open hand held high.
            [ControlIcon.Block] = new[]
            {
                "...#.#.#.....",
                "...#.#.#.#...",
                "...#.#.#.#...",
                "...#######...",
                "#..#######...",
                ".#.#######...",
                "..########...",
                "...######....",
                "....####.....",
                "....####.....",
                "....####.....",
                ".............",
                ".............",
            },
            // Hand swiping at a ball.
            [ControlIcon.Steal] = new[]
            {
                ".............",
                ".........ooo.",
                "........ooooo",
                "........ooooo",
                ".........ooo.",
                "#####........",
                "######.......",
                "#######......",
                "######.......",
                "#####.#......",
                "......##.....",
                ".......#.....",
                ".............",
            },
            // Two arrows swapping.
            [ControlIcon.Switch] = new[]
            {
                ".............",
                "........#....",
                "........##...",
                "..#########..",
                "........##...",
                "........#....",
                ".............",
                "....#........",
                "...##........",
                "..#########..",
                "...##........",
                "....#........",
                ".............",
            },
            // Ball on a high lob.
            [ControlIcon.Oop] = new[]
            {
                ".....ooo.....",
                "....ooooo....",
                "....ooooo....",
                "..#..ooo..#..",
                ".#.........#.",
                ".#.........#.",
                "#...........#",
                "#...........#",
                "#.........###",
                "...........#.",
                "......#######",
                ".......#.#.#.",
                "........#.#..",
            },
        };

        public static PixelCanvas Generate(ControlIcon icon)
        {
            var c = new PixelCanvas(Size + 2, Size + 2);
            var grid = Grids[icon];
            for (int row = 0; row < Size; row++)
                for (int col = 0; col < Size; col++)
                {
                    char ch = row < grid.Length && col < grid[row].Length ? grid[row][col] : '.';
                    if (ch == '.') continue;
                    c.Set(col + 1, Size - row, ch == 'o' ? Ball : Line);
                }
            CharacterSpriteGenerator.AddOutline(c);
            return c;
        }

        /// <summary>Icon for a button in context (defence swaps the action).</summary>
        public static ControlIcon For(ControlButton b, bool defense, bool oop = false)
        {
            switch (b)
            {
                case ControlButton.Shoot: return defense ? ControlIcon.Block : ControlIcon.Shoot;
                case ControlButton.Pass: return defense ? ControlIcon.Switch : (oop ? ControlIcon.Oop : ControlIcon.Pass);
                case ControlButton.Dunk: return defense ? ControlIcon.Steal : ControlIcon.Dunk;
                case ControlButton.Layup: return ControlIcon.Layup;
                default: return ControlIcon.Call;
            }
        }
    }
}
