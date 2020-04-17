using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DiscordBot.Utils
{
    public static class MethodInfoUtil
    {
        public static bool IsOverride(this MethodInfo methodInfo)
        {
            return (methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType);
        }
    }
}
