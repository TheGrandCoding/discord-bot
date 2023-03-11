using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Commands;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class Docs : AuthedAPIBase
    {
        public Docs(APIContext c) : base(c, "bot")
        {
            Sidebar = SidebarType.None;
            InjectObjects = new List<HTMLBase>();
        }
        private Div msgBox(string type, string message)
        {
            return new Div(cls: "alert-box " + type)
            {
                Children =
                {
                    new RawObject($"<blockquote>{message}</blockquote>")
                }
            };
        }
        private Div info(string message) => msgBox("info", message);
        private Div warn(string message) => msgBox("warn", message);

        string CurrentLook { get; set; }

        string getListInfo(ModuleInfo module, int level)
        {
            // assume we're already in an OL, just set correct indent and headers
            string text = $"<li class='level-{level}'>" +
                $"{aLink("/docs/cmd/" + module.Name.Replace(" ", "."), module.Name)}</li>";
            foreach (var child in module.Submodules)
                text += getListInfo(child, level + 1);
            return text;
        }

        Replacements rep(string cmd, string api)
        {
            return new Replacements()
                .Add("sideContent", getSidebar(cmd, api));
        }

        string span(string cls, string content) => $"<span class='{cls}'>{content}</span>";
        string getInfo(Discord.Commands.ParameterInfo param)
        {
            string text = " ";
            text += param.IsOptional ? span("paramo", "[") : span("paramr", "&lt;");
            text += span("paramtype", param.Type.Name);
            text += param.IsOptional
                ? span("paramname paramo", " " + param.Name)
                : span("paramname paramr", " " + param.Name);
            text += param.IsOptional ? span("paramo", "]") : span("paramr", "&gt;");
            return text;
        }
        string getInfo(CommandInfo cmd)
        {
            string text = "<div class='cmd'>";
            text += $"<code>{Program.Prefix}{cmd.Aliases.First()}";
            foreach (var param in cmd.Parameters)
                text += getInfo(param);
            text += "</code>";
            text += getInfoBoxes(cmd);
            text += $"<p>{(string.IsNullOrWhiteSpace(cmd.Summary) ? "No summary" : cmd.Summary)}</p>";
            return text + "</div>";
        }
        static CmdDisableService disable;
        string getInfoBoxes(ModuleInfo module)
        {
            string text = "";
            disable ??= Context.Services.GetRequiredService<CmdDisableService>();
            if(disable.IsDisabled(module, out var reason))
            {
                text += $"<div class='msgBox error'><p><strong>This module has been disabled</strong></p>" +
                    $"<p>{reason}</p>";
            }
            foreach(var x in module.Attributes)
            {
                if (x is DocBoxAttribute at)
                    text += at.HTML();
            }
            return text;
        }
        string getInfoBoxes(CommandInfo cmd)
        {
            string text = "";
            disable ??= Context.Services.GetRequiredService<CmdDisableService>();
            if (disable.IsDisabled(cmd, out var reason))
            {
                text += $"<div class='msgBox error'><p><strong>This command has been disabled</strong></p>" +
                    $"<p>{reason}</p>";
            }
            foreach (var x in cmd.Attributes)
            {
                if (x is DocBoxAttribute at)
                    text += at.HTML();
            }
            return text;
        }
        ListItem[] listPerms(ChannelPermission? channel, GuildPermission? guild)
        {
            var ls = new List<ListItem>();
            var flags = new List<ulong>();
            if(channel != null)
            {
                var a = new ChannelPermissions((ulong)channel);
                foreach (var x in a.ToList())
                {
                    ls.Add(new ListItem(x.ToString()));
                    flags.Add((ulong)x);
                }
            }
            if(guild != null)
            {
                var b = new GuildPermissions((ulong)guild);
                foreach (var x in b.ToList())
                    if(!flags.Contains((ulong)x))
                        ls.Add(new ListItem(x.ToString()));
            }
            return ls.ToArray();
        }
        UnorderedList htmlListPerms(ChannelPermission? channel, GuildPermission? guild)
        {
            var perms = listPerms(channel, guild);
            var ul = new UnorderedList();
            foreach (var x in perms)
                ul.AddItem(x);
            return ul;
        }
        
        
        #region Common

        public static string escapeForUrl(string text)
            => text == null
            ? null
            : Uri.EscapeDataString(text.ToLower()
                .Replace("/", "-")
                .Replace(" ", "-")
                .Replace(".", "_"));
        string unescapeFromUrl(string text)
            => text == null
            ? null
            : Uri.UnescapeDataString(text)
                .Replace("_", ".")
                .Replace("-", " ");

        public static HTMLBase linkHeader(int order, string text, string id = null, string cls = null, string linkText = null)
        {
            id ??= escapeForUrl(text);
            var html = new Header(order, null, id, cls);
            if (order == 1)
                html.ClassList.Add("h1-E4giPK");
            else if (order == 2)
                html.ClassList.Add("h2-2MZoq3");
            else if (order == 3)
                html.ClassList.Add("h3-1nY9uO");
            else if (order == 6)
                html.ClassList.Add("h6-3ZuB-g");
            if (linkText != null)
                html.Children.Add(new Anchor(linkText, text));
            else
                html.Children.Add(new RawObject(text));
            var anchor = new Anchor("#" + id, "", cls: "anchor-3Z-8Bb hyperlink");
            anchor.Children.Add(new Div().WithTag("name", id));
            html.Children.Add(anchor);
            return html;
        }
        Span docParagraph()
        {
            return new Span(cls: "paragraph-mttuxw");
        }

        HTMLBase getSidebar(string cmdSelected, string apiSelected)
        {
            var wrapper = new Div(cls: "wrapper-36iaZw");
            var scrollerWrap = new Div(cls: "scrollerWrap-2lJEkd scrollerWrapper-2xG8VZ scrollerThemed-2oenus themeGhost-28MSn0");
            wrapper.Children.Add(scrollerWrap);

            var scroller = new Div(cls: "scroller-2FKFPG scroller-2y6PPh");
            scrollerWrap.Children.Add(scroller);

            var wrapperInner = new Div(cls: "wrapperInner-2p1_wN wrapperInner-2HPIEA");
            scroller.Children.Add(wrapperInner);

            var content = new Div(cls: "content-32JuGj wrapper-30VLTo");
            wrapperInner.Children.Add(content);

            var subNavigation = new Div(cls: "section-X9hK_F flush-27Pr3U subNavigation-2ZLDNC");
            content.Children.Add(subNavigation);

            subNavigation.Children.Add(getSidebarCommands(cmdSelected));
            subNavigation.Children.Add(getSidebarAPI(apiSelected));

            return wrapper;
        }

        Div explainPrecondition(PreconditionAttribute attribute, bool module)
        {
            string summary;
            string type = module ? "This module " : "This command ";
            if (attribute is RequireContextAttribute rca)
                summary = $"must be executed in a {rca.Contexts}";
            else if (attribute is RequireUserPermissionAttribute rupa)
                summary = $"must be executed by a user who has the following permission:<br/>" +
                    htmlListPerms(rupa.ChannelPermission, rupa.GuildPermission);
            else if (attribute is RequireOwnerAttribute)
                summary = $"must be executed by the Owner of the bot";
            else if (attribute is RequireBotPermissionAttribute rbpa)
                summary = $"requires the bot's account to have the following permissions:<br/>" +
                    htmlListPerms(rbpa.ChannelPermission, rbpa.GuildPermission);
            else if (attribute is RequirePermission rp)
                summary = $"must be executed by a user who has the <code>{rp.Node}</code> permission";
            else
                summary = attribute.GetType().Name.Replace("Attribute", "");
            return warn(type + summary);
        }

        Div explainPreconditions(bool module, params PreconditionAttribute[] attributes)
        {
            if (attributes.Length == 0)
                return new Div();
            if (attributes.Length == 1)
                return explainPrecondition(attributes[0], module);
            var orGroups = new Dictionary<string, List<PreconditionAttribute>>();
            var andGroups = new Dictionary<string, List<PreconditionAttribute>>();
            foreach (var x in attributes)
            {
                if (string.IsNullOrWhiteSpace(x.Group))
                {
                    andGroups.AddInner("", x);
                }
                else
                {
                    orGroups.AddInner(x.Group, x);
                }
            }
            var div = new Div();
            foreach (var or in orGroups)
            {
                var orDiv = info("Any of the following must be satisfied:");
                foreach (var x in or.Value)
                    orDiv.Children.Add(explainPrecondition(x, module));
                div.Children.Add(orDiv);
            }
            foreach (var and in andGroups)
            {
                var andDiv = info("All of the following apply:");
                foreach (var x in and.Value)
                    andDiv.Children.Add(explainPrecondition(x, module));
                div.Children.Add(andDiv);
            }
            if (div.Children.Count == 1)
                return div.Children[0] as Div;
            return div;
        }


        string explainAuthentication(RequireAuthentication ra)
        {
            // This module must be executed by 
            if (!ra._auth)
                return "does not require authentication";
            var s = "must be executed by an authenticated session";
            if (ra._discord)
                return s + ", and that authenticated account must have a validly linked email address";
            return s;
        }
        UnorderedList listWithCode(string[] arr)
        {
            var ul = new UnorderedList();
            foreach (var x in arr)
                ul.AddItem(new ListItem()
                {
                    Children =
                    {
                        new Code(x)
                    }
                });
            return ul;
        }
        
        #endregion

        #region Commands

        Div getSidebarCommands(string selected)
        {
            var section = new Div(cls: "section-X9hK_F");

            section.Children.Add(new Paragraph("commands", cls:
                "heading-10iJKV marginBottom8-1wldKw small-29zrCQ size12-DS9Pyp" +
                " height16-3r2Q2W primary200-1Ayq8L weightSemiBold-tctXJ7 uppercase-1K74Lz"));

            var list = new UnorderedList(cls: "mainList-otExiM");
            section.Children.Add(list);

            foreach(var module in Program.Commands.Modules.OrderBy(x => x.Name))
            {
                var href = $"/docs/commands/{escapeForUrl(module.Name)}";
                var anchor = new Anchor(href,
                        text: module.Name,
                        cls: "navLink-1Neui4 navLinkSmall-34Tbhm");
                list.Children.Add(new ListItem()
                {
                    Children =
                    {
                        anchor
                    }
                });
                if(escapeForUrl(module.Name) == selected)
                {
                    anchor.ClassList.Add("activeLink-22b0_I");
                    var spy = new Div(cls: "ScrollSpy");
                    list.Children.Add(spy);
                    foreach(var x in module.Commands)
                    {
                        list.Children.Add(new Anchor($"{href}#{escapeForUrl(x.Name)}", x.Name,
                            cls: "anchor-3Z-8Bb subLink-3M1J2_"));
                    }
                }
            }

            return section;
        }

        Div cmdGetMain(ModuleInfo module)
        {
            var main = new Div(cls: "markdown-11q6EU");
            if(module == null)
            {
                main.Children.Add(msgBox("error", "There is no module by that name."));
                return main;
            }
            main.Children.Add(linkHeader(1, module.Name));
            if (module.Summary != null)
                main.Children.Add(docParagraph().WithRawText(module.Summary));
            if (module.Preconditions.Count > 0)
                main.Children.Add(explainPreconditions(true, module.Preconditions.ToArray()));

            foreach(var x in module.Commands)
            {
                var req = new HttpReqUrl(x.Aliases.First(), x.Name);
                foreach (var param in x.Parameters)
                    req.AddParam(param);
                main.Children.Add(req.ToHtml());
                if(x.Summary != null)
                    main.Children.Add(docParagraph().WithRawText(x.Summary));
                if (x.Preconditions.Count > 0)
                    main.Children.Add(explainPreconditions(false, x.Preconditions.ToArray()));
            }
            return main;
        }

        #endregion

        #region API
        Div explainPrecondition(APIPrecondition attribute, bool module)
        {
            string summary;
            string type = module ? "This module " : "This command ";
#if INCLUDE_CHESS
            if (attribute is RequireChess rc)
                summary = $"must be executed by Chess user with <code>{rc._perm}</code> permission";
#endif
            if (attribute is RequireAuthentication ra)
                summary = explainAuthentication(ra);
            else if (attribute is RequireApprovalAttribute raa)
                summary = raa._require ? "must be executed by a user who has been approved to use this website" : "does not need any approval";
            else if (attribute is RequireValidHTTPAgent rvh)
                summary = $"must send a User-Agent containing one of:<br/>" + listWithCode(rvh.ValidAgents);
            else if (attribute is RequirePermNode rpn)
                summary = $"requires all of the following permissions:</br>" + listWithCode(rpn.Nodes);
            else if (attribute is RequireVerifiedAccount rva)
                summary = rva._require ? "must be executed by a verified account" : "requires no verification";
            else if (attribute is RequireUser ru)
                summary = $"must be executed by {(Context.BotDB.GetUserFromDiscord(ru._user, true).Result.Value?.Name ?? $"a specific user, of ID {ru._user}")}";
            else if (attribute is RequireOwner)
                summary = $"must be executed by the developer of this bot";
            else if (attribute is RequireScopeAttribute rs)
                return new Div();
            else if (attribute is RequireNoExcessQuery rneq)
                summary = rneq.Required ? $"requires exactly the query parmaters requested" : "will ignore any additional query parmaters";
            else
                summary = attribute.GetType().Name.Replace("Attribute", "");
            return warn(type + summary);
        }
        Div explainPreconditions(bool module, params APIPrecondition[] attributes)
        {
            if (attributes.Length == 0)
                return new Div();
            if (attributes.Length == 1)
                return explainPrecondition(attributes[0], module);
            var orGroups = new Dictionary<string, List<APIPrecondition>>();
            var andGroups = new Dictionary<string, List<APIPrecondition>>();
            foreach(var x in attributes)
            {
                if (string.IsNullOrWhiteSpace(x.OR))
                {
                    andGroups.AddInner(x.AND, x);
                } else
                {
                    orGroups.AddInner(x.OR, x);
                    if (!string.IsNullOrWhiteSpace(x.AND))
                        andGroups.AddInner(x.AND, x);
                }
            }
            var div = new Div();
            foreach (var or in orGroups)
            {
                var orDiv = info("Any of the following must be satisfied:");
                foreach (var x in or.Value)
                    orDiv.Children.Add(explainPrecondition(x, module));
                div.Children.Add(orDiv);
            }
            foreach (var and in andGroups)
            {
                var andDiv = info("All of the following apply:");
                foreach (var x in and.Value)
                    andDiv.Children.Add(explainPrecondition(x, module));
                div.Children.Add(andDiv);
            }
            if (div.Children.Count == 1)
                return div.Children[0] as Div;
            return div;
        }

        Div getSidebarAPI(string selected)
        {
            var section = new Div(cls: "section-X9hK_F");

            section.Children.Add(new Paragraph("API routes", cls:
                "heading-10iJKV marginBottom8-1wldKw small-29zrCQ size12-DS9Pyp" +
                " height16-3r2Q2W primary200-1Ayq8L weightSemiBold-tctXJ7 uppercase-1K74Lz"));

            var list = new UnorderedList(cls: "mainList-otExiM");
            section.Children.Add(list);

            var modules = new Dictionary<string, List<APIEndpoint>>();
            foreach (var ep in Handler.Endpoints.Values)
            {
                foreach (var cmd in ep)
                {
                    var name = cmd.Module.Name;
                    if (modules.TryGetValue(name, out var epLs))
                        epLs.Add(cmd);
                    else
                        modules[name] = new List<APIEndpoint>() { cmd };
                }
            }

            foreach (var keypair in modules)
            {
                var href = $"/docs/api/{escapeForUrl(keypair.Key)}";
                var anchor = new Anchor(href,
                        text: keypair.Key,
                        cls: "navLink-1Neui4 navLinkSmall-34Tbhm");
                list.Children.Add(new ListItem(null)
                {
                    Children =
                    {
                        anchor
                    }
                });
                if (escapeForUrl(keypair.Key) == selected)
                {
                    anchor.ClassList.Add("activeLink-22b0_I");
                    var spy = new Div(cls: "ScrollSpy");
                    list.Children.Add(spy);
                    foreach (var cmd in keypair.Value)
                    {
                        var name = cmd.Name;
                        list.Children.Add(new Anchor($"{href}#{escapeForUrl(name)}", name,
                            cls: "anchor-3Z-8Bb subLink-3M1J2_"));
                    }
                }
            }

            return section;
        }

        string getRegexPattern(System.Reflection.ParameterInfo info)
        {
            var attr = info.GetCustomAttribute<RegexAttribute>();
            if (attr != null)
                return attr.Regex;
            var attrs = info.Member.GetCustomAttributes<RegexAttribute>();
            return attrs.FirstOrDefault(x => x.Name == info.Name)?.Regex;
        }

        Table getTableOfParams(System.Reflection.ParameterInfo[] parameters)
        {
            var table = new Table();
            table.Children.Add(new TableRow()
            {
                Children =
                {
                    new TableHeader("Field").WithTag("scope", "col"),
                    new TableHeader("Type").WithTag("scope", "col"),
                    new TableHeader("Description").WithTag("scope", "col"),
                }
            });
            foreach (var p in parameters)
            {
                var typeName = Program.GetTypeName(p.ParameterType, out var isNullable);
                var typeText = (isNullable ? "?" : "") + typeName;
                var summary = p.GetCustomAttribute<SummaryAttribute>()?.Text;
                var regex = getRegexPattern(p);
                if (regex != null)
                    summary = summary == null ? $"Must match regex: <code>{regex}</code>"
                        : summary + $"<hr/>Must match regex: <code>{regex}</code>";
                if (string.IsNullOrWhiteSpace(summary))
                    summary = "None";
                table.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableData((Nullable.GetUnderlyingType(p.ParameterType) != null ? "?" : "") + p.Name + (p.IsOptional ? "?" : "")),
                        new TableData(typeText),
                        new TableData(null) {RawHTML = summary}
                    }
                });
            }
            return table;
        }

        Div apiGetMain(APIModule module)
        {
            var main = new Div(cls: "markdown-11q6EU");
            if (module == null)
            {
                main.Children.Add(msgBox("error", "There is no module by that name."));
                return main;
            }
            main.Children.Add(linkHeader(1, module.Name));
            if (module.Summary != null)
                main.Children.Add(docParagraph().WithRawText(module.Summary));
            if (module.Preconditions.Count > 0)
                main.Children.Add(explainPreconditions(true, module.Preconditions.ToArray()));

            foreach (var x in module.Endpoints)
            {
                var paramaters = x.Function.GetParameters();
                var link = Handler.RelativeLink(x.Function, paramaters.Select(x => x.Name).ToArray());
                var req = new HttpReqUrl(x.Method, x.Name, link: link);
                var path = x.GetNicePath().Split('/', StringSplitOptions.RemoveEmptyEntries);
                var pathParams = new List<string>();
                foreach(var section in path)
                {
                    req.AddPath("/");
                    if(section.StartsWith("{"))
                    {
                        var name = section[1..^1];
                        var p = paramaters.FirstOrDefault(x => x.Name == name);
                        pathParams.Add(name);
                        req.AddParam(p, withSpace: false);
                    } else
                    {
                        req.AddPath(section);
                    }
                }
                var nonPathParams = paramaters.Where(x => pathParams.Contains(x.Name) == false)
                    .ToArray();
                if(x.Method != "POST")
                {
                    bool addedFirst = false;
                    foreach(var prm in nonPathParams)
                    {
                        var chr = addedFirst ? "&" : "?";
                        addedFirst = true;
                        req.AddPath($"{chr}");

                        var anchor = new Anchor("#", "");
                        anchor.Children.Add(new RawObject("{"));
                        var spn = new Span()
                        {
                            RawText = Uri.EscapeDataString(prm.Name),
                            Style = "color: yellow"
                        };
                        anchor.Children.Add(spn);
                        if (prm.IsOptional)
                        {
                            anchor.Children.Add(new RawObject(" = "));
                            anchor.Children.Add(new Span()
                            {
                                Style = "color: blue",
                                RawText = $"{(prm.DefaultValue ?? "null")}"
                            });
                        }
                        anchor.Children.Add(new RawObject("}"));
                        req.Add(anchor);
                    }
                    main.Children.Add(req.ToHtml());
                    if(nonPathParams.Length > 0)
                    {
                        main.Children.Add(linkHeader(6, "Query Params", escapeForUrl(x.Name) + "-query-params"));
                        main.Children.Add(getTableOfParams(nonPathParams));
                    }
                }
                else
                {
                    main.Children.Add(req.ToHtml());
                    if(nonPathParams.Length > 0)
                    {
                        main.Children.Add(linkHeader(6, "POST Params", escapeForUrl(x.Name) + "-post-params"));
                        main.Children.Add(getTableOfParams(nonPathParams));
                    }
                }
                if (pathParams.Count > 0)
                {
                    main.Children.Add(linkHeader(6, "Path Params", escapeForUrl(x.Name) + "-path-params"));
                    main.Children.Add(getTableOfParams(paramaters.Where(x => pathParams.Contains(x.Name)).ToArray()));
                }
                if (x.Regexs.TryGetValue(".", out var pattern))
                    main.Children.Add(docParagraph().WithRawText($"Request URI must match pattern: <code>{pattern}</code>"));
                if (x.Summary != null)
                    main.Children.Add(docParagraph().WithRawText(x.Summary));
                if (x.Preconditions.Length > 0)
                    main.Children.Add(explainPreconditions(false, x.Preconditions));
            }
            return main;
        }

