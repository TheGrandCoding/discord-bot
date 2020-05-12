using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            throw new NotSupportedException($"Cannot write {value.GetType().FullName}");
        }
    }
}
