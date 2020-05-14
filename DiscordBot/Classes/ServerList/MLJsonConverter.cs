using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DiscordBot.Classes.ServerList
{
    public class MLJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Player);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(Player))
                return Player.FromJson(reader);
            throw new NotSupportedException($"Cannot read {objectType.FullName}");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Player p)
                p.ToJson(writer);
            else
                throw new NotSupportedException($"Cannot write {value.GetType().FullName}");
        }
    }

    public class IPConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IPAddress);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var s = (string)reader.Value;
            if (IPAddress.TryParse(s, out var a))
                return a;
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is IPAddress a)
            {
                var jval = new JValue(a.ToString());
                jval.WriteTo(writer);
            }
        }
    }

}
