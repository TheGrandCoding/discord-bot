using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ExternalAPIs.Facebook
{
    public class FBPage
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        [JsonPropertyName("category")]
        public string Category { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("tasks")]
        public string[] Tasks { get; set; }
    }
    public class FBPageList
    {
        [JsonPropertyName("data")]
        public FBPage[] Data { get; set; }
    }
}
