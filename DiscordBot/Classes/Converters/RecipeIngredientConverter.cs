using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
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


    public class InventoryItemConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(InventoryItem);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var sv = Program.Services.GetRequiredService<FoodService>();
            int x = int.Parse((string)reader.Value);
            return sv.GetInventoryItem(x);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is InventoryItem i)
            {
                writer.WriteValue($"{i.Id}");
            }
        }
    }
}
