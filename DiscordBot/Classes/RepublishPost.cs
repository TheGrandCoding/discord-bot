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
        [NotMapped]
        [JsonIgnore]
        public PublishTikTok TikTok => Platforms.First(x => x.Platform == PublishPlatform.TikTok) as PublishTikTok;


        public APIErrorResponse GetErrors()
        {
            var errors = new APIErrorResponse();
            if (Default == null)
                return errors.Child(nameof(Default)).EndRequired();
            if (Instagram == null)
                errors.Child(nameof(Instagram)).WithRequired();
            if (Discord == null)
                errors.Child(nameof(Discord)).WithRequired();
            if (TikTok == null)
                errors.Child(nameof(TikTok)).WithRequired();
            foreach (var p in Platforms)
            {
                if(p.Platform != PublishPlatform.Default)
                {
                    if (p.Kind == PublishKind.DoNotPublish) continue;
                }
                var err = errors.Child(p.Platform.ToString().ToLower());
                if (string.IsNullOrWhiteSpace(p.Caption ?? Default.Caption))
                    err.Child("caption").WithRequired();
                if (p.Media == null || p.Media.Count == 0)
                {
                    err.Child("media").WithRequired();
                }
                else
                {
                    var m = err.Child("media");
                    var media = p.Media;
                    if (media == null || media.Count == 0)
                        media = Default.Media;
                    for (int i = 0; i < media.Count; i++)
                    {
                        var merr = m.Child(i);
                        if (p.Platform == PublishPlatform.TikTok && media[i].Type != MediaType.Video)
                            merr.Child("type").WithChoices("VIDEO");
                        merr.Extend(media[i].GetErrors());
                    }
                    var duplicates = media.Select(x => x.RemoteUrl).Distinct().Count();
                    if(duplicates < media.Count)
                    {
                        m.WithError("DUPLICATE", "Media URLs must be unique");
                    }
                }
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
    [JsonSubtypes.KnownSubType(typeof(PublishTikTok), PublishPlatform.TikTok)]
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

        [JsonProperty("sentId")]
        public string SentId { get; set; }
        [JsonProperty("originalId")]
        public string OriginalId { get; set; }
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
    }
    public class PublishDiscord : PublishBase
    {
        [JsonProperty("platform")]
        public override PublishPlatform Platform => PublishPlatform.Discord;
    }
    public class PublishTikTok : PublishBase
    {
        [JsonProperty("platform")]
        public override PublishPlatform Platform => PublishPlatform.TikTok;
    }


    public class PublishMedia
    {
        [Key]
        public uint Id { get; set; }
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
