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
            var found = Service.Modify(id, async penalty =>
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
            if (!found)
            {
                await ReplyAsync("Could not find any penalty by that Id");
                await List();
            }
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

        [Command("penalty duration")]
        [RequireUserPermission(GuildPermission.ManageMessages, Group = "or")]
        [RequireUserPermission(GuildPermission.KickMembers, Group = "or")]
        public async Task<RuntimeResult> SetDuration(int penaltyId, TimeSpan duration)
        {
            if(Service.Penalties.TryGetValue(penaltyId, out var penalty))
            {
                var perms = (Context.User as SocketGuildUser).GuildPermissions;
                if(!(perms.Administrator || perms.ManageGuild))
                {
                    if(penalty is MutePenalty || penalty is TopicBlockPenalty || penalty is ImageBlockPenalty)
                    {
                        if (!perms.ManageMessages)
                            return new BotResult($"You do not have permission to perform {penalty.GetType().Name}, so you cannot change its duration");
                    }
                    if (penalty is TempBanPenalty)
                        if(!perms.KickMembers && !perms.BanMembers)
                            return new BotResult($"You do not have permission to perform {penalty.GetType().Name}, so you cannot change its duration");
                }
                penalty.Duration = duration;
                await ReplyAsync("Duration set.");
            } else
            {
                await ReplyAsync("No penalty by that id");
                await List();
            }
            return new BotResult();
        }



        [Command("penalty default")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetDefaultDuration(string defaultDuration)
        {
            if(defaultDuration == "null")
            {
                Service.DefaultDurations.Remove(Context.Guild.Id);
                Success("Removed default duration. All mutes will by default be indefinite");
                return;
            }
            var ts = PenaltyService.GetDurationForDefault(defaultDuration);
            if(!ts.HasValue)
            {
                await ReplyAsync("Invalid format.\r\nValid formats include:\r\n" +
                    "- Any timespan format (eg, '1h5m3s')\r\n" +
                    "- `next:hhmm`, eg `next:0900`, which mutes when that time occurs tomorrow");
                return;
            }
            Service.DefaultDurations[Context.Guild.Id] = defaultDuration;
            Service.OnSave();
            Success($"Default duration set to `{defaultDuration}`, this would be in {Program.FormatTimeSpan(ts.Value, true)}");
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
