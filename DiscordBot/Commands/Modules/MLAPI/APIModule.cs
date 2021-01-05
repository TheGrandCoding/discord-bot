using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.MLAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.MLAPI
{
    [Group("mlapi")]
    [Name("API Module")]
    public class APIModule : BotBase
    {
        [Command("password")]
        [Alias("pwd", "pass")]
        [Summary("Sets your MLAPI password.")]
        public async Task SetPassword()
        {
            Context.BotUser.MLAPIPassword = null;
            await ReplyAsync(embed: new EmbedBuilder()
                .WithDescription($"Please [click here]({Handler.LocalAPIUrl}/login/discord) to set your password")
                .Build());
        }
    }
}
