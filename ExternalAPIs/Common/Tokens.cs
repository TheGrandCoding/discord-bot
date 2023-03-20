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
        public OAuthToken(string accessToken, DateTime? expiresAt, string? tokenType = null)
        {
            AccessToken = accessToken;
            TokenType = tokenType;
            ExpiresAt = expiresAt;
        }
        [JsonConstructor]
        public OAuthToken(string accessToken, int? expiresIn, string? tokenType = null) 
            : this(accessToken, DateTime.Now.AddSeconds(expiresIn.GetValueOrDefault(3600)), tokenType)
        {
            expires_in = expiresIn;
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
        public IGOAuthToken(string accessToken, DateTime expiresAt, string? tokenType = null) : base(accessToken, expiresAt, tokenType)
        {
        }
        [JsonConstructor]
        public IGOAuthToken(string accessToken, int? expiresIn, string? tokenType = null) : base(accessToken, expiresIn, tokenType)
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
        public TikTokOAuthToken(string accessToken, int? expiresIn, string refreshToken, int? refreshExpiresIn) : base(accessToken, expiresIn, null)
        {
            RefreshToken = refreshToken;
            RefreshExpiresIn = refreshExpiresIn;
        }
        [JsonPropertyName("open_id")]
        public string OpenId { get; set; }
        [JsonPropertyName("scope")]
        public string Scope { get; set; }
        [JsonPropertyName("refresh_token")]
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