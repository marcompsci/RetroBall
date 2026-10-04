using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>Weekly Challenges and the free Hoops Pass track, opened from PLAY ▸ EVENTS.</summary>
    public sealed partial class MainMenuController
    {
        private static readonly Color PassPurple = new Color32(0x9B, 0x4D, 0xFF, 255);

        private void ShowWeekly()
        {
            var c = App.Catalog;
            var career = App.Career;
            int day = App.Today;
            career.weekly = career.weekly ?? new WeeklySaveData();
            career.pass = career.pass ?? new PassSaveData();
            Weekly.Sync(career.weekly, day);
            HoopsPass.Sync(career.pass, day);
            var column = OpenOverlay("WEEKLY & PASS", out var footer);

            // ---- weekly challenges
            int left = Weekly.DaysLeft(day);
            UiKit.Size(UiKit.Label(column, "WEEKLY CHALLENGES", 40f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);
            UiKit.Size(UiKit.Label(column, "New goals every Monday (" + left + (left == 1 ? " day" : " days") + " left). Any game against the CPU counts. Each goal: +"
                                   + Weekly.RewardSp + " SP and +" + HoopsPass.WeeklyGoalXp + " pass XP. All three: +" + Weekly.PerfectWeekSp + " SP more.",
                                   26f, Theme.Muted), 100f);
            var goals = Weekly.For(career.weekly.week);
            for (int i = 0; i < goals.Length; i++)
            {
                bool done = career.weekly.done[i];
                int have = career.weekly.progress[i];
                UiKit.Size(UiKit.Label(column, (done ? "<color=#4CC9F0>✓ </color>" : "") + goals[i].Describe().ToUpperInvariant()
                                       + "  <color=#8D99AE>" + have + "/" + goals[i].Target + "</color>",
                                       32f, done ? Theme.Cyan : Theme.Cream, TextAlignmentOptions.Left, true), 52f);
                Bar(column, goals[i].Target > 0 ? have / (float)goals[i].Target : 0f, done ? Theme.Cyan : Theme.Pink);
            }

            // ---- hoops pass
            var p = career.pass;
            int tier = HoopsPass.Tier(p.xp);
            UiKit.Size(UiKit.Label(column, "HOOPS PASS  ·  SEASON " + (p.season + 1), 40f, PassPurple, TextAlignmentOptions.Center, true), 76f);
            UiKit.Size(UiKit.Label(column, "Free. No purchases, ever. Play games, finish Weekly and Daily Challenges, climb " + HoopsPass.Tiers
                                   + " tiers. Gear on tiers 5, 10, 15 and 20 is only found here, and comes round again next season.",
                                   26f, Theme.Muted), 100f);
            int daysLeft = HoopsPass.DaysLeft(day);
            UiControls.Stat(column, "TIER", tier + " / " + HoopsPass.Tiers + "  ·  " + daysLeft + " days left");
            int into = tier >= HoopsPass.Tiers ? HoopsPass.XpPerTier : p.xp - tier * HoopsPass.XpPerTier;
            Bar(column, into / (float)HoopsPass.XpPerTier, PassPurple);
            UiKit.Size(UiKit.Label(column, tier >= HoopsPass.Tiers ? "Pass complete! See you next season."
                                   : (HoopsPass.XpPerTier - into) + " XP to tier " + (tier + 1) + "  ·  game +" + HoopsPass.GameXp + ", win +" + HoopsPass.WinXp
                                     + ", daily +" + HoopsPass.DailyXp, 26f, Theme.Cream), 48f);

            for (int t = 1; t <= HoopsPass.Tiers; t++)
            {
                var r = HoopsPass.RewardFor(c, p.season, t);
                bool got = t <= p.granted;
                bool gear = r.CosmeticId != null;
                bool owned = gear && career.ownedCosmetics.Contains(r.CosmeticId);
                string mark = got ? "<color=#4CC9F0>✓</color>" : (t == tier + 1 ? "<color=#FFD166>></color>" : "  ");
                string label = gear ? "<color=#9B4DFF>" + r.Label.ToUpperInvariant() + "</color>" + (owned && !got ? "  <size=22><color=#8D99AE>(owned: +" + HoopsPass.OwnedGearSp + " SP)</color></size>" : "")
                                    : r.Label;
                UiKit.Size(UiKit.Label(column, mark + "  TIER " + t + "   " + label, gear ? 32f : 28f, got ? Theme.Muted : Theme.Cream, TextAlignmentOptions.Left, gear), gear ? 56f : 44f);
            }
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "PLAY", ShowQuickCall, ButtonStyle.Primary, 130f);
        }

        /// <summary>A thin progress bar (0..1).</summary>
        private static void Bar(Transform column, float fraction, Color fill)
        {
            var back = UiKit.Panel(column, new Color(1f, 1f, 1f, 0.12f), name: "Bar");
            UiKit.Size(back, 22f);
            var front = UiKit.Panel(back.transform, fill, name: "Fill");
            var rt = front.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            front.raycastTarget = back.raycastTarget = false;
        }
    }
}
