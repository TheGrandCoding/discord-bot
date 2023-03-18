using JsonSubTypes;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
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
    public enum MediaType
    {
        Image,
        Video
    }
    public enum PublishPlatform
    {
        Default,
        Instagram,
        Discord,
        TikTok
    }
    public class PublishPost
    {
        public uint Id { get; set; }
        public uint? AuthorId { get; set; }
        public uint? ApprovedById { get; set; }

        [JsonProperty("platforms")]
        public List<PublishBase> Platforms { get; set; } = new();

        [NotMapped]
        [JsonIgnore]
        public PublishDefault Default => Platforms.First(x => x.Platform == PublishPlatform.Default) as PublishDefault;
        [NotMapped]
        [JsonIgnore]
        public PublishInstagram Instagram => Platforms.First(x => x.Platform == PublishPlatform.Instagram) as PublishInstagram;
        [NotMapped]
        [JsonIgnore]
        public PublishDiscord Discord => Platforms.First(x => x.Platform == PublishPlatform.Discord) as PublishDiscord;


        public APIErrorResponse GetErrors()
        {
            var errors = new APIErrorResponse();
            var defText = Default?.Caption ?? null;
            if (string.IsNullOrWhiteSpace(defText))
                errors.Child("defaultText").WithRequired();
            var defMedia = Default?.Media ?? null;
            if (defMedia == null || defMedia.Count == 0)
                errors.Child("defaultMedia").WithRequired();
            if (Instagram == null)
                errors.Child(nameof(Instagram)).WithRequired();
            else if (Instagram.Kind != PublishKind.DoNotPublish)
            {
                var insta = errors.Child(nameof(Instagram));
                if (string.IsNullOrWhiteSpace(Instagram.Caption ?? defText))
                    insta.Child("caption").WithRequired();
                var med = insta.Child("media");
                for(int i = 0; i < Instagram.Media.Count; i++)
                {
                    var err = med.Child(i);
                    err.Extend(Instagram.Media[i].GetErrors());
                }
                if ((Instagram?.Media?.Count ?? 0) == 0)
                    med.WithRequired();
            }
            if (Discord == null)
                errors.Child(nameof(Discord)).WithRequired();
            else if (Discord.Kind != PublishKind.DoNotPublish)
            {
                var ds = errors.Child(nameof(Discord));
                if (string.IsNullOrWhiteSpace(Discord.Caption ?? defText))
                    return ds.Child("caption").WithRequired();
                var med = ds.Child("media");
                for (int i = 0; i < Discord.Media.Count; i++)
                {
                    var err = med.Child(i);
                    err.Extend(Discord.Media[i].GetErrors());
                }
                if ((Discord?.Media?.Count ?? 0) == 0)
                    med.WithRequired();
            }
            if(errors.HasAnyErrors())
                return errors.Build();
            return null;
        }
    }
    [JsonConverter(typeof(JsonSubtypes), nameof(PublishBase.Platform))]
    [JsonSubtypes.KnownSubType(typeof(PublishDefault), PublishPlatform.Default)]
    [JsonSubtypes.KnownSubType(typeof(PublishInstagram), PublishPlatform.Instagram)]
    [JsonSubtypes.KnownSubType(typeof(PublishDiscord), PublishPlatform.Discord)]
    public abstract class PublishBase
    {
        [JsonProperty("postId")]
        public uint PostId { get; set; }
        [JsonProperty("platform")]
        [JsonConverter(typeof(StringEnumConverter))]
        public virtual PublishPlatform Platform { get; protected set; }
        [JsonProperty("caption")]
        public string Caption { get; set; }
        [JsonProperty("media")]
        public List<PublishMedia> Media { get; set; }

        [JsonProperty("kind")]
        [JsonConverter(typeof(StringEnumConverter))]
        public PublishKind Kind { get; set; }
    }
    public class PublishDefault : PublishBase
    {
        [JsonProperty("platform")]
        public override PublishPlatform Platform => PublishPlatform.Default;
    }
    public class PublishInstagram : PublishBase
    {
        [JsonProperty("platform")]
        public override PublishPlatform Platform => PublishPlatform.Instagram;
        [JsonProperty("originalId")]
        public string OriginalId { get; set; }
    }
    public class PublishDiscord : PublishBase
    {
        [JsonProperty("platform")]
        public override PublishPlatform Platform => PublishPlatform.Discord;
    }


    public class PublishMedia
    {
        [JsonProperty("postId")]
        public uint PostId { get; set; }
        [JsonProperty("platform")]
        public PublishPlatform Platform { get; set; }

        [JsonProperty("type")]
        public MediaType Type { get; set; }
        [JsonProperty("remoteUrl")]
        public string RemoteUrl { get; set; }
        [JsonProperty("localPath")]
        public string LocalPath { get; set; }

        public APIErrorResponse GetErrors()
        {
            var errors = new APIErrorResponse();
            if (string.IsNullOrWhiteSpace(RemoteUrl))
                errors.Child(nameof(RemoteUrl)).EndRequired();
            if (!Uri.TryCreate(RemoteUrl, UriKind.Absolute, out _))
                errors.Child(nameof(RemoteUrl)).EndError("INVALID", "Not a valid URI");
            if (errors.HasAnyErrors())
                return errors;
            return null;
        }

    }
}
