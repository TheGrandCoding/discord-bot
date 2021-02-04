using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace qBitApi
{
    public static class qBitApiConfig
    {
        public static string Version { get; } =
            typeof(qBitApiConfig).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            typeof(qBitApiConfig).GetTypeInfo().Assembly.GetName().Version.ToString(3) ??
            "Unknown";
        public const int APIVersion = 2;
    }
}
