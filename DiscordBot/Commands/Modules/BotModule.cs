﻿using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Services;
using DiscordBot.Utils;
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
    public sealed class BotCmdModule : BotModule
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
        public async Task Close(int code = 0)
        {
            await Success("Closing");
            Program.Close(code);
        }

        [Command("approve")]
        [Description("Approves a user for wider access to bot things")]
        [RequirePermission(Perms.Bot.ApproveUser)]
        public async Task Approve(BotUser b)
        {
            b.IsApproved = true;
            b.Permissions.Add(Perm.Parse(Perms.Bot.User.All));
            await ReplyAsync("User has been approved.");
        }

        [Command("disapprove")]
        [Description("Disapproves a user for wider access to bot things")]
        [RequirePermission(Perms.Bot.ApproveUser)]
        public async Task Disapprove(BotUser b)
        {
            b.IsApproved = false;
            b.Permissions = new List<Perm>();
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
                var rs = Program.Services.GetRequiredService<ReactionService>();
                var msg = await ReplyAsync($"No daily tick recorded. Use `{Program.Prefix}bot dtick true` or react to perform one.");
                await msg.AddReactionAsync(Emotes.HAMMER);
                rs.Register(msg, EventAction.Added, async (object sender, ReactionEventArgs e) =>
                {
                    rs.Unregister(msg);
                    await DoDailyTick(true);
                }, doSave: false);
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
    

        [Command("name")]
        [Summary("Views your own override name")]
        public async Task BotName()
        {
            var bUser = Program.GetUser(Context.User);
            await ReplyAsync($"{(bUser.OverrideName == null ? "<null>" : bUser.OverrideName)}");
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
            var bUser = Program.GetUser(Context.User);
            bUser.OverrideName = newName;
            await ReplyAsync(":ballot_box_with_check: Done");
            return new BotResult();
        }

        [Command("name")]
        [Summary("View someone else's name")]
        [RequirePermission(Perms.Bot.User.ViewOtherName)]
        public async Task SeeOtherName(BotUser bUser)
        {
            await ReplyAsync($"{(bUser.OverrideName == null ? "<null>" : bUser.OverrideName)}");
        }

        [Command("name")]
        [Summary("Sets someone else's name")]
        [RequirePermission(Perms.Bot.User.ChangeOtherName)]
        public async Task<RuntimeResult> ChangeOtherName(BotUser bUser, string newName)
        {
            var s = checkName(newName);
            if (!s.IsSuccess)
                return s;
            bUser.OverrideName = newName;
            await ReplyAsync(":ballot_box_with_check: Done");
            return new BotResult();
        }
    
        [Command("msg")]
        [Summary("Views prior content of a message")]
        [RequireOwner]
        public async Task<RuntimeResult> SeeMessageHistory(ulong messageId)
        {
            var db = Program.Services.GetRequiredService<MsgService>();
            using var DB = Program.Services.GetRequiredService<LogContext>();
            var dbMsg = DB.Messages.FirstOrDefault(x => x.MessageId == db.cast(messageId));
            if (dbMsg == null)
                return new BotResult("Message is not in database.");
            var contents = db.GetContents(messageId).OrderBy(x => x.Timestamp);
            var paginator = new PaginatedMessage();
            paginator.Title = $"Message History";
            var url = $"https://discord.com/channels/{dbMsg.Guild}/{dbMsg.Channel}/{dbMsg.Message}";
            paginator.Content = $"{url}";
            if(!string.IsNullOrWhiteSpace(dbMsg.Attachments))
            {
                Console.WriteLine(dbMsg.Attachments);
                paginator.Content += "\r\n" + dbMsg.Attachments.Replace(",", "\r\n");
            }
            var ls = new List<string>();
            int i = 0;
            foreach(var content in contents)
            {
                ls.Add(content.Content);
                paginator.Content += $"\r\n{i++}: {content.Timestamp:dd/MM/yyyy HH:mm:ss}";
            }
            paginator.Pages = ls;
            await PagedReplyAsync(paginator);
            return new BotResult();
        }
    
        [Command("log")]
        [Summary("Gets invite to logging guild")]
        public async Task GetLogInvite()
        {
            var srv = Program.Services.GetRequiredService<LoggingService>();
            var chnl = await srv.LogGuild.GetDefaultChannelAsync();
            var inv = await chnl.CreateInviteAsync(maxAge: 60 * 5, maxUses: 1);
            await ReplyAsync(inv.ToString());
        }

        [Command("ladmin")]
        [Summary("Toggles admin rights in log guild")]
        [RequireOwner]
        public async Task ToggleAdmin()
        {
            var srv = Program.Services.GetRequiredService<LoggingService>();
            var usr = (SocketGuildUser)Context.User;
            var role = srv.LogGuild.Roles.First(x => x.Name == "Log Master");
            if (usr.Roles.Any(x => x.Name == "Log Master"))
                await usr.RemoveRoleAsync(role);
            else
                await usr.AddRoleAsync(role);
            await ReplyAsync("Done.");
        }
    }
}
