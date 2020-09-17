using Discord;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("bot")]
    [Name("Bot Commands")]
    public class BotCmdModule : BotModule
    {
        [Command("close")]
        [Summary("Closes the bot")]
        public async Task Close(int code = 0)
        {
            await Success("Closing");
            Program.Close(code);
        }

        [Command("dtick")]
        [Summary("Calls OnDailyTick for all services")]
        [RequireOwner]
        public async Task DoDailyTick(bool doIt = false)
        {
            if(doIt)
            {
                await ReplyAsync("Sending...");
                Service.SendDailyTick();
                await ReplyAsync("Sent.");
            } else
            {
                await SeeDailyTick();
            }
        }

        [Command("dtick")]
        [Summary("Views the last time a daily tick occured")]
        [RequireOwner]
        public async Task SeeDailyTick()
        {
            if(Service.lastDailyTick.HasValue)
            {
                await ReplyAsync($"Last daily tick: {Service.lastDailyTick.Value.ToString("yyyy/MM/dd hh:mm:ss.fff")}");
            } else
            {
                var rs = Program.Services.GetRequiredService<ReactionService>();
                var msg = await ReplyAsync($"No daily tick recorded. Use `{Program.Prefix}bot dtick true` or react to perform one.");
                await msg.AddReactionAsync(Emotes.HAMMER);
                rs.Register(msg, EventAction.Added, async (object sender, ReactionEventArgs e) =>
                {
                    rs.Unregister(msg);
                    await DoDailyTick(true);
                });
            }
        }

        [Command("dyndns")]
        [Summary("Sets dynamic DNS link for bot to GET to.")]
        [RequireOwner]
        public async Task UpdateURL(Uri url)
        {
            var serv = Program.Services.GetRequiredService<DynDNService>();
            serv.URL = url.ToString();
            var response = await serv.Perform();
            await ReplyAsync(embed:
                new EmbedBuilder()
                .WithTitle($"GET Response")
                .WithFooter(url.ToString())
                .WithColor(response.IsSuccessStatusCode ? Color.Green : Color.Red)
                .AddField("Status Code", response.StatusCode, true)
                .AddField("Status Text", response.ReasonPhrase)
                .Build());
        }
    }
}
