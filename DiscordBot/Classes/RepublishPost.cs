using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
    public class PublishPost
    {
        public uint Id { get; set; }
        public uint? AuthorId { get; set; }
        public uint? ApprovedById { get; set; }

        [JsonProperty("defaultText")]
        public string DefaultText { get; set; }
        [JsonProperty("defaultMediaUrl")]
        public string DefaultMediaUrl { get; set; }
        [JsonProperty("instagram")]
        public PublishInstagram Instagram { get; set; } = new();
        [JsonProperty("discord")]
        public PublishDiscord Discord { get; set; } = new();

        public APIErrorResponse GetErrors()
        {
            var errors = new APIErrorResponse();
            if (string.IsNullOrWhiteSpace(DefaultText))
                errors.Child(nameof(DefaultText)).WithRequired();
            if (string.IsNullOrWhiteSpace(DefaultMediaUrl))
                errors.Child(nameof(DefaultMediaUrl)).WithRequired();
            if (Instagram.Kind != PublishKind.DoNotPublish)
            {
                var insta = errors.Child(nameof(Instagram));
                if (string.IsNullOrWhiteSpace(Instagram.Caption ?? DefaultText))
                    insta.Child("caption").WithRequired();
                if (string.IsNullOrWhiteSpace(Instagram.MediaUrl ?? DefaultMediaUrl))
                    insta.Child("mediaUrl").WithRequired();
            }
            if (Discord.Kind != PublishKind.DoNotPublish)
            {
                var ds = errors.Child(nameof(Discord));
                if (string.IsNullOrWhiteSpace(Discord.Caption ?? DefaultText))
                    return ds.Child("caption").WithRequired();
                if (string.IsNullOrWhiteSpace(Discord.MediaUrl ?? DefaultMediaUrl))
                    return ds.Child("mediaUrl").WithRequired();
            }
            if(errors.HasAnyErrors())
                return errors;
            return null;
        }
    }
    [Owned]
    public class PublishBase
    {
        [JsonProperty("caption")]
        public string Caption { get; set; }
        [JsonProperty("mediaUrl")]
        public string MediaUrl { get; set; }

        [JsonProperty("kind")]
        public PublishKind Kind { get; set; }
    }
    [Owned]
    public class PublishInstagram : PublishBase
    {
        [JsonProperty("originalId")]
        public string OriginalId { get; set; }
    }
    [Owned]
    public class PublishDiscord : PublishBase
    {
    }
}
