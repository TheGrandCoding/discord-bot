using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using WebSocketSharp;

namespace DiscordBot.MLAPI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireServerName : APIPrecondition
    {
        public string Domain
        {
            get
            {
                if (_domain == null)
                    return _domain;
                if (_domain.StartsWith("c:"))
                {
                    _domain = _domain.Substring("c:".Length);
                    _domain = Program.Configuration[$"domains:{_domain}"];
                }
                if(_domain.StartsWith("a:"))
                {
                    _domain = _domain.Substring("a:".Length);
                    _domain = _domain + "." + Handler.LocalAPIDomain;
                }
                return _domain;
            }
        }
        private string _domain;
        private readonly bool _debugResult;
        public RequireServerName(string domainName, bool failIfdebug = false)
        {
            _domain = domainName;
            _debugResult = !failIfdebug;
        }
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }

        public override PreconditionResult Check(APIContext context)
        {
#if DEBUG
            if(_domain == "localhost")
            {
                if (IPAddress.TryParse(context.Host, out var ips) && ips.IsLocal())
                    return PreconditionResult.FromSuccess();
            }
            if(context.Host.EndsWith("ngrok.io")) return PreconditionResult.FromSuccess();
#endif
            return _domain == null || context.Host == Domain
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("Authentication failed: Host mistmatch");
        }
    }
}
