using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class RequireServiceAttribute : Attribute
    {
        public Type[] Types { get; }
        public RequireServiceAttribute(params Type[] types)
        {
            Types = types;
        }
    }
}
