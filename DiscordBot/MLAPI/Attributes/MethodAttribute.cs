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
            if(method == "GET")
                Method = HttpMethod.Get;
            else if(method == "POST")
                Method = HttpMethod.Post;
            else if(method == "PUT")
                Method = HttpMethod.Put;
        }
    }
}
