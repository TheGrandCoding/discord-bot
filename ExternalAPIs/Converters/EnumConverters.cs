using ExternalAPIs.Instagram;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ExternalAPIs.Converters
{
    public class IGAccountTypeConverter : JsonConverter<IGAccountType>
    {
        public override IGAccountType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch(reader.GetString()!.ToLower())
            {
                case "business":
                    return IGAccountType.Business;
                case "media_creator":
                    return IGAccountType.MediaCreator;
                default:
                    return IGAccountType.Personal;
            }
        }

        public override void Write(Utf8JsonWriter writer, IGAccountType value, JsonSerializerOptions options)
        {
            switch(value)
            {
                case IGAccountType.Business:
                    writer.WriteStringValue("BUSINESS");
                    break;
                case IGAccountType.MediaCreator:
                    writer.WriteStringValue("MEDIA_CREATOR");
                    break;
                default:
                    writer.WriteStringValue("PERSONAL");
                    break;
            }
        }
    }
}
