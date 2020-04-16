using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class DateEquality : EqualityComparer<DateTime>
    {
        public override bool Equals(DateTime x, DateTime y)
        {
            if (x == null || y == null)
            {
                return false;
            }
            return x.Year == y.Year && x.DayOfYear == y.DayOfYear;
        }

        public override int GetHashCode(DateTime obj)
        {
            return obj.Year * obj.DayOfYear;
        }
    }
}
