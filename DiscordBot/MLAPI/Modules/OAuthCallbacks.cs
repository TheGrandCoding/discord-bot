using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using ExternalAPIs;
using ExternalAPIs.Facebook;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    [Path("/oauth")]
    [Host(null)]
    public class OAuthCallbacks : APIBase
    {
        public OAuthCallbacks(APIContext c) : base(c, "oauth") { }
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

        [Method("GET"), Path("/instagram")]
        public async Task HandleIGOAuthFail([FromQuery] OAuthError errData)
        {
            await RespondRaw($"Error: {errData.error}, {errData.error_reason}: {errData.error_description}");
        }

        [Method("GET"), Path("/instagram")]
        public async Task HandleIGOauthSuccess([FromQuery] OAuthSuccess success)
        {
            if (await CheckState(success.state.GetValueOrDefault(null)) == false) return;
            var http = Context.Services.GetRequiredService<HttpClient>();
            var insta = await InstagramClient.CreateOAuthAsync(success.code,
                Program.Configuration["tokens:instagram:app_id"],
                Program.Configuration["tokens:instagram:app_secret"],
                Context.GetFullUrl(nameof(Modules.OAuthCallbacks.HandleIGOauthSuccess)),
                http);

            if (Context.User == null)
            {
                Context.User = await Context.BotDB.GetUserByInstagram(insta.oauth.UserId.ToString(), true);
                await Handler.SetNewLoginSession(Context, Context.User, true, true);
            }
            if (!Context.User.HasDisplayName)
            {
                var me = await insta.GetMeAsync(ExternalAPIs.Instagram.IGUserFields.Username);
                Context.User.DisplayName = me.Username;
            }
            var result = await insta.GetLongLivedAccessToken(Program.Configuration["tokens:instagram:app_secret"]);
            Context.User.Instagram = new BotDbInstagram()
            {
                AccountId = insta.oauth.UserId.ToString(),
                AccessToken = result.AccessToken,
                ExpiresAt = result.ExpiresAt.Value
            };
            await Context.BotDB.SaveChangesAsync();

            await RedirectTo(nameof(Modules.Republisher.Republish.ViewRepublisher));
        }

        async Task handleManagedPages(FacebookClient client, FBUser user)
        {
            var pages = await client.GetMyAccountsAsync();
            if (pages.Count == 0)
            {
                await RespondRaw($"Error: you do not have any connected pages despite trying to setup publishing to such a page, or a page's connected Instagram.", 400);
                return;
            }
            if (pages.Count > 1)
            {
                await RespondRaw("Conflict: mutiple pages. Choosing is not yet implemented", 400);
                return;
            }
            var page = pages.First();
            var connected = await client.GetPageInstagramAccountAsync(page.Id);
            if (connected == null)
            {
                await RespondRaw($"Error: page {page.Name} does not have any Instagram account connected to it.");
                return;
            }

            var srv = Context.Services.GetRequiredService<RepublishService>();
            srv.Data.Facebook = new()
            {
                ExpiresAt = client.oauth.ExpiresAt.Value,
                Id = user.Id,
                PageId = page.Id,
                Token = client.oauth.AccessToken,
                InstagramId = connected
            };
            srv.OnSave();
            await RedirectTo(nameof(Modules.Republisher.Republish.ViewRepublisher));
        }

        [Method("GET"), Path("/facebook")]
        public async Task HandleFBOauth([FromQuery] FBOAuthSuccess success)
        {
            if (await CheckState(success.state.GetValueOrDefault(null)) == false) return;
            if (!string.IsNullOrWhiteSpace(success.denied_scopes.GetValueOrDefault(null)))
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
                    Context.GetFullUrl(nameof(Modules.OAuthCallbacks.HandleFBOauth)),
                    http);
            }
            catch (HttpException ex)
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
                AccessToken = result.AccessToken,
                ExpiresAt = result.ExpiresAt.Value
            };
            await Context.BotDB.SaveChangesAsync();
            if (success.granted_scopes.Contains("pages_show_list") && Context.User.RepublishRole.HasFlag(BotRepublishRoles.Admin))
            { // they're authorizing to give admin access.
                await handleManagedPages(fb, me);
            }
            else
            {
                await RedirectTo(nameof(Modules.Republisher.Republish.ViewRepublisher));
            }
        }

        [Method("GET"), Path("/facebook")]
        public async Task HandleFBOAuthFail([FromQuery] OAuthError errData)
        {
            await RespondRaw($"Error: {errData.error}, {errData.error_reason}: {errData.error_description}");
        }

    }
}
