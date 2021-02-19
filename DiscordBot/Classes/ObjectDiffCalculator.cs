using Discord;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot.Classes
{
    public class ObjectDiffCalculator<T>
    {
        public T Before { get; set; }
        public T After { get; set; }
        public int Depth { get;  }
        public ObjectDiffCalculator(T before, T after, int depth = 0)
        {
            Before = before;
            After = after;
            Depth = depth;
        }

        /*public List<Change> GetChanges()
        {
            var ls = new List<Change>();
            foreach(var x in this.GetType().GetMethods().Where(x => x.ReturnType == typeof(Change)))
            {
                ls.Add((Change)x.Invoke(this, new object[] { }));
            }
            return ls.Where(x => x != null).ToList();
        }*/

        public List<Change> GetChangesReflection()
        {
            if (Depth > 5)
                return new List<Change>();
            var properties = typeof(T).GetProperties();
            var ls = new List<Change>();
            foreach (var property in properties)
            {
                if (property.Name == "Guild")
                    continue;
                if (property.Name == "VoiceChannel"
                    || property.Name == "VoiceSessionId"
                    || property.Name == "VoiceState")
                    continue;
                if (property.Name == "MutualGuilds")
                    continue;
                if (typeof(T) == property.PropertyType && property.Name.EndsWith("Now"))
                    continue;
                if (IsPropertyACollection(property))
                    ls.AddRange(getChangesList(property));
                else
                    ls.AddRange(getChangesSingular(property));
            }
            return ls;
        }

        bool IsPropertyACollection(PropertyInfo property)
        {
            if (property.PropertyType == typeof(string))
                return false;
            return property.PropertyType.GetInterface(typeof(IEnumerable<>).FullName) != null;
        }

        string toStr(IEnumerable list)
        {
            if (list == null)
                return "null";
            var sb = new StringBuilder();
            sb.Append("[");
            foreach (var x in list)
                sb.Append(toStr(x));
            sb.Append("]");
            return sb.ToString();
        }
        string toStr(object thing)
        {
            if (thing == null)
                return "null";
            if (thing is GuildPermissions perms)
            {
                return perms.RawValue.ToString();
            }
            else
            {
                return thing.ToString();
            }
        }

        List<Change> getChangesList(PropertyInfo property)
        {
            var before = ((IEnumerable)property.GetValue(Before)).Cast<object>();
            var after = ((IEnumerable)property.GetValue(After)).Cast<object>();
            if (before == null || after == null)
                return new Change(property.Name, toStr(before), toStr(after));
            var added = after.Where(x => before.Contains(x) == false).ToList();
            var removed = before.Where(x => after.Contains(x) == false).ToList();
            var ls = new List<Change>();
            if (added.Count > 0)
                ls.Add(new Change("+" + property.Name, null, toStr(added)));
            if (removed.Count > 0)
                ls.Add(new Change("-" + property.Name, null, toStr(removed)));
            return ls;
        }
        List<Change> getChangesSingular(PropertyInfo property)
        {
            var before = property.GetValue(Before);
            var after = property.GetValue(After);
            if (before == null && after == null)
                return new List<Change>();
            if (before == null || after == null)
                return new Change(property.Name, toStr(before), toStr(after));
            if (property.PropertyType.IsEnum)
                return Enum.Equals(before, after)
                    ? new List<Change>()
                    : new Change(property.Name, toStr(before), toStr(after));
            if(property.PropertyType.IsPrimitive)
            {
                return before.Equals(after)
                ? new List<Change>()
                : new Change(property.Name, toStr(before), toStr(after));
            }
            if(property.PropertyType == typeof(GuildPermissions) || property.PropertyType == typeof(ChannelPermissions))
            {
                return before.Equals(after)
                ? new List<Change>()
                : new Change(property.Name, toStr(before), toStr(after));
            }
            if (property.PropertyType.FullName.StartsWith("System"))
                return before.Equals(after)
                    ? new List<Change>()
                    : new Change(property.Name, toStr(before), toStr(after));
            // Complex object, so we want to go deeper.
            var genericType = typeof(ObjectDiffCalculator<>).MakeGenericType(new Type[] { property.PropertyType });
            dynamic instance = Activator.CreateInstance(genericType, new object[] { before, after, Depth + 1 });
            Console.WriteLine(new string(' ', Depth * 3) + $"Deeper dive into {property.Name} of {property.PropertyType.Name}");
            List<Change> changes = instance.GetChangesReflection();
            foreach (var x in changes)
                x.Type = property.Name + "." + x.Type;
            return changes;
        }
    }

    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    public class Change
    {
        public string Type { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public Change(string type, string before, string after)
        {
            Type = type;
            Before = before;
            After = after;
        }
        public static Change Status(UserStatus before, UserStatus after)
            => new Change("Status", before.ToString(), after.ToString());
        public static Change Activity(string before, string after)
            => new Change("Activity", before, after);

        public static implicit operator List<Change>(Change This) => new List<Change>() { This };

        private string DebuggerDisplay { get
            {
                return $"{Type}: {Before} -> {After}";
            } }
    }
}
