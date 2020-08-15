using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Commands.Attributes
{
    /// <summary>
    /// For commands.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ReliesOnServiceAttribute : Attribute
    {
        public readonly Type[] Services;
        public ReliesOnServiceAttribute(params Type[] services)
        { // trusting here that the types do inherit Service
            Services = services;
        }
    }
}
