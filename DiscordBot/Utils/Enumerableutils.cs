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

        public static void OrderedInsert<TValue>(this List<TValue> list, TValue value, Func<TValue, TValue, bool> isBefore)
        {
            int index = 0;
            foreach(var item in list)
            {
                if (isBefore(item, value))
                    index++;
                else
                    break;
            }
            list.Insert(index, value);
        }
    
        public static void Deconstruct(this string[] arr, out string arg1, out string arg2)
        {
            arg1 = arr[0];
            arg2 = arr[1];
        }
    }
}
