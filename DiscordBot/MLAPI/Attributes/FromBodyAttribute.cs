using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromBodyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromQueryAttribute : Attribute { }
}
