using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;

namespace DiscordBot.MLAPI
{
    public class RequireValidHTTPAgent : APIPrecondition
    {
        public string[] ValidAgents = new string[]
        {
            "Chrome", "iOS", "Safari", "Opera"
        };
        private bool requires;
        public RequireValidHTTPAgent(bool requiresWebBrowser = true)
        {
            requires = requiresWebBrowser;
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            if(child is RequireValidHTTPAgent)
                return true;
            return false;
        }

        public override PreconditionResult Check(APIContext context, IServiceProvider services)
        {
            if(requires)
            {
                foreach(var usr in ValidAgents)
                {
                    if (context.Request.UserAgent.Contains(usr))
                        return PreconditionResult.FromSuccess();
                }
                return PreconditionResult.FromError($"User-Agent must contain: {string.Join(",", ValidAgents)}");
            }
            return PreconditionResult.FromSuccess();
        }
    }
}
