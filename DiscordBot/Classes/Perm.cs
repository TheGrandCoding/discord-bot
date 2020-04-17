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

        public string Description => GetAttribute<Description>().Value;

        T getAttrInParent<T>(Type parent) where T : PermissionAttribute
        {
            if (parent == null)
                return null;
            return parent.GetCustomAttribute<T>() ?? getAttrInParent<T>(parent.DeclaringType);
        }

        public T GetAttribute<T>(bool inherit = true) where T : PermissionAttribute
        {
            var attr = Field.GetCustomAttribute<T>();
            if (attr == null && inherit)
                attr = getAttrInParent<T>(Field.DeclaringType);
            return attr;
        }

        public bool HasAttr<T>(bool inherit = true) where T : PermissionAttribute
        {
            return GetAttribute<T>(inherit) != null;
        }

        public Perm(FieldInfo info)
        {
            Field = info;
            RawNode = (string)info.GetValue(null);
        }

        [JsonConstructor]
        private Perm(string node)
        {
            RawNode = node;
        }
    
        bool grantsPerm(Perm hasNode, out bool inherited)
        {
            inherited = false;
            if (this.RawNode == hasNode.RawNode)
                return true;
            if (this.RawNode == Perms.All)
                return false;
            inherited = true;
            var hasSplit = hasNode.Node.Split('.');
            var wantedSplit = this.Node.Split('.');
            for (int i = 0; i < hasSplit.Length && i < wantedSplit.Length; i++)
            {
                if (hasSplit[i] == "*")
                    return true;
                if (hasSplit[i] != wantedSplit[i])
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the <see cref="BotUser"/> has this permission
        /// </summary>
        /// <param name="inheritsPerm">If true, user recieves perm from wildcard, and does not have it directly</param>
        /// <returns></returns>
        public bool UserHasPerm(BotUser user, out bool inheritsPerm)
        {
            inheritsPerm = false;
            if (user == null)
                return false;
            foreach(var node in user.Permissions)
            {
                if (grantsPerm(node, out inheritsPerm))
                    return true;
            }
            return false;
        }

        public bool UserHasPerm(BotUser user) => UserHasPerm(user, out _);

        public bool HasPerm(BotCommandContext context) => UserHasPerm(context.BotUser);
        public bool HasPerm(APIContext context) => UserHasPerm(context.User);
    
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
            var jval = new JValue(perm.RawNode);
            jval.WriteTo(writer);
        }
    }

}
