using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class BotDbUserConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(BotDbUser);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var _int = (int)reader.Value;
            var id = Convert.ToUInt32(_int);
            using var db = BotDbContext.Get();
            return db.GetUserAsync(id).Result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is BotDbUser bUs)
            {
                var jval = new JValue(bUs.Id);
                jval.WriteTo(writer);
            }
        }
    }
}
