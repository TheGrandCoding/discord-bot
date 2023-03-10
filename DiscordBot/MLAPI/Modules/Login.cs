using Discord;
using DiscordBot.Classes;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    public class Login : APIBase
    {
        public OauthCallbackService Callback { get; set; }
        public Login(APIContext c) : base(c, "login") 
        {
            Callback = c.Services.GetRequiredService<OauthCallbackService>();
        }

        void handleDiscordOAuth(object sender, object[] stateArgs)
        {
            // Funky C#8, disposed at end of this function
            var oauth = new DiscordOauth("identify", Context.GetFullPath("/oauth2/discord"), Context.GetQuery("code"));
            var userInfo = oauth.GetUserInformation().Result;
            try
            {
                var result = Context.BotDB.GetUserFromDiscord(userInfo, true).Result;
                if(!result.Success)
                {
                    RespondRaw($"Faield: {result.ErrorMessage}", 500);
                    return;
                }
                var usr = result.Value;
                setSessionTokens(usr).Wait();
                string redirectTo = Context.Request.Cookies["redirect"]?.Value;
                if (string.IsNullOrWhiteSpace(redirectTo))
                    redirectTo = Context.User?.RedirectUrl ?? "/";
                if (usr.Approved.HasValue == false)
                {
                    redirectTo = "/login/approval";
                    var admin_id = ulong.Parse(Program.Configuration["settings:admin"]);
                    string avatar = userInfo.GetAnyAvatarUrl();
                    Program.Client.GetUser(admin_id).SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("MLAPI User")
                        .WithDescription($"{userInfo.Username}#{userInfo.Discriminator} ({userInfo.Id}) is awaiting approval")
                        .WithUrl(Handler.LocalAPIUrl + "/bot/approve")
                        .WithThumbnailUrl(avatar)
                        .AddField("User-Agent", Context.Request.Headers["User-Agent"] ?? "none", true)
                        .AddField("IP", Context.Request.Headers["X-Forwarded-For"] ?? "localhost", true)
                        .AddField("Origin", Context.Request.Headers["Origin"] ?? "none", true)
                        .Build());
                } else if (usr.Approved.Value == false)
                {
                    redirectTo = "/login/approval";
                } else if (usr.Connections.PasswordHash == null)
                {
                    redirectTo = "/login/setpassword";
                }
                RespondRedirect(redirectTo).Wait();
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "LoginOauth");
                HTTPError(HttpStatusCode.InternalServerError, "", ex.Message).Wait();
            }
        }

        [Method("GET"), Path("/login")]
        public async Task LoginBase()
        {
            if(Context.User != null)
            { // we'll log them out to troll them
                Context.HTTP.Response.Headers["Location"] = "/";
                var l = Context.HTTP.Request.Cookies[BotDbAuthSession.CookieName];
                l.Expires = DateTime.Now.AddDays(-1);
                Context.HTTP.Response.SetCookie(l);
                await RespondRaw($"Logged you out; redirecting to base path", HttpStatusCode.Redirect);
                return;
            }
            await ReplyFile("login.html", 200, new Replacements()
                .Add("link", "/login/discord"));
        }

        [Method("GET"), Path("/logout")]
        public async Task Logout(string back = "/")
        {
            Context.HTTP.Response.Headers["Location"] = back;
            var l = Context.HTTP.Request.Cookies[BotDbAuthSession.CookieName] ?? new Cookie(BotDbAuthSession.CookieName, "null");
            l.Expires = DateTime.Now.AddDays(-1);
            Context.HTTP.Response.SetCookie(l);
            await RespondRedirect(back);
        }

        [Method("GET"), Path("/login/discord")]
        public async Task RedirectToDiscord(string redirect = "/")
        {
            var state = Callback.Register(handleDiscordOAuth, Context.User);
            var uri = UrlBuilder.Discord()
                .Add("redirect_uri", Context.GetFullPath("/oauth2/discord"))
                .Add("response_type", "code")
                .Add("scope", "identify")
                .Add("state", state);
            await RespondRedirect(uri, redirect);
        }

        [Method("POST"), Path("/login")]
        // We expect this data to be sent in form-data-encoded or wahtever form in the content/body
        // rather than in the Query string
        // the APIContext has the means to parse both, and CommandFinder uses both.
        public async Task LoginPassword(string username, string password)
        {
            if(Context.User != null)
            {
                await RespondRaw("Error: you are already logged in", 400);
                return;
            }
            var result = Context.BotDB.AttemptLoginAsync(username, password).Result;
            if(!result.Success)
            {
                await RespondRedirect($"#username={Uri.EscapeDataString(username)}&fbl={Uri.EscapeDataString(result.ErrorMessage)}");
                return;
            }
            await setSessionTokens(result.Value); // essentially logs them in
            await RespondRedirect("/");
        }
        [Method("POST"), Path("/register")]
        public async Task RegisterWithPassword(string username, string password)
        {
            if (Context.User != null)
            {
                await RespondRaw("Error: you are already logged in", 400);
                return;
            }
            var result = Context.BotDB.AttemptRegisterAsync(username, password, Context.Services).Result;
            if (!result.Success)
            {
                await RespondRedirect($"/login#username={Uri.EscapeDataString(username)}&fbr={Uri.EscapeDataString(result.ErrorMessage)}");
                return;
            }
            await setSessionTokens(result.Value); // essentially logs them in
            await RespondRedirect("/");
            try
            {
                var embed = new EmbedBuilder()
                    .WithTitle("MLAPI Account Created")
                    .AddField("Username", $"**{username}**", true)
                    .AddField("ID", result.Value.Id.ToString(), true)
                    .AddField("IP", Context.IP)
                    .AddField("User-Agent", Context.HTTP.Request.UserAgent, true)
                    .WithDescription($"Go [here to approve]({Context.GetFullPath("/bot/approve")}");
                await Program.SendLogMessageAsync(embed: embed.Build());
            } catch { }
        }

        [Method("GET"), Path("/login/approval")]
        [RequireAuthentication(requireDiscordConnection: false)]
        [RequireApproval(false)]
        public async Task ApprovalPage()
        {
            if(!Context.User.Approved.HasValue)
            {
                await RespondRaw("Your account is pending approval; please wait.", 200);
            } else if (Context.User.Approved.Value)
            {
                await RespondRaw("Your account is approved; please visit the main page.", 200);
            }
            else
            {
                await RespondRaw("Your account has not been approved and is unable to access this website.", 200);
            }
        }


        Task setSessionTokens(BotDbUser user) => Handler.SetNewLoginSession(Context, user);

        async Task<HttpResponseMessage> postJson(JObject json, BotHttpClient client, string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            string text = "";
            foreach (JProperty item in json.Children())
                text += $"{item.Name}={System.Web.HttpUtility.UrlEncode(item.Value.ToString())}&";
            text = text.Substring(0, text.Length - 1);
            request.Content = new StringContent(text, Encoding.UTF8, "application/x-www-form-urlencoded");
            return await client.SendAsync(request);
        }

        HttpResponseMessage addUserToGuild(ulong id, ulong guildId, BotHttpClient client, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"guilds/{guildId}/members/{id}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", Program.Configuration["tokens:discord"]);
            var jobj = new JObject();
            jobj["access_token"] = token;
            request.Content = new StringContent(jobj.ToString(), Encoding.UTF8, "application/json");
            return client.SendAsync(request).Result;
        }


        [Method("GET"), Path("/oauth2/discord")]
        public async Task OauthLogin(string code, string state)
        {
            if(!Callback.Invoke(Context, state))
            {
                await RespondRaw("Unknown callback - state mismatch. Perhaps the bot was restarted since you were redirected?", 400);
            }
        }

        [Method("GET"), Path("/oauth2/misc")]
        public async Task OauthMisc(string state)
        {
            if (!Callback.Invoke(Context, state))
            {
                await RespondRaw("Unknown callback - state mismatch. Perhaps the bot was restarted since you were redirected?", 400);
            }
        }

        [Method("GET"), Path("/oauth2/trakt")]
        [RequireAuthentication]
        public async Task OauthTrakt(string code = null)
        {
            var srv = Context.Services.GetRequiredService<TraktService>();
            if(string.IsNullOrWhiteSpace(code))
            {
                await RespondRedirect(srv.OAuthRedirectUri.ToString());
                return;
            }
            srv.AddUser(Context.User.Id, code).Wait();
            await RespondRaw("Success");
        }

        [Method("GET"), Path("/login/setpassword")]
        [RequireAuthentication]
        public async Task SeePswdPage()
        {
            await ReplyFile("pwd.html", HttpStatusCode.OK, new Replacements()
                .IfElse("dowhat",
                    Context.User.Connections.PasswordHash == null,
                    "create a new password", "replace your old password"));
        }
    
        [Method("POST"), Path("/login/setpassword")]
        [RequireAuthentication]
        public async Task SetLoginPswd(string pwd)
        {
            if (pwd.Length < 8 || pwd.Length > 32)
            {
                await RespondRaw($"Password must be between 8 and 32 charactors in length", 400);
                return;
            }
            if(Program.IsPasswordLeaked(Context.Services.GetRequiredService<BotHttpClient>(), pwd).Result)
            {
                await RespondRaw($"Password is known to be compromised; it cannot be used.", 400);
                return;
            }
            Context.User.Connections.PasswordHash = PasswordHash.HashPassword(pwd);
            await Handler.SetNewLoginSession(Context, Context.User, true);
            await RespondRedirect(Context.User.RedirectUrl ?? "/");
        }
    
    
        [Method("GET"), Path("/verify")]
        [RequireAuthentication(true)]
        [RequireVerifiedAccount(true)]
        [RequireApproval(false)]
        public async Task ForceVerify()
        {
            Context.User.Verified = true;
            var service = Context.Services.GetRequiredService<EnsureLevelEliteness>();
            foreach(var guild in Program.Client.Guilds)
            {
                if (!service.Guilds.TryGetValue(guild.Id, out var save))
                    continue;
                var usr = guild.GetUser(Context.User.Id);
                if (usr == null)
                    continue;
                var role = save.VerifyRole;
                usr.AddRoleAsync(role, new RequestOptions()
                {
                    AuditLogReason = $"User has now verified"
                }).Wait();
            }
            service.OnSave();
            await RespondRaw($"Verification process complete. You can close this window");
        }
    }
}
