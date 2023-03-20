using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using DiscordBot.Utils;
using ExternalAPIs;
using ExternalAPIs.Facebook;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
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
        public class TikTokOAuthSuccess : OAuthSuccess
        {
            public string scopes { get; set; }
            public Optional<string> state { get; set; }
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
                await Handler.SetNewLoginSession(Context, Context.User, true);
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
                await Handler.SetNewLoginSession(Context, Context.User, true);
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

        async Task<TikTokClient> getTikTok(string code)
        {
            var client = await TikTokClient.CreateOAuthAsync(code,
                Program.Configuration["tokens:tiktok:client_key"],
                Program.Configuration["tokens:tiktok:client_secret"],
                Context.Services.GetRequiredService<HttpClient>());

            if(Context.User == null)
            {
                Context.User = await Context.BotDB.GetUserByFacebook(client.oauth.OpenId, true);
                await Context.BotDB.SaveChangesAsync();
                await Handler.SetNewLoginSession(Context, Context.User, true);
            }
            Context.User.TikTok = new()
            {
                AccessToken = client.oauth.AccessToken,
                AccountId = client.oauth.OpenId,
                ExpiresAt = client.oauth.ExpiresAt ?? DateTime.Now.AddDays(1),
                RefreshExpiresAt = client.oauth.RefreshExpiresAt ?? DateTime.Now.AddDays(365),
                RefreshToken = client.oauth.RefreshToken,
            };
            await Context.BotDB.SaveChangesAsync();

            return client;
        }
        async Task handleTikTokAdmin(string code)
        {
            var client = await getTikTok(code);
            if(Context.User.RepublishRole != BotRepublishRoles.Admin)
            {
                await RespondRaw("Error: you do not have permission to do that.", 403);
                return;
            }
            var srv = Context.Services.GetRequiredService<RepublishService>();
            srv.Data.TikTok = new()
            {
                Id = client.oauth.OpenId,
                Token = client.oauth.AccessToken,
                ExpiresAt = client.oauth.ExpiresAt ?? DateTime.Now.AddDays(1),
                RefreshToken = client.oauth.RefreshToken,
                RefreshExpiresAt = client.oauth.RefreshExpiresAt ?? DateTime.Now.AddDays(365),
            };
            srv.OnSave();
            await RedirectTo(nameof(Republisher.Republish.ViewRepublisher));
        }
        async Task handleTikTokUser(string code)
        {
            var client = await getTikTok(code);

            if(!Context.User.HasDisplayName)
            {
                var me = await client.GetMeAsync(ExternalAPIs.TikTok.TikTokUserFields.DisplayName | ExternalAPIs.TikTok.TikTokUserFields.OpenId);
                Context.User.DisplayName = me.DisplayName;
                await Context.BotDB.SaveChangesAsync();
            }

            await RedirectTo(nameof(Republisher.Republish.ViewRepublisher));
        }

        [Method("GET"), Path("/tiktok")]
        public async Task HandleTiktokOAuth([FromQuery]TikTokOAuthSuccess success)
        {
            var stateWithPrefix = success.state.GetValueOrDefault("m:_");
            (var prefix, var state) = stateWithPrefix.Split(':');
            if (await CheckState(state) == false) return;
            var scopes = success.scopes.Split(',');
            if(prefix == "a")
            {
                if(!scopes.Contains("video.upload"))
                {
                    await RespondRaw("Error: you need to authorize 'video.upload' for admin setup.", 400);
                    return;
                }
                await handleTikTokAdmin(success.code);
            } else if(prefix == "u")
            {
                if(!scopes.Contains("video.list"))
                {
                    await RespondRaw("Error: you need to authorize 'video.list'", 400);
                    return;
                }
                if (!scopes.Contains("user.info.basic"))
                {
                    await RespondRaw("Error: you need to authorize 'user.info.basic'", 400);
                    return;
                }
                await handleTikTokUser(success.code);
            }
        }

        async Task handleDiscordWebhooks(DiscordOAuthClient discord)
        {
            if (discord.oauth!.Webhook == null)
            {
                await RespondRaw("Error: you did not give webhook authority.", 200);
                return;
            }
            if(Context.User == null)
            {
                var user = await discord.GetUserInformation();
                var result = await Context.BotDB.GetUserFromDiscord(ulong.Parse((string)user["id"]), false);
                if (!result.Success)
                {
                    await RespondRaw("Error: your account is unrecognised.", 400);
                    return;
                }
                await Handler.SetNewLoginSession(Context, result.Value, true);
                Context.User = result.Value;
            }
            if (Context.User.RepublishRole != BotRepublishRoles.Admin)
            {
                await RespondRaw("Error: you do not have permission to do that.", 403);
                return;
            }
            var service = Context.Services.GetRequiredService<RepublishService>();
            await service.SetDiscordWebhook(discord.oauth.Webhook.Url);
            var dswc = new Discord.Webhook.DiscordWebhookClient(discord.oauth.Webhook.Url);
            await RedirectTo(nameof(Republisher.Republish.ViewAdmin));
        }


        class JsonUser : IUser
        {
            [JsonProperty("avatar")]
            public string AvatarId { get; set; }
            public string Discriminator { get; set; }
            public ushort DiscriminatorValue => ushort.Parse(Discriminator);
            public bool IsBot { get; set; }
            public bool IsWebhook { get; set; }
            public string Username { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public ulong Id { get; set; }
            public string Mention => $"<@{Id}>";
            public IActivity Activity { get; set; }
            public UserStatus Status { get; set; }
            public IImmutableSet<ClientType> ActiveClients { get; set; }
            public IImmutableList<IActivity> Activities { get; set; }
            public UserProperties? PublicFlags { get; set; }
            public string BannerId => throw new NotImplementedException();
            public Color? AccentColor => throw new NotImplementedException();
            IReadOnlyCollection<ClientType> IPresence.ActiveClients => throw new NotImplementedException();
            IReadOnlyCollection<IActivity> IPresence.Activities => throw new NotImplementedException();
            public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null)
            {
                throw new NotImplementedException();
            }
            public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
                => Discord.CDN.GetUserAvatarUrl(Id, AvatarId, size, format);
            public string GetBannerUrl(ImageFormat format = ImageFormat.Auto, ushort size = 256)
            {
                throw new NotImplementedException();
            }
            public string GetDefaultAvatarUrl()
                => Discord.CDN.GetDefaultUserAvatarUrl(DiscriminatorValue);
            public Task<IDMChannel> GetOrCreateDMChannelAsync(RequestOptions options = null)
            {
                throw new NotImplementedException();
            }
        }
        async Task handleDiscordLogin(DiscordOAuthClient discord)
        {
            var userJson = await discord.GetUserInformation();
            var asString = userJson.ToString();
            var userInfo = JsonConvert.DeserializeObject<JsonUser>(asString);
            try
            {
                var result = await Context.BotDB.GetUserFromDiscord(userInfo, true);
                if (!result.Success)
                {
                    await RespondRaw($"Failed: {result.ErrorMessage}", 500);
                    return;
                }
                var usr = result.Value;
                await Handler.SetNewLoginSession(Context, usr);
                Context.User = usr;
                if (Context.User.Approved.HasValue == false)
                {
                    var admin_id = ulong.Parse(Program.Configuration["settings:admin"]);
                    string avatar = userInfo.GetAnyAvatarUrl();
                    await Program.Client.GetUser(admin_id).SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("MLAPI User")
                        .WithDescription($"{userInfo.Username}#{userInfo.Discriminator} ({userInfo.Id}) is awaiting approval")
                        .WithUrl(Handler.LocalAPIUrl + "/bot/approve")
                        .WithThumbnailUrl(avatar)
                        .AddField("User-Agent", Context.Request.Headers["User-Agent"] ?? "none", true)
                        .AddField("IP", Context.Request.Headers["X-Forwarded-For"] ?? "localhost", true)
                        .AddField("Origin", Context.Request.Headers["Origin"] ?? "none", true)
                        .Build());
                }
                // redirect by javascript, workaround two cookies not working properly.
                await RespondRedirect(getRedirectReturn(clearCookie: false), code: 200);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "LoginOauth");
                await HTTPError(HttpStatusCode.InternalServerError, "", ex.Message);
            }
        }

        [Method("GET"), Path("/discord")]
        [RequireNoExcessQuery(false)]
        public async Task HandleDiscordOAuth([FromQuery]OAuthSuccess success)
        {
            var state = success.state.GetValueOrDefault("_:_");
            state = state.Substring(state.IndexOf(':') + 1);
            if (await CheckState(state) == false) return;
            var http = Context.Services.GetRequiredService<HttpClient>();
            bool login = success.state.GetValueOrDefault("").StartsWith("l:");
            DiscordOAuthClient discord;
            try
            {
                DiscordOAuthScopes scopes = (DiscordOAuthScopes)0;
                if (login || Context.User == null)
                    scopes |= DiscordOAuthScopes.Identify;
                if (!login)
                    scopes |= DiscordOAuthScopes.WebhookIncoming;
                discord = await DiscordOAuthClient.CreateOAuthAsync(success.code,
                    Program.AppInfo.Id.ToString(),
                    Program.Configuration["tokens:appSecret"],
                    scopes,
                    Context.GetFullUrl(nameof(HandleDiscordOAuth)),
                    http);
            }
            catch (HttpException ex)
            {
                var err = ex._content;
                var json = JObject.Parse(err);
                await RespondJson(json, 400);
                return;
            }
            if(login)
            {
                await handleDiscordLogin(discord);
            } else
            {
                await handleDiscordWebhooks(discord);
            }
        }
    }
}
