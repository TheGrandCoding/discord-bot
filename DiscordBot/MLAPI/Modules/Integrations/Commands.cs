using Discord;
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
            Program.LogMsg($"Fetched webhook, sending message...");
            await webhook.SendMessageAsync(
                message.Content,
                false,
                message.Embeds == null ? null : message.Embeds.Select(x => x as Embed),
                message.Author.Username,
                message.Author.GetAnyAvatarUrl()
                );
            Program.LogMsg($"Message sent, deleting message...");
            await message.DeleteAsync();
            Program.LogMsg($"Moved {message.Id}");
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
            Program.LogMsg($"Invoked with {amount} {channel}");
            if(amount > 25)
            {
                await ErrorAsync("Cannot move that many messages");
                return;
            }
            var user = Context.User as IGuildUser;
            Program.LogMsg($"Invoked by {user.Username}");
            var from = Context.Channel as ITextChannel;
            Program.LogMsg($"Invoked in {from.Name}");
            var to = Program.Client.GetChannel(channel) as ITextChannel;
            Program.LogMsg($"Moving to {to.Name}");
            var fromPerms = user.GetPermissions(from);
            var toPerms = user.GetPermissions(to);
            if(!(fromPerms.ManageMessages && toPerms.ManageMessages))
            {
                await ErrorAsync("You do not have permission to move messages between those channels");
                return;
            }
            Program.LogMsg($"Fetching messages...");
            var _m = await from.GetMessagesAsync((int)amount).FlattenAsync();
            Program.LogMsg($"Fetched {_m.Count()} messages; sending ACK with source.");
            await AcknowledgeAsync(true);
            Program.LogMsg($"Starting move of messages...");
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
