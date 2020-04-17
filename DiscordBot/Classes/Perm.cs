using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.MLAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static DiscordBot.Perms;

namespace DiscordBot.Classes
{
    [JsonConverter(typeof(JsonPermConverter))]
    public class Perm
    {
        [JsonProperty("p")]
        public string RawNode { get; set; }
        public string Node => RawNode;
        public FieldInfo Field { get; set; }

        public T GetAttribute<T>() where T : PermissionAttribute
        {
            return Field.GetCustomAttribute<T>();
        }

        public bool HasAttr<T>() where T : PermissionAttribute
        {
            return GetAttribute<T>() != null;
        }

        public Perm(FieldInfo info)
        {
            RawNode = (string)info.GetValue(null);
        }

        [JsonConstructor]
        public Perm(string node)
        {
            RawNode = node;
        }
    
        bool grantsPerm(Perm wanted)
        {
            if (wanted.RawNode == Perms.All)
                return true;
            if (wanted.RawNode == this.RawNode)
                return true;
            var hasSplit = this.Node.Split('.');
            var wantedSplit = wanted.Node.Split('.');
            for (int i = 0; i < hasSplit.Length && i < wantedSplit.Length; i++)
            {
                if (hasSplit[i] == "*")
                    return true;
                if (hasSplit[i] != wantedSplit[i])
                    return false;
            }
            return false;
        }

        bool HasPerm(BotUser user)
        {
            if (user == null)
                return false;
            foreach(var node in user.Permissions)
            {
                if (grantsPerm(node))
                    return true;
            }
            return false;
        }

        public bool HasPerm(BotCommandContext context) => HasPerm(context.BotUser);
        public bool HasPerm(APIContext context) => HasPerm(context.User);
    
    }

    public class JsonPermConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Perm);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return Perms.Parse((string)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var perm = (Perm)value;
            var jobj = new JObject(perm.RawNode);
            jobj.WriteTo(writer);
        }
    }

}
