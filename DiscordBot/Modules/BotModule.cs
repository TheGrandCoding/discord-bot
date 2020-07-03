using Discord;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
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
