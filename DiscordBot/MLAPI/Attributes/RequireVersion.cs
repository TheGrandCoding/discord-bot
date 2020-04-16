using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;

namespace DiscordBot.MLAPI
{
    public class RequireVersion : APIPrecondition
    {
        public const string DEBUG = "debug";
        public RequireVersion(string v)
        {
            Version = v;
        }
        public string Version;
        public override bool CanChildOverride(APIPrecondition child)
        {
            if(child is RequireVersion v)
            {
                return v.Version.Contains(Version + "-");
            }
            return false;
        }

        public override PreconditionResult Check(APIContext context)
        {
            string v = context.HTTP.Request.Headers.Get("X-VERSION");
            return v == Version ? PreconditionResult.FromSuccess() : PreconditionResult.FromError($"Expected version {Version}, but got version '{(v ?? "<null>")}'");
        }
    }
}
