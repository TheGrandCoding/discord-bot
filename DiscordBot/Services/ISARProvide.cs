using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    /// <summary>
    /// Indicates that the service is capable of providing data pursuant to a SAR
    /// </summary>
    public interface ISARProvider
    {
        JToken GetSARDataFor(ulong userId);
    }
}
