﻿using Discord;
using Discord.Webhook;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes.Rules
{
    public class BanAppeal
    {
        [JsonIgnore]
        public BotUser Appellant
        {
            get
            {
                _user ??= Program.GetUserOrDefault(userId);
                return _user;
            }
            set
            {
                _user = value;
                userId = value?.Id ?? 0;
            }
        }

        [JsonRequired]
        private ulong userId;
        private BotUser _user;

        public IGuild Guild { get; set; }
        public ITextChannel AppealChannel { get; set; }
        public DateTime? MutedUntil { get; set; }
        

        private IBan _ban;
        [JsonIgnore]
        public IBan Ban { get
            {
                _ban ??= Guild.GetBanAsync(Appellant.Id).Result;
                return _ban;
            } }
        [JsonIgnore]
        public bool IsMuted {  get
            {
                if (!MutedUntil.HasValue)
                    return false;
                if(MutedUntil.Value < DateTime.Now)
                {
                    MutedUntil = null;
                    return false;
                }
                return true;
            } }

        private DiscordWebhookClient _webhook;
        [JsonIgnore]
        public DiscordWebhookClient Webhook {  get
            {
                if(_webhook == null)
                {
                    var whs = AppealChannel.GetWebhooksAsync().Result;
                    var wb = whs.FirstOrDefault(x => x.Creator.Id == Program.Client.CurrentUser.Id);
                    if(wb == null)
                    {
                        wb = AppealChannel.CreateWebhookAsync("mlapi-appeal").Result;
                    }
                    _webhook = new DiscordWebhookClient(wb);
                }
                return _webhook;
            } }

        public async Task SendMessageAsync(string message, string authorName, string authorAvatar)
        {
            await Webhook.SendMessageAsync(message, username: authorName, avatarUrl: authorAvatar, allowedMentions: AllowedMentions.None);
        }
    }
}
