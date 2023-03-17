using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ExternalAPIs.TikTok
{
    [Flags]
    public enum TikTokUserFields
    {
        OpenId      = 0b01,
        DisplayName = 0b10
    }
    public class TikTokUser
    {
        [JsonPropertyName("open_id")]
        public string OpenId { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }
    }
}
