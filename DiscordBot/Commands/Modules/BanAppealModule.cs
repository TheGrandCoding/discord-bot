using Discord;
using Discord.Commands;
using DiscordBot.Classes.Rules;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Name("Appeals Handling")]
    [Group("appeals"), Alias("appeal", "app")]
    [RequireAppealContext]
    public class BanAppealModule : BotBase
    {
        public BanAppealsService Service { get; set; }
        public BanAppeal Appeal;

        protected override void BeforeExecute(CommandInfo command)
        {
            base.BeforeExecute(command);
            Appeal = Service.GetAppeal(Context.Guild, ulong.Parse((Context.Channel as ITextChannel).Topic));
        }

        [Command("mute")]
        [Summary("Prevents the banned user from sending messages to this appeal for the time period")]
        public async Task Mute([Remainder]TimeSpan? timeout = null)
        {
            Appeal.MutedUntil = DateTime.Now.Add(timeout.GetValueOrDefault(TimeSpan.FromHours(24)));
            await ReplyAsync(embed: new EmbedBuilder()
                .WithTitle($"Appeal Muted")
                .WithDescription($"Appeal has been muted until {Appeal.MutedUntil:yyyy/MM/dd HH:mm:ss}")
                .WithTimestamp(Appeal.MutedUntil.Value)
                .Build());
        }

        [Command("approve")]
        [Summary("Approves the appeal, thus revoking the original ban")]
        public async Task Approve()
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle("Approval not yet implemented").Build());
        }

        [Command("reject")]
        [Summary("Mutes the ban appeal for a prolonged period of time, at least one month, increasing each time")]
        public async Task Reject()
        {
            var duration = DateTime.Now - Context.Channel.CreatedAt;
            if (duration.TotalDays < 31)
                await Mute(TimeSpan.FromDays(31));
            else
                await Mute(duration);
        }
    }
}
