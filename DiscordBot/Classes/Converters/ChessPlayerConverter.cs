#if INCLUDE_CHESS
using DiscordBot.Classes.Chess;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Converters
{
    public class ChessPlayerConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(ChessPlayer);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            using var db = Program.Services.GetRequiredService<ChessDbContext>();
            return db.Players.FirstOrDefault(x => x.Id == (int)reader.Value);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is ChessPlayer p)
            {
                var jval = JValue.FromObject(p.Id);
                jval.WriteTo(writer);
            } else if(value is IEnumerable<ChessPlayer> ls)
            {
                var jarray = new JArray();
                foreach (var item in ls)
                    jarray.Add(item.Id);
                jarray.WriteTo(writer);
            }
        }
    }
}
#endif