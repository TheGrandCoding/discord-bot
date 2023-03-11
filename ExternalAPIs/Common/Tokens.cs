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
        public string AccessToken { get; }
        [JsonPropertyName("token_type")]
        public string? TokenType { get; }
        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; }
    }
    public class IGOAuthToken : OAuthToken
    {
        public IGOAuthToken(string access_token, DateTime expires_at, string? token_type = null) : base(access_token, expires_at, token_type)
        {
        }
        public IGOAuthToken(string access_token, int expires_in, string? token_type = null) : base(access_token, expires_in, token_type)
        {
        }

        [JsonPropertyName("user_id")]
        public ulong UserId { get; set; }
    }
}