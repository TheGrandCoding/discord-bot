using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Utils
{
    public static class DateTimeUtils
    {
        public static DateTime ToLastSecond(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day,
                23, 59, 59);
        }
        public static DateTime NextDay(this DateTime date, DayOfWeek day)
        {
            while (date.DayOfWeek != day)
                date = date.AddDays(1);
            return date;
        }
        public static bool IsSameDay(this DateTime date, DateTime other)
        {
            return date.Year == other.Year && date.DayOfYear == other.DayOfYear;
        }
    }
}
