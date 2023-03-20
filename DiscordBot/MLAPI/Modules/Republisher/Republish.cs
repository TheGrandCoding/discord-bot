using Discord;
using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using DiscordBot.Utils;
using ExternalAPIs;
using ExternalAPIs.Facebook;
using ExternalAPIs.Instagram;
using ExternalAPIs.TikTok;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Republisher
{
    // The actual interactions with Instagram's API has been relocated to the 'FacebookAPI' project.

#if DEBUG
    [Path("/republisher")]
#else
    [Host("publish.cheale14.com")]
#endif
    public class Republish : APIBase
    {
        public Republish(APIContext c) : base(c, "republisher") 
        {
            Sidebar = SidebarType.Local;
        }

        public PublishPost GetCurrentPost()
        {
            var cookie = Context.Request.Cookies["current-post"];
            PublishPost post;
            if (cookie == null)
            {
                post = new();
            } else
            {
                var str = Uri.UnescapeDataString(cookie.Value);
                post = JsonConvert.DeserializeObject<PublishPost>(str);
            }
            if(post.Platforms.Count == 0)
            {
                post.Platforms = new List<PublishBase>()
                {
                    new PublishDefault(),
                    new PublishInstagram(), 
                    new PublishDiscord(),
                    new PublishTikTok()
                };
            }
            foreach (var plat in post.Platforms)
                if (plat.Media == null)
                    plat.Media = new();
            return post;
        }
        public void SetCurrentPost(PublishPost post)
        {
            System.Net.Cookie cookie;
            if(post == null)
            {
                cookie = new("current-post", "");
                cookie.Expires = DateTime.Now.AddDays(-1); // ensure it expires, essentially removes it
            } else
            {
                var str = JsonConvert.SerializeObject(post);
                cookie = new("current-post", Uri.EscapeDataString(str));
                cookie.Expires = DateTime.Now.AddDays(1);
            }
            cookie.Path = "/";
            cookie.Secure = true;
            cookie.HttpOnly = false;
            Context.HTTP.Response.Cookies.Add(cookie);
        }

        string getInstaUrl()
        {
            return InstagramClient.GetBasicRedirectUri(Program.Configuration["tokens:instagram:app_id"],
                Context.GetFullUrl(nameof(Modules.OAuthCallbacks.HandleIGOauthSuccess)),
                ExternalAPIs.Instagram.BasicAPIScopes.All,
                SetState()).ToString();
        }

        string getFacebookUrl()
        {
            return FacebookClient.GetRedirectUri(Program.Configuration["tokens:facebook:app_id"],
                Context.GetFullUrl(nameof(Modules.OAuthCallbacks.HandleFBOauth)), 
                ExternalAPIs.Facebook.OAuthScopes.InstagramBasic 
                | OAuthScopes.InstagramContentPublish
                | ExternalAPIs.Facebook.OAuthScopes.PagesShowList,
                SetState()).ToString();
        }

        List<SocialMediaPlatform> getPlatforms(PublishPost post)
        {
            return new List<SocialMediaPlatform>()
            {
                new InstagramRow(SetState, post, Context),
                new TikTokRow(SetState, post, Context),
                new DiscordWebhookRow(SetState, post, Context)
            };
        }

        async Task viewPost(PublishPost post)
        {
            var rep = new Replacements();
            var service = Context.Services.GetRequiredService<RepublishService>();
            var http = Context.Services.GetRequiredService<HttpClient>();
            var main = new Div();

            if(post.ApprovedById.HasValue)
            {
                main.Children.Add(new Div(cls: "warn").WithRawText("This post has already been approved and cannot be edited."));
            }
            else if (post.AuthorId.HasValue)
            {
                main.Children.Add(new Div(cls: "warn").WithRawText("This post has already been submitted for approval, but it can be edited until it is published"));
            }


            var platforms = getPlatforms(post);
            foreach (var row in platforms)
            {
                var div = await row.GetDivAsync();
                main.Children.Add(div);
                main.Children.Add(new RawHTML("<hr/>"));
            }

            rep.Add("content", main);

            if(post.ApprovedById.HasValue)
            {
                rep.Add("actions", "<p>This post has been submitted and approved. It is now live.</p>");
            } else
            {
                var container = new Div();
                if (post.AuthorId.HasValue && (Context.User?.RepublishRole.HasFlag(BotRepublishRoles.Approver) ?? false))
                {
                    container.Children.Add(new Input("button", "Approve and publish")
                    {
                        OnClick = "tryApprove(event)"
                    });
                }
                if (Context.User?.RepublishRole.HasFlag(BotRepublishRoles.Provider) ?? false)
                {
                    container.Children.Add(new Input("button", post.AuthorId.HasValue ? "Save submission" : "Submit for approval")
                    {
                        OnClick = "tryPublish(event)"
                    });
                }
                rep.Add("actions", container);
            }
            


            await ReplyFile("select.html", 200, rep);
        }

        [Method("GET"), Path("/")]
        public Task ViewRepublisher()
        {
            var cur = GetCurrentPost();
            if(cur.Id != 0)
            { // probably an old cookie, clear it.
                SetCurrentPost(null);
                cur = new();
            }
            return viewPost(cur);
        }
        [Method("GET"), Path("/post/{id}")]
        [Regex("id", "[0-9]+")]
        [RequireRepublishRole(BotRepublishRoles.Provider, OR = "perm")]
        [RequireRepublishRole(BotRepublishRoles.Approver, OR = "perm")]
        public async Task ViewRepublisherPost(uint id)
        {
            var post = await Context.BotDB.GetPost(id);
            if(post == null)
            {
                await RespondRaw($"Error: no post found by ID '{id}'", 404);
                return;
            }
            if (!canAccess(post))
            {
                await RespondRaw("Error: you cannot access this post", 400);
                return;
            }
            SetCurrentPost(post); // make sure cookie is correct
            await viewPost(post);
        }

        async Task<Table> getUserManageTable(RepublishService service)
        {
            BotDbUser[] users;
            var db = Context.Services.GetBotDb("RepublishTable");
            users = await db.GetUsersWithExternal();
            var table = new Table();
            table.WithHeaderColumn("Name");
            table.WithHeaderColumn("Instagram");
            table.WithHeaderColumn("TikTok");
            table.WithHeaderColumn("Facebook");
            table.WithHeaderColumn("Role");
            foreach(var user in users.OrderByDescending(x => x.Id))
            {
                var row = new TableRow(id: user.Id.ToString());
                row.WithCell(user.DisplayName);
                row.WithCell(user.Instagram?.AccountId ?? "null");
                row.WithCell(user.TikTok?.AccountId ?? "null");
                row.WithCell(user.Facebook?.AccountId ?? "null");
                var td = new TableData(null);
                var select = new Select();
                select.OnChange = "return updateAccess()";
                select.OnFocus = "setPrev()";
                select.Add($"No access", $"{(int)BotRepublishRoles.None}", user.RepublishRole == BotRepublishRoles.None);
                select.Add($"Provide information only", $"{(int)BotRepublishRoles.Provider}", user.RepublishRole == BotRepublishRoles.Provider);
                select.Add($"Approve for publish", $"{(int)BotRepublishRoles.Approver}", user.RepublishRole == BotRepublishRoles.Approver);
                select.Add($"Full Administrator", $"{(int)BotRepublishRoles.Admin}", user.RepublishRole == BotRepublishRoles.Admin);
                td.Children.Add(select);
                row.Children.Add(td);
                table.Children.Add(row);
            }
            return table;
        }

        async Task<Table> getPostManageTable()
        {
            var table = new Table()
                .WithHeaderColumn("Post ID")
                .WithHeaderColumn("Submitted by")
                .WithHeaderColumn("Sends to Instagram")
                .WithHeaderColumn("Sends to Discord");
            var unapprovedPosts = Context.BotDB.GetAllPosts().ToList();
            foreach(var post in unapprovedPosts)
            {
                var row = new TableRow(post.Id.ToString(), cls: "post link");
                row.OnClick = $"gotoPost(event)";
                row.WithCell(post.Id.ToString());
                var author = await Context.BotDB.GetUserAsync(post.AuthorId.Value);
                row.WithCell(author.DisplayName);
                if ((post.Instagram?.Kind ?? PublishKind.DoNotPublish) == PublishKind.DoNotPublish)
                    row.WithCell("No");
                else
                    row.WithCell("Yes");
                if ((post.Discord?.Kind ?? PublishKind.DoNotPublish) == PublishKind.DoNotPublish)
                    row.WithCell("No");
                else
                    row.WithCell("Yes");
                table.Children.Add(row);
            }
            if (unapprovedPosts.Count == 0)
            {
                var row = new TableRow();
                row.Children.Add(new TableData("No posts pending approval.")
                {
                    ColSpan = "4"
                });
                table.Children.Add(row);
            }
            return table;
        }

        [Method("GET"), Path("/admin")]
        [RequireRepublishRole(BotRepublishRoles.Approver)]
        public async Task ViewAdmin()
        {
            var service = Context.Services.GetRequiredService<RepublishService>();
            var main = new Div();
            
            foreach(var platform in getPlatforms(null))
            {
                var div = platform.GetAdminDiv();
                main.Children.Add(div);
                main.Children.Add(new RawHTML("<hr>"));
            }

            var userTable = await getUserManageTable(service);
            var postTable = await getPostManageTable();
            await ReplyFile("admin.html", 200, new Replacements()
                .Add("platforms", main)
                .Add("userTable", userTable)
                .Add("postTable", postTable));
        }


        Task noSidebar(string file)
        {
            Sidebar = SidebarType.None;
            return ReplyFile(file + ".html", 200);
        }
        [Method("GET"), Path("/terms")]
        public Task RepTerms() => noSidebar("terms");
        [Method("GET"), Path("/privacy")]
        public Task RepPrivacy() => noSidebar("privacy");

        
        [Method("GET"), Path("/api/ig")]
        public async Task APIGetInstaItems()
        {
            if(!(Context.User?.Instagram?.IsValid(out _) ?? false))
            {
                await RespondRedirect(getInstaUrl());
                return;
            }
            var http = Context.Services.GetRequiredService<HttpClient>();
            var insta = Context.User.Instagram.CreateClient(http);
            var user = await insta.GetMeAsync(IGUserFields.Id | IGUserFields.Username | IGUserFields.AccountType | IGUserFields.MediaCount | IGUserFields.Media);
            string ig = "";
            foreach(var media in user.MediaIds)
            {
                var full = await insta.GetMediaAsync(media, IGMediaFields.All);
                ig += full.ToHtml() + "\n";
            }
            await RespondRaw(ig, 200);
        }
        
        [Method("GET"), Path("/api/tiktok")]
        public async Task APIGetTikTokItems()
        {
            if (!(Context.User?.TikTok?.IsValid(out _) ?? false))
            {
                var m = new TikTokRow(SetState, null, Context);
                await RespondRedirect(m.ProviderRedirectUri());
                return;
            }

            var http = Context.Services.GetRequiredService<HttpClient>();
            var tiktok = Context.User.TikTok.CreateClient(http);

            var media = tiktok.GetMyVideosAsync(ExternalAPIs.TikTok.TikTokVideoFields.All);

            string tt = "";
            await foreach (var full in media)
            {
                tt += full.ToHtml() + "\n";
            }
            await RespondRaw(tt, 200);
        }

        public struct PatchUserData
        {
            public int? role;
        }

        [Method("PATCH"), Path("/api/admin")]
        [RequireRepublishRole(BotRepublishRoles.Admin)]
        public async Task APIPatchUser(uint user, [FromBody]PatchUserData data)
        {
            if(user == Context.User.Id)
            {
                await RespondRaw("You cannot modify your own roles", 400);
                return;
            }
            if(!data.role.HasValue)
            {
                await RespondRaw("No role value provided.", 400);
                return;
            }
            var db = Context.Services.GetBotDb(nameof(APIPatchUser));
            var usr = await db.GetUserAsync(user);
            if (usr == null)
            {
                await RespondRaw($"Unknown user", 400);
                return;
            }
            var enumValue = (BotRepublishRoles)data.role.Value;
            BotRepublishRoles[] possible = new[] {
                BotRepublishRoles.None,
                BotRepublishRoles.Provider,
                BotRepublishRoles.Approver,
                BotRepublishRoles.Admin
            };
            if(possible.Contains(enumValue))
            {
                usr.RepublishRole = enumValue;
            } else {
                await RespondRaw("Enum value is invalid.", 400);
                return;
            }
            await RespondRaw("");
        }

        public struct PatchDiscordData
        {
            public string webhook;
        }

        [Method("PATCH"), Path("/api/admin/discord")]
        [RequireRepublishRole(BotRepublishRoles.Admin)]
        public async Task APIPatchDiscord([FromBody] PatchDiscordData data)
        {
            var service = Context.Services.GetRequiredService<RepublishService>();
            service.Data.Discord.Token = string.IsNullOrWhiteSpace(data.webhook) ? null : data.webhook;
            service.OnSave();
            await RespondRaw("");
        }

        [Method("POST"), Path("/api/approve")]
        [RequireRepublishRole(BotRepublishRoles.Approver)]
        public async Task APIApproveAndExecutePost(uint id)
        {
            var doPost = new List<SocialMediaPlatform>();
            var post = await Context.BotDB.GetPost(id);
            if(post.ApprovedById.HasValue)
            {
                await RespondRaw("Error: this post has already been approved", 400);
                return;
            }
            var errors = new APIErrorResponse();
            if ((post.Instagram?.Kind ?? PublishKind.DoNotPublish) != PublishKind.DoNotPublish)
            {
                var instagram = new InstagramRow(SetState, post, Context);
                if (!instagram.IsSetup(out var expired))
                {
                    errors.Child("instagram").WithRequired(expired ? "The instagram auth token has expired" : "Instagram has not been setup");
                }
                else
                {
                    doPost.Add(instagram);
                }
            }
            if ((post.Discord?.Kind ?? PublishKind.DoNotPublish) != PublishKind.DoNotPublish)
            {
                var ds = new DiscordWebhookRow(SetState, post, Context);
                if (!ds.IsSetup(out var expired))
                {
                    errors.Child("discord").WithRequired("The webhook URL has not been setup");
                }
                else
                {
                    doPost.Add(ds);
                }
            }
            if (errors.HasAnyErrors())
            {
                await RespondError(errors);
                return;
            }
            post.ApprovedById = Context.User.Id;
            await Context.BotDB.SaveChangesAsync();
            var result = new StringBuilder();
            foreach (var platform in doPost)
            {
                try
                {
                    var status = await platform.ExecuteAsync();
                    if (!status.Success)
                    {
                        result.AppendLine($"Failed for {platform.Name}: {status.ErrorMessage}");
                    }
                    else
                    {
                        result.AppendLine($"{platform.Name} success: {status.Value}");
                    }
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "Publish");
                    result.AppendLine($"Errored with {platform.Name}: {ex.Message}");
                }
            }
            await RespondRaw(result.ToString());
        }

        bool canAccess(PublishPost post)
        {
            if (post.Id == 0) return true; // new post being drafted
            if (post.AuthorId.GetValueOrDefault(0) != Context.User.Id)
            {
                if (Context.User.RepublishRole == BotRepublishRoles.Provider)
                {
                    return false;
                }
            }
            return true;
        }

        PublishPost reformatPost(PublishPost post)
        {
            foreach(var platform in post.Platforms)
            {
                platform.PostId = post.Id;
                foreach(var media in platform.Media)
                {
                    media.Platform = platform.Platform;
                    media.PostId = post.Id;
                }
            }
            return post;
        }

        [Method("POST"), Path("/api/post")]
        [RequireRepublishRole(BotRepublishRoles.Provider)]
        public async Task APISchedulePublish()
        {
            var service = Context.Services.GetRequiredService<RepublishService>();
            var post = GetCurrentPost();
            if(post == null)
            {
                await RespondRaw($"You do not have a post pending, or do not have cookies enabled.");
                return;
            }
            if(post.ApprovedById.HasValue)
            {
                await RespondRaw("Error: this post has already been approved and therefore cannot be edited.", 400);
                return;
            }
            var errors = post.GetErrors();
            if(errors != null)
            {
                await RespondError(errors);
                return;
            }
            SetCurrentPost(null);
            post = reformatPost(post); // make sure platforms are all set correctly
            if (post.Id != 0) // ID should be zero since its not been put into the database yet
            { // so if its not zero, we might be modifying an existing post
                // so check permissions from DB version
                // since the 'post' variable is from the client, we can't trust it
                var fromDb = await Context.BotDB.GetPost(post.Id);
                if(!canAccess(fromDb))
                {
                    await RespondRaw("Error: this post cannot be modified by you", 400);
                    return;
                }
                post.Id = fromDb.Id;
                post.AuthorId = fromDb.AuthorId;
                post.ApprovedById = null;
                Context.BotDB.Update(post);
            } else
            {
                post.Id = 0;
                post.AuthorId = Context.User.Id;
                post.ApprovedById = null;
                await Context.BotDB.CreateNewPost(post);
            }
            await Context.BotDB.SaveChangesAsync();
            await RespondRedirect(Context.GetFullUrl(nameof(ViewRepublisherPost), post.Id.ToString()));
        }

    }

    public static class Extensions
    {
        public static Div ToHtml(this IGMedia media, bool selected = false)
        {
            var div = new Div(id: $"ig_{media.Id}", cls: "ig media");
            div.WithTag("data-type", $"{media.MediaType}".ToLower());
            div.OnClick = "igSelectPost(event)";
            if (selected) div.ClassList.Add("selected");
            if(media.MediaType.ToLower() == "image")
            {
                var img = new Img(media.MediaUrl)
                {
                    Style = "width: 32px"
                };
                div.Children.Add(img);
            } else if(media.MediaType.ToLower() == "video")
            {
                var vid = new Video(media.MediaUrl)
                {
                    Style = "width: 32px"
                };
                div.Children.Add(vid);
            } else
            {
                var err = new Anchor(media.MediaUrl, "[unknown type]");
                div.Children.Add(err);
            }
            var anchor = new Anchor(media.Permalink, media.Caption)
            {
                OnClick = "igSelectPost(event)"
            };
            div.Children.Add(anchor);
            return div;
        }
        public static Div ToHtml(this TikTokVideo media, bool selected = false)
        {
            var div = new Div(id: $"tt_{media.Id}", cls: "tt media");
            div.WithTag("data-type", "video");
            div.WithTag("data-media-url", media.EmbedLink);
            div.OnClick = "ttSelectPost(event)";
            if (selected) div.ClassList.Add("selected");
            var img = new Img(media.CoverImageUrl)
            {
                Style = "width: 32px"
            };
            div.Children.Add(img);
            var anchor = new Anchor("#", media.Title)
            {
                OnClick = "ttSelectPost(event)"
            };
            div.Children.Add(anchor);
            return div;
        }
    }

    public abstract class SocialMediaPlatform
    {
        public string Name { get; }
        protected APIContext Context { get; }
        protected PublishPost Post { get; }
        protected Func<string> SetState { get; }
        protected RepublishService Service { get; }

        public SocialMediaPlatform(string name, Func<string> stateSetter, PublishPost post, APIContext c)
        {
            Service = c.Services.GetRequiredService<RepublishService>();
            Name = name;
            SetState = stateSetter;
            Post = post;
            Context = c;
        }

        public abstract string AdminRedirectUri();
        public abstract string ProviderRedirectUri();
        public abstract bool IsSetup(out bool expired);
        public abstract bool IsUserAuthenticated(out bool expired);

        protected PublishBase ThisData { get
            {
                return Post.Platforms.FirstOrDefault(x => x.Platform.ToString() == Name);
            } }
        protected List<PublishMedia> ThisMedia { get
            {
                var thisM = ThisData?.Media ?? new();
                if (thisM.Count == 0)
                    return Post.Default.Media;
                return thisM;
            } }
        protected string ThisCaption { get
            {
                return ThisData?.Caption ?? Post.Default.Caption;
            } }

        public virtual Div GetAdminDiv()
        {
            var container = new Div();
            container.Children.Add(new H1(Name));
            var main = new Div();
            var url = AdminRedirectUri();
            main.ClassList.Add("box");
            main.WithTag("data-url", url);
            main.OnClick = "redirectErr(event)";
            container.Children.Add(main);
            var account = GetAccount();
            var ul = new UnorderedList();
            main.Children.Add(ul);
            foreach(var property in account.GetType().GetProperties())
            {
                var value = property.GetValue(account);
                var li = new ListItem();
                li.Children.Add(new StrongText(property.Name + ": "));
                if (value == null)
                    li.Children.Add(new Code("<null>"));
                else if (property.Name.Contains("token", StringComparison.OrdinalIgnoreCase))
                    li.Children.Add(new Code("***"));
                else
                    li.Children.Add(new Code($"{value}"));
                ul.AddItem(li);
            }
            main.Children.Add(new RawHTML("You can click this text to (re)authorize this connection<br/>"));
            if (!IsSetup(out bool expired))
            {
                string reason;
                if (account?.Id == null)
                    reason = "A connection has not yet been made to a valid Facebook account.";
                else if (account?.Token == null)
                    reason = "The authorization token is invalid or has not been provided.";
                else if (expired)
                    reason = "The login has expired";
                else
                    reason = "Some other issue is present.";
                main.Children.Add(new RawHTML($"<strong>There is a problem with this connection:</strong><br/><strong>{reason}</strong>"));
                main.ClassList.Add("error");
                return container;
            }
            return container;
        }
        public abstract BaseAccount GetAccount();

        public virtual Task addMediaList(Classes.HTMLHelpers.HTMLBase container)
        {
            container.Children.Add(new Label("Media:"));
            int i = 0;
            if (ThisData.Media.Count == 0)
                ThisData.Media.AddRange(Post.Default.Media);
            foreach(var media in ThisData.Media)
            {
                var mediaDiv = new Div();
                mediaDiv.WithTag("data-id", i.ToString());
                mediaDiv.WithTag("data-platform", media.Platform.ToString().ToLower());
                mediaDiv.Children.Add(new Label($"{media.Type} {i}:"));
                var inp = new Input("url", media.RemoteUrl)
                {
                    OnChange = "mediaUrlChange(event)"
                };
                mediaDiv.Children.Add(inp);
                var rmBtn = new Input("button", "-", cls: "mediaRemove")
                {
                    OnClick = "mediaRemove(event)"
                };
                mediaDiv.Children.Add(rmBtn);
                i++;
                container.Children.Add(mediaDiv);
            }
            container.Children.Add(new Input("button", "Add new")
            {
                OnClick = "addNewMedia(event)"
            }.WithTag("data-platform", ThisData.Platform.ToString().ToLower()));
            return Task.CompletedTask;
        }

        public abstract Task addLeftcolumn(Div left);
        public virtual async Task addRightColumn(Div right)
        {

            var sel = new Select();
            sel.WithTag("for", Name.ToLower());
            sel.Add("Do not publish", $"{PublishKind.DoNotPublish}", ThisData.Kind == PublishKind.DoNotPublish);
            sel.Add("Publish with the following information", $"{PublishKind.PublishWithText}", ThisData.Kind == PublishKind.PublishWithText);
            sel.WithTag("onchange", "setKind()");
            right.Children.Add(sel);

            var form = new Form(id: Name.ToLower());
            if (ThisData.Kind == PublishKind.DoNotPublish)
                form.Style = "display: none";
            var onC = "setValue()";
            var onI = "setDirty()";
            form.AddLabeledInput("caption", "Caption: ", "textarea", "Caption", ThisData.Caption ?? Post.Default.Caption,
                onChange: onC, onInput: onI);
            form.Children.Add(new Classes.HTMLHelpers.LineBreak());
            await addMediaList(form);
            right.Children.Add(form);
        }

        public async Task<Div> GetDivAsync()
        {
            var container = new Div();
            container.Children.Add(new H1(Name));
            var main = new Div();
            container.Children.Add(main);
            if (!IsSetup(out bool expired))
            {
                var url = AdminRedirectUri();
                main.WithTag("data-url", url);
                main.OnClick = "redirectErr(event)";
                main.Class = "error";
                main.RawHTML = (expired ? 
                      "The administrative access token for this social media has expired." 
                    : $"This social media has not yet been set up to publish to.") +  
                    "<br/>Please click this box if you are able to do so.";
                return container;
            }
            if (!IsUserAuthenticated(out expired))
            {
                var url = ProviderRedirectUri();
                main.WithTag("data-url", url);
                main.OnClick = "redirectErr(event)";
                main.Class = "error";
                main.RawHTML = (expired ?
                      $"The access token for your {Name} account has expired."
                    : $"You have not yet linked your {Name} account to this website") +
                    "<br/>Please click this box to login";
                return container;
            }
            if (Context.User != null && Context.User.RepublishRole == BotRepublishRoles.None)
            {
                main.Class = "error";
                main.RawHTML = "You are logged in, however your account has not been given access to publish information.<br/>" +
                    "You may need to wait for someone to approve your account.";
                return container;
            }
            main.Class = "container";
            var left = new Div(cls: "column left");
            main.Children.Add(left);
            left.Children.Add(new H2("Original"));
            await addLeftcolumn(left);


            var right = new Div(cls: "column right");
            main.Children.Add(right);
            right.Children.Add(new H2("Republish as"));
            await addRightColumn(right);


            return container;
        }

        public abstract Task<Result<string>> ExecuteAsync();
    }

    public class InstagramRow : SocialMediaPlatform
    {
        public InstagramRow(Func<string> stateSetter, PublishPost post, APIContext c) : base("Instagram", stateSetter, post, c)
        {
        }
        private InstagramClient _ic;
        public InstagramClient insta {  get
            {
                return _ic ??= Context.User.Instagram.CreateClient(Context.Services.GetRequiredService<HttpClient>());
            } }

        public override string AdminRedirectUri()
        {
            return FacebookClient.GetRedirectUri(Program.Configuration["tokens:facebook:app_id"],
                Context.GetFullUrl(nameof(Modules.OAuthCallbacks.HandleFBOauth)),
                ExternalAPIs.Facebook.OAuthScopes.InstagramBasic
                | OAuthScopes.InstagramContentPublish
                | ExternalAPIs.Facebook.OAuthScopes.PagesShowList,
                SetState()).ToString();
        }

        public override string ProviderRedirectUri()
        {
            return InstagramClient.GetBasicRedirectUri(Program.Configuration["tokens:instagram:app_id"],
                Context.GetFullUrl(nameof(Modules.OAuthCallbacks.HandleIGOauthSuccess)),
                ExternalAPIs.Instagram.BasicAPIScopes.UserMedia | BasicAPIScopes.UserProfile,
                SetState()).ToString();
        }

        public override bool IsSetup(out bool expired)
        {
            expired = false;
            return Service?.IsInstagramValid(out expired) ?? false;
        }

        public override bool IsUserAuthenticated(out bool expired)
        {
            expired = false;
            return Context?.User?.Instagram?.IsValid(out expired) ?? false;
        }
        public override async Task addLeftcolumn(Div left)
        {
            left.Children.Add(new Input("button", "Search for Instagram post")
            {
                OnClick = "igSearch()"
            });
            var result = new Div("instaPosts", cls: "withScroll");
            left.Children.Add(result);
            if (Post.Instagram.OriginalId != null)
            {
                var info = await insta.GetMediaAsync(Post.Instagram.OriginalId, IGMediaFields.All);
                if (info != null)
                {
                    result.RawHTML = info.ToHtml(true);
                }
                else
                {
                    Post.Instagram.OriginalId = null;
                }
            }
        }

        async Task<string> createContainer(FacebookClient api, PublishMedia media, string userId, string caption)
        {
            string id;
            if (media.Type == MediaType.Image)
                id = await api.CreateIGImageContainer(userId, media.RemoteUrl, caption, caption == null);
            else
                id = await api.CreateIGVideoContainer(userId, media.RemoteUrl, caption, caption == null);
            return id;
        }

        public async override Task<Result<string>> ExecuteAsync()
        {
            var http = Context.Services.GetRequiredService<HttpClient>();
            var insta = Context.User.Instagram.CreateClient(http);
            var instaMe = await insta.GetMeAsync(IGUserFields.Username);
            var api = Service.Data.Facebook.CreateClient(http);

            var media = ThisMedia ?? new();
            var caption = ThisCaption + "\r\nVia @" + instaMe.Username;
            string containerId;
            if(media.Count == 0)
            {
                return new("No media has been selected.", null);
            } else if(media.Count == 1)
            {
                containerId = await createContainer(api, media.First(), instaMe.Id, caption);
            } else
            {
                var ids = new List<string>();
                foreach (var med in media)
                    ids.Add(await createContainer(api, med, instaMe.Id, null));
                containerId = await api.CreateIGCarouselContainer(instaMe.Id, ids.ToArray(), caption);
            }
            var id = await api.PublishIGMediaContainer(instaMe.Id, containerId);
            var full = await api.GetMediaAsync(id, IGMediaFields.All);
            ThisData.SentId = full.Id;
            return new(full.Permalink);
        }

        public override BaseAccount GetAccount() => Service.Data?.Facebook;
    }

    public class TikTokRow : SocialMediaPlatform
    {
        public TikTokRow(Func<string> stateSetter, PublishPost post, APIContext c) : base("TikTok", stateSetter, post, c)
        {
        }

        public override async Task addLeftcolumn(Div left)
        {
            left.Children.Add(new StrongText("This does not work properly! TikTok's API does not officially support downloading the video file, so this won't work properly."));
            left.Children.Add(new Input("button", "Search for TikTok video")
            {
                OnClick = "ttSearch(event)"
            });
            var result = new Div("tiktokPosts", cls: "withScroll");
            left.Children.Add(result);
            if (Post.TikTok.OriginalId != null)
            {
                var tiktok = Service.Data.TikTok.CreateClient(Context.Services.GetRequiredService<HttpClient>());
                var info = await tiktok.GetMyVideoAsync(new[] { Post.TikTok.OriginalId }, TikTokVideoFields.All);
                if (info != null && info.Length > 0)
                {
                    result.RawHTML = info[0].ToHtml(true);
                }
                else
                {
                    Post.Instagram.OriginalId = null;
                }
            }
        }

        public override string AdminRedirectUri()
        {
            return TikTokClient.GetRedirectUri(Program.Configuration["tokens:tiktok:client_key"],
                ExternalAPIs.TikTok.TikTokAuthScopes.All, 
                Context.GetFullUrl(nameof(OAuthCallbacks.HandleTiktokOAuth)),
                "a:" + SetState());
        }
        public override string ProviderRedirectUri()
        {
            return TikTokClient.GetRedirectUri(Program.Configuration["tokens:tiktok:client_key"],
                ExternalAPIs.TikTok.TikTokAuthScopes.UserInfoBasic | ExternalAPIs.TikTok.TikTokAuthScopes.VideoList,
                Context.GetFullUrl(nameof(OAuthCallbacks.HandleTiktokOAuth)),
                "u:" + SetState());
        }

        public override bool IsSetup(out bool expired)
        {
            expired = false;
            return Service.Data?.TikTok?.IsValid(out expired) ?? false;
        }

        public override bool IsUserAuthenticated(out bool expired)
        {
            expired = false;
            if (Context.User == null) return false;
            return Context.User.TikTok.IsValid(out expired);
        }

        public override Task<Result<string>> ExecuteAsync()
        {
            return Task.FromResult(new Result<string>("Not implemented", null));
        }

        public override BaseAccount GetAccount() => Service.Data.TikTok;
    }

    public class DiscordWebhookRow : SocialMediaPlatform
    {
        public DiscordWebhookRow(Func<string> stateSetter, PublishPost post, APIContext c) : base("Discord", stateSetter, post, c)
        {
        }

        public override Task addLeftcolumn(Div left)
        {
            left.Children.Add(new StrongText("N/A. Information must come from other platforms."));
            return Task.CompletedTask;
        }


        public override string AdminRedirectUri()
        {
            var scope = DiscordOAuthScopes.WebhookIncoming;
            if (Context.User == null)
                scope |= DiscordOAuthScopes.Identify;
            return DiscordOAuthClient.GetRedirectUri(Program.AppInfo.Id.ToString(),
                Context.GetFullUrl(nameof(OAuthCallbacks.HandleDiscordOAuth)),
                scope, "w:" + SetState()).ToString();
        }
        public override string ProviderRedirectUri() => null;

        public override bool IsSetup(out bool expired)
        {
            expired = false;
            return Service.Data?.Discord?.IsValid(out expired) ?? false;
        }

        public override bool IsUserAuthenticated(out bool expired)
        { 
            // discord is publish-only, so users can't be authenticated (there's no OAuth to get a Discord message)
            expired = false; // we'll still verify that they're logged in though.
            return Context.User != null;
        }

        public async override Task<Result<string>> ExecuteAsync()
        {
            var client = Service.Data.Discord.CreateClient();
            var embeds = new List<EmbedBuilder>();
            var embed = new EmbedBuilder();
            embed.Title = $"Republished message";
            embed.Description = Post.Discord.Caption ?? Post.Default.Caption;
            embeds.Add(embed);
            string content = null;
            var media = ThisMedia ?? new(); ;
            for(int i = 0; i < media.Count; i++)
            {
                if(i < embeds.Count)
                    embeds[i].ImageUrl = media[i].RemoteUrl;
                else
                {
                    embeds.Add(new EmbedBuilder().WithImageUrl(media[i].RemoteUrl));
                }
            }
            var msg = await client.SendMessageAsync(text: content, 
                embeds: embeds.Select(x => x.Build()));
            ThisData.SentId = msg.ToString();
            return new($"Message ID {msg}");
        }


        public override BaseAccount GetAccount() => Service.Data.Discord;

    }
}
