﻿using Discord;
using Discord.Webhook;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
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
        public BotDbUser Appellant
        {
            get
            {
                if(_user == null)
                {
                    using var db = Program.GlobalServices.GetBotDb("BanAppealGet");
                    _user = db.GetUserAsync(userId).Result;
                }
                return _user;
            }
            set
            {
                _user = value;
                userId = value?.Id ?? 0;
            }
        }

        [JsonRequired]
        private uint userId;
        private BotDbUser _user;

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
                    var srv = Program.GlobalServices.GetRequiredService<WebhookService>();
                    _webhook = srv.GetWebhookClientAsync(AppealChannel).Result;
                }
                return _webhook;
            } }

        public async Task SendMessageAsync(string message, string authorName, string authorAvatar)
        {
            await Webhook.SendMessageAsync(message, username: authorName, avatarUrl: authorAvatar, allowedMentions: AllowedMentions.None);
        }
    }
}
