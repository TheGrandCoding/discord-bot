using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Utils
{
    public static class EnumerableUtils
    {
        public static void AddInner<TKey, TListType>(this Dictionary<TKey, List<TListType>> dict, TKey key, TListType value)
        {
            if (dict.TryGetValue(key, out var ls))
                ls.Add(value);
            else
                dict[key] = new List<TListType>() { value };
        }
    }
}
