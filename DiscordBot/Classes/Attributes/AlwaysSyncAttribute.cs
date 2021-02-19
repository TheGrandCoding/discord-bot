using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Attributes
{
    /// <summary>
    /// Indicates that a debug version should always sync the service specified
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AlwaysSyncAttribute : Attribute
    {
    }
}
