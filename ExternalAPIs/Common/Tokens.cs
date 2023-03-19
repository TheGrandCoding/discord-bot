using System.Text.Json.Serialization;

namespace ExternalAPIs
{
    public class OAuthResponse
    {

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
        [JsonPropertyName("expires_in")]
        public int? expires_in { get; set; }
    }
    public class OAuthToken
    {
        public OAuthToken(string access_token, DateTime expires_at, string? token_type = null)
        {
            AccessToken = access_token;
            TokenType = token_type;
            ExpiresAt = expires_at;
        }
        [JsonConstructor]
        public OAuthToken(string access_token, int expires_in, string? token_type = null) 
            : this(access_token, DateTime.Now.AddSeconds(expires_in), token_type)
        {
        }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; internal set; }

        private int? expires_in;
        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get
            {
                return expires_in;
            } set
            {
                expires_in = value;
                if (value.HasValue)
                    ExpiresAt = DateTime.Now.AddSeconds(value.Value);
            }
        }
    }
    public class IGOAuthToken : OAuthToken
    {
        public IGOAuthToken(string access_token, DateTime expires_at, string? token_type = null) : base(access_token, expires_at, token_type)
        {
        }
        [JsonConstructor]
        public IGOAuthToken(string access_token, int expires_in, string? token_type = null) : base(access_token, expires_in, token_type)
        {
        }

        [JsonPropertyName("user_id")]
        public ulong UserId { get; set; }
    }
    public class TikTokOAuthToken : OAuthToken
    {
        public TikTokOAuthToken(string access_token, DateTime expires_at, string refresh_token, DateTime refresh_expires_at, string? token_type = null) : base(access_token, expires_at, token_type)
        {
            RefreshToken = refresh_token;
            RefreshExpiresAt = refresh_expires_at;
        }
        [JsonConstructor]
        public TikTokOAuthToken(string access_token, int expires_in, string refresh_token, int refresh_expires_in) : base(access_token, expires_in, null)
        {
            RefreshToken = refresh_token;
            RefreshExpiresIn = refresh_expires_in;
        }
        public string OpenId { get; set; }
        public string Scope { get; set; }
        public string RefreshToken { get; set; }
        [JsonPropertyName("refresh_expires_at")]
        public DateTime? RefreshExpiresAt { get; internal set; }
        private int? refresh_expires_in;
        [JsonPropertyName("refresh_expires_in")]
        public int? RefreshExpiresIn
        {
            get
            {
                return refresh_expires_in;
            }
            set
            {
                refresh_expires_in = value;
                if (value.HasValue)
                    RefreshExpiresAt = DateTime.Now.AddSeconds(value.Value);
            }
        }
    }
}