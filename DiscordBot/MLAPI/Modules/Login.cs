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
            Callback = Program.Services.GetRequiredService<OauthCallbackService>();
        }

        void handleLogin(object sender, object[] stateArgs)
        {
            Program.LogMsg("Entered OauthLogin");
            // Funky C#8, disposed at end of this function
            var oauth = new DiscordOauth("identify", Context.GetQuery("code"));
            var userInfo = oauth.GetUserInformation().Result;
            Program.LogMsg($"Found user info: {userInfo.Username}");
            try
            {
                var usr = handleUserInfo(userInfo);
                Program.LogMsg($"Handled user: {usr.Username}, {usr.Id}");
                setSessionTokens(usr);
                Program.LogMsg("Set session tokens, now logged in.");
                var pwd = usr.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
                string redirectTo = Context.Request.Cookies["redirect"]?.Value;
                if (string.IsNullOrWhiteSpace(redirectTo))
                    redirectTo = "/";
                if (pwd == null)
                    redirectTo = "/login/setpassword";
                RespondRaw(LoadRedirectFile(redirectTo), HttpStatusCode.TemporaryRedirect);
                Program.LogMsg("Users redirected.");
            }
            catch (Exception ex)
            {
                Program.LogMsg(ex, "LoginOauth");
                HTTPError(HttpStatusCode.InternalServerError, "", ex.Message);
            }
        }

        [Method("GET"), Path("/login")]
        [RequireAuthentication(false)]
        public void LoginBase()
        {
            if(Context.User != null)
            { // we'll log them out to troll them
                Context.HTTP.Response.Headers["Location"] = "/";
                var l = Context.HTTP.Request.Cookies[AuthToken.SessionToken];
                l.Expires = DateTime.Now.AddDays(-1);
                Context.HTTP.Response.SetCookie(l);
                RespondRaw($"Logged you out; redirecting to base path", HttpStatusCode.TemporaryRedirect);
                return;
            }
            var state = Callback.Register(handleLogin, Context.User);
            var uri = UrlBuilder.Discord()
                .Add("redirect_uri", Handler.LocalAPIUrl + "/oauth2/discord")
                .Add("response_type", "code")
                .Add("scope", "identify")
                .Add("state", state);
            ReplyFile("login.html", 200, new Replacements()
                .Add("link", uri));
        }

        [Method("POST"), Path("/login")]
        // We expect this data to be sent in form-data-encoded or wahtever form in the content/body
        // rather than in the Query string
        // the APIContext has the means to parse both, and CommandFinder uses both.
        [RequireAuthentication(false)]
        public void LoginPassword(string identifier, string password)
        {
            if(Context.User != null)
            {
                RespondRaw("Error: you are already logged in", 400);
                return;
            }
            BotUser user;
            if(ulong.TryParse(identifier, out var id))
            {
                user = Program.GetUserOrDefault(id);
            } else
            {
                user = Program.Users.FirstOrDefault(x 
                    => x.FirstValidUser?.Username == identifier
                    || x.OverrideName == identifier);
            }
            if(user == null)
            {
                RespondRaw("Unknown user by your identifiers.", 404);
                return;
            }
            var token = user.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
            if(token == null)
            { // since confirming whether the user does or does not have a pwd is maybe a bad idea?
                RespondRaw("Username or password incorrect", 404);
                return;
            }
            if(!token.SamePassword(password))
            {
                RespondRaw("Username or password incorrect", 400);
                return;
            }
            setSessionTokens(user); // essentially logs them in
            RespondRaw("Ok", 200);
        }

        public static void SetLoginSession(APIContext context, BotUser user)
        {
            // password valid, we need to log them in.
            var session = user.Tokens.FirstOrDefault(x => x.Name == AuthToken.SessionToken);
            if (session == null)
            {
                session = new AuthToken(AuthToken.SessionToken, 1);
                user.Tokens.Add(session);
            }
            // New session, so we'll invalidate any other login
            session.Regenerate(32);
            Program.Save(); // ensure we save the session so it persists for multiple days
            context.HTTP.Response.Cookies.Add(new Cookie(AuthToken.SessionToken, session.Value, "/")
            {
                Expires = DateTime.Now.AddDays(3)
            });
        }

        void setSessionTokens(BotUser user) => SetLoginSession(Context, user);

        async Task<HttpResponseMessage> postJson(JObject json, HttpClient client, string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            string text = "";
            foreach (JProperty item in json.Children())
                text += $"{item.Name}={System.Web.HttpUtility.UrlEncode(item.Value.ToString())}&";
            text = text.Substring(0, text.Length - 1);
            request.Content = new StringContent(text, Encoding.UTF8, "application/x-www-form-urlencoded");
            return await client.SendAsync(request);
        }

        HttpResponseMessage addUserToGuild(ulong id, ulong guildId, HttpClient client, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"guilds/{guildId}/members/{id}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", Program.Configuration["tokens:discord"]);
            var jobj = new JObject();
            jobj["access_token"] = token;
            request.Content = new StringContent(jobj.ToString(), Encoding.UTF8, "application/json");
            return client.SendAsync(request).Result;
        }

        BotUser handleUserInfo(IUser user)
        {
            var webUser = new WebUser()
            {
                Id = user.Id,
                Discriminator = user.DiscriminatorValue,
                Username = user.Username
            };
            var bUser = Program.GetUserOrDefault(webUser.Id);
            if(bUser == null)
            {
                bUser = new BotUser(webUser);
                Program.Users.Add(bUser);
                Program.Save();
            }
            return bUser;
        }

        [Method("GET"), Path("/oauth2/discord")]
        [RequireAuthentication(false)]
        public void OauthLogin(string code, string state)
        {
            if(!Callback.Invoke(Context, state))
            {
                RespondRaw("Unknown callback - state mismatch. Perhaps the bot was restarted since you were redirected?", 400);
            }
        }

        [Method("GET"), Path("/oauth2/misc")]
        [RequireAuthentication(false)]
        public void OauthMisc(string state)
        {
            if (!Callback.Invoke(Context, state))
            {
                RespondRaw("Unknown callback - state mismatch. Perhaps the bot was restarted since you were redirected?", 400);
            }
        }

        [Method("GET"), Path("/login/setpassword")]
        public void SeePswdPage()
        {
            ReplyFile("pwd.html", HttpStatusCode.OK, new Replacements()
                .IfElse("dowhat",
                    Context.User.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword) == null,
                    "create a new password", "replace your old password"));
        }
    
        [Method("POST"), Path("/login/setpassword")]
        public void SetLoginPswd(string pwd)
        {
            if (pwd.Length < 8 || pwd.Length > 32)
            {
                RespondRaw($"Password must be between 8 and 32 charactors in length", 400);
                return;
            }
            if(Program.IsPasswordLeaked(pwd).Result)
            {
                RespondRaw($"Password is known to be compromised; it cannot be used.", 400);
                return;
            }
            var token = Context.User.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
            if(token == null)
            {
                token = new AuthToken(AuthToken.LoginPassword, pwd);
                Context.User.Tokens.Add(token);
            }
            token.SetHashValue(pwd);
            Program.Save();
            Context.HTTP.Response.Headers["Location"] = "/";
            RespondRaw("Set", HttpStatusCode.Redirect);
        }
    }
}
