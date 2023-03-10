using DiscordBot.Classes;
using FacebookAPI;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        public override void OnReady(IServiceProvider services)
        {
            base.OnReady(services);
            /*if(IsInstagramValid())
            {
                Task.Run(async () =>
                {
                    var http = services.GetRequiredService<HttpClient>();
                    var fb = FacebookClient.Create(Data.Facebook.Token, Data.Facebook.ExpiresAt, http);
                    var media = await fb.GetUserMediaAsync(Data.Facebook.InstagramId);
                    var us = await fb.GetMeAsync();

                    var container = await fb.CreateIGMediaContainer(Data.Facebook.InstagramId, "https://i.imgur.com/vFv4k6x.png", "test post");
                    var mediaId = await fb.PublishIGMediaContainer(Data.Facebook.InstagramId, container);

                    await Task.CompletedTask;
                });
            }*/
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

        public APIErrorResponse GetErrors()
        {
            var errors = new APIErrorResponse();
            if (string.IsNullOrWhiteSpace(defaultText))
                return errors.Child(nameof(defaultText)).EndRequired();
            if (string.IsNullOrWhiteSpace(defaultMediaUrl))
                return errors.Child(nameof(defaultMediaUrl)).EndRequired();
            var insta = errors.Child(nameof(Instagram));
            if (string.IsNullOrWhiteSpace(Instagram.Caption))
                return insta.Child("caption").EndRequired();
            if (string.IsNullOrWhiteSpace(Instagram.MediaUrl))
                return insta.Child("mediaUrl").EndRequired();
            return null;
        }
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
        public FacebookClient CreateClient(HttpClient http)
            => FacebookClient.Create(Token, ExpiresAt, http);
    }
}
