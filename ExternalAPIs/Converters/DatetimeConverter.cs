using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ExternalAPIs.Converters
{
    public class DatetimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // 2023-03-08T22:15:29+0000
            // yyyy-MM-ddThh:mm:ss+0000
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (DateTime.TryParse(reader.GetString(), out var d)) return d;
            return null;
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime? dateTimeValue,
            JsonSerializerOptions options) => throw new NotImplementedException();
    }
}
