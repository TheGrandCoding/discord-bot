using Discord;
using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using DiscordBot.Utils;
using ExternalAPIs;
using ExternalAPIs.Facebook;
using ExternalAPIs.Instagram;
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
        }

        public PublishPost GetCurrentPost()
        {
            var cookie = Context.Request.Cookies["current-post"];
            if (cookie == null) return null;
            var str = Uri.UnescapeDataString(cookie.Value);
            return JsonConvert.DeserializeObject<PublishPost>(str);
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

        [Method("GET"), Path("/")]
        public async Task ViewRepublisher()
        {
            var rep = new Replacements();
            var service = Context.Services.GetRequiredService<RepublishService>();
            var http = Context.Services.GetRequiredService<HttpClient>();


            var main = new Div();
            var post = GetCurrentPost() ?? new();
            foreach(var row in new[] {new InstagramRow(SetState, post, Context)})
            {
                var div = await row.GetDivAsync();
                main.Children.Add(div);
            }


            rep.Add("content", main);

            if(Context.User?.RepublishRole.HasFlag(BotRepublishRoles.Provider) ?? false)
            {
                rep.Add("actions", new Input("button", "Publish!")
                {
                    OnClick = "tryPublish(event)"
                });
            }

            await ReplyFile("select.html", 200, rep);
        }

        async Task<Table> getUserManageTable(RepublishService service)
        {
            BotDbUser[] users;
            using(var db = Context.Services.GetBotDb("RepublishTable"))
            {
                users = await db.GetUsersWithExternal();
            }
            var table = new Table();
            table.WithHeaderColumn("Name");
            table.WithHeaderColumn("Instagram");
            table.WithHeaderColumn("Facebook");
            table.WithHeaderColumn("Role");
            foreach(var user in users.OrderByDescending(x => x.Id))
            {
                var row = new TableRow(id: user.Id.ToString());
                row.WithCell(user.DisplayName);
                row.WithCell(user.Instagram?.AccountId ?? "null");
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

        [Method("GET"), Path("/admin")]
        [RequireRepublishRole(BotRepublishRoles.Approver)]
        public async Task ViewAdmin()
        {
            var service = Context.Services.GetRequiredService<RepublishService>();
            var main = new Div();
            if (!service.IsInstagramValid(out var expired))
            {
                string reason;
                if (service.Data.Facebook?.Id == null)
                    reason = "A connection has not yet been made to a valid Facebook account.";
                else if (service.Data.Facebook?.Token == null)
                    reason = "The authorization token is invalid or has not been provided.";
                else if (service.Data.Facebook?.InstagramId == null)
                    reason = "Whist a Facebook account has been linked, it is not correctly connected to a Instagram Business account";
                else if (expired)
                    reason = "The login has expired";
                else
                    reason = "An unknown issue is present.";
                main.RawHTML = $"There is a problem with this connection:<br/><strong>{reason}</strong>";
                main.Class = "error";
                if(Context.User.RepublishRole.HasFlag(BotRepublishRoles.Admin))
                {
                    main.RawHTML += "<br/>Please click this box to force a re-authentication flow, if you are able to do so.";
                    var url = getFacebookUrl();
                    main.WithTag("data-url", url);
                    main.OnClick = "redirectErr(event)";
                }
            } else
            {
                var data = new Dictionary<string, string>();
                data["Facebook Account ID"] = service.Data.Facebook.Id;
                data["Facebook Page ID"] = service.Data.Facebook.PageId;
                data["Instagram Account ID"] = service.Data.Facebook.InstagramId;
                var http = Context.Services.GetRequiredService<HttpClient>();
                var fb = service.Data.Facebook.CreateClient(http);
                try
                {
                    var me = await fb.GetMeAsync();
                    data["Facebook Account Name"] = me.Name;
                } catch(Exception ex)
                {
                    Program.LogError(ex, "ViewAdminRp");
                }
                data["Token Expires in Seconds"] = (service.Data.Facebook.ExpiresAt - DateTime.Now).TotalSeconds.ToString();
                var ul = new UnorderedList();
                foreach((var key, var value) in data)
                {
                    ul.AddItem(new ListItem()
                    {
                        Children =
                        {
                            new StrongText(key + ": "),
                            new Code(value)
                        }
                    });
                }
                main.Children.Add(ul);
            }
            var table = await getUserManageTable(service);
            await ReplyFile("admin.html", 200, new Replacements()
                .Add("facebook", main)
                .Add("userTable", table));
        }
        
        
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
                await RespondRaw("No role value privded.", 400);
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
            var errors = post.GetErrors();
            if(errors != null)
            {
                await RespondError(errors);
                return;
            }
            errors ??= new();
            if((post.Instagram?.Kind ?? PublishKind.DoNotPublish) != PublishKind.DoNotPublish)
            {
                if (!service.IsInstagramValid(out var expired))
                {
                    await RespondError(errors.Child("instagram").EndRequired("Instagram has not been set up or authorization has expired."));
                    return;
                }
                var http = Context.Services.GetRequiredService<HttpClient>();
                var insta = Context.User.Instagram.CreateClient(http);
                var instaMe = await insta.GetMeAsync(IGUserFields.Username);
                var api = service.Data.Facebook.CreateClient(http);
                var container = await api.CreateIGMediaContainer(service.Data.Facebook.InstagramId,
                    post.Instagram.MediaUrl ?? post.defaultMediaUrl,
                    (post.Instagram.Caption ?? post.defaultText) + "\r\nVia @" + instaMe.Username,
                    new[] { instaMe.Username });
                var id = await api.PublishIGMediaContainer(service.Data.Facebook.InstagramId, container);
                var full = await api.GetMediaAsync(id, IGMediaFields.All);
                SetCurrentPost(null);
                await RespondRaw($"Published. Here's a link: {full.Permalink}");
                return;
            }
            await RespondRaw("OK");
        }

    }

    public static class Extensions
    {
        public static Div ToHtml(this IGMedia media, bool selected = false)
        {
            var div = new Div(id: $"ig_{media.Id}", cls: "ig_media");
            div.OnClick = "igSelectPost(event)";
            if (selected) div.ClassList.Add("selected");
            var img = new Img(media.MediaUrl)
            {
                Style = "width: 32px"
            };
            div.Children.Add(img);
            var anchor = new Anchor(media.Permalink, media.Caption)
            {
                OnClick = "igSelectPost(event)"
            };
            div.Children.Add(anchor);
            return div;
        }
    }

    public abstract class SocialMediaRow
    {
        public string Name { get; }
        protected APIContext Context { get; }
        protected PublishPost Post { get; }
        protected Func<string> SetState { get; }
        protected RepublishService Service { get; }

        public SocialMediaRow(string name, Func<string> stateSetter, PublishPost post, APIContext c)
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

        public abstract Task addLeftcolumn(Div left);
        public abstract Task addRightColumn(Div right);

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
    }

    public class InstagramRow : SocialMediaRow
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
            var result = new Div("instaPosts");
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
        public override Task addRightColumn(Div right)
        {
            var sel = new Select();
            sel.WithTag("for", "instagram");
            sel.Add("Do not publish", $"{PublishKind.DoNotPublish}", Post.Instagram.Kind == PublishKind.DoNotPublish);
            sel.Add("Publish with the following information", $"{PublishKind.PublishWithText}", Post.Instagram.Kind == PublishKind.PublishWithText);
            sel.WithTag("onchange", "setKind()");
            right.Children.Add(sel);

            var form = new Form(id: "instagram");
            if (Post.Instagram.Kind == PublishKind.DoNotPublish)
                form.Style = "display: none";
            var onC = "setValue()";
            var onI = "setDirty()";
            form.AddLabeledInput("caption", "Caption: ", "text", "Caption", Post.Instagram.Caption ?? Post.defaultText,
                onChange: onC, onInput: onI);
            form.AddLabeledInput("mediaUrl", "Media url: ", "url", "URL", Post.Instagram.MediaUrl ?? Post.defaultMediaUrl,
                onChange: onC, onInput: onI);
            right.Children.Add(form);
            return Task.CompletedTask;
        }
    }

}
