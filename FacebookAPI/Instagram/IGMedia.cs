using FacebookAPI.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FacebookAPI.Instagram
{
    [Flags]
    public enum IGMediaFields
    {
        Caption         = 0b000000001,
        Id              = 0b000000010,
        IsSharedToFeed  = 0b000000100,
        MediaType       = 0b000001000,
        MediaUrl        = 0b000010000,
        Permalink       = 0b000100000,
        ThumbnailUrl    = 0b001000000,
        Timestamp       = 0b010000000,
        Username        = 0b100000000,

        All = Caption|Id|IsSharedToFeed|MediaType|MediaUrl|Permalink|ThumbnailUrl|Timestamp|Username
    }
    public class IGMedia
    {
        [JsonPropertyName("caption")]
        public string? Caption {get; set;}
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("is_shared_to_feed")]
        public string? IsSharedToFeed { get; set; }
        [JsonPropertyName("media_type")]
        public string? MediaType { get; set; }
        [JsonPropertyName("media_url")]
        public string? MediaUrl { get; set; }
        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }
        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }
        [JsonPropertyName("timestamp")]
        [JsonConverter(typeof(DatetimeConverter))]
        public DateTime? Timestamp { get; set; }
        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }
}
