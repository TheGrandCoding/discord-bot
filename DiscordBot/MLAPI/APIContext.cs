using Discord.Commands;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class APIContext
    {
        public HttpListenerContext HTTP { get; set; }
        public HttpListenerRequest Request => HTTP.Request;
        public string Path => Request.Url.AbsolutePath;
        public string Query => Request.Url.Query;
        public string Method => Request.HttpMethod;

        public AuthToken Token { get; set; }

        public Guid Id { get; set; }

        public APIEndpoint Endpoint { get; set; }

        public APIContext(HttpListenerContext c)
        {
            HTTP = c;
        }

        public BotUser User { get; set; }
    }
}
