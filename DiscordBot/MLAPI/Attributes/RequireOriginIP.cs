using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Discord;
using Discord.Commands;

namespace DiscordBot.MLAPI
{
    public class RequireOriginIP : APIPrecondition
    {
        private readonly IPAddress _ip;
        /// <summary>
        /// Creates via string input
        /// </summary>
        /// <param name="input">Either a config location, or an IP address</param>
        public RequireOriginIP(string input)
        {
            if (!IPAddress.TryParse(input, out _ip))
            {
                Program.LogWarning($"Unable to parse IP: '{input}'", "RqeOrIp");
            }
        }

        public override bool CanChildOverride(APIPrecondition child)
        {
            return false;
        }

        public override PreconditionResult Check(APIContext context)
        {
            if (_ip == null)
                return PreconditionResult.FromError("Internal error: precondition failed, invalid setting - please contact admin");
            IPEndPoint end = context.Request.RemoteEndPoint;
            if(end.Address.Equals(_ip))
            {
                return PreconditionResult.FromSuccess();
            }
            return PreconditionResult.FromError("You do not have permission to access this endpoint");
        }
    }
}
