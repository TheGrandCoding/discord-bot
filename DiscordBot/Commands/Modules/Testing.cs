using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Summary("Testing Commands")]
    public class Testing : BotBase
    {
        public ReactionService React { get; set; }

        [Command("react")]
        public async Task Thing()
        {
            var msg = await ReplyAsync("Testing 123.");
            await msg.AddReactionAsync(Emotes.THUMBS_UP);
            React.Register(msg, EventAction.Added, response, Context.User.Id.ToString());
        }

        [Command("buttons")]
        public async Task Buttons()
        {
            ComponentBuilder builder = new ComponentBuilder();
            builder.WithButton("Test button 1", "btn1", ButtonStyle.Danger);
            builder.WithButton("Test button 2", "btn2", ButtonStyle.Danger);
            var msg = await ReplyAsync("Click button below", component: builder.Build());
            var srv = Program.Services.GetRequiredService<MessageComponentService>();
            srv.Register(msg, handleButton);
        }
        public static async Task handleButton(CallbackEventArgs args)
        {
            var token = args.Interaction;
            await token.RespondAsync(text: $"Clicked {args.ComponentId}", type: InteractionResponseType.UpdateMessage);
        }

        [Command("emote")]
        public async Task Emote(IEmote e)
        {
            await ReplyAsync(e.ToString());
        }

        [Command("delete")]
        public async Task Delete()
        {
            await Context.Message.DeleteAndTrackAsync("test");
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
