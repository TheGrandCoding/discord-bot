﻿using Discord.Commands;
using DiscordBot.Classes;
using HttpMultipartParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace DiscordBot.MLAPI
{
    public class APIContext
    {
        public HttpListenerContext HTTP { get; set; }
        public HttpListenerRequest Request => HTTP.Request;
        public string Path => Request.Url.AbsolutePath;
        public string Query => Request.Url.Query;
        public string Method => Request.HttpMethod;

        public bool HasPerm(string node) => Perms.Parse(node).HasPerm(this);

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
        public bool WantsJson {  get
            {
                return Request.AcceptTypes.Contains("application/json");
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
        public List<FilePart> Files { get; set; } = new List<FilePart>();

        void parseUrlEncode()
        {
            if (!string.IsNullOrWhiteSpace(Body))
            {
                foreach (var item in Body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var pair = item.Split('=');
                    //postData[pair[0]] = Uri.UnescapeDataString(pair[1].Replace("+", " ");
                    paramaters[pair[0]] = HttpUtility.UrlDecode(pair[1]);
                }
            }
        }
        void parseMultipartForm()
        {
            var parsed = MultipartFormDataParser.Parse(Request.InputStream);
            foreach(var x in parsed.Parameters)
            {
                paramaters[x.Name] = x.Data;
            }
            Files = parsed.Files;
        }

        Dictionary<string, string> paramaters = null;
        string getFromParamaters(string key)
        {
            if (!Request.HasEntityBody)
                return null;
            if(paramaters == null)
            {
                paramaters = new Dictionary<string, string>();
                if(Request.ContentType != null)
                {
                    if (Request.ContentType.StartsWith("multipart/form-data"))
                        parseMultipartForm();
                    else //if (Request.ContentType.StartsWith("application/x-www-form-urlencoded"))
                        parseUrlEncode();
                }
            }
            paramaters.TryGetValue(key, out var s);
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
            r ??= getFromParamaters(key);
            return r;
        }

        public List<string> GetAllKeys()
        {
            var query = new List<string>();
            query.AddRange(Request.QueryString.AllKeys);
            if(paramaters != null)
                query.AddRange(paramaters.Keys);
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
