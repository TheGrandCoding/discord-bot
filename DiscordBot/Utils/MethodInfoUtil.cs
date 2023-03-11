using System;
using System.Collections.Generic;
using System.Linq;
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

        public static IEnumerable<T> GetAttributesInParents<T>(this MethodInfo info) where T : Attribute
        {
            foreach(T attr in info.GetCustomAttributes<T>())
            {
                yield return attr;
            }
            Type type = info.DeclaringType;
            while (type != null)
            {
                foreach (T attr in type.GetCustomAttributes<T>(false))
                    yield return attr;
                type = type.BaseType;
            }
        }
    }
}
