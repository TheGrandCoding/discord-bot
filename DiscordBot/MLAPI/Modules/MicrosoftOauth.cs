#if INCLUDE_MS_OAUTH_VERIFICATION
using Discord;
using DiscordBot.Classes;
using DiscordBot.Classes.Chess;
using DiscordBot.Services;
using IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace DiscordBot.MLAPI.Modules
{
    public class MicrosoftOauth : APIBase
    {
        public MicrosoftOauth(APIContext c) : base(c, "/") // shouldn't need any files.
        {
        }

        public static string getUrl(IUser user, Action<MSScopeOptions> action = null) => getUrl(user.Id, action);
        public static string getUrl(BotUser user, Action<MSScopeOptions> action = null) => getUrl(user.Id, action);
        public static string getUrl(ulong id, Action<MSScopeOptions> action = null)
        {
            var msScope = new MSScopeOptions();
            if (action == null)
                action = x =>
                {
                    x.OpenId = true;
                    x.User_Read = true;
                };
            action(msScope);
            string stateValue = id.ToString();
            stateValue += "." + Program.ToBase64(msScope.ToString());
            var ru = new RequestUrl($"https://login.microsoftonline.com/{Program.Configuration["ms_auth:tenant_id"]}/oauth2/v2.0/authorize");
            var url = ru.CreateAuthorizeUrl(
                clientId: Program.Configuration["ms_auth:client_id"],
                responseType: "id_token code",
                responseMode: "form_post",
                redirectUri: Handler.LocalAPIUrl + "/login/msoauth",
                nonce: DateTime.Now.DayOfYear.ToString(),
                state: stateValue,
                scope: msScope.GetScopes());
            Console.WriteLine(url);
            return url;
        }

        bool actOnUserProfile(TokenResponse response, HttpClient client)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            request.Headers.Add("Authorization", "Bearer " + response.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var identityResponse = client.SendAsync(request).Result;
            if (!identityResponse.IsSuccessStatusCode)
            {
                await RespondRaw("Could not complete Oauth", identityResponse.StatusCode);
                return false;
            }
            var content = identityResponse.Content.ReadAsStringAsync().Result;
            var jobj = JObject.Parse(content);
            Context.User.VerifiedEmail = jobj["mail"].ToObject<string>();
            Context.User.IsVerified = true;
            if (string.IsNullOrWhiteSpace(Context.User.Name) || Context.User.Name == Context.User.Id.ToString())
            {
                Context.User.OverrideName = jobj["displayName"].ToObject<string>();
            }
#if INCLUDE_CHESS
            var service = Program.Services.GetRequiredService<ChessService>();
            if (service != null && !Context.User.ServiceUser && !Context.User.GeneratedUser)
            {
                using var db = Program.Services.GetRequiredService<ChessDbContext>();
                string name = $"{jobj["givenName"]} {jobj["surname"].ToObject<string>()[0]}";
                var existing = db.Players.AsQueryable().FirstOrDefault(x => x.Name == name && !x.IsBuiltInAccount);
                if (existing != null)
                {
                    existing.ConnectedAccount = Context.User.Id;
                }
                else
                {
                    var chs = db.Players.AsQueryable().FirstOrDefault(x => x.DiscordAccount == ChessService.cast(Context.User.Id) && !x.IsBuiltInAccount);
                    if (chs != null)
                    {
                        chs.Name = name;
                    }
                }
                service.OnSave();
            }
#endif
            return true;
        }

        bool actOnTeams(TokenResponse response, HttpClient client)
        {
            var teamsRequest = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/joinedTeams");
            teamsRequest.Headers.Add("Authorization", "Bearer " + response.AccessToken);
            teamsRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var teamsResponse = client.SendAsync(teamsRequest).Result;
            if (!teamsResponse.IsSuccessStatusCode)
            {
                await RespondRaw("Could not retrieve your teams information", teamsResponse.StatusCode);
                return false;
            }
            var content = teamsResponse.Content.ReadAsStringAsync().Result;
            var jobj = JObject.Parse(content);
            var jvalue = jobj["value"];
            var teamsArray = (JArray)jvalue;
            Dictionary<string, string> classes = new Dictionary<string, string>();
            foreach(JToken jTeam in teamsArray)
            {
                var name = jTeam["displayName"].ToObject<string>();
                var split = name.Split('-');
                if (split.Length != 2)
                    continue;
                // class - Subject
                // eg
                // 1Mt3 - Maths
                classes[split[0].Trim()] = split[1].Trim();
            }
            Context.User.Classes = classes;
            return true;
        }

        [Method("POST"), Path("/login/msoauth")]
        [RequireAuthentication(requireAuth:true, requireValid:false)]
        [RequireApproval(false)]
        public void LoginCallback(string id_token, string code, string session_state = null, string state = null, string nonce = null)
        {
            if(Context.User.IsVerified)
            {
                await RespondRaw($"This account is already verified", 400);
                return;
            }
            var stateSplit = state.Split('.');
            var scopes = new MSScopeOptions(Program.FromBase64(stateSplit[1]).Split(' '));
            if(stateSplit[0] != Context.User.Id.ToString())
            {
                await RespondRaw("State mismatch", 400);
                return;
            }
            var jwt = new JwtSecurityToken(id_token);
            var client = Program.Services.GetRequiredService<HttpClient>();
            // TODO: validate this.
            var response = client.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest()
            {
                Address = $"https://login.microsoftonline.com/{Program.Configuration["ms_auth:tenant_id"]}/oauth2/v2.0/token",
                ClientId = Program.Configuration["ms_auth:client_id"],
                Code = code,
                RedirectUri = $"{Handler.LocalAPIUrl}/login/msoauth",
                ClientSecret = Program.Configuration["ms_auth:client_secret"],
                GrantType = "authorization_code",
                Parameters =
                {
                    {"scope", new MSScopeOptions() {
                        User_Read = true,
                        Team_ReadBasic_All = scopes.Team_ReadBasic_All
                    }}
                }
            }).Result;

            if (!actOnUserProfile(response, client))
                return;

            if(scopes.Team_ReadBasic_All)
            {
                if (!actOnTeams(response, client))
                    return;
            }

            Context.User.IsApproved = true;

            Program.Save();
            var redirect = Context.Request.Cookies["redirect"]?.Value;
            if (string.IsNullOrWhiteSpace(redirect))
                redirect = Context.User?.RedirectUrl ?? "%2F";
            redirect = Uri.UnescapeDataString(redirect);
            await RespondRedirect(redirect), System.Net.HttpStatusCode.Redirect);
        }
    
    }
}
#endif