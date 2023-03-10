using Discord;
using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using DiscordBot.Utils;
using FacebookAPI;
using FacebookAPI.Facebook;
using FacebookAPI.Instagram;
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
                Context.GetFullPath("/oauth/instagram"),
                FacebookAPI.Instagram.BasicAPIScopes.All).ToString();
        }

        string getFacebookUrl()
        {
            return FacebookClient.GetRedirectUri(Program.Configuration["tokens:facebook:app_id"],
                Context.GetFullPath("/oauth/facebook"), 
                FacebookAPI.Facebook.OAuthScopes.InstagramBasic 
                | OAuthScopes.InstagramContentPublish
                | FacebookAPI.Facebook.OAuthScopes.PagesShowList).ToString();
        }

        async Task<Div> getInstagramRow(RepublishService service, HttpClient http)
        {
            var main = new Div();
            if(!service.IsInstagramValid())
            {
                var url = getFacebookUrl();
                main.WithTag("data-url", url);
                main.OnClick = "redirectErr(event)";
                main.Class = "error";
                main.RawHTML = $"This social media has not yet been set up to publish to.<br/>Please click this box if you are able to do so.";
                return main;
            }
            if (Context.User?.Instagram?.IsInvalid() ?? true)
            {
                var url = getInstaUrl();
                main.WithTag("data-url", url);
                main.OnClick = "redirectErr(event)";
                main.Class = "error";
                main.RawHTML = $"You are not logged in to Instagram.<br/>Click this box to login";
                return main;
            }
            if(Context.User != null && Context.User.RepublishRole == BotRepublishRoles.None)
            {
                main.Class = "error";
                main.RawHTML = "You are logged in, however your account has not been given access to publish information.<br/>" +
                    "You may need to wait for someone to approve your account.";
                return main;
            }
            var insta = Context.User.Instagram.CreateClient(http);
            main.Class = "container";
            var left = new Div(cls: "column left");
            main.Children.Add(left);
            var current = GetCurrentPost();
            if(current == null)
                current = new();
            left.Children.Add(new H2("Original"));
            left.Children.Add(new Input("button", "Search for Instagram post")
            {
                OnClick = "igSearch()"
            });
            var result = new Div("instaPosts");
            left.Children.Add(result);
            if(current.Instagram.OriginalId != null)
            {
                var info = await insta.GetMediaAsync(current.Instagram.OriginalId, IGMediaFields.All);
                if(info != null)
                {
                    result.RawHTML = ToHtml(info, true);
                } else
                {
                    current.Instagram.OriginalId = null;
                }
            }

            var right = new Div(cls: "column right");
            main.Children.Add(right);
            right.Children.Add(new H2("Republish as"));

            var sel = new Select();
            sel.WithTag("for", "instagram");
            sel.Add("Do not publish", $"{PublishKind.DoNotPublish}", current.Instagram.Kind == PublishKind.DoNotPublish);
            sel.Add("Publish with the following information", $"{PublishKind.PublishWithText}", current.Instagram.Kind == PublishKind.PublishWithText);
            sel.WithTag("onchange", "setKind()");
            right.Children.Add(sel);

            var form = new Form(id: "instagram");
            if (current.Instagram.Kind == PublishKind.DoNotPublish)
                form.Style = "display: none";
            var onC = "setValue()";
            var onI = "setDirty()";
            form.AddLabeledInput("caption", "Caption: ", "text", "Caption", current.Instagram.Caption ?? current.defaultText,
                onChange: onC, onInput: onI);
            form.AddLabeledInput("mediaUrl", "Media url: ", "url", "URL", current.Instagram.MediaUrl ?? current.defaultMediaUrl,
                onChange: onC, onInput: onI);
            right.Children.Add(form);


            return main;
        }


        [Method("GET"), Path("/republisher")]
        public async Task View()
        {
            var rep = new Replacements();
            var service = Context.Services.GetRequiredService<RepublishService>();
            var http = Context.Services.GetRequiredService<HttpClient>();


            var main = new Div();
            main.Children.Add(new H1("Instagram"));
            main.Children.Add(await getInstagramRow(service, http));
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

        [Method("GET"), Path("/republisher/admin")]
        [RequireRepublishRole(BotRepublishRoles.Approver)]
        public async Task ViewAdmin()
        {
            var service = Context.Services.GetRequiredService<RepublishService>();
            var main = new Div();
            if (!service.IsInstagramValid())
            {
                string reason;
                if (service.Data.Facebook?.Id == null)
                    reason = "A connection has not yet been made to a valid Facebook account.";
                else if (service.Data.Facebook?.Token == null)
                    reason = "The authorization token is invalid or has not been provided.";
                else if (service.Data.Facebook?.InstagramId == null)
                    reason = "Whist a Facebook account has been linked, it is not correctly connected to a Instagram Business account";
                else if ((service.Data.Facebook?.ExpiresAt ?? DateTime.MinValue) < DateTime.Now)
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


        public Div ToHtml(IGMedia media, bool selected = false)
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
        [Method("GET"), Path("/api/republisher/ig")]
        public async Task APIGetInstaItems()
        {
            if(Context.User?.Instagram?.IsInvalid() ?? true)
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
                ig += ToHtml(full) + "\n";
            }
            await RespondRaw(ig, 200);
        }
        
        public struct PatchUserData
        {
            public int? role;
        }

        [Method("PATCH"), Path("/api/republisher/admin")]
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

        [Method("POST"), Path("/api/republisher/post")]
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
                if (!service.IsInstagramValid())
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

        public struct OAuthError
        {
            public string error { get; set; }
            public Optional<string> error_reason { get; set; }
            public Optional<string> error_description { get; set; }
        }
        public class OAuthSuccess
        {
            public string code { get; set; }
            public Optional<string> state { get; set; }
        }
        public class FBOAuthSuccess : OAuthSuccess
        {
            public string granted_scopes { get; set; }
            public Optional<string> denied_scopes { get; set; }
        }

        [Method("GET"), Path("/oauth/instagram")]
        public async Task HandleIGOAuthFail([FromQuery]OAuthError errData)
        {
            await RespondRaw($"Error: {errData.error}, {errData.error_reason}: {errData.error_description}");
        }

        [Method("GET"), Path("/oauth/instagram")]
        public async Task HandleIGOauthSuccess([FromQuery]OAuthSuccess success)
        {
            var http = Context.Services.GetRequiredService<HttpClient>();
            var insta = await InstagramClient.CreateOAuthAsync(success.code,
                Program.Configuration["tokens:instagram:app_id"],
                Program.Configuration["tokens:instagram:app_secret"],
                Context.GetFullPath("/oauth/instagram"),
                http);

            if(Context.User == null)
            {
                Context.User = await Context.BotDB.GetUserByInstagram(insta.oauth.user_id.ToString(), true);
                await Handler.SetNewLoginSession(Context, Context.User, true, true);
            }
            if(!Context.User.HasDisplayName)
            {
                var me = await insta.GetMeAsync(IGUserFields.Username);
                Context.User.DisplayName = me.Username;
            }
            var result = await insta.GetLongLivedAccessToken(Program.Configuration["tokens:instagram:app_secret"]);
            Context.User.Instagram = new BotDbInstagram()
            {
                AccountId = insta.oauth.user_id.ToString(),
                AccessToken = result.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(result.expires_in.Value)
            };
            await Context.BotDB.SaveChangesAsync();

            await RespondRedirect("/republisher");
        }

        async Task handleManagedPages(FacebookClient client, FBUser user)
        {
            var pages = await client.GetMyAccountsAsync();
            if(pages.Count == 0)
            {
                await RespondRaw($"Error: you do not have any connected pages despite trying to setup publishing to such a page, or a page's connected Instagram.", 400);
                return;
            }
            if(pages.Count > 1)
            {
                await RespondRaw("Conflict: mutiple pages. Choosing is not yet implemented", 400);
                return;
            }
            var page = pages.First();
            var connected = await client.GetPageInstagramAccountAsync(page.Id);
            if(connected == null)
            {
                await RespondRaw($"Error: page {page.Name} does not have any Instagram account connected to it.");
                return;
            }

            var srv = Context.Services.GetRequiredService<RepublishService>();
            srv.Data.Facebook = new()
            {
                ExpiresAt = client.oauth.expires_at,
                Id = user.Id,
                PageId = page.Id,
                Token = client.oauth.access_token,
                InstagramId = connected
            };
            srv.OnSave();
            await RespondRedirect("/republisher");
        }
        
        [Method("GET"), Path("/oauth/facebook")]
        public async Task HandleFBOauth([FromQuery]FBOAuthSuccess success)
        {
            if(!string.IsNullOrWhiteSpace(success.denied_scopes.GetValueOrDefault(null)))
            {
                await RespondRaw("Error: you denied the following permissions that are required to proceed: " + success.denied_scopes, 400);
                return;
            }
            var http = Context.Services.GetRequiredService<HttpClient>();
            FacebookClient fb = null;
            try
            {
                fb = await FacebookClient.CreateOAuthAsync(success.code,
                    Program.Configuration["tokens:facebook:app_id"],
                    Program.Configuration["tokens:facebook:app_secret"],
                    Context.GetFullPath("/oauth/facebook"), 
                    http);
            } catch(HttpException ex)
            {
                var err = ex._content;
                var json = JObject.Parse(err);
                await RespondJson(json, 400);
                return;
            }
            var me = await fb.GetMeAsync();
            if (Context.User == null)
            {
                Context.User = await Context.BotDB.GetUserByFacebook(me.Id, true);
                await Handler.SetNewLoginSession(Context, Context.User, true, true);
            }
            if (!Context.User.HasDisplayName)
            {
                Context.User.DisplayName = me.Name;
            }
            var result = await fb.GetLongLivedAccessToken(Program.Configuration["tokens:facebook:app_id"], Program.Configuration["tokens:facebook:app_secret"]);
            Context.User.Facebook = new BotDbFacebook()
            {
                AccountId = me.Id,
                AccessToken = result.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(result.expires_in.Value)
            };
            await Context.BotDB.SaveChangesAsync();
            if(success.granted_scopes.Contains("pages_show_list") && Context.User.RepublishRole.HasFlag(BotRepublishRoles.Admin))
            { // they're authorizing to give admin access.
                await handleManagedPages(fb, me);
            } else
            {
                await RespondRedirect("/republisher");
            }
        }

        [Method("GET"), Path("/oauth/facebook")]
        public async Task HandleFBOAuthFail([FromQuery]OAuthError errData)
        {
            await RespondRaw($"Error: {errData.error}, {errData.error_reason}: {errData.error_description}");
        }



    }
}
