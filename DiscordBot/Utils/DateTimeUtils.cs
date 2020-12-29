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
    }
}
