using DiscordBot.Classes;
using ExternalAPIs;
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
        public bool IsInstagramValid(out bool expired)
        {
            expired = false;
            return Data.Facebook?.IsValid(out expired) ?? false;
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
    
    public class RepublishSave
    {
        public FacebookAccount Facebook { get; set; } = new();
        public DiscordWebhook Discord { get; set; } = new();
    }
    public class BaseAccount
    {
        public string Id { get; set; }
        public string Token { get; set; }
        public DateTime ExpiresAt { get; set; }

        public virtual bool IsValid(out bool expired)
        {
            expired = false;
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Token))
                return false;
            expired = ExpiresAt < DateTime.Now;
            return !expired;
        }
    }
    public class FacebookAccount : BaseAccount
    {
        public string PageId { get; set; }
        public string InstagramId { get; set; }

        public override bool IsValid(out bool expired)
        {
            return base.IsValid(out expired) && !(string.IsNullOrWhiteSpace(PageId) || string.IsNullOrWhiteSpace(InstagramId));
        }
        public FacebookClient CreateClient(HttpClient http)
            => FacebookClient.Create(Token, ExpiresAt, http);
    }
    public class DiscordWebhook : BaseAccount
    {
        public override bool IsValid(out bool expired)
        {
            expired = false;
            // token has webhook URL
            // everything else is irrelevant
            return !string.IsNullOrWhiteSpace(Token);
        }
        public Discord.Webhook.DiscordWebhookClient CreateClient()
        {
            return new(Token);
        }
    }
}
