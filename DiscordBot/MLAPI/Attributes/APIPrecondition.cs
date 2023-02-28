using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;

namespace DiscordBot.MLAPI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public abstract class APIPrecondition : Attribute
    {
        public string OR { get; set; } = "";
        public string AND { get; set; } = "";

        public bool Overriden { get; set; } = false;

        public abstract PreconditionResult Check(APIContext context, IServiceProvider services);

        public static T Get<T>(APIEndpoint command) where T : APIPrecondition
        {
            foreach(var p in command.Preconditions)
            {
                if (p is T)
                    return p as T;
            }
            return null;
        }

        public abstract bool CanChildOverride(APIPrecondition child);
    }
}
