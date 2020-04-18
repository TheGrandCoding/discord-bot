using DiscordBot.Classes;
using DiscordBot.Services;
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
        public Login(APIContext c) : base(c, "login") { }

        [Method("GET"), Path("/login")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
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
            ReplyFile("login.html", 200, new Replacements()
                .Add("link",
"https://discordapp.com/api/oauth2/authorize?client_id=432861863437402113&redirect_uri=http%3A%2F%2Flocalhost%3A8887%2Flogin%2Foauth2&response_type=code&scope=identify%20guilds.join"));
        }

        [Method("POST"), Path("/login")]
        // We expect this data to be sent in form-data-encoded or wahtever form in the content/body
        // rather than in the Query string
        // the APIContext has the means to parse both, and CommandFinder uses both.
        [AllowNonAuthed]
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
                RespondRaw("Error: Unknown user by your identifiers.", 404);
                return;
            }
            var token = user.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
            if(token == null)
            { // since confirming whether the user does or does not have a pwd is maybe a bad idea?
                RespondRaw("Error: username or password incorrect", 404);
                return;
            }
            if(token.Value != password)
            {
                RespondRaw("Error: username or password incorrect", 400);
                return;
            }
            setSessionTokens(user); // essentially logs them in
            RespondRaw("Ok", 200);
        }

        void setSessionTokens(BotUser user)
        {
            // password valid, we need to log them in.
            var session = user.Tokens.FirstOrDefault(x => x.Name == AuthToken.SessionToken);
            if (session == null)
            {
                session = new AuthToken(AuthToken.SessionToken, 1);
                user.Tokens.Add(session);
            }
            // New session, so we'll invalidate any other login
            session.Value = AuthToken.Generate(32);
            Program.Save(); // ensure we save the session so it persists for multiple days
            Context.HTTP.Response.Cookies.Add(new Cookie(AuthToken.SessionToken, session.Value, "/"));
        }

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

        BotUser handleUserInfo(JObject info, HttpClient client, string token)
        {
            var webUser = new WebUser()
            {
                Id = info["id"].ToObject<ulong>(),
                Discriminator = ushort.Parse(info["discriminator"].ToObject<string>()),
                Username = info["username"].ToObject<string>()
            };
            var bUser = Program.GetUserOrDefault(webUser.Id);
            if(bUser == null)
            {
                bUser = new BotUser(webUser);
                Program.Users.Add(bUser);
                Program.Save();
            }
            var linked = ChessService.Players.FirstOrDefault(x => x.ConnectedAccount == bUser.Id);
            if(linked != null)
            {
                var chsUser = Program.ChessGuild.GetUser(bUser.Id);
                if(chsUser == null)
                {
                    var result = addUserToGuild(bUser.Id, Program.ChessGuild.Id, client, token);
                }
            }
            return bUser;
        }

        [Method("GET"), Path("/login/oauth2")]
        [AllowNonAuthed]
        public void OauthLogin(string code, string state = null)
        {
            Program.LogMsg("Entered OauthLogin");
            // Funky C#8, disposed at end of this function
            using var client = new HttpClient();
            client.BaseAddress = new Uri("https://discordapp.com/api/");
            client.DefaultRequestHeaders.Add("User-Agent", "tgc-bot-manual");

            var getToken = new JObject();
            getToken["client_id"] = 432861863437402113;
            getToken["client_secret"] = Program.Configuration["tokens:appSecret"];
            getToken["grant_type"] = "authorization_code";
            getToken["code"] = code;
            getToken["redirect_uri"] = $"{Handler.LocalAPIUrl}/login/oauth2";
            getToken["scope"] = "identify guilds.join";
            var tokenResponse = postJson(getToken, client, "oauth2/token").Result;
            Program.LogMsg($"Token recieved: {tokenResponse.StatusCode}");
            var tokenContent = tokenResponse.Content.ReadAsStringAsync().Result;
            if (!tokenResponse.IsSuccessStatusCode)
            {
                HTTPError(HttpStatusCode.BadRequest, $"Failed", $"Upstream error: {tokenResponse.StatusCode} <code>{tokenContent}</code>");
                return;
            }
            var tokenInfo = JObject.Parse(tokenContent);
            var token = tokenInfo["access_token"].ToObject<string>();
            var idenRequest = new HttpRequestMessage(HttpMethod.Get, "users/@me");
            idenRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var identityResponse = client.SendAsync(idenRequest).Result;
            Program.LogMsg($"Identity recieved: {identityResponse.StatusCode}");
            if (!identityResponse.IsSuccessStatusCode)
            {
                HTTPError(HttpStatusCode.BadRequest, $"Failed", $"Upstream error: {tokenResponse.StatusCode} <code>{tokenContent}</code>");
                return;
            }
            var userInfo = JObject.Parse(identityResponse.Content.ReadAsStringAsync().Result);
            Program.LogMsg($"Found user info: {userInfo["username"]}");
            try
            {
                var usr = handleUserInfo(userInfo, client, token);
                Program.LogMsg($"Handled user: {usr.Username}, {usr.Id}");
                setSessionTokens(usr);
                Program.LogMsg("Set session tokens, now logged in.");
                var pwd = usr.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
                string redirectTo = "/";
                if(pwd == null)
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
            var token = Context.User.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
            if(token == null)
            {
                token = new AuthToken(AuthToken.LoginPassword, pwd);
                Context.User.Tokens.Add(token);
            }
            token.Value = pwd;
            Program.Save();
            Context.HTTP.Response.Headers["Location"] = "/";
            RespondRaw("Set", HttpStatusCode.Redirect);
        }
    }
}
