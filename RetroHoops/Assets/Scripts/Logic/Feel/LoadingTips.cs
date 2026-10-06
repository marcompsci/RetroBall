using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Phase 35: one-line tips shown on the screen wipe between screens, taken in turn (never the same one twice in a row).
    /// All original advice about this game.
    /// </summary>
    public static class LoadingTips
    {
        public static readonly string[] All =
        {
            "Release in the green for your best chance to score.",
            "Heat up with three makes in a row; HEAT CHECK makes the next one easier.",
            "Tap PASS near a teammate running the baseline to throw an alley-oop.",
            "The SHOT CHART after each game shows where you're hot. Go back to those spots.",
            "Cold from a spot? The chart turns it blue. Try somewhere else.",
            "Franchise: switch GAME DAY to COACH to call plays and subs from the sideline.",
            "Coaching: FEED THE HOT HAND gets the ball to whoever is scoring tonight.",
            "Coaching: if they keep hitting threes, switch your defense to MAN or PRESSURE.",
            "Subs go in at the next dead ball. Tired legs miss shots.",
            "Game tapes: open REPLAY THEATER to watch in slow motion and jump between baskets.",
            "In the theater, ADD MARK saves the moment with the tape.",
            "Pick-and-roll: wait for the screen, then drive off it.",
            "A defender on your back? Big men can drop-step to the baseline.",
            "Full Court: get the ball over half court within eight seconds.",
            "Box out on a miss to win the rebound.",
            "Steal attempts leave you off balance for a moment. Pick your time.",
            "Play the Daily for a quick reward every day.",
            "Weekly goals reset on Monday.",
            "The Gauntlet: the same four drills for everyone each day. Your best run counts.",
            "Your Locker Room keeps your career shot chart.",
            "Two phones nearby? A third friend can WATCH the game live.",
            "Turn on Reduce Motion in Settings for plain fades instead of wipes.",
            "Bigger text? Settings can follow your phone's text size.",
            "Legacy: a good game raises your coach's trust.",
        };

        /// <summary>The tip for the <paramref name="n"/>th transition (a stable shuffle, so they don't come in list order).</summary>
        public static string Tip(int n)
        {
            int len = All.Length;
            int i = (int)(((uint)n * 7u + 3u) % (uint)len); // 7 and the list length share no factor: every tip in turn
            return All[i];
        }
    }
}
