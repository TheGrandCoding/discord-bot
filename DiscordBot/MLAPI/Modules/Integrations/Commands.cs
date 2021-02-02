﻿using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Integrations
{
    public class Commands : InteractionBase
    {
        public Commands(InteractionCommandContext context) : base(context)
        {
            Webhooks = Program.Services.GetRequiredService<WebhookService>();
        }
        public WebhookService Webhooks { get; }

        [Id(790595185910087722)]
        public async Task Ping(string message = null)
        {
            await ReplyAsync(message == null ? "Pong!" : message);
        }

        [Id(790917803087495168)]
        public async Task Ephemeral(string message = null)
        {
            await ReplyAsync(message == null ? "Test ephemeral message" : message, flags: InteractionResponseFlags.Ephemeral);
        }

        [Id(790593912271273995)]
        public async Task Test()
        {
            await ReplyAsync("Test command success", embed: new EmbedBuilder()
                .WithTitle("Hello!")
                .WithDescription("What even means this")
                .WithColor(Color.Green)
                .Build());
        }

        [Id(790929406008098836)]
        [Id(790933585812652042)] // global
        public async Task Translate(string message, string language = null)
        {
            string fromLanguage = null;
            if(language != null)
            {
                fromLanguage = LanguageCodesUtils.ToCode(language);
                if(fromLanguage == null)
                {
                    await ReplyAsync($":x: Language not recognised.", flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
            }
            var client = TranslationClient.Create();
            var response = await client.TranslateTextAsync(message, LanguageCodes.English, fromLanguage);
            var actualFrom = response.DetectedSourceLanguage == null ? fromLanguage : response.DetectedSourceLanguage;
            var name = LanguageCodesUtils.ToName(actualFrom);
            await ReplyAsync($"Translated from {name}:\r\n>>> {response.TranslatedText}", flags: InteractionResponseFlags.Ephemeral);
        }

        public async Task Move(IUserMessage message, ITextChannel to)
        {
            var webhook = await Webhooks.GetWebhookClientAsync(to);
            var embeds = new List<Embed>();
            if (message.Embeds != null)
                foreach (var em in message.Embeds)
                    embeds.Add((Embed)em);
            if(message.Attachments != null)
            {
                foreach(var x in message.Attachments)
                {
                    embeds.Add(new EmbedBuilder()
                        .WithTitle(x.Filename)
                        .WithUrl(x.Url)
                        .WithImageUrl(x.Url)
                        .WithFooter("Note: This file link may not work.")
                        .Build());
                }
            }
            await webhook.SendMessageAsync(
                message.Content,
                false,
                embeds,
                message.Author.Username,
                message.Author.GetAnyAvatarUrl()
                );
            await message.DeleteAsync();
        }

        /*public async Task Move(ulong message, ulong channel)
        {
            var from = Context.Channel;
            var to = Program.Client.GetChannel(channel) as ITextChannel;
            var msg = await from.GetMessageAsync(message);
            if(msg == null)
            {
                await ErrorAsync("Message was not found");
                return;
            }
            if(!(msg is IUserMessage umsg))
            {
                await ErrorAsync("Cannot move those types of messages");
                return;
            }
            await AcknowledgeAsync();
            await Move(umsg, to);
        }*/


        [Id(806096572546023474)]
        public async Task MoveBatch(int amount, ulong channel)
        {
            if(amount > 25)
            {
                await ErrorAsync("Cannot move that many messages");
                return;
            }
            var user = await Context.Guild.GetUserAsync(Context.User.Id);
            var from = Context.Channel as ITextChannel;
            var to = Program.Client.GetChannel(channel) as ITextChannel;
            var fromPerms = user.GetPermissions(from);
            var toPerms = user.GetPermissions(to);
            if(!(fromPerms.ManageMessages && toPerms.ManageMessages))
            {
                await ErrorAsync("You do not have permission to move messages between those channels");
                return;
            }
            var _m = await from.GetMessagesAsync((int)amount).FlattenAsync();
            await AcknowledgeAsync(true);
            foreach(var msg in _m.OrderBy(x => x.CreatedAt))
            {
                if(msg is IUserMessage umsg)
                {
                    await Move(umsg, to);
                }
            }
        }
    }
}
