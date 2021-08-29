using Discord;
using Discord.Commands;
using Discord.WebSocket;
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

        [Command("confirm")]
        public async Task Confirm()
        {
            var result = await ConfirmAsync("Are you sure?");
            if (result == null)
                await ReplyAsync("You didn't reply");
            else
                await ReplyAsync($"You are {(result.Value ? "sure" : "not sure")}");
        }
        public static async Task handleButton(CallbackEventArgs args)
        {
            var token = args.Interaction;
            await token.UpdateAsync(x =>
            {
                x.Content = $"Clicked {args.ComponentId}";
            });
        }

        [Command("thread")]
        public async Task Thread()
        {
            var chnl = Context.Channel as SocketTextChannel;
            var thread = await chnl.CreateThreadAsync("Test Thread",
                autoArchiveDuration: ThreadArchiveDuration.OneHour, 
                message: Context.Message);
            await thread.SendMessageAsync($"A new thread has been opened!");
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
