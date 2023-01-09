using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Permissions;
using DiscordBot.Services;
using HttpMultipartParser;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
        public IReadOnlyDictionary<string, string> Headers { get; }

        static PermissionsService pService;

        public string IP => Program.GetIP(Request.Headers["X-Forwarded-For"], Request.RemoteEndPoint.Address);

        public bool isInNetwork => IP.StartsWith("192.168.1.");

        public bool IsBehindFirewall => Request.Headers["Via"]?.Contains("smoothwall") ?? false;

        public bool HasPerm(string perm)
        {
            pService ??= Program.Services.GetRequiredService<PermissionsService>();
            var node = pService.FindNode(perm);
            if (node == null)
            {
                Program.LogInfo($"Attempted checking invalid perm: {Path}, '{perm}'", IP);
                return false;
            }
            return PermChecker.HasPerm(this, node);
        }

        static string[] browser_uas = new string[]
        {
            "Chrome",
            "Safari",
            "Firefox",
            "Edge"
        };
        public bool WantsHTML { get
            {
                return (Request.AcceptTypes ?? new string[] { }).Contains("text/html");
            } }
        public bool WantsJson {  get
            {
                return (Request.AcceptTypes ?? new string[] { }).Contains("application/json");
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
            Files = parsed.Files.ToList();
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
                    else if(!Request.ContentType.StartsWith("application/json")) //if (Request.ContentType.StartsWith("application/x-www-form-urlencoded"))
                        parseUrlEncode();
                }
            }
            paramaters.TryGetValue(key, out var s);
            return s;
        }

        string getFromNamedRegex(string key)
        {
            var rgx = new Regex(Endpoint.GetRegexPattern());
            var keys = rgx.GetGroupNames();
            if(keys.Contains(key))
            {
                var val = rgx.Match(Request.Url.AbsolutePath).Groups[key];
                return val.Value;
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

        public AuthSession Session { get; set; }

        public AuthToken Token { get; set; }

        public Guid Id { get; set; }

        public APIEndpoint Endpoint { get; set; }

        public APIContext(HttpListenerContext c)
        {
            HTTP = c;
            var dict = new Dictionary<string, string>();
            foreach(string key in c.Request.Headers.Keys)
            {
                var value = c.Request.Headers[key];
                dict[key] = value;
            }
            Headers = dict.ToImmutableDictionary();
        }

        public BotUser User { get; set; }
    
        
        /// <summary>
        /// Generates a new session from this context
        /// </summary>
        /// <returns></returns>
        public AuthSession GenerateNewSession(BotUser user, bool? forceApproved = null)
        {
            return Handler.GenerateNewSession(user, IP, this.Request.UserAgent ?? "none", forceApproved).Result;
        }
    
    }
}
