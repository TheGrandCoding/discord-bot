using Newtonsoft.Json.Linq;
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

        public static void Increment<TKey>(this Dictionary<TKey, int> dict, TKey key)
        {
            dict[key] = dict.GetValueOrDefault(key, 0) + 1;
        }

        public static JObject ToJson<TValue>(this Dictionary<string, TValue> dict, Func<TValue, JToken> converter)
        {
            var j = new JObject();
            foreach(var keypair in dict)
                j[keypair.Key] = converter(keypair.Value);
            return j;
        }
        public static JObject ToJson<TValue>(this Dictionary<string, TValue> dict)
        {
            return ToJson<TValue>(dict, (x => JToken.FromObject(x)));
        }
    }
}
