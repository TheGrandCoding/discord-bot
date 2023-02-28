using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Services;
using DiscordBot.Utils;
using Interactivity;
using Interactivity.Pagination;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("bot")]
    [Name("Bot Commands")]
    public sealed class BotCmdModule : BotBase
    {
        List<char> allowed_chars;
        public BotCmdModule()
        {
            allowed_chars = new List<char>();
            for(int i = 65; i < (65+26); i++)
            {
                allowed_chars.Add(Convert.ToChar(i));
            }
            for (int i = 97; i < (97 + 26); i++)
            {
                allowed_chars.Add(Convert.ToChar(i));
            }
            allowed_chars.Add(' ');
            allowed_chars.Add('\'');
        }
        [Command("close")]
        [Summary("Closes the bot")]
        [RequireOwner]
        public async Task Close(int code = 0)
        {
            var confirm = await ConfirmAsync("Are you sure you wish to close this bot?\r\n" +
                "A non-zero error code (default 0) will result in a restart.");
            if(confirm.GetValueOrDefault(false))
            {
                await Success("Closing");
                Program.Close(code);
            }
        }

        [Command("pc_reason")]
        [Description("Sets the fail or wait reason for website:/pc/shutdown")]
        [RequireOwner]
        public async Task SetPcShutdownReason([Remainder]string text)
        {
            DiscordBot.MLAPI.Modules.Bot.Internal.failOrWaitReason = text == "-" ? null : text;
            await Success("Done!");
        }

        [Command("approve")]
        [Description("Approves a user for wider access to bot things")]
        [RequirePermission(Perms.Bot.ApproveUser)]
        public async Task Approve(BotDbUser b)
        {
            b.Approved = true;
            b.WithPerm(Perm.Parse(Perms.Bot.User.All));
            Program.Save();
            await ReplyAsync("User has been approved.");
        }

        [Command("disapprove")]
        [Description("Disapproves a user for wider access to bot things")]
        [RequirePermission(Perms.Bot.ApproveUser)]
        public async Task Disapprove(BotDbUser b)
        {
            b.Approved = false;
            b.Permissions = new List<BotDbPermission>();
            await ReplyAsync("User has been disapproved.");
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
                var result = await ConfirmAsync("No daily tick recorded. Do you want to perform one now?");
                if(result.GetValueOrDefault(false))
                {
                    await DoDailyTick(true);
                }
            }
        }

        [Command("dhttp")]
        [Summary("Toggles whether HTTP requests to be logged")]
        [RequireOwner]
        public async Task HttpDebug()
        {
            BotHttpClient.ForceDebug = !BotHttpClient.ForceDebug;
            await ReplyAsync($"HTTP forced debug is {(BotHttpClient.ForceDebug ? "enabled" : "disabled")}");
        }

        [Command("name")]
        [Summary("Views your own override name")]
        public async Task BotName()
        {
            await ReplyAsync($"{(Context.BotDbUser.Name == null ? "<null>" : Context.BotDbUser.Name)}");
        }

        List<char> findIllegalChars(string name)
        {
            var ls = new List<char>();
            foreach(var x in name)
            {
                if (!allowed_chars.Contains(x))
                    ls.Add(x);
            }
            return ls;
        }

        BotResult checkName(string newName)
        {
            if (newName.Length <= 2 || newName.Length >= 16)
                return new BotResult("Length of name too few or great.");
            var illegal = findIllegalChars(newName).Distinct().ToList();
            if(illegal.Count > 0)
            {
                var asString = $"Names cannot contain the below: ";
                foreach(var il in illegal)
                {
                    asString += "\r\n- " + il.ToEnglishName();
                }
                return new BotResult(asString);
            }
            return new BotResult();

        }

        [Command("name")]
        [Summary("Sets your own name")]
        [RequirePermission(Perms.Bot.User.ChangeSelfName)]
        public async Task<RuntimeResult> ChangeSelfName(string newName)
        {
            var s = checkName(newName);
            if (!s.IsSuccess)
                return s;
            Context.BotDbUser.Name = newName;
            Program.Save();
            await ReplyAsync(":ballot_box_with_check: Done");
            return new BotResult();
        }

        [Command("name")]
        [Summary("View someone else's name")]
        [RequirePermission(Perms.Bot.User.ViewOtherName)]
        public async Task SeeOtherName(BotDbUser bUser)
        {
            await ReplyAsync($"{(bUser.Name == null ? "<null>" : bUser.Name)}");
        }

        [Command("name")]
        [Summary("Sets someone else's name")]
        [RequirePermission(Perms.Bot.User.ChangeOtherName)]
        public async Task<RuntimeResult> ChangeOtherName(BotDbUser bUser, string newName)
        {
            var s = checkName(newName);
            if (!s.IsSuccess)
                return s;
            bUser.Name = newName;
            Program.Save();
            await ReplyAsync(":ballot_box_with_check: Done");
            return new BotResult();
        }
    
        [Command("msg")]
        [Summary("Views prior content of a message")]
        [RequireOwner]
        public async Task<RuntimeResult> SeeMessageHistory(ulong messageId)
        {
            var db = Services.GetRequiredService<MsgService>();
            using var DB = Services.GetRequiredService<LogContext>();
            var dbMsg = DB.Messages.FirstOrDefault(x => x.MessageId == db.cast(messageId));
            if (dbMsg == null)
                return new BotResult("Message is not in database.");
            var contents = db.GetContents(messageId, Services).OrderBy(x => x.Timestamp);
            
            var url = $"https://discord.com/channels/{dbMsg.Guild}/{dbMsg.Channel}/{dbMsg.Message}";
            var paginator = new StaticPaginatorBuilder()
                .WithDefaultEmotes()
                .WithFooter(PaginatorFooter.PageNumber);
            foreach (var content in contents)
            {
                var page = new PageBuilder()
                    .WithTitle($"Message at {content.Timestamp:dd/MM/yyyy HH:mm:ss}")
                    .WithText(url)
                    .WithDescription(content.Content);
                paginator.AddPage(page);
            }

            await PagedReplyAsync(paginator);
            return new BotResult();
        }

        [Command("slash"), Summary("Toggles whether this instance ignores slash commands")]
        public async Task IgnoreCommandsToggle()
        {
            Program.ignoringCommands = !Program.ignoringCommands;
            await ReplyAsync("We are " + (Program.ignoringCommands ? "ignoring" : "not ignoring") + " slash commands");
        }
    
        [Command("log")]
        [Summary("Gets invite to logging guild")]
        public async Task GetLogInvite()
        {
            var srv = Services.GetRequiredService<LoggingService>();
            var chnl = await srv.LogGuild.GetDefaultChannelAsync();
            var inv = await chnl.CreateInviteAsync(maxAge: 60 * 5, maxUses: 1);
            await ReplyAsync(inv.ToString());
        }

        [Command("ladmin")]
        [Summary("Toggles admin rights in log guild")]
        [RequireOwner]
        public async Task ToggleAdmin()
        {
            var srv = Services.GetRequiredService<LoggingService>();
            var usr = (SocketGuildUser)Context.User;
            var role = srv.LogGuild.Roles.First(x => x.Name == "Log Master");
            if (usr.Roles.Any(x => x.Name == "Log Master"))
                await usr.RemoveRoleAsync(role);
            else
                await usr.AddRoleAsync(role);
            await ReplyAsync("Done.");
        }
    
        [Command("verify")]
        [Summary("Verifies the specified user in the eyes of the bot")]
        [RequireOwner]
        public Task VerifyUser(BotDbUser busr)
        {
            busr.Verified = true;
            Context.BotDB.Update(busr);
            return Task.CompletedTask;
        }

        [Command("verified")]
        [Summary("Lists users with the specified verification state")]
        public async Task ListVerified(bool state = true)
        {
            var str = "Users " + (state ? "verified" : "not verified");
            foreach(var usr in Context.BotDB.Users.Where(x => x.Verified == state))
            {
                str += $"\r\n- {usr.Connections.Discord.Mention} ({usr.Name})";
            }
            await ReplyAsync(str, allowedMentions: new AllowedMentions(AllowedMentionTypes.None));
        }

        [Command("verifyrole"), Alias("verify_role")]
        [Summary("The specified role requires verification by bot to be issued")]
        [RequireContext(ContextType.Guild)]
        public async Task VerifyRole(IRole role)
        {
            var sv = Services.GetRequiredService<EnsureLevelEliteness>();
            if(!sv.Guilds.TryGetValue(Context.Guild.Id, out var save))
            {
                save = new EnsureLevelEliteness.GuildSave();
                sv.Guilds[Context.Guild.Id] = save;
            }
            save.VerifyRole = role;
            sv.OnSave();
            await sv.Catchup(Services);
            await ReplyAsync("Set.");
        }
    
        [Command("delete_dm"), Alias("ddm")]
        [Summary("Deletes a bot's message in your DMs")]
        public async Task DeleteDM(ulong id)
        {
            var dm = await Context.User.CreateDMChannelAsync();
            var msg = await dm.GetMessageAsync(id);
            if(msg == null)
            {
                await ReplyAsync(":x: Could not find message.");
            } else
            {
                await msg.DeleteAsync();
            }
        }

        [Command("delete_after_dm"), Alias("dadm")]
        [Summary("Deletes all messages including and following the one specified, in DM")]
        public async Task DeleteDMUpTo(ulong id)
        {
            var dm = await Context.User.CreateDMChannelAsync();
            int deleted = 0;
            await Context.Message.AddReactionAsync(Emotes.MAG);

            ulong lastMessageId = id;

            var stopAt = Discord.SnowflakeUtils.ToSnowflake(DateTimeOffset.Now);
            while(deleted < 250 && lastMessageId < stopAt) // while we're in the past
            {
                var messages = await dm.GetMessagesAsync(lastMessageId, Direction.After).FlattenAsync();
                foreach(var msg in messages)
                {
                    if (msg.Id > lastMessageId)
                        lastMessageId = msg.Id;
                    if (lastMessageId > stopAt) // we've went past .now
                        break;

                    if (msg.Author.IsBot)
                        await msg.DeleteAsync();

                    deleted++;
                }
            }
            await Context.Message.AddReactionAsync(Emotes.WHITE_CHECK_MARK);
            await Context.Message.RemoveReactionAsync(Emotes.MAG, Context.User);
        }
    }
}
