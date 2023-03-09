using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Utils;
using FacebookAPI;
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

        [DebuggerStepThrough]
        string getUriBase()
        {
#if DEBUG
            return "https://e58a-88-107-8-97.ngrok.io";
#else
            return Handler.LocalAPIUrl;
#endif
        }

        string getInstaUrl()
        {
            return InstagramClient.GetBasicRedirectUri(Program.Configuration["tokens:instagram:app_id"],
                $"{getUriBase()}/oauth/instagram",
                FacebookAPI.Instagram.BasicAPIScopes.All).ToString();
        }

        [Method("GET"), Path("/republisher")]
        public async Task View()
        {
            var rep = new Replacements();

            if(Context.User?.Instagram?.IsInvalid() ?? true)
            {
                var url = getInstaUrl();
                rep.Add("instagram", $"<div data-url='{url}' onclick='redirectErr(event)' class='error'>Not logged in. Please click anywhere in this box to login to instagram</div>");
            } else
            {
                rep.Add("instagram", $"<input type='button' value='Search for Instagram post' onclick='igSearch()'><div id='instaPosts'></div>");
            }

            await ReplyFile("select.html", 200, rep);
        }

        public Div ToHtml(IGMedia media)
        {
            var div = new Div(id: $"ig_{media.Id}", cls: "ig_media");
            var img = new Img(media.MediaUrl)
            {
                Style = "width: 32px"
            };
            div.Children.Add(img);
            var anchor = new Anchor(media.Permalink, media.Caption);
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
            var insta = InstagramClient.Create(Context.User.Instagram.AccessToken, Context.User.Instagram.AccountId, http);
            var user = await insta.GetMeAsync(IGUserFields.Id | IGUserFields.Username | IGUserFields.AccountType | IGUserFields.MediaCount | IGUserFields.Media);
            string ig = "";
            foreach(var media in user.MediaIds)
            {
                var full = await insta.GetMediaAsync(media, IGMediaFields.All);
                ig += ToHtml(full) + "\n";
            }
            await RespondRaw(ig, 200);
        }
        
        
        
        
        [Method("GET"), Path("/oauth/instagram")]
        public async Task HandleOauth(string code = null, string state = null, 
                                      string error = null, string error_reason = null, string error_description = null)
        {
            if(error != null)
            {
                await RespondRaw($"Error: {error}, {error_reason}: {error_description}");
                return;
            }
            var http = Context.Services.GetRequiredService<HttpClient>();
            var insta = await InstagramClient.CreateOAuthAsync(code,
                Program.Configuration["tokens:instagram:app_id"],
                Program.Configuration["tokens:instagram:app_secret"],
                $"{getUriBase()}/oauth/instagram",
                http);

            if(Context.User == null)
            {
                Context.User = await Context.BotDB.GetUserByInstagram(insta.oauth.user_id.ToString(), true);
                await Handler.SetNewLoginSession(Context, Context.User, true, true);
            }
            Context.User.Instagram = new BotDbInstagram()
            {
                AccountId = insta.oauth.user_id.ToString(),
                AccessToken = insta.oauth.access_token,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            await RespondRedirect("/republisher");
        }
    }
}
