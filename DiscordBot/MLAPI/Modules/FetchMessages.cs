using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Commands.Modules.MLAPI;
using DiscordBot.Services;
using DiscordBot.Utils;
using EduLinkDLL.API.Models;
using Markdig.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI.Modules
{
    [RequireAuthentication]
    public class VPN : APIBase
    {
        public MsgService DB { get; set; }
        const string urlName = "vpn";
        const string channelIcon = "<path fill='currentColor' d='M14 8C14 7.44772 13.5523 7 13 7H9.76001L10.3657 3.58738C10.4201 3.28107 10.1845 3 9.87344 3H8.88907C8.64664 3 8.43914 3.17391 8.39677 3.41262L7.76001 7H4.18011C3.93722 7 3.72946 7.17456 3.68759 7.41381L3.51259 8.41381C3.45905 8.71977 3.69449 9 4.00511 9H7.41001L6.35001 15H2.77011C2.52722 15 2.31946 15.1746 2.27759 15.4138L2.10259 16.4138C2.04905 16.7198 2.28449 17 2.59511 17H6.00001L5.39427 20.4126C5.3399 20.7189 5.57547 21 5.88657 21H6.87094C7.11337 21 7.32088 20.8261 7.36325 20.5874L8.00001 17H14L13.3943 20.4126C13.3399 20.7189 13.5755 21 13.8866 21H14.8709C15.1134 21 15.3209 20.8261 15.3632 20.5874L16 17H19.5799C19.8228 17 20.0306 16.8254 20.0724 16.5862L20.2474 15.5862C20.301 15.2802 20.0655 15 19.7549 15H16.35L16.6758 13.1558C16.7823 12.5529 16.3186 12 15.7063 12C15.2286 12 14.8199 12.3429 14.7368 12.8133L14.3504 15H8.35045L9.41045 9H13C13.5523 9 14 8.55228 14 8Z'></path>";
        public bool canViewAllChannels = false;
        public VPN(APIContext c) : base(c, "vpn")
        {
            canViewAllChannels = Program.AppInfo.Owner.Id == c.User?.Id;
            DB = Program.Services.GetRequiredService<MsgService>();
        }
        
        
        bool hasAccessTo(SocketTextChannel c)
        {
            if (canViewAllChannels)
                return true;
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
            if (canViewAllChannels)
                return true;
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

        HTMLBase getTextChannel(SocketTextChannel chnl, SocketTextChannel selected)
        {
            var guild = chnl.Guild;
            var container = new Div(cls: "containerDefault--pIXnN");
            var iconVisibility = new Div(cls: "iconVisibility-sTNpHs wrapper-2jXpOf");
            if (chnl.Id == selected?.Id)
                iconVisibility.ClassList.Add("modeSelected-346R90");
            DateTime last;
            if (Context.User.LastVisitVPN.TryGetValue(chnl.Id, out var item))
                last = item;
            else
                last = DateTime.Now.AddDays(-1);
            var diff = DateTime.Now - last;
            int count = 0;
            if (diff.TotalMinutes >= 10)
                count = numUnreadMessages(chnl, out bool mention);
            if (count > 0)
            {
                iconVisibility.ClassList.Add("modeUnread-1qO3K1");
                iconVisibility.Children.Add(new Div(cls: "unread-2lAfLh"));
            }
            container.Children.Add(iconVisibility);
            var content = new Div(cls: "content-1x5b-n");
            iconVisibility.Children.Add(content);
            var href = new Anchor($"/{urlName}?guild={guild.Id}&channel={chnl.Id}", "", cls: "mainContent-u_9PKf");
            content.Children.Add(href);
            var svg = new Svg("0 0 24 24", "24", "24", cls: "icon-1DeIlz");
            href.Children.Add(svg);
            svg.Children.Add(new RawObject(channelIcon));

            href.Children.Add(new Div(cls: "name-23GUGE overflow-WK9Ogt").WithRawText(chnl.Name));
            return container;
        }

        HTMLBase getCategoryChannel(SocketCategoryChannel chnl)
        {
            var container = new Div(cls: "containerDefault-3tr_sE");
            var iconVisibility = new Div(cls: "iconVisibility-fhcwiH wrapper-PY0fhH clickable-536fPF");
            container.Children.Add(iconVisibility);
            var mainContent = new Div(cls: "mainContent-2h-GEV");
            iconVisibility.Children.Add(mainContent);
            mainContent.Children.Add(new H2(cls: "name-3l27Hl container-2ax-kl").WithRawText(chnl.Name.ToUpper()));
            return container;
        }

        HTMLBase getChannels(SocketGuild guild, SocketTextChannel selected)
        {
            var usr = guild.GetUser(Context.User.Id);
            if (usr == null)
                return new Div().WithRawText("An internal error occured processing this request");
            var chnlContent = new Div(cls: "content-3YMskv");
            foreach (var txt in guild.TextChannels.Where(x => x.CategoryId.HasValue == false).OrderBy(x => x.Position))
                chnlContent.Children.Add(getTextChannel(txt, selected));
            foreach(var cat in guild.CategoryChannels.OrderBy(x => x.Position))
            {
                chnlContent.Children.Add(getCategoryChannel(cat));
                foreach(var chnl in cat.Channels.OrderBy(x => x.Position))
                {
                    if (chnl is SocketTextChannel txt)
                    {
                        if (!hasAccessTo(txt))
                            continue;
                        chnlContent.Children.Add(getTextChannel(txt, selected));
                    }
                }
            }
            return chnlContent;
        }

        static string getRGBFromColor(Color clr, string a = "")
        {
            return $"rgb({clr.R}, {clr.G}, {clr.B}{(string.IsNullOrWhiteSpace(a) ? "" : ", " + a)})";
        }

        string getRoleColor(SocketGuildUser user)
        {
            if (user != null)
            {
                foreach (var role in user.Roles.OrderByDescending(x => x.Position))
                {
                    if (role.Color == Color.Default)
                        continue;
                    return getRGBFromColor(role.Color);
                }
            }
            return $"rgb(185,187,190)";
        }

        SocketGuildUser Self;
        bool HasUnreadMessages = false;
        
        Div addDateSep(DateTime now, bool isNew)
        {
            var div = new Div()
            {
                ClassList =
                {
                    "divider-3_HH5L",
                    "hasContent-1_DUdQ" +
                    "divider-JfaTT5" +
                    "hasContent-1cNJDh"
                }
            };
            if (isNew)
                div.ClassList.Add("msg-unread");
            div.Children.Add(new Span(cls: "content-1o0f9g")
            {
                RawText = now.ToString("MMMM d, yyyy")
            });
            return div;
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

        #region Message HTML

        static string[] imageExtensions = new string[] { "jpeg", "jpg", "png" };
        static bool isImage(string x)
        {
            foreach (var ext in imageExtensions)
                if (x.ToLower().EndsWith(ext))
                    return true;
            return false;
        }

        static HTMLBase getMsgAttachments(ReturnedMsg message, bool doProxy)
        {
            var split = message.Attachments.Split(',').Where(x => isImage(x)).ToList();
            if (split.Count == 0)
                return null;
            var container = new Div(cls: "container-1ov-mD");
            foreach(var image in split)
            {
                var anchor = new Anchor(image, cls: "anchor-3Z-8Bb anchorUnderlineOnHover-2ESHQB imageWrapper-2p5ogY imageZoom-1n-ADA clickable-3Ya1ho embedWrapper-lXpS3L");
                anchor.RawText = "";
                anchor.Style = "min-height: 50px; width: 100%;";
                var img = new DiscordBot.Classes.HTMLHelpers.Objects.Img();
                img.Style = "position: relative; max-height: 300px; max-width: 90%; width: auto;";
                if(doProxy)
                {
                    var uri = new Uri(image);
                    img.Src = $"{Handler.LocalAPIUrl}{uri.PathAndQuery}";
                } else
                {
                    img.Src = image;
                }
                anchor.Children.Add(img);
                container.Children.Add(anchor);
            }
            return container;
        }

        static string parseMarkdown(IGuild guild, string content, ulong contextUser, out bool mentioned, bool redact = false)
        {
            mentioned = false;
            string basic = content;
            if(redact)
            {
                var sb = new StringBuilder();
                for(int i = 0; i <basic.Length; i++)
                {
                    if(basic[i].IsAlpha())
                    {
                        sb.Append('█');
                    } else
                    {
                        sb.Append(basic[i]);
                    }
                }
                basic = sb.ToString();
            }

            #region Mention Parsing

            #region User Mentions
            var rgx = new Regex(@"(?<!\\)<@!?([0-9]{17,18})>");
            var done = new List<ulong>();
            foreach (Match match in rgx.Matches(basic))
            {
                var userId = ulong.Parse(match.Groups[1].Value);
                if (done.Contains(userId))
                    continue;
                if (userId == contextUser)
                    mentioned = true;
                done.Add(userId);
                string display = "@unkown-user";
                var user = guild.GetUserAsync(userId).Result;
                if (user != null)
                {
                    display = $"@{user.Nickname ?? user.Username}";
                }
                else
                {
                    var any = Program.GetUserOrDefault(userId);
                    if (any != null)
                        display = $"@{any.Name}";
                }
                var span = new Span(cls: "mention wrapper-3WhCwL mention interactive")
                    .WithTag("tabindex", "0")
                    .WithTag("data-id", $"{userId}")
                    .WithRawText(display);
                basic = basic.Replace(match.Value, span.ToString());
            }
            #endregion

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
            basic = Markdig.Markdown.ToHtml(basic).Replace("<p>", "").Replace("</p>", "");
            // Skip for now.
            #endregion
            return basic;
        }

        static HTMLBase getEmbed(IGuild guild, IEmbed embed, ulong contextUser)
        {
            var embedContainer = new Div(cls: "embedWrapper-lXpS3L embedFull-2tM8-- embed-IeVjo6 markup-2BOw-j");
            embedContainer.Style = $"border-color: {getRGBFromColor(embed.Color ?? Color.Default)};";
            var grid = new Div(cls: "grid-1nZz7S");
            embedContainer.Children.Add(grid);

            if(!string.IsNullOrWhiteSpace(embed.Title))
            {
                grid.Children.Add(new Div(cls: "embedTitle-3OXDkz embedMargin-UO5XwE").WithRawText(embed.Title));
            }
            if(!string.IsNullOrWhiteSpace(embed.Description))
            {
                var descMrkd = parseMarkdown(guild, embed.Description, contextUser, out _);
                grid.Children.Add(new Div(cls: "embedDescription-1Cuq9a embedMargin-UO5XwE").WithRawText(descMrkd));
            }
            if(embed.Fields.Length > 0)
            {
                var fields = new Div(cls: "embedFields-2IPs5Z");
                var rows = new List<EmbedField?[]>();
                grid.Children.Add(fields);
                var currentRow = new EmbedField?[3];
                foreach(var field in embed.Fields)
                {
                    if(!field.Inline)
                    {
                        if(currentRow[0] != null)
                        {
                            rows.Add(currentRow);
                            currentRow = new EmbedField?[3];
                        }
                        currentRow[0] = field;
                        rows.Add(currentRow);
                        currentRow = new EmbedField?[3];
                    }
                    else
                    {
                        if(currentRow[2] != null)
                        {
                            rows.Add(currentRow);
                            currentRow = new EmbedField?[3];
                        }
                        if (currentRow[0] == null)
                            currentRow[0] = field;
                        else if (currentRow[1] == null)
                            currentRow[1] = field;
                        else
                            currentRow[2] = field;
                    }
                }
                if (currentRow[0] != null)
                    rows.Add(currentRow);


                foreach (var array in rows)
                {
                    int length = array[2] != null ? 3 : array[1] != null ? 2 : 1;
                    for(int i = 0; i < length; i++)
                    {
                        var field = array[i].Value;
                        var embedField = new Div(cls: "embedField-1v-Pnh");
                        embedField.Style = $"grid-column: {getGrid(i, length)}";
                        embedField.Children.Add(new Div(cls: "embedFieldName-NFrena").WithRawText(field.Name));
                        embedField.Children.Add(new Div(cls: "embedFieldValue-nELq2s")
                            .WithRawText(parseMarkdown(guild, field.Value, contextUser, out _)));
                        fields.Children.Add(embedField);
                    }
                }
            }
            if (embed.Footer.HasValue || embed.Timestamp.HasValue)
            {
                var footer = new Div(cls: "embedFooter-3yVop- embedMargin-UO5XwE");
                grid.Children.Add(footer);

                var footerText = new Div(cls: "embedFooterText-28V_Wb");
                footer.Children.Add(footerText);

                if (embed.Footer.HasValue && !string.IsNullOrWhiteSpace(embed.Footer.Value.Text))
                {
                    footerText.RawText = embed.Footer.Value.Text;
                }
                if(embed.Timestamp.HasValue)
                {
                    if(!string.IsNullOrWhiteSpace(footerText.RawText))
                    {
                        footerText.RawText += new Span(cls: "embedFooterSeparator-3klTIQ").WithRawText("•");
                    }
                    footerText.RawText += embed.Timestamp.Value.ToString($"yyyy/MM/dd HH:mm:ss.fff");
                }
            }
            return embedContainer;
        }

        static string getGrid(int rowNumber, int total)
        {
            if (total == 1)
                return "1 / 13";
            if(total == 2)
                return rowNumber == 0 ? "1 / 7" : "7 / 13";
            if (rowNumber == 0)
                return "1 / 5";
            if (rowNumber == 1)
                return "5 / 9";
            return "9 / 13";
        }

        static HTMLBase getMsgContent(IGuild guild, ReturnedMsg m, ulong contextUser, bool shouldRedactDeleted, out bool mentioned)
        {
            var div = new Div(cls: "markup-2BOw-j messageContent-2qWWxC");
            var basic = parseMarkdown(guild, m.Content, contextUser, out mentioned, shouldRedactDeleted);
            return div.WithRawText(basic);
        }

        static HTMLBase getMessageHeader(ReturnedMsg msg, string authorName, string roleColor)
        {
            var h2 = new H2(cls: "header-23xsNx");
            h2.Children.Add(new Span(cls: "latin24CompactTimeStamp-2V7XIQ timestamp-3ZCmNB timestampVisibleOnHover-2bQeI4")
            {
                Children =
                {
                    new Span()
                    {
                        Children =
                        {
                            new ItalicText("[", cls: "separator-2nZzUB").WithTag("aria-hidden", "true"),
                            new RawObject(msg.CreatedAt.ToString("hh:mm tt")),
                            new ItalicText("] ", cls: "separator-2nZzUB").WithTag("aria-hidden", "true")
                        }
                    }.WithTag("aria-label", msg.CreatedAt.ToString("hh:mm tt"))

                }
            });
            var usernameStuffs = new Span(cls: "headerText-3Uvj1Y");
            usernameStuffs.Children.Add(new Span(cls: "username-1A8OIy clickable-1bVtEA")
                                    .WithTag("style", $"color: {roleColor}")
                                    .WithTag("tabIndex", "0")
                                    .WithTag("aria-controls", "popout_320")
                                    .WithTag("aria-expanded", "false")
                                    .WithTag("role", "button")
                                    .WithRawText(authorName));
            usernameStuffs.Children.Add(new ItalicText(":", cls: "separator-2nZzUB"));
            h2.Children.Add(usernameStuffs);
            return h2;
        }

        public static HTMLBase getMessage(ReturnedMsg msg, IGuild guild, string authorName, string roleColor, ulong contextUser, bool shouldRedact, bool doProxy)
        {
            var messageDiv = new Div(id: msg.Id.ToString(), cls: "message-2qnXI6 groupStart-23k01U wrapper-2a6GCs compact-T3H92H zalgo-jN1Ica");
            messageDiv.WithTag("role", "group")
                .WithTag("tabindex", "-1");
            messageDiv.OnClick = "ctrlPopupId(this, event);";
            var contentsDiv = new Div(cls: "contents-2mQqc9").WithTag("role", "document");
            if (msg.IsDeleted)
                messageDiv.ClassList.Add("message-deleted");
            contentsDiv.Children.Add(getMessageHeader(msg, authorName, roleColor));
            bool mentioned = false;
            try
            {
                contentsDiv.Children.Add(getMsgContent(guild, msg, contextUser, shouldRedact, out mentioned));
            }
            catch (Exception ex)
            {
                Program.LogMsg($"Failed with {msg.Id}", LogSeverity.Critical, "VPN");
                Program.LogMsg($"{msg.Author == null}");
                Program.LogMsg($"{msg.Content == null}");
                Program.LogMsg($"{msg.CreatedAt == null}");
                Program.LogMsg($"{msg.Timestamp == null}");
                throw;
            }
            var attachments = getMsgAttachments(msg, doProxy);
            if (attachments != null)
                contentsDiv.Children.Add(attachments);
            messageDiv.Children.Add(contentsDiv);
            if (mentioned)
                messageDiv.ClassList.Add("mentioned-xhSam7");

            if(msg.Embeds.Count > 0)
            {
                var containerDiv = new Div(cls: "container-1ov-mD");
                messageDiv.Children.Add(containerDiv);
                foreach(var embed in msg.Embeds)
                {
                    containerDiv.Children.Add(getEmbed(guild, embed, contextUser));
                }
            }

            return messageDiv;
        }

        #endregion


        HTMLBase getChat(SocketGuild guild, SocketTextChannel channel, ulong before = ulong.MaxValue)
        {
            if (!hasAccessTo(channel))
            {
                return new Div(cls: "error") { RawText = "You are unable to access this channel" };
            }
            List<ReturnedMsg> messages = DB.GetCombinedMsgs(guild.Id, channel.Id, before, limit: 100).Result;
            var main = new Div(cls: "scrollerInner-2YIMLh")
                .WithTag("role", "log")
                .WithTag("tabindex", "0")
                .WithTag("id", "chat-messages");
            DateTime lastMessage = DateTime.MinValue;
            bool hasSetNew = false;
            foreach (var msg in messages.OrderBy(x => x.Id))
            {
                if (main.Children.Count == 0 && messages.Count == 100)
                { // if count below 100, then clearly there are no more messages to find, as we cant even get 100
                    main.Children.Add(new Div(id: "loadMoreBtn", cls: "hasMore-3e72_v")
                    {
                        OnClick = $"loadMore('{msg.Id}');",
                        RawText = "Load more messages"
                    }
                    .WithTag("tabIndex", "0")
                    .WithTag("role", "button")
                    );
                }
                bool isNew = hasSetNew == false && isMessageNew(channel, msg);
                if (lastMessage.DayOfYear != msg.CreatedAt.DayOfYear || isNew)
                {
                    main.Children.Add(addDateSep(msg.CreatedAt.DateTime, isNew));
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
                var mDiv = getMessage(msg, guild, authorName, roleColor, 
                    Context.User.Id, msg.IsDeleted && canViewAllChannels == false, 
                    Context.IsBehindFirewall);
                main.Children.Add(mDiv);
            }
            if (before != ulong.MaxValue)
            { // offer ability to jump back to present
                main.Children.Add(new Div(cls: "hasMore-3e72_v")
                {
                    OnClick = "loadMore('0')",
                    RawText = "Back to present"
                }.WithTag("tabIndex", "0").WithTag("role", "button"));
            }
            Context.User.LastVisitVPN[channel.Id] = DateTime.Now;
            Program.Save();
            var overall = new Div(cls: "scrollerContent-WzeG7R content-3YMskv");
            overall.Children.Add(main);
            return overall;
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

        const string onlineFill = "#43b581";
        (string, string) getStatusMaskInfo(SocketGuildUser user)
        {
            if (user.ActiveClients.Any(x => x == Discord.ClientType.Mobile))
                return ("online-mobile", onlineFill);
            if (user.Status == UserStatus.Online)
                return ("online", onlineFill);
            if (user.Status == UserStatus.DoNotDisturb)
                return ("dnd", "#f04747");
            if (user.Status == UserStatus.Idle || user.Status == UserStatus.AFK)
                return ("idle", "orange");
            return ("none", "black");
        }

        HTMLBase getUserAvatar(SocketGuildUser user)
        {
            var avatar = new Div(cls: "avatar-3uk_u9");
            var div = new Div(cls: "wrapper-3t9DeA")
                .WithTag("style", "width: 32px; height: 32px;");
            avatar.Children.Add(div);
            var svg = new Svg("0 0 40 32", "40", "32", cls: "mask-1l8v16 svg-2V3M55");
            div.Children.Add(svg);
            var foreignObject = new ForeignObject()
                .WithTag("x", "0").WithTag("y", "0")
                .WithTag("width", "32").WithTag("height", "32")
                .WithTag("mask", "url(#svg-mask-avatar-status-round-32)");
            var img = new Img(cls: "avatar-VxgULZ")
            {
                Src = user.GetAnyAvatarUrl()
            };
            foreignObject.Children.Add(img);
            svg.Children.Add(foreignObject);

            var status = new Rect("22", "22", "10", "10", cls: "pointerEvents-2zdfdO")
            {
                Fill = "#43b581",
            };
            (string ending, string colour) = getStatusMaskInfo(user);
            status.Mask = $"url(#svg-mask-status-{ending})";
            status.Fill = colour;
            if(ending != "none")
                svg.Children.Add(status);
            return avatar;
        }

        HTMLBase getUserSubText(SocketGuildUser user)
        {
            var subText = new Div(cls: "subText-1KtqkB");
            if (user.Activity == null)
                return subText;
            var activity = new Div(cls: "activity-2Gy-9S");
            subText.Children.Add(activity);
            var txt = $"{user.Activity.Type} <strong>{user.Activity.Name}</strong>";
            if (user.Activity is CustomStatusGame cs)
                txt = cs.State;
            var activityText = new Div(cls: "activityText-yGKsKm");
            activity.Children.Add(activityText);

            activityText.RawText = txt;

            var activityRuler = new Div(cls: "textRuler-wO-qHe activityText-yGKsKm")
                .WithTag("aria-hidden", "true");
            activityRuler.RawText = txt;
            activity.Children.Add(activityRuler);
            return subText;
        }
        HTMLBase getUserContent(SocketGuildUser user)
        {
            var content = new Div(cls: "content-3QAtGj");

            var nameAndDecorators = new Div(cls: "nameAndDecorators-5FJ2dg");
            content.Children.Add(nameAndDecorators);
            var name = new Div(cls: "name-uJV0GL");
            nameAndDecorators.Children.Add(name);
            var nameSpan = new Span(cls: "roleColor-rz2vM0");
            name.Children.Add(nameSpan);
            nameSpan.Style = "color:" + getRoleColor(user);
            nameSpan.RawText = user.Nickname ?? user.Username;

            if(user.IsBot)
            {
                var botDiv = new Div(cls: "botTag-3W9SuW botTagRegular-2HEhHi botTag-2WPJ74 px-10SIf7");
                botDiv.Children.Add(new Span(cls: "botText-1526X_").WithRawText("BOT"));
                nameAndDecorators.Children.Add(botDiv);
            }

            content.Children.Add(getUserSubText(user));
            return content;
        }
        HTMLBase getUser(SocketGuildUser user)
        {
            var memberContainer = new Div(cls: "member-3-YXUe container-2Pjhx- clickable-1JJAn8");
            var containerLayout = new Div(cls: "layout-2DM8Md");
            memberContainer.Children.Add(containerLayout);
            containerLayout.Children.Add(getUserAvatar(user));
            containerLayout.Children.Add(getUserContent(user));
            return containerLayout;
        }

        HTMLBase getUsers(SocketGuild guild, SocketTextChannel channel)
        {
            if (guild == null || channel == null)
                return new Div().WithRawText("An unknown error occured!");
            var membersWrap = new Div(cls: "membersWrap-2h-GB4 hiddenMembers-2dcs_q");
            var members = new Div(cls: "members-1998pB thin-1ybCId scrollerBase-289Jih fade-2kXiP2")
                .WithTag("style", "overflow: hidden scroll; padding-right: 0px;")
                .WithTag("id", $"members-{channel.Id}");
            membersWrap.Children.Add(members);
            var content = new Div(cls: "content-3YMskv");
            members.Children.Add(content);

            List<ulong> done = new List<ulong>();
            var offlineList = new List<HTMLBase>();
            int offlineCount = 0;
            foreach (var role in guild.Roles.OrderByDescending(x => x.Position))
            {
                if (!role.IsHoisted && !role.IsEveryone)
                    continue;
                var header = new H2(cls: "membersGroup-v9BXpm container-2ax-kl");
                var headerSpan = new Span();
                header.Children.Add(headerSpan);
                int roleMembers = 0;
                foreach (var member in channel.Users)
                {
                    if (done.Contains(member.Id))
                        continue;
                    if (!member.Roles.Contains(role))
                        continue;
                    var div = getUser(member);
                    done.Add(member.Id);
                    if (member.Status == UserStatus.Offline)
                    {
                        offlineList.Add(div);
                        offlineCount++;
                    }
                    else
                    {
                        roleMembers++;
                        if (roleMembers == 1) // must add header
                            members.Children.Add(header);
                        members.Children.Add(div);
                    }
                }
                if (roleMembers > 0)
                {
                    headerSpan.RawText = $"{role.Name} — {roleMembers}";
                }
            }
            if (offlineCount > 0)
            {
                members.Children.Add(new H2(cls: "membersGroup-v9BXpm container-2ax-kl")
                {
                    Children =
                    {
                        new Span(cls: "").WithRawText($"Offline — {offlineCount}")
                    }
                });
                foreach (var x in offlineList)
                    members.Children.Add(x);
            }
            return membersWrap;
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
    
        [Method("GET")]
        [Path("/attachments/{channel_id}/{message_id}/{filename}")]
        [Regex("channel_id", @"[0-9]{17,18}")]
        [Regex("message_id", @"[0-9]{17,18}")]
        [Regex("filename", "[A-Za-z0-9]+")]
        public void ProxyImage(ulong channel_id, ulong message_id, string filename)
        {
            var client = Program.Services.GetRequiredService<HttpClient>();
            var stream = client.GetStreamAsync($"https://cdn.discordapp.com{Context.Path}").Result;
            stream.CopyTo(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
            StatusSent = 200;
        }
    }
}
