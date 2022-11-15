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

    public class LazyInventoryItem : InventoryItem
    {
        public LazyInventoryItem(int id)
        {
            Id = id;
            Program.LogDebug($"Init with {Id}", "LazyInvItem");
        }
        private InventoryItem _item;

        private InventoryItem Item { get
            {
                if(_item == null)
                {
                    Program.LogDebug($"Fetch with {Id}", "LazyInvItem");
                    var srv = Program.Services.GetRequiredService<FoodService>();
                    _item = srv.GetInventoryItem(Id);
                }
                return _item;
            } }

        public override string InventoryId => Item.InventoryId;
        public override Product Product => Item.Product;
        public override string ProductId => Item.ProductId;
        public override DateTime AddedAt => Item.AddedAt;
        public override DateTime InitialExpiresAt => Item.InitialExpiresAt;
        public override bool Frozen => Item.Frozen;
        public override int TimesUsed => Item.TimesUsed;

    }


    public class InventoryItemConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(InventoryItem);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null) return null;
            int x = int.Parse((string)reader.Value);
            return new LazyInventoryItem(x);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is InventoryItem i)
            {
                writer.WriteValue($"{i.Id}");
            } else if(value is Lazy<InventoryItem> li)
            {
                var v = li.Value;
            }
        }
    }
}
