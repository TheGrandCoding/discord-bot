using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Services.Rules;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Rules
{
    [Name("Penalties")]
    public class PenaltyModule : BotBase
    {
        public PenaltyService Service { get; set; }

        [RequireUserPermission(Discord.GuildPermission.ManageGuild)]
        [Summary("Lists all penalties currently active in the guild")]
        [Command("penalty list")]
        public async Task List()
        {
            var embed = new EmbedBuilder();
            embed.Title = $"Active Penalties";
            Service.execute(() =>
            {
                foreach (var x in Service.Penalties.Values)
                {
                    if (x.Guild.Id == Context.Guild.Id)
                    {
                        string expires = x.Duration.HasValue 
                            ? Program.FormatTimeSpan(x.Performed.Add(x.Duration.Value) - DateTime.Now)
                            : "indefinite";
                        embed.AddField($"#{x.Id}", $"{x.GetType().Name.Replace("Penalty", "")}\r\n" +
                            $"{x.Operator.GetName()} on {x.Target.GetName()}, remain: {expires}");
                    }
                }
            });
            if (embed.Fields.Count == 0)
                embed.Description = $"No penalties currently active.";
            await ReplyAsync(embed: embed.Build());
        }

        [Command("penalty remove")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Remove(int id)
        {
            Service.RemovePenalty(id);
            Success("Done");
        }

        [Command("penalty nsfw")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ToggleIgnoreNsfw(int id)
        {
            Service.Modify(id, async penalty =>
            {
                if (penalty is ContentBlockPenalty cbp)
                {
                    cbp.IgnoreNSFW = !cbp.IgnoreNSFW;
                    await ReplyAsync(cbp.IgnoreNSFW ? "Penalty now ignores NSFW channels" : "Penalty does not ignore NSFW channels");
                }
                else
                {
                    await ReplyAsync($"{penalty.GetType().Name} is independent of any nsfw channel logic, and thus cannot be toggled.");
                }
            });
        }

        [Command("setreason"), Alias("sreason", "penalty setreason", "penalty sreason")]
        [RequireUserPermission(GuildPermission.KickMembers, Group = "or")]
        [RequireUserPermission(GuildPermission.BanMembers, Group = "or")]
        [RequireUserPermission(GuildPermission.ManageMessages, Group = "or")]
        public async Task SetReason([Remainder]string reason = null)
        {
            if(reason == null)
            {
                await ReplyAsync(Context.BotUser.Reason == null ? "No reason set" : Context.BotUser.Reason);
            } else
            {
                Context.BotUser.Reason = reason;
                await ReplyAsync("Reason set.");
            }

        }

        [Command("mute")]
        [RequireUserPermission(GuildPermission.ManageMessages, Group = "or")]
        public async Task Mute(BotUser target, TimeSpan duration)
        {
            await Service.AddMute(Context.User as SocketGuildUser,
                Context.Guild.GetUser(target.Id),
                Context.BotUser.Reason, duration);
            Success("Muted.");
        }

        [Command("tban"), Alias("tempban")]
        [RequireUserPermission(GuildPermission.KickMembers, Group = "or")]
        [RequireUserPermission(GuildPermission.BanMembers, Group = "or")]
        public async Task TempBan(BotUser target, TimeSpan duration)
        {
            await Service.AddTempBan(Context.User as SocketGuildUser,
                Context.Guild.GetUser(target.Id),
                Context.BotUser.Reason, duration);
            Success("Tempbanned.");
        }

        [Command("tblock"), Alias("topicblock")]
        [RequireUserPermission(GuildPermission.ManageMessages, Group = "or")]
        public async Task TopicBlock(BotUser target, TimeSpan duration, [Remainder]string regex)
        {
            await Service.AddTopicBlock(Context.User as SocketGuildUser,
                Context.Guild.GetUser(target.Id),
                Context.BotUser.Reason, duration, regex);
            Success("Topic block added.");
        }

        [Command("iblock"), Alias("imageblock")]
        [Summary("Prevents a particular image being displayed within the guild")]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task ImageBlock(ulong hash, TimeSpan? duration = null)
        {
            await Service.AddImageBlock(Context.User as SocketGuildUser, null,
                Context.BotUser.Reason, duration, hash);
        }
    }
}
