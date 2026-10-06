using System;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Phase 33: the playoff bracket drawn as a picture — the two semifinals on the left, joined by lines to the
    /// final on the right, then the champion. Seeds, scores and the winner of each game are shown; your team is gold.
    /// </summary>
    public static class BracketView
    {
        private const float Height = 380f;
        private static readonly Color Line = new Color32(0x58, 0x58, 0x68, 255);
        private static readonly Color Box = new Color32(0x2A, 0x2A, 0x45, 255);

        public static void Draw(Transform column, PlayoffBracket b, Func<string, string> name, string mineId, string title)
        {
            UiKit.Size(UiKit.Label(column, b.Projected ? title + "  ·  " + Loc.T("IF THE SEASON ENDED TODAY") : title, 32f, Theme.Pink, TextAlignmentOptions.Left, true), 52f);
            var area = UiKit.NewRect("Bracket", column);
            UiKit.Size(area, Height);

            // Semifinals: left 46%, top and bottom halves. Final: right 46%, centred.
            Matchup(area, b.SemiA, 0f, 0.46f, 0.55f, 1f, name, mineId);
            Matchup(area, b.SemiB, 0f, 0.46f, 0f, 0.45f, name, mineId);
            Matchup(area, b.Final, 0.54f, 1f, 0.27f, 0.73f, name, mineId);

            // Connectors: out of each semi, down/up to the middle, across to the final.
            Bar(area, 0.46f, 0.50f, 0.775f, 0.785f);
            Bar(area, 0.46f, 0.50f, 0.215f, 0.225f);
            Bar(area, 0.495f, 0.505f, 0.22f, 0.78f);
            Bar(area, 0.50f, 0.54f, 0.495f, 0.505f);

            if (!string.IsNullOrEmpty(b.ChampionId))
                UiKit.Size(UiKit.Label(column, Loc.T("CHAMPION") + ": " + (name(b.ChampionId) ?? "?").ToUpperInvariant(), 34f,
                                       b.ChampionId == mineId ? Theme.Gold : Theme.Cyan, TextAlignmentOptions.Center, true), 56f);
        }

        private static void Matchup(RectTransform area, PlayoffBracket.Matchup m, float x0, float x1, float y0, float y1, Func<string, string> name, string mineId)
        {
            var box = UiKit.Panel(area, Box, Theme.PanelSprite(), true, m.Label);
            var rt = box.rectTransform;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var head = UiKit.Label(rt, m.Label, 20f, Theme.Muted, TextAlignmentOptions.Center, true);
            Place(head.rectTransform, 0.66f, 1f);
            Row(rt, m.Top, 0.34f, 0.68f, name, mineId);
            Row(rt, m.Bottom, 0f, 0.34f, name, mineId);
        }

        private static void Row(RectTransform box, PlayoffBracket.Slot s, float y0, float y1, Func<string, string> name, string mineId)
        {
            string team = s.Known ? (s.Seed > 0 ? s.Seed + "  " : "") + (name(s.TeamId) ?? "?") : "—";
            string score = s.Score >= 0 ? "  " + s.Score : "";
            var color = !s.Known ? Theme.Muted : s.TeamId == mineId ? Theme.Gold : (s.Score >= 0 && !s.Won ? Theme.Muted : Theme.Cream);
            var label = UiKit.Label(box, (s.Won ? "<b>" : "") + team + score + (s.Won ? "</b>" : ""), 26f, color, TextAlignmentOptions.Left, false);
            Place(label.rectTransform, y0, y1, 14f);
        }

        private static void Place(RectTransform rt, float y0, float y1, float inset = 6f)
        {
            rt.anchorMin = new Vector2(0f, y0);
            rt.anchorMax = new Vector2(1f, y1);
            rt.offsetMin = new Vector2(inset, 0f);
            rt.offsetMax = new Vector2(-inset, 0f);
        }

        private static void Bar(RectTransform area, float x0, float x1, float y0, float y1)
        {
            var bar = UiKit.Panel(area, Line, name: "Connector");
            bar.rectTransform.anchorMin = new Vector2(x0, y0);
            bar.rectTransform.anchorMax = new Vector2(x1, y1);
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
