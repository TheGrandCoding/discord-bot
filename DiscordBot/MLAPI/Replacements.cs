using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class Replacements
    {
        public Dictionary<string, object> objs = new Dictionary<string, object>();
        public Replacements Add(string name, object obj)
        {
            objs[name] = obj;
            return this;
        }

        bool TryGetFieldOrProperty(object obj, string name, out object value)
        {
            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var split = name.Split('.');
            foreach(var p in properties)
            {
                if(p.Name.ToLower() == name)
                {
                    value = p.GetValue(obj);
                    return true;
                }
                if(p.Name.ToLower() == split[0])
                    return TryGetFieldOrProperty(p.GetValue(obj), string.Join(".", split[1..]), out value);
            }
            value = null;
            return false;
        }

        public bool TryGetValue(string key, out object obj)
        {
            obj = null;
            if (objs.TryGetValue(key, out obj))
                return true;
            var wanted = key.Split('.');
            if(objs.TryGetValue(wanted[0], out var cls))
            {
                return TryGetFieldOrProperty(cls, string.Join(".", wanted[1..]), out obj);
            }
            return false;
        }
    }
}
