using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class BotUserConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(BotUser);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var lng = (long)reader.Value;
            var id = Convert.ToUInt64(lng);
            return Program.GetUserOrDefault(id);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is BotUser bUs)
            {
                var jval = new JValue(bUs.Id);
                jval.WriteTo(writer);
            }
        }
    }
}