#endregion

        [Method("GET"), Path("/docs")]
        [Name("View documentation")]
        public async Task ViewDocs()
        {
            await ReplyFile("docs.html", 200, rep(null, null)
                .Add("mainContent", "<strong>Please select an item on the left!</strong>"));
        }

        [Method("GET"), Path(@"/docs/commands/{name}")]
        [Regex("name", @"[\S\._]+")]
        [Name("View Command Module")]
        public async Task CommandItem(string name)
        {
            CurrentLook = name;
            var item = Program.Commands.Modules.FirstOrDefault(x => escapeForUrl(x.Name) == name);
            await ReplyFile("docs.html", 200, rep(name, null)
                .Add("mainContent", cmdGetMain(item)));
        }

        [Method("GET"), Path("/docs/api/{name}")]
        [Regex("name", @"[\S\._]+")]
        [Name("View API Module")]
        public async Task APIItem(string name)
        {
            CurrentLook = name;
            var module = Handler.Modules.FirstOrDefault(x => escapeForUrl(x.Name) == name);
            await ReplyFile("docs.html", 200, rep(null, name)
                .Add("mainContent", apiGetMain(module)));
        }

    }

    class HttpReqUrl
    {
        Div html;
        Span url;
        public HttpReqUrl(string method, string name, string id = null, string link = null)
        {
            html = new Div(cls: "http-req");
            html.Children.Add(Docs.linkHeader(2, name, id, "http-req-title", linkText: link));
            html.Children.Add(new Span(cls: "http-req-verb").WithRawText(method));
            url = new Span(cls: "http-req-url");
            html.Children.Add(url);
        }
        public HttpReqUrl AddPath(string text)
        {
            url.Children.Add(new Span().WithRawText(text));
            return this;
        }
        public HttpReqUrl Add(HTMLBase spn)
        {
            url.Children.Add(spn);
            return this;
        }
        public HttpReqUrl AddVariable(string name, string link = "#")
        {
            url.Children.Add(new Span()
            {
                Children =
                {
                    new Anchor(link, "{" + name + "}", cls: "http-req-variable")
                }
            });
            return this;
        }
        public HttpReqUrl AddRawVariable(string text, string link = "#")
        {
            url.Children.Add(new Span()
            {
                Children =
                {
                    new Anchor(link, text, cls: "http-req-variable")
                }
            });
            return this;
        }
        public HttpReqUrl AddParam(Discord.Commands.ParameterInfo param, string link = "#", bool withSpace = true)
        {
            var span = new Span();
            var anchor = new Anchor(link, "", cls: "http-req-variable");
            span.Children.Add(anchor);
            anchor.Children.Add(new RawObject("{"));
            anchor.Children.Add(new Span()
            {
                RawText = Program.GetTypeName(param.Type, out var _),
                Style = "color: red"
            });
            anchor.Children.Add(new Span().WithRawText(" " + param.Name));
            if(param.IsOptional)
            {
                anchor.Children.Add(new Span().WithRawText($" = {(param.DefaultValue ?? "null")}")
                    .WithTag("style", "color: blue"));
            }
            anchor.Children.Add(new RawObject("}"));
            if(withSpace)
                span.Children.Add(new RawObject(" "));
            url.Children.Add(span);
            return this;
        }
        public HttpReqUrl AddParam(System.Reflection.ParameterInfo param, string link = "#", bool withSpace = true)
        {
            var span = new Span();
            var anchor = new Anchor(link, "", cls: "http-req-variable");
            span.Children.Add(anchor);
            anchor.Children.Add(new RawObject("{"));
            anchor.Children.Add(new Span()
            {
                RawText = Program.GetTypeName(param.ParameterType, out var _),
                Style = "color: red"
            });
            anchor.Children.Add(new Span().WithRawText(" " + param.Name));
            if (param.IsOptional)
            {
                anchor.Children.Add(new Span().WithRawText($" = {(param.DefaultValue ?? "null")}")
                    .WithTag("style", "color: blue"));
            }
            anchor.Children.Add(new RawObject("}"));
            if(withSpace)
                span.Children.Add(new RawObject(" "));
            url.Children.Add(span);
            return this;
        }
        public override string ToString() => html?.ToString() ?? "";
        public HTMLBase ToHtml() => html;
    }
}
