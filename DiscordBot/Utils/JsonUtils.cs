using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Utils
{
    public static class JsonUtils
    {
        public static T GetOrDefault<T>(this JToken token, string name, T def = default(T))
        {
            var x = token[name];
            if (x == null) return def;
            return x.ToObject<T>();
        }
        public static T AtOrDefault<T>(this JArray array, int index, T def = default(T))
        {
            var x = array[index];
            if (x == null) return def;
            return x.ToObject<T>();
        }
    }
}
