using DiscordBot.Services.Sonarr;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Converters
{
    public class SonarrHistoryConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(SonarrHistoryRecord);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.ReadFrom(reader);
            if (obj["eventType"].ToObject<string>() == "grabbed")
                return obj.ToObject<SonarrHistoryGrabbedRecord>();
            return obj.ToObject<SonarrHistoryGenericRecord>();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
