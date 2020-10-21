using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Commands.Modules.MLAPI;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    [RequireAuthentication]
    public class VPN : APIBase
    {
        public MsgService DB { get; set; }
        const string urlName = "vpn";
        const string channelIcon = "<svg width='24' height='24' viewBox='0 0 24 24' class='icon-1_QxNX'><path fill='currentColor' fill-rule='evenodd' clip-rule='evenodd' d='M5.88657 21C5.57547 21 5.3399 20.7189 5.39427 20.4126L6.00001 17H2.59511C2.28449 17 2.04905 16.7198 2.10259 16.4138L2.27759 15.4138C2.31946 15.1746 2.52722 15 2.77011 15H6.35001L7.41001 9H4.00511C3.69449 9 3.45905 8.71977 3.51259 8.41381L3.68759 7.41381C3.72946 7.17456 3.93722 7 4.18011 7H7.76001L8.39677 3.41262C8.43914 3.17391 8.64664 3 8.88907 3H9.87344C10.1845 3 10.4201 3.28107 10.3657 3.58738L9.76001 7H15.76L16.3968 3.41262C16.4391 3.17391 16.6466 3 16.8891 3H17.8734C18.1845 3 18.4201 3.28107 18.3657 3.58738L17.76 7H21.1649C21.4755 7 21.711 7.28023 21.6574 7.58619L21.4824 8.58619C21.4406 8.82544 21.2328 9 20.9899 9H17.41L16.35 15H19.7549C20.0655 15 20.301 15.2802 20.2474 15.5862L20.0724 16.5862C20.0306 16.8254 19.8228 17 19.5799 17H16L15.3632 20.5874C15.3209 20.8261 15.1134 21 14.8709 21H13.8866C13.5755 21 13.3399 20.7189 13.3943 20.4126L14 17H8.00001L7.36325 20.5874C7.32088 20.8261 7.11337 21 6.87094 21H5.88657ZM9.41045 9L8.35045 15H14.3504L15.4104 9H9.41045Z'></path></svg>";
        public VPN(APIContext c) : base(c, "vpn")
        {
            DB = Program.Services.GetRequiredService<MsgService>();
        }
        bool hasAccessTo(SocketTextChannel c)
        {
            if (c.IsNsfw)
                return false;
            // This will return only if user has permission to view channel
            var usr = c.GetUser(Context.User.Id);
            return usr != null;
        }
        bool hasWritePermissions(SocketTextChannel c)
        {
            if (Self == null)
                return false;
            if (c == null)
                return false;
            if (Self.GuildPermissions.Administrator || Self.Id == Self.Guild.OwnerId)
                return true;
            var userPerm = c.GetPermissionOverwrite(Self).GetValueOrDefault(new OverwritePermissions(sendMessages: PermValue.Inherit));
            if (userPerm.SendMessages == PermValue.Allow)
            { // regardless of role settings, they can send.
                return true;
            }
            if (userPerm.SendMessages == PermValue.Deny)
            { // again, role perms irrelevant
                return false;
            }
            // now we must inspect roles.
            // if *any* role is allow, we go approve.
            // if *any* is deny AND none are allow, we say no.
            bool anyDeny = false;
            foreach (var role in Self.Roles)
            {
                if (role.IsEveryone)
                    continue;
                var perm = c.GetPermissionOverwrite(role);
                if (perm == null || !perm.HasValue)
                    continue;
                if (perm.Value.SendMessages == PermValue.Allow)
                    return true;
                if (perm.Value.SendMessages == PermValue.Deny)
                    anyDeny = true;
            }
            if (anyDeny)
                return false;
            // now we need to consider global ability to send messages globally
            foreach (var role in Self.Roles)
            {
                if (role.Permissions.SendMessages)
                    return true;
            }
            // for safety, since im not sure, we'll just say no:
            return false;
        }

        bool hasAccessTo(SocketCategoryChannel c)
        {
            return c.GetUser(Context.User.Id) != null;
        }

        RestWebhook getOrCreateWebhook(SocketTextChannel c)
        {
            var hooks = c.GetWebhooksAsync().Result;
            return hooks.FirstOrDefault(x => x.Name == "mlapi-vpn") ?? c.CreateWebhookAsync("mlapi-vpn").Result;
        }

        string getGuilds()
        {
            string txt = "";
            foreach (var guild in Program.Client.Guilds)
            {
                if (guild.GetUser(Context.User.Id) == null)
                    continue;
                txt += $"<div class='guild-entry-item'>" +
                    $"<a href='/vpn?guild={guild.Id}' alt='{guild.Name}'>" +
                    $"<img style='border-radius: 50%;' src='{guild.IconUrl}' alt='{guild.Name}' width='48' height='48'>" +
                    $"</a>" +
                    $"</div>";
            }
            return txt;
        }

        int numUnreadMessages(SocketTextChannel channel, out bool unreadMentions)
        {
            int msgs = 0;
            unreadMentions = false;
            DateTime last;
            foreach (var msg in DB.GetMessagesAsync(channel.Guild.Id, channel.Id).Result)
            {
                if (!isMessageNew(channel, msg))
                    continue;
                msgs++;
                //if (msg.MentionedUserIds.Contains(Context.User.Id))
                    //unreadMentions = true;
            }
            return msgs;
        }

        string getChannels(SocketGuild guild, SocketTextChannel selected)
        {
            var usr = guild.GetUser(Context.User.Id);
            if (usr == null)
                return "";
            string txt = "";
            foreach (var cat in guild.CategoryChannels.OrderBy(x => x.Position))
            {
                string TEXT = "";
                TEXT += $"<div class='chat-category' id='{cat.Id}'>{cat.Name}</div>";
                bool any = false;
                foreach (var chnl in cat.Channels.OrderBy(x => x.Position))
                {
                    if (!(chnl is SocketTextChannel textC))
                        continue;
                    if (!hasAccessTo(textC))
                        continue;
                    any = true;
                    DateTime last;
                    if (Context.User.LastVisitVPN.TryGetValue(chnl.Id, out var item))
                        last = item;
                    else
                        last = DateTime.Now.AddDays(-1);
                    var diff = DateTime.Now - last;
                    int count = 0;
                    if (diff.TotalMinutes >= 10)
                        count = numUnreadMessages(textC, out bool mention);
                    TEXT += $"<div class='containerDefault-1ZnADq'>";
                    TEXT += "<div tabindex='0' class='wrapper-1ucjTd' role='button'>";
                    if (count > 0)
                    {
                        TEXT += $"<div class='chat-unread-blob'></div>";
                    }
                    TEXT += "<div class='content-3at_AU'>";
                    TEXT += $"{channelIcon}" +
                        $"<div class='name-3_Dsmg {(count > 0 ? "chat-unread" : "")}'><a href='/vpn?guild={guild.Id}&channel={chnl.Id}'>{chnl.Name}</a>";
                    TEXT += $"</div>" +
                        $"</div>" +
                        $"</div>" +
                        $"</div>";
                }
                if (any)
                    txt += TEXT;
            }
            return txt;
        }

        string getRGBFromColor(Color clr, string a = "")
        {
            return $"rgb({clr.R}, {clr.G}, {clr.B}{(string.IsNullOrWhiteSpace(a) ? "" : ", " + a)})";
        }

        string getRoleColor(SocketGuildUser user)
        {
            if (user != null)
            {
                foreach (var role in user.Roles.OrderByDescending(x => x.Position))
                {
                    return getRGBFromColor(role.Color);
                }
            }
            return $"rgb(185,187,190)";
        }

        SocketGuildUser Self;
        bool HasUnreadMessages = false;
        string getMsgContent(SocketGuild guild, ReturnedMsg m, out bool mentioned)
        {
            mentioned = false;
            string basic = m.Content;
            #region Strip Harmful Stuff
            // Remove < and > to prevent injection i guess
            basic = basic.Replace("<", "&lt;").Replace(">", "&gt;");
            #endregion

            #region Mention Parsing
            /*
            #region User Mentions
            foreach (var id in m.MentionedUserIds)
            {
                if (id == Context.User.Id)
                    mentioned = true;
                var usr = guild.GetUser(id);
                string[] replaceFroms = new string[]
                {
                    $"&lt;@!{id}&gt;",
                    $"&lt;@{id}&gt;",
                };
                string replaceTo = "";
                if (usr == null)
                {
                    replaceTo = "<span class='mention wrapper-3WhCwL'>@deleted-user</span>";
                }
                else
                {
                    replaceTo = $"<span class='wrapper-3WhCwL mention interactive'>@{(usr.Nickname ?? usr.Username)}</span>";
                }
                foreach (var from in replaceFroms)
                    basic = basic.Replace(from, replaceTo);
            }
            #endregion
            #region Role Mentions
            foreach (var id in m.MentionedRoleIds)
            {
                var role = guild.GetRole(id);
                if (Self.Roles.Contains(role))
                    mentioned = true;
                string replaceFrom = $"&lt;@&{id}&gt;";
                string replaceTo = "";
                if (role == null)
                {
                    replaceTo = "<span class='mention wrapper-3WhCwL'>@deleted-role</span>";
                }
                else
                {
                    replaceTo = $"<span style='color: {getRGBFromColor(role.Color)};" +
                        $"background-color: {getRGBFromColor(role.Color, "0.1")}' class='wrapper-3WhCwL mention interactive'>@{role.Name}</span>";
                }
                basic = basic.Replace(replaceFrom, replaceTo);
            }
            #endregion
            */
            #endregion

            #region Markdown Replacement
            // Skip for now.
            #endregion
            return basic;
        }

        string addDateSep(DateTime now, bool isNew)
        {
            return $"<div class='divider-3_HH5L {(isNew ? "msg-unread" : "")} hasContent-1_DUdQ divider-JfaTT5 hasContent-1cNJDh'>" +
                    $"<span class='content-1o0f9g'>{now.ToString("MMMM d, yyyy")}</span>" +
                "</div>";
        }

        string getEmbed(Embed embed)
        {
            if (embed == null)
                return "";
            string text = "<div class='container-1ov-mD'>";
            text += "<div class='embedWrapper-lXpS3L embedFull-2tM8-- embed-IeVjo6 markup-2BOw-j'>";
            text += "<div class='grid-1nZz7S'>";
            text += $"<div class='embedTitle-3OXDkz embedMargin-UO5XwE'>{embed.Title}</div>";
            if (!string.IsNullOrWhiteSpace(embed.Description))
                text += $"<div class='embedDescription-1Cuq9a embedMargin-UO5XwE'>{embed.Description}</div>";
            if (embed.Fields.Length > 0)
            {
                text += $"<div class='embedFields-2IPs5Z'>";
                int maxCol = 5;
                int column = 1;
                foreach (var field in embed.Fields)
                {
                    if (!field.Inline)
                    {
                        column = 1;
                    }
                    text += $"<div class='embedField-1v-Pnh' style='grid-column: {column} / {maxCol};'>";
                    text += $"<div class='embedFieldName-NFrena'>{field.Name}</div>" +
                        $"<div class='embedFieldValue-nELq2s'>{field.Value}</div>";
                    text += "</div>";
                    if (field.Inline)
                    {
                        if (maxCol == 5)
                        {
                            maxCol = 9;
                            column = 5;
                        }
                        else if (maxCol == 9)
                        {
                            maxCol = 13;
                            column = 9;
                        }
                    }
                }
                text += "</div>";
            }
            if (embed.Footer.HasValue)
            {
                text += $"<div class='embedFooter-3yVop- embedMargin-UO5XwE'>" +
                    $"<span class='embedFooterText-28V_Wb'>{embed.Footer.Value.Text}</span>" +
                    $"</div>";
            }
            text += "</div>";
            text += "</div>";
            text += "</div>";
            return text;
        }

        bool isMessageNew(SocketTextChannel channel, ReturnedMsg message)
        {
            DateTime last;
            if (Context.User.LastVisitVPN.TryGetValue(channel.Id, out var item))
                last = item;
            else
                last = DateTime.Now.AddDays(-1);
            return message.CreatedAt.DateTime > last;
        }

        string getChat(SocketGuild guild, SocketTextChannel channel, ulong before = ulong.MaxValue)
        {
            if (!hasAccessTo(channel))
            {
                return "<div class='error'>You are unable to access this channel</div>";
            }
            List<ReturnedMsg> messages = DB.GetCombinedMsgs(guild.Id, channel.Id, before, limit: 100).Result;
            string txt = "";
            DateTime lastMessage = DateTime.MinValue;
            bool hasSetNew = false;
            foreach (var msg in messages.OrderBy(x => x.Id))
            {
                if (string.IsNullOrWhiteSpace(txt) && messages.Count == 100)
                { // if count below 100, then clearly there are no more messages to find, as we cant even get 100
                    txt = $"<div id='loadMoreBtn' tabindex='0' class='hasMore-3e72_v' role='button' " +
                        $"onclick='loadMore(\"{msg.Id}\")'>Load more messages</div>";
                }
                bool isNew = hasSetNew == false && isMessageNew(channel, msg);
                if (lastMessage.DayOfYear != msg.CreatedAt.DayOfYear || isNew)
                {
                    txt += addDateSep(msg.CreatedAt.DateTime, isNew);
                }
                hasSetNew = hasSetNew || isNew;
                lastMessage = msg.CreatedAt.DateTime;

                string authorName;
                string roleColor;
                if (msg.Author is SocketGuildUser sus)
                {
                    authorName = sus.Nickname ?? sus.Username;
                    roleColor = getRoleColor(sus);
                }
                else if (msg.Author is IWebhookUser hook)
                {
                    authorName = hook.Username;
                    roleColor = getRoleColor(null);
                }
                else if(msg.Author is IUser user)
                {
                    authorName = user.Username;
                    roleColor = getRoleColor(null);
                } else
                {
                    authorName = "unknown";
                    roleColor = getRoleColor(null);
                }
                string MESSAGE = "";
                var deletedThing = msg.IsDeleted ? "message-deleted" : "";
                MESSAGE += $"<div class='message-2qnXI6 {deletedThing} wrapper-2a6GCs compact-T3H92H zalgo-jN1Ica[[MENTION]]'>";
                MESSAGE += "<div class='contents-2mQqc9'>";
                #region Message Metadata - Author and Time
                MESSAGE += "<h2 class='header-23xsNx'>";
                #region Message Time
                MESSAGE += "<span class='latin24CompactTimeStamp-2V7XIQ timestamp-3ZCmNB timestampVisibleOnHover-2bQeI4'>";
                MESSAGE += $"<span aria-label='{msg.CreatedAt.ToString("f")}'>";
                MESSAGE += "<i class='separator-2nZzUB' aria-hidden='true'>[</i>";
                MESSAGE += $"{msg.CreatedAt.ToString("hh:mm tt")}";
                MESSAGE += "<i class='separator-2nZzUB' aria-hidden='true'>]</i>";
                MESSAGE += "</span>";
                MESSAGE += "</span>";
                #endregion
                #region Message Author
                MESSAGE += $"<span tabindex='0' aria-controls='popout_320' aria-expanded='false' " +
                    $"class='username-1A8OIy clickable-1bVtEA' role='button' " +
                    $"style='color: {roleColor};'>{authorName}</span>";
                MESSAGE += "<i class='separator-2nZzUB'>:</i>";
                #endregion
                MESSAGE += "</h2>";
                #endregion
                MESSAGE += $"<div class='markup-2BOw-j messageContent-2qWWxC'>{getMsgContent(guild, msg, out bool mentioned)}</div>";
                MESSAGE += "</div>";
                //foreach (var embd in msg.Embeds)
                //{
                //    MESSAGE += getEmbed(embd as Embed);
                //}
                MESSAGE += "</div>";
                txt += MESSAGE.Replace("[[MENTION]]", mentioned ? " mentioned-xhSam7" : "");
            }
            if (before != ulong.MaxValue)
            { // offer ability to jump back to present
                txt += $"<div tabindex='0' class='hasMore-3e72_v' role='button' " +
                    $"onclick='loadMore(\"0\")'>Back to present</div>";
            }
            Context.User.LastVisitVPN[channel.Id] = DateTime.Now;
            Program.Save();
            return txt;
        }

        SocketGuild getDefaultGuild()
        {
            foreach (var guild in Program.Client.Guilds)
            {
                var usr = guild.GetUser(Context.User.Id);
                if (usr != null)
                    return guild;
            }
            return null;
        }

        SocketTextChannel getDefaultChannel(SocketGuild g)
        {
            if (hasAccessTo(g.DefaultChannel))
                return g.DefaultChannel;
            foreach (var chnl in g.TextChannels)
            {
                if (hasAccessTo(chnl))
                    return chnl;
            }
            return null;
        }

        string getUsers(SocketGuild guild, SocketTextChannel channel)
        {
            if (guild == null || channel == null)
                return "<div>An error occured!</div>";
            string USERS = "";
            List<ulong> done = new List<ulong>();
            string offLine = "";
            int offlineCount = 0;
            int blockedUsers = 0;
            foreach (var role in guild.Roles.OrderByDescending(x => x.Position))
            {
                if (!role.IsHoisted && !role.IsEveryone)
                    continue;
                string ROLE_NAME = $"<header class='membersGroup-v9BXpm container-2ax-kl'>{role.Name} — [[COUNT]]</header>";
                string ROLE = "";
                int roleMembers = 0;
                foreach (var member in channel.Users)
                {
                    if (done.Contains(member.Id))
                        continue;
                    if (!member.Roles.Contains(role))
                        continue;
                    string USER = "";
                    USER += "<div tabindex='0' class='member-3-YXUe container-2Pjhx- clickable-1JJAn8' " +
                        "aria-controls='popout_884' aria-expanded='false' role='button'>";
                    USER += "<div class='layout-2DM8Md'>";
                    USER += "<div class='avatar-3uk_u9'>";
                    USER += "<div class='wrapper-3t9DeA' " +
                        $"role='img' aria-label='{member.Username}, {member.Status}'" +
                        " aria-hidden='false' style='width: 32px; height: 32px;'>";
                    USER += "<svg width='40' height='32' viewBox='0 0 40 32' class='mask-1l8v16 svg-2V3M55' aria-hidden='true'>";
                    USER += "<foreignObject x='0' y='0' width='32' height='32' mask='url(#svg-mask-avatar-status-round-32)'>";
                    USER += $"<img src='{member.GetAvatarUrl(size: 256) ?? member.GetDefaultAvatarUrl()}' alt=' ' class='avatar-VxgULZ' aria-hidden='true'>";
                    USER += "</foreignObject>";
                    USER += "</svg></div></div>";

                    USER += "<div class='content-3QAtGj'>";
                    USER += "<div class='nameAndDecorators-5FJ2dg'>";
                    USER += "<span class='roleColor-rz2vM0' style='color: " +
                        $"{getRoleColor(member)};'>{member.Nickname ?? member.Username}</span>";
                    USER += "</div></div>";
                    USER += $"<div class='subText-1KtqkB'>{member.Activity?.Name ?? ""}</div>";
                    USER += "</div>";
                    USER += "</div>";
                    done.Add(member.Id);
                    if (member.Status == UserStatus.Offline)
                    {
                        offLine += USER;
                        offlineCount++;
                    }
                    else
                    {
                        ROLE += USER;
                        roleMembers++;
                    }
                }
                if (roleMembers > 0)
                {
                    USERS += ROLE_NAME.Replace("[[COUNT]]", roleMembers.ToString());
                    USERS += ROLE;
                }
            }
            if (offlineCount > 0)
            {
                USERS += $"<header class='membersGroup-v9BXpm container-2ax-kl'>Offline — {offlineCount}</header>";
                USERS += offLine;
            }
            if (blockedUsers > 0)
                USERS += $"<header class='membersGroup-v9BXpm container-2ax-kl'>Blocked — {blockedUsers}</header>";
            return USERS;
        }

        [Method("GET"), Path("/" + urlName)]
        public void Base(ulong guild = 0, ulong channel = 0, ulong msg = 0)
        {
            string guilds = getGuilds();
            var gul = guild == 0 ? getDefaultGuild() : Program.Client.GetGuild(guild);
            Self = gul.GetUser(Context.User.Id);
            var chnl = channel == 0 ? getDefaultChannel(gul) : gul.GetTextChannel(channel);
            string chnls = getChannels(gul, chnl);
            string chat = getChat(gul, chnl, msg == 0 ? ulong.MaxValue : msg);
            string users = getUsers(gul, chnl);
            ReplyFile("base.html", 200, new Replacements()
                .Add("guilds", guilds)
                .Add("channels", chnls)
                .Add("chat", chat)
                .Add("users", users)
                .Add("guildid", gul.Id)
                .Add("chnlid", chnl.Id));
        }

        [Method("GET"), Path("/" + urlName + "/messages")]
        public void GetMoreMessages(ulong guild, ulong channel, ulong before)
        {
            var gul = guild == 0 ? getDefaultGuild() : Program.Client.GetGuild(guild);
            Self = gul.GetUser(Context.User.Id);
            var chnl = channel == 0 ? getDefaultChannel(gul) : gul.GetTextChannel(channel);
            string chat = getChat(gul, chnl, before == 0 ? ulong.MaxValue : before);
            RespondRaw(chat, 200);
        }

        [Method("POST"), Path("/" + urlName + "/message")]
        public void SendMessage(ulong guild, ulong channel)
        {
            var gul = Program.Client.GetGuild(guild);
            if (gul == null)
            {
                RespondRaw("Guild unknown", 404);
                return;
            }
            Self = gul.GetUser(Context.User.Id);
            if (Self == null)
            { // security through obscurity
                RespondRaw("Guild not found", 404);
                return;
            }
            var chnl = gul.GetTextChannel(channel);
            if (chnl == null)
            {
                RespondRaw("Unknwn channel", 404);
                return;
            }
            if (!hasAccessTo(chnl))
            {
                RespondRaw("Channel not found", 404);
                return;
            }
            if (!hasWritePermissions(chnl))
            {
                RespondRaw("Unable to send messages", 403);
                return;
            }
            string TEXT;
            using (StreamReader ms = new StreamReader(Context.Request.InputStream, Encoding.UTF8))
                TEXT = ms.ReadToEnd();
            if (string.IsNullOrWhiteSpace(TEXT) || TEXT.Length > 2000)
            {
                RespondRaw("Failed: Message invalid. Check length", 400);
                return;
            }
            try
            {
                var hook = getOrCreateWebhook(chnl);
                Discord.Webhook.DiscordWebhookClient vv = new Discord.Webhook.DiscordWebhookClient(hook);
                vv.SendMessageAsync(TEXT, username: (Self.Nickname ?? Self.Username), avatarUrl: Self.GetAvatarUrl());
                RespondRaw("");
            }
            catch (Exception ex)
            {
                Program.LogMsg("VPN", ex);
                RespondRaw(ex.Message, System.Net.HttpStatusCode.InternalServerError);
            }
        }
    }
}
