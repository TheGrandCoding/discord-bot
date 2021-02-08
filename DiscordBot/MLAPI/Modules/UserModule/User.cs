using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public void MainPage()
        {
            ReplyFile("base.html", 200);
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
                var botU = Program.GetUser(oper);
                if (PermChecker.UserHasPerm(botU, Perms.Bot.User.ChangeServerNickname))
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
        public void NicknamePage()
        {
            var nicks = getTableNicknames();
            var other = getOtherNicknames();
            ReplyFile("nicknames.html", 200, new Replacements()
                .Add("table_nicks", nicks)
                .Add("table_other_nicks", other));
        }

        [Path("/user/nickchange"), Method("PATCH")]
        public void ChangeUserNickname(ulong guildId, ulong userId, string newnick = "")
        {
            var guild = Program.Client.GetGuild(guildId);
            if (guild == null)
            {
                RespondRaw("Unknown server", 404);
            }
            else
            {
                var usrInGuild = guild.GetUser(Context.User.Id);
                var target = guild.GetUser(userId);
                if (usrInGuild == null || target == null)
                {
                    RespondRaw("Not in that guild", 404);
                }
                else
                {
                    if (!canChangeNickname(usrInGuild, target))
                    {
                        RespondRaw("You are unable to change their nickname", 403);
                        return;
                    }
                    var botUser = guild.CurrentUser;
                    if (botUser.Hierarchy < target.Hierarchy)
                    {
                        RespondRaw("Bot is unable to modify that user (no perms)", 500);
                        return;
                    }
                    try
                    {
                        target.ModifyAsync(x =>
                        {
                            x.Nickname = newnick;
                        }, new Discord.RequestOptions() { AuditLogReason = $"via API: {Context.User.Id}" }).GetAwaiter().GetResult();
                        RespondRaw("Success");
                    }
                    catch (Discord.Net.HttpException ex)
                    {
                        Program.LogMsg("REST", ex);
                        RespondRaw(ex.Message, (int)ex.HttpCode);
                    }
                }
            }
        }

        #endregion

        #region Tokens

        AuthToken GetToken(string n)
        {
            if (n == AuthToken.SessionToken || n == AuthToken.LoginPassword)
                return null; // cant modify current token or login password (separate page for dat)
            return Context.User.Tokens.FirstOrDefault(x => x.Name.ToLower() == n.ToLower());
        }

        [Path("/user/tokens"), Method("GET")]
        public void TokenPage()
        {
            var tokenTable = "";
            foreach (var token in Context.User.Tokens)
            {
                bool hide = token.Name == AuthToken.LoginPassword || token.Name == AuthToken.SessionToken;
                string ROW = $"<tr>";
                ROW += $"<td>{token.Name}</td>";
                ROW += $"<td><p class='hide'>{(hide ? "[redacted]" : token.Value)}</p></td>";
                tokenTable += ROW + "</tr>";
            }

            ReplyFile("token.html", 200, new Replacements().Add("tokentable", tokenTable));
        }

        [Path("/user/tokens/remove"), Method("PUT")]
        public void RemoveToken(string name)
        {
            var token = GetToken(name);
            if (token == null)
            {
                RespondRaw("Unknown token", 404);
                return;
            }

            Context.User.Tokens.Remove(token);
            Program.Save();
            RespondRaw("Token has been removed", 200);
        }

        [Path("/user/tokens"), Method("PUT")]
        public void NewToken(string name)
        {
            if (name.Contains(" "))
            {
                RespondRaw("Names cannot contain spaces", 400);
                return;
            }

            var token = GetToken(name);
            if (name == AuthToken.SessionToken)
            {
                RespondRaw("That token is auto-generated and cannot be changed!", 400);
                return;
            }
            if(name == AuthToken.LoginPassword)
            {
                RespondRaw($"{Handler.LocalAPIUrl}/login/setpassword", 400);
                return;
            }
            if (token == null)
            {
                token = new AuthToken(name, 16);
                Context.User.Tokens.Add(token);
                RespondRaw("Token generated and added");
                return;
            }
            token.Value = AuthToken.Generate(token.Value.Length);
            RespondRaw("Token regenerated");
        }
        #endregion
    }
}
