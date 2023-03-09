using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class RepublishService : SavedClassService<RepublishSave>
    {
        public bool IsInstagramValid()
        {
            return Data.Facebook?.IsValid() ?? false;
        }
    }
    public enum PublishKind
    {
        DoNotPublish,
        PublishWithText
    }
    public enum ExtendedPublishKind
    {
        DoNotPublish,
        PublishWithText,
        PublishByReference
    }
    public class PublishPost
    {
        public string defaultText { get; set; }
        public string defaultMediaUrl { get; set; }
        [JsonProperty("instagram")]
        public PublishInstagram Instagram { get; set; } = new();
    }
    public class PublishInstagram
    {
        [JsonProperty("originalId")]
        public string OriginalId { get; set; }
        [JsonProperty("caption")]
        public string Caption { get; set; }
        [JsonProperty("mediaUrl")]
        public string MediaUrl { get; set; }

        [JsonProperty("kind")]
        public PublishKind Kind { get; set; }
    }
    public class RepublishSave
    {
        public FacebookAccount Facebook { get; set; } = new();
    }
    public class BaseAccount
    {
        public string Id { get; set; }
        public string Token { get; set; }
        public DateTime ExpiresAt { get; set; }

        public virtual bool IsValid()
        {
            return !(string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Token) || ExpiresAt < DateTime.Now);
        }
    }
    public class FacebookAccount : BaseAccount
    {
        public string PageId { get; set; }
        public string InstagramId { get; set; }

        public override bool IsValid()
        {
            return base.IsValid() && !(string.IsNullOrWhiteSpace(PageId) || string.IsNullOrWhiteSpace(InstagramId));
        }
    }
}
