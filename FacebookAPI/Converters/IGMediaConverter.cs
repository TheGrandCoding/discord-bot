using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FacebookAPI.Converters
{
    public class IGMediaConverter : JsonConverter<string[]>
    {
        public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var current = JsonNode.Parse(ref reader) as JsonObject;
            var data = current["data"] as JsonArray;
            return data.Select(x =>
            {
                var child = x as JsonObject;
                var id = child["id"];
                return id.ToString();
            }).ToArray();
        }

        public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
