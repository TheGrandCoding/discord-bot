using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Summary("Testing Commands")]
    public class Testing : BotModule
    {
        public ReactionService React { get; set; }

        [Command("react")]
        public async Task Thing()
        {
            var msg = await ReplyAsync("Testing 123.");
            await msg.AddReactionAsync(Emotes.THUMBS_UP);
            React.Register(msg, EventAction.Added, response, Context.User.Id.ToString());
        }

        [Command("emote")]
        public async Task Emote(IEmote e)
        {
            await ReplyAsync(e.ToString());
        }

        [Command("delete")]
        public async Task Delete()
        {
            await Context.Message.DeleteAsync();
        }

        public static void response(object sender, ReactionEventArgs e)
        {
            e.Message.ModifyAsync(x =>
            {
                x.Content = $"Reacted! Sent in response to {e.State}";
            });
        }
    }
}
