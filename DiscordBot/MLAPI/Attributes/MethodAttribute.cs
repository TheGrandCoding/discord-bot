using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class MethodAttribute : Attribute
    {
        public HttpMethod Method;
        public MethodAttribute(string method)
        {
            Method = new HttpMethod(method);
        }
    }
}
