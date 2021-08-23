using DiscordBot.Classes;
using DiscordBot.MLAPI;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public abstract class BotWSBase : WebSocketBehavior
    {

        public string SessionToken { get
            {
                string strToken = null;
                var cookie = Context.CookieCollection[AuthSession.CookieName];
                strToken ??= cookie?.Value;
                strToken ??= Context.QueryString.Get(AuthSession.CookieName);
                strToken ??= Context.Headers.Get($"X-{AuthSession.CookieName.ToUpper()}");
                return strToken;
            } }

        string _ip = null;
        public string IP
        {
            get
            {
                _ip ??= Context.Headers["X-Forwarded-For"] ?? Context.UserEndPoint.Address.ToString();
                return _ip;
            }
        }

        private BotUser user;
        public BotUser User { get
            {
                if (user != null)
                    return user;
                Program.LogDebug($"WS connection has session token: {(SessionToken ?? "null")}", IP);
                if (!Handler.findSession(SessionToken, out var usr, out _))
                {
                    Program.LogDebug($"{IP} provided an unknown session token.", "WSLog");
                    return null;
                }
                user = usr;
                return user;
            } }
    }
}
