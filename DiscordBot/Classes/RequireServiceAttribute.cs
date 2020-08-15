using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    /// <summary>
    /// For services.
    /// </summary>
    public class RequireServiceAttribute : Attribute
    {
        public Type[] Types { get; }
        public RequireServiceAttribute(params Type[] types)
        {
            Types = types;
        }
    }
}
