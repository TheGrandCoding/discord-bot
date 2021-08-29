using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot.Classes
{
    public abstract class DiffCalculator
    {
        public abstract List<Change> GetChanges();

        public static DiffCalculator Create<K>(K before, K after)
        {
            return Create(typeof(K), before, after, new AlreadyDoneSet());
        }

        private static ConstructorInfo GetAttributeConstructor(Type objectType)
        {
            IEnumerator<ConstructorInfo> en = objectType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(c => c.IsDefined(typeof(MarkConstructor), true)).GetEnumerator();

            if (en.MoveNext())
            {
                ConstructorInfo conInfo = en.Current;
                return conInfo;
            }

            return null;
        }

        public static DiffCalculator Create(Type type, object before, object after, AlreadyDoneSet done, int depth = 0)
        {


            Type diffType;
            if (type == typeof(SocketThreadChannel) || type == typeof(RestThreadChannel) || type == typeof(IThreadChannel))
                diffType = typeof(ThreadDiffCalculator);
            else
                diffType = typeof(ObjectDiffCalculator<>).MakeGenericType(new Type[] { type });

            var constructor = GetAttributeConstructor(diffType);


            dynamic instance = constructor.Invoke(new object[] { before, after, done, depth + 1 });
            return instance;
        }
    }

    public class MarkConstructor : Attribute
    {
    }

    public class ObjectDiffCalculator<T> : DiffCalculator
    {
        public T Before { get; set; }
        public T After { get; set; }
        public int Depth { get;  }

        [MarkConstructor]
        protected ObjectDiffCalculator(T before, T after, AlreadyDoneSet done, int depth = 0)
        {
            Before = before;
            After = after;
            Depth = depth;
            alreadyDone = done;
            if(after is IEntity<ulong> e)
                alreadyDone.Add(e);
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

        private AlreadyDoneSet alreadyDone;
        public override List<Change> GetChanges()
        {
            if (Depth > 5)
                return new List<Change>();
            var properties = typeof(T).GetProperties();
            var ls = new List<Change>();
            foreach (var property in properties)
            {
                try
                {
                    if (IsPropertyACollection(property))
                        ls.AddRange(getChangesList(property));
                    else
                        ls.AddRange(getChangesSingular(property));
                } catch(TargetInvocationException ex) when (ex.InnerException is NotImplementedException)
                {
                }
            }
            return ls;
        }

        protected bool IsPropertyACollection(PropertyInfo property)
        {
            if (property.PropertyType == typeof(string))
                return false;
            return property.PropertyType.GetInterface(typeof(IEnumerable<>).FullName) != null;
        }

        protected string toStr(IEnumerable list)
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
        protected string toStr(object thing)
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

        protected List<Change> getChangesList(PropertyInfo property)
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
        protected List<Change> getChangesSingular(PropertyInfo property)
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
            if(property.PropertyType == typeof(DateTime))
            {
                var bef = (DateTime)before;
                var aft = (DateTime)after;
                return bef.Ticks == aft.Ticks
                    ? new List<Change>()
                    : new Change(property.Name, toStr(bef), toStr(aft));

            }
            if(property.PropertyType == typeof(DateTimeOffset))
            {
                var bef = (DateTimeOffset)before;
                var aft = (DateTimeOffset)after;
                return bef.Ticks == aft.Ticks
                    ? new List<Change>()
                    : new Change(property.Name, toStr(bef), toStr(aft));
            }
            if(typeof(Discord.IEntity<ulong>).IsAssignableFrom(property.PropertyType))
            {
                var bef = (IEntity<ulong>)before;
                var aft = (IEntity<ulong>)after;
                return bef.Id == aft.Id
                    ? new List<Change>()
                    : new Change(property.Name, toStr(bef), toStr(aft));
            }
            // Complex object, so we want to go deeper.
            var instance = DiffCalculator.Create(property.PropertyType, before, after, alreadyDone, Depth + 1);
            Console.WriteLine(new string(' ', Depth * 3) + $"Deeper dive into {property.Name} of {property.PropertyType.Name}");
            List<Change> changes = instance.GetChanges();
            foreach (var x in changes)
                x.Type = property.Name + "." + x.Type;
            return changes;
        }
    }

    public class AlreadyDoneSet
    {
        private HashSet<ulong> _doneIds = new HashSet<ulong>();

        public void Add(ulong id)
        {
            _doneIds.Add(id);
        }
        public bool Contains(ulong id)
        {
            return _doneIds.Contains(id);
        }
        public void Add<T>(T obj) where T : IEntity<ulong>
        {
            Add(obj.Id);
        }
        public void Add(object obj, out ulong? value)
        {
            value = null;
            if(obj is IEntity<ulong> thing)
            {
                Add(thing);
                value = thing.Id;
            }
        }
        public override string ToString()
        {
            return "[" + string.Join(", ", _doneIds) + "]";
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


    public class ThreadDiffCalculator : ObjectDiffCalculator<IThreadChannel>
    {
        [MarkConstructor]
        protected ThreadDiffCalculator(IThreadChannel before, IThreadChannel after, AlreadyDoneSet alreadyDoneSet, int depth = 0)
            : base(before, after, alreadyDoneSet, depth)
        {

        }

        public override List<Change> GetChanges()
        {
            var type = (Before ?? After).GetType();
            var props = new PropertyInfo[]
            {
                type.GetProperty("Name"),
                type.GetProperty(nameof(IThreadChannel.Joined)),
                type.GetProperty(nameof(IThreadChannel.ArchiveTimestamp)),
                type.GetProperty(nameof(IThreadChannel.AutoArchiveDuration)),
                type.GetProperty(nameof(IThreadChannel.Locked)),
                type.GetProperty(nameof(IThreadChannel.MemberCount)),
                type.GetProperty(nameof(IThreadChannel.MessageCount)),
                type.GetProperty(nameof(IThreadChannel.Archived))
            };
            return props
                .Where(x => x != null)
                .Select(x => getChangesSingular(x))
                .SelectMany(x => x).ToList();
        }
    }
}
