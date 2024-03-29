﻿using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.UserModule
{
    public class User : AuthedAPIBase
    {
        public User(APIContext c) : base(c, "user") { }

        string utf8(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var bytes = Encoding.Default.GetBytes(text);
            return Encoding.UTF8.GetString(bytes);
        }

        [Path("/user"), Method("GET")]
        public async Task MainPage()
        {
            await ReplyFile("base.html", 200);
        }

        #region Nicks

        bool canChangeNickname(SocketGuildUser oper, SocketGuildUser target)
        {
            if (oper == null)
                return false;
            var bUser = oper.Guild.CurrentUser;
            if (bUser.Hierarchy <= target.Hierarchy)
                return false;
            if (oper.GuildPermissions.Administrator)
                return true;
            if ((oper.Hierarchy > target.Hierarchy))
            {
                return oper.GuildPermissions.Administrator || oper.GuildPermissions.ManageNicknames;
            }
            else if (oper.Id == target.Id)
            {
                if (oper.GuildPermissions.ChangeNickname)
                    return true;
                var result = Context.BotDB.GetUserFromDiscord(oper, false).Result;
                if (!result.Success) return false;
                if (PermChecker.UserHasPerm(result.Value, Perms.Bot.User.ChangeServerNickname))
                    return true;
            }
            return false;
        }

        string getTableNicknames()
        {
            string TEXT = "";
            foreach (var guild in Program.Client.Guilds)
            {
                var usr = guild.GetUser(Context.User.Id);
                if (usr != null)
                {
                    string row = "<tr>";
                    row += $"<td>{guild.Name}</td>";
                    string input = $"<td>";
                    input += $"<input type='text' onkeyup='updateNick(this, event);' {(canChangeNickname(usr, usr) ? "" : "readonly")} value='{utf8(usr.Nickname)}' placeholder='Enter nickname..' id='{guild.Id}-{usr.Id}'/>";
                    input += "</td>";
                    row += input + "</tr>";
                    TEXT += row;
                }
            }
            return TEXT;
        }

        string getOtherNicknames()
        {
            string TEXT = "";
            foreach (var guild in Program.Client.Guilds)
            {
                var doesexist = guild.GetUser(Context.User.Id);
                if (doesexist != null || HasNode(Perms.Bot.Developer.ViewNicknamesAllServers))
                {
                    TEXT += $"<tr class='guildsep'><th colspan='2'>{guild.Name}</th><tr>";
                    foreach (var usr in guild.Users.OrderByDescending(x => x.Hierarchy).ThenBy(x => x.Nickname ?? x.Username))
                    {
                        if (usr.Id == doesexist?.Id)
                            continue;
                        string row = $"<tr>";
                        string botText = "";
                        if (usr.IsBot)
                        {
                            botText = $"<span class='bot'>BOT</span> ";
                        }
                        row += $"<td>{botText}{utf8(usr.Username)}#{usr.Discriminator}</td>";
                        row += $"<td><input type='text' onkeyup='updateNick(this, event);' {(canChangeNickname(doesexist, usr) ? "" : "readonly")} placeholder='Enter nickname...' value='{utf8(usr.Nickname)}' id='{guild.Id}-{usr.Id}'/></td>";
                        TEXT += row + "</tr>";
                    }
                }
            }
            return TEXT;
        }

        [Path("/user/nicknames"), Method("GET")]
        public async Task NicknamePage()
        {
            var nicks = getTableNicknames();
            var other = getOtherNicknames();
            await ReplyFile("nicknames.html", 200, new Replacements()
                .Add("table_nicks", nicks)
                .Add("table_other_nicks", other));
        }

        [Path("/user/nickchange"), Method("PATCH")]
        public async Task ChangeUserNickname(ulong guildId, ulong userId, string newnick = "")
        {
            var guild = Program.Client.GetGuild(guildId);
            if (guild == null)
            {
                await RespondRaw("Unknown server", 404);
            }
            else
            {
                var usrInGuild = guild.GetUser(Context.User.Id);
                var target = guild.GetUser(userId);
                if (usrInGuild == null || target == null)
                {
                    await RespondRaw("Not in that guild", 404);
                }
                else
                {
                    if (!canChangeNickname(usrInGuild, target))
                    {
                        await RespondRaw("You are unable to change their nickname", 403);
                        return;
                    }
                    var BotDbUser = guild.CurrentUser;
                    if (BotDbUser.Hierarchy < target.Hierarchy)
                    {
                        await RespondRaw("Bot is unable to modify that user (no perms)", 500);
                        return;
                    }
                    try
                    {
                        target.ModifyAsync(x =>
                        {
                            x.Nickname = newnick;
                        }, new Discord.RequestOptions() { AuditLogReason = $"via API: {Context.User.Id}" }).GetAwaiter().GetResult();
                        await RespondRaw("Success");
                    }
                    catch (Discord.Net.HttpException ex)
                    {
                        Program.LogError(ex, "REST");
                        await RespondRaw(ex.Message, (int)ex.HttpCode);
                    }
                }
            }
        }

        #endregion

        #region Tokens

        BotDbAuthToken GetToken(string n)
        {
            return Context.User.AuthTokens.FirstOrDefault(x => x.Name.ToLower() == n.ToLower());
        }

        [Path("/user/tokens"), Method("GET")]
        public async Task TokenPage()
        {
            var tokenTable = "";
            foreach (var token in Context.User.AuthTokens)
            {
                string ROW = $"<tr>";
                ROW += $"<td>{token.Name}</td>";
                ROW += $"<td><p class='hide'>{token.Token}</p></td>";
                tokenTable += ROW + "</tr>";
            }

            await ReplyFile("token.html", 200, new Replacements().Add("tokentable", tokenTable));
        }

        [Path("/user/tokens/remove"), Method("PUT")]
        public async Task RemoveToken(string name)
        {
            var token = GetToken(name);
            if (token == null)
            {
                await RespondRaw("Unknown token", 404);
                return;
            }

            Context.User.AuthTokens.Remove(token);
            await RespondRaw("Token has been removed", 200);
        }

        [Path("/user/tokens"), Method("PUT")]
        public async Task NewToken(string name)
        {
            if (name.Contains(" "))
            {
                await RespondRaw("Names cannot contain spaces", 400);
                return;
            }

            var token = GetToken(name);
            if (token == null)
            {
                token = new BotDbAuthToken()
                {
                    Name = name,
                    UserId = Context.User.Id,
                    User = Context.User,
                    Token = PasswordHash.RandomToken(16)
                };
                Context.User.AuthTokens.Add(token);
                await RespondRaw("Token generated and added");
                return;
            } 
            token.Token = PasswordHash.RandomToken(token.Token.Length);
            await RespondRaw("Token regenerated");
        }
        #endregion
    }
}
