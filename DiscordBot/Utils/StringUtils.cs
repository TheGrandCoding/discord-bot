using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Utils
{
    public static class StringUtils
    {
        public static string Repeat(this string s, int amount)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < amount; i++)
                sb.Append(s);
            return sb.ToString();
        }
    }
}
