using Discord;
using DiscordBot.Services;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class MicrosoftOauth : APIBase
    {
        public MicrosoftOauth(APIContext c) : base(c, "/") // shouldn't need any files.
        {
        }

        public static string getUrl(IUser user)
        {
            var ru = new RequestUrl($"https://login.microsoftonline.com/{Program.Configuration["ms_auth:tenant_id"]}/oauth2/v2.0/authorize");
            var url = ru.CreateAuthorizeUrl(
                clientId: Program.Configuration["ms_auth:client_id"],
                responseType: "id_token code",
                responseMode: "form_post",
                redirectUri: Handler.LocalAPIUrl + "/login/msoauth",
                nonce: DateTime.Now.DayOfYear.ToString(),
                state: user.Id.ToString(),
                scope: "openid https://graph.microsoft.com/user.read");
            Console.WriteLine(url);
            return url;
        }

        [Method("POST"), Path("/login/msoauth")]
        public void LoginCallback(string id_token, string code, string session_state = null, string state = null, string nonce = null)
        {
            if(!string.IsNullOrWhiteSpace(Context.User.VerifiedEmail))
            {
                RespondRaw($"This account is already verified", 400);
                return;
            }
            if(state != Context.User.Id.ToString())
            {
                RespondRaw("State mismatch", 400);
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
                    {"scope", "https://graph.microsoft.com/user.read" }
                }
            }).Result;

            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            request.Headers.Add("Authorization", "Bearer " + response.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var identityResponse = client.SendAsync(request).Result;
            var content = identityResponse.Content.ReadAsStringAsync().Result;
            var jobj = JObject.Parse(content);
            Context.User.VerifiedEmail = jobj["mail"].ToObject<string>();
            var service = Program.Services.GetRequiredService<ChessService>();
            if(service != null)
            {
                var chs = ChessService.Players.FirstOrDefault(x => x.ConnectedAccount == Context.User.Id);
                if(chs != null)
                {
                    chs.Name = $"{jobj["givenName"]} {jobj["surname"].ToObject<string>()[0]}";
                    service.OnSave();
                }
            }
            Program.Save();
            var redirect = Context.Request.Cookies["Redirect"]?.Value;
            if (string.IsNullOrWhiteSpace(redirect))
                redirect = "/";
            RespondRaw(LoadRedirectFile(redirect), 303);
        }
    }
}
