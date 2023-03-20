using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ExternalAPIs.TikTok
{
    internal class TikTokVideoList
    {
        public TikTokVideo[] videos { get; set; }
        public ulong cursor { get; set; }
        public bool has_more { get; set; }
    }
    [Flags]
    public enum TikTokVideoFields
    {
        
        Id              = 1 << 1,
        CreateTime      = 1 << 2,
        CoverImageUrl   = 1 << 3,
        ShareUrl        = 1 << 4,
        VideoDescription= 1 << 5,
        Duration        = 1 << 6,
        Height          = 1 << 7,
        Width           = 1 << 8,
        Title           = 1 << 9,
        EmbedHtml       = 1 << 10,
        EmbedLink       = 1 << 11,
        LikeCount       = 1 << 12,
        CommentCount    = 1 << 13,
        ShareCount      = 1 << 14,
        ViewCount       = 1 << 15,

        All = Id 
            | CreateTime 
            | CoverImageUrl 
            | ShareUrl 
            | VideoDescription
            | Duration
            | Height
            | Width
            | Title
            | EmbedHtml
            | EmbedLink
            | LikeCount
            | CommentCount
            | ShareCount
            | ViewCount
    }
    public class TikTokVideo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        [JsonPropertyName("create_time")]
        public ulong? CreationTimestamp { get; set; }
        [JsonPropertyName("cover_image_url")]
        public string? CoverImageUrl { get; set; }
        [JsonPropertyName("share_url")]
        public string? ShareUrl { get; set; }
        [JsonPropertyName("video_description")]
        public string VideoDescription { get; set; }
        [JsonPropertyName("duration")]
        public int? Duration { get; set; }
        [JsonPropertyName("height")]
        public int? Height { get; set; }
        [JsonPropertyName("width")]
        public int? Width{ get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("embed_html")]
        public string? EmbedHTML { get; set; }
        [JsonPropertyName("embed_link")]
        public string? EmbedLink { get; set; }


    }
}
