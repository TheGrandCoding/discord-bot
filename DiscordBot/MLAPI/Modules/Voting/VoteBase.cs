using DiscordBot.Classes;
using DiscordBot.Classes.Voting;
using DiscordBot.MLAPI.Exceptions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Voting
{
    [RequireAuthentication(false, false)]
    [RequireVerifiedAccount(false)]
    public class VoteBase : APIBase
    {
        public VoteBase(APIContext c) : base(c, "vote")
        {
            Service = Program.Services.GetRequiredService<VoteService>();
        }

        public User Account { get; set; }
        public string EmailName {  get
            {
                return Context.User.VerifiedEmail?.Substring(0, Context.User.VerifiedEmail.IndexOf('@')).ToLower();
            }
        }
        public VoteService Service { get; set; }

        public override void BeforeExecute()
        {
            if(Context.User == null)
            {
                ulong id = (ulong)(200 + Program.Users.Count);
                var bUser = new BotUser(new WebUser()
                {
                    Id = id,
                    Discriminator = 0,
                    Username = null
                });
                Program.Users.Add(bUser);
                var auth = new AuthToken(AuthToken.HttpFullAccess, 24);
                bUser.Tokens.Add(auth);
                Context.HTTP.Response.AppendCookie(new System.Net.Cookie(AuthToken.SessionToken, auth.Value)
                {
                    Expires = DateTime.Now.AddHours(3),
                    Domain = null,
                    Path = "/"
                });
                var url = DiscordBot.MLAPI.Modules.MicrosoftOauth.getUrl(bUser);
                Context.User = bUser;
                RespondRaw(LoadRedirectFile(url, Context.Request.Url.PathAndQuery), System.Net.HttpStatusCode.OK);
                throw new ReqHandledException();
            }
            if (!Context.User.IsVerified)
            {
                var url = DiscordBot.MLAPI.Modules.MicrosoftOauth.getUrl(Context.User);
                throw new RedirectException(url, "Must verify");
            }
            Service.Lock.WaitOne();
            try
            {
                string s = EmailName;
                var query = Service.Users.AsQueryable()
                    .Where(x => x.UserName.ToLower() == s)
                    .Select(x => x);
                Account = query.FirstOrDefault();
            } finally
            {
                Service.Lock.Release();
            }
            if (Account == null)
            {
                if(Context.Path != "/vote/nouser")
                    throw new RedirectException("/vote/nouser", "No user");
            }
            Service.Lock.WaitOne(); // for request to be handled..
        }

        public override void AfterExecute()
        {
            Service.Lock.Release();
        }
    }
}
