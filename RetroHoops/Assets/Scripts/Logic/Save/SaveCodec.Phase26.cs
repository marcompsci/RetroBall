using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        private static object EncodeStory(StorySaveData s)
        {
            s = s ?? new StorySaveData();
            var best = new List<object>();
            foreach (var b in s.best ?? new List<int>()) best.Add(b);
            return new Dictionary<string, object> { ["cleared"] = s.cleared, ["best"] = best, ["attempts"] = s.attempts, ["finished"] = s.finished };
        }

        private static StorySaveData DecodeStory(Dictionary<string, object> o)
        {
            var s = new StorySaveData();
            if (o == null) return s;
            s.cleared = Math.Max(0, Math.Min(StoryMode.Chapters, Int(o, "cleared", 0)));
            foreach (var item in Arr(o, "best"))
            {
                if (s.best.Count >= StoryMode.Chapters) break;
                s.best.Add(item is double d && d > 0 ? 1 : 0);
            }
            s.attempts = Math.Max(0, Int(o, "attempts", 0));
            s.finished = Bool(o, "finished", false) && s.cleared >= StoryMode.Chapters;
            return s;
        }

        private static object EncodeLive(LiveSaveData l)
        {
            l = l ?? new LiveSaveData();
            return new Dictionary<string, object>
            {
                ["rating"] = l.rating, ["best"] = l.best, ["wins"] = l.wins, ["losses"] = l.losses, ["games"] = l.games,
                ["team"] = l.teamId ?? "", ["until"] = l.subscribedUntil, ["month"] = l.month,
            };
        }

        private static LiveSaveData DecodeLive(Dictionary<string, object> o)
        {
            var l = new LiveSaveData();
            if (o == null) return l;
            l.rating = Math.Max(100, Math.Min(4000, Int(o, "rating", LiveMode.StartRating)));
            l.best = Math.Max(l.rating, Math.Min(4000, Int(o, "best", l.rating)));
            l.wins = Math.Max(0, Int(o, "wins", 0));
            l.losses = Math.Max(0, Int(o, "losses", 0));
            l.games = Math.Max(l.wins + l.losses, Int(o, "games", 0));
            l.teamId = Str(o, "team", "");
            l.subscribedUntil = o.TryGetValue("until", out var u) && u is double d ? d : 0.0;
            l.month = Math.Max(0, Int(o, "month", 0));
            return LiveMode.Sanitize(l);
        }

        private static object EncodeCouch(CouchCupSaveData c)
        {
            c = c ?? new CouchCupSaveData();
            var games = new List<object>();
            foreach (var g in c.games ?? new List<CouchGame>())
                games.Add(new Dictionary<string, object> { ["r"] = g.round, ["a"] = g.a, ["b"] = g.b, ["sa"] = g.scoreA, ["sb"] = g.scoreB, ["p"] = g.played });
            return new Dictionary<string, object>
            {
                ["names"] = Strings(c.names), ["teams"] = Strings(c.teamIds), ["games"] = games, ["champion"] = c.champion, ["cups"] = c.cupsPlayed,
            };
        }

        private static CouchCupSaveData DecodeCouch(Dictionary<string, object> o)
        {
            var c = new CouchCupSaveData();
            if (o == null) return c;
            c.names = StrList(o, "names");
            c.teamIds = StrList(o, "teams");
            c.cupsPlayed = Math.Max(0, Int(o, "cups", 0));
            int n = c.names.Count;
            bool ok = n >= CouchCup.MinPlayers && n <= CouchCup.MaxPlayers && c.teamIds.Count == n;
            foreach (var item in Arr(o, "games"))
            {
                if (!(item is Dictionary<string, object> g)) { ok = false; break; }
                var game = new CouchGame
                {
                    round = Math.Max(0, Int(g, "r", 0)), a = Int(g, "a", 0), b = Int(g, "b", -1),
                    scoreA = Math.Max(0, Int(g, "sa", 0)), scoreB = Math.Max(0, Int(g, "sb", 0)), played = Bool(g, "p", false),
                };
                if (game.a < 0 || game.a >= n || game.b >= n || game.b < -1) ok = false;
                c.games.Add(game);
            }
            c.champion = Int(o, "champion", -1);
            if (c.champion >= n) ok = false;
            if (!ok)
            {
                // A damaged cup is dropped (the count of cups played is kept).
                int cups = c.cupsPlayed;
                c = new CouchCupSaveData { cupsPlayed = cups };
            }
            return c;
        }
    }
}
