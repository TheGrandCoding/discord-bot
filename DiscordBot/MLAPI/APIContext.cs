using Discord.Commands;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        static string[] browser_uas = new string[]
        {
            "Chrome",
            "Safari",
            "Firefox",
            "Edge"
        };
        public bool WantsHTML { get
            {
                return Request.AcceptTypes.Contains("text/html");
            } }

        public string Host {  get
            {
                return Request.Headers["X-Forwarded-Host"] ?? Request.Url.Host;
            } }

        string m_body;
        public string Body { get
            {
                if(m_body == null)
                {
                    using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                        m_body = reader.ReadToEnd();
                }
                return m_body;
            } }

        Dictionary<string, string> postData = null;
        string getFromPostData(string key)
        {
            if (!Request.HasEntityBody)
                return null;
            if(postData == null)
            {
                postData = new Dictionary<string, string>();
                if(!string.IsNullOrWhiteSpace(Body))
                {
                    foreach(var item in Body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var pair = item.Split('=');
                        postData[pair[0]] = Uri.UnescapeDataString(pair[1]);
                    }
                }
            }
            postData.TryGetValue(key, out var s);
            return s;
        }

        string getFromNamedRegex(string key)
        {
            if(Endpoint.Path is PathRegex rgx)
            {
                var keys = rgx.Regex.GetGroupNames();
                if(keys.Contains(key))
                {
                    var val = rgx.RgxMatch.Groups[key];
                    return val.Value;
                }
            }
            return null;
        }

        public string GetQuery(string key)
        {
            var r = Request.QueryString.Get(key);
            r ??= getFromNamedRegex(key);
            r ??= getFromPostData(key);
            return r;
        }

        public List<string> GetAllKeys()
        {
            var query = new List<string>();
            query.AddRange(Request.QueryString.AllKeys);
            if(postData != null)
                query.AddRange(postData.Keys);
            return query;
        }

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
