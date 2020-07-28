using DiscordBot.MLAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Converters
{
    public class ErrorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ErrorItem);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is ErrorItem err)
            {
                var jobj = new JObject();
                jobj["path"] = err.endpoint?.Path?.Path ?? "*";
                jobj["reason"] = err.reason;
                jobj.WriteTo(writer);
            }
        }
    }
}
