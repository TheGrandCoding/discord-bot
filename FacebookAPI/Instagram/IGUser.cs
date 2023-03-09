using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FacebookAPI.Instagram
{
    public enum IGAccountType
    {
        Personal,
        MediaCreator,
        Business
    }
    [Flags]
    public enum IGUserFields
    {
        Id              = 0b00001,
        MediaCount      = 0b00010,
        AccountType     = 0b00100,
        Username        = 0b01000,
        Media           = 0b10000,

        All = Id | MediaCount | AccountType | Username | Media
    }
    public class IGUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("media_count")]
        public int? MediaCount { get; set; }
        [JsonPropertyName("account_type")]
        [JsonConverter(typeof(Converters.IGAccountTypeConverter))]
        public IGAccountType? AccountType { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonConverter(typeof(Converters.IGMediaConverter))]
        [JsonPropertyName("media")]
        public string[] MediaIds { get; set; }
    }
}
