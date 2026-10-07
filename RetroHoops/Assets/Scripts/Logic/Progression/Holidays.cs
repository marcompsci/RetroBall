using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One Holiday Game: its court, name and when it's in season.</summary>
    public sealed class HolidayGame
    {
        public HolidayTheme Theme;
        public string Name;
        public string CourtId;
        public string Blurb;
    }

    /// <summary>
    /// Holiday Games (Play ► HOLIDAY GAMES): a Christmas, Halloween, Easter and Fourth of July game, each on
    /// its own decorated court. All four are always playable; the one in season gets a banner on the main
    /// menu (all of October, December 1–26, Easter week, July 1–7).
    /// </summary>
    public static class Holidays
    {
        public const string ChristmasCourtId = "court.holiday_christmas";
        public const string HalloweenCourtId = "court.holiday_halloween";
        public const string EasterCourtId = "court.holiday_easter";
        public const string FourthCourtId = "court.holiday_fourth";

        public static readonly List<HolidayGame> All = new List<HolidayGame>
        {
            new HolidayGame { Theme = HolidayTheme.Christmas, Name = "CHRISTMAS GAME", CourtId = ChristmasCourtId,
                              Blurb = "Snow on the blacktop and lights on the fence." },
            new HolidayGame { Theme = HolidayTheme.Halloween, Name = "HALLOWEEN GAME", CourtId = HalloweenCourtId,
                              Blurb = "Jack-o'-lanterns, bats, and a big orange moon." },
            new HolidayGame { Theme = HolidayTheme.Easter, Name = "EASTER GAME", CourtId = EasterCourtId,
                              Blurb = "Spring grass and painted eggs along the baseline." },
            new HolidayGame { Theme = HolidayTheme.FourthOfJuly, Name = "FOURTH OF JULY GAME", CourtId = FourthCourtId,
                              Blurb = "Stars, stripes, and fireworks over the stands." },
        };

        public static HolidayGame Get(HolidayTheme theme) => All.Find(h => h.Theme == theme);

        /// <summary>Western (Gregorian) Easter Sunday for a year (anonymous Gregorian algorithm).</summary>
        public static DateTime EasterSunday(int year)
        {
            int a = year % 19, b = year / 100, c = year % 100, d = b / 4, e = b % 4;
            int f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31;
            int day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateTime(year, month, day);
        }

        /// <summary>The holiday in season on <paramref name="date"/>, or None.</summary>
        public static HolidayTheme InSeason(DateTime date)
        {
            var d = date.Date;
            if (d.Month == 10) return HolidayTheme.Halloween;
            if (d.Month == 12 && d.Day <= 26) return HolidayTheme.Christmas;
            if (d.Month == 7 && d.Day <= 7) return HolidayTheme.FourthOfJuly;
            var easter = EasterSunday(d.Year);
            if (d >= easter.AddDays(-6) && d <= easter.AddDays(1)) return HolidayTheme.Easter;
            return HolidayTheme.None;
        }

        /// <summary>A Holiday Game against a league team on the holiday court (Quick Call rules and rewards).</summary>
        public static MatchRequest Request(HolidayTheme theme, string homeTeamId, string awayTeamId, string difficultyId)
        {
            var h = Get(theme);
            return new MatchRequest
            {
                Mode = GameMode.QuickCall,
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                CourtId = h?.CourtId,
                DifficultyId = difficultyId,
            };
        }
    }
}
