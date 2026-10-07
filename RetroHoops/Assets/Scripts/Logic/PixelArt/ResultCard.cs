namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Phase 40: the post-game SHARE RESULT card. A small pixel card (teams, the final score in big digits, your line
    /// and the player of the game) upscaled so it stays crisp in Messages and Photos.
    /// </summary>
    public static class ResultCard
    {
        public const int Width = 128, Height = 96, Upscaled = 8;
        private static readonly RgbColor Back = new RgbColor(18, 14, 32);
        private static readonly RgbColor Gold = new RgbColor(255, 209, 102);
        private static readonly RgbColor Cream = new RgbColor(246, 232, 200);
        private static readonly RgbColor Muted = new RgbColor(140, 134, 160);
        private static readonly RgbColor Win = new RgbColor(6, 214, 160);

        /// <summary>The card for <paramref name="s"/>; <paramref name="title"/> is the post-game headline ("SOLAR CROWNS WIN").</summary>
        public static PixelCanvas Render(MatchSummary s, string title)
        {
            var c = new PixelCanvas(Width, Height);
            c.Fill(Back);
            // A thin court-line frame.
            c.Line(1, 1, Width - 2, 1, Muted);
            c.Line(1, Height - 2, Width - 2, Height - 2, Muted);
            c.Line(1, 1, 1, Height - 2, Muted);
            c.Line(Width - 2, 1, Width - 2, Height - 2, Muted);
            if (s == null) return Finish(c);

            int top = Height - 6;
            PixelFont.DrawCentered(c, ShotChartArt.Fit(title ?? "FINAL", Width - 8), Width / 2, top, Gold);
            string a = ShotChartArt.Fit(s.teamAName ?? "HOME", Width / 2 - 6), b = ShotChartArt.Fit(s.teamBName ?? "AWAY", Width / 2 - 6);
            PixelFont.DrawCentered(c, a, Width / 4, top - 12, s.winner == 0 ? Win : Cream);
            PixelFont.DrawCentered(c, b, Width * 3 / 4, top - 12, s.winner == 1 ? Win : Cream);
            PixelFont.DrawCentered(c, s.scoreA.ToString(), Width / 4, top - 22, s.winner == 0 ? Win : Cream, 3);
            PixelFont.DrawCentered(c, s.scoreB.ToString(), Width * 3 / 4, top - 22, s.winner == 1 ? Win : Cream, 3);
            PixelFont.DrawCentered(c, "-", Width / 2, top - 28, Muted, 2);

            var you = s.HumanLine;
            int y = top - 46;
            if (you?.stats != null)
            {
                var t = you.stats;
                PixelFont.DrawCentered(c, ShotChartArt.Fit("YOU " + t.points + " PTS " + t.assists + " AST " + t.rebounds + " REB", Width - 8), Width / 2, y, Cream);
                y -= 9;
            }
            var potg = s.Line(s.playerOfTheGame);
            if (potg != null && !string.IsNullOrEmpty(potg.name))
                PixelFont.DrawCentered(c, ShotChartArt.Fit("PLAYER OF THE GAME", Width - 8), Width / 2, y, Muted);
            if (potg != null && !string.IsNullOrEmpty(potg.name))
                PixelFont.DrawCentered(c, ShotChartArt.Fit(potg.name, Width - 8), Width / 2, y - 8, Gold);
            return Finish(c);
        }

        private static PixelCanvas Finish(PixelCanvas c)
        {
            PixelFont.DrawCentered(c, "RETRO HOOPS", Width / 2, 9, Muted);
            return ShotChartArt.Upscale(c, Upscaled);
        }
    }
}
