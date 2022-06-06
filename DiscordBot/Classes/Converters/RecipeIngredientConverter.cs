using DiscordBot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes.Converters
{
    public class RecipeIngredientConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(SavedIngredient);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(reader.TokenType == JsonToken.Integer)
                return new SavedIngredient((int)(long)reader.Value, false);
            var j = JToken.ReadFrom(reader) as JObject;
            return new SavedIngredient(j.GetValue("a").ToObject<int>(), j.GetValue("f").ToObject<bool>());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is SavedIngredient si)
            {
                if(si.Frozen)
                {
                    var j = new JObject();
                    j["a"] = si.Amount;
                    j["f"] = si.Frozen;
                    j.WriteTo(writer);
                } else
                {
                    writer.WriteValue(si.Amount);
                }
            }
        }
    }
}
