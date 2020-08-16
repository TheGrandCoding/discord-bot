using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Commands.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class SensitiveAttribute : Attribute
    {
    }
}
