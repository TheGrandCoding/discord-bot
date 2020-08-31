﻿using DiscordBot.MLAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI
{
    [RequireAuthentication]
    [RequireApproval]
#if LINUX
    [RequireServerName("ml-api." + Handler.LocalAPIDomain)]
#else
    [RequireServerName("localhost")]
#endif
    public class APIBase
    {
        public APIBase(APIContext context, string path)
        {
            Context = context;
            BaseFolder = path;
        }
        private string BaseFolder { get; set; }
        public APIContext Context { get; set; }

        public bool HasNode(string perm)
        {
            var node = Perms.Parse(perm);
            if(node == null)
            {
                Program.LogMsg($"Attempted checking invalid perm: {Context.Path}, '{perm}'");
                return false;
            }
            return node.HasPerm(Context);
        }

        public enum SidebarType
        {
            None = 0,
            Global = 1,
            Local = 2,
        }

        protected SidebarType Sidebar { get; set; } = SidebarType.None;
        protected List<Classes.HTMLHelpers.HTMLBase> InjectObjects { get; set; } = new List<Classes.HTMLHelpers.HTMLBase>()
        {
            new Classes.HTMLHelpers.Objects.PageLink("stylesheet", "text/css", "/_/css/common.css"),
            new Classes.HTMLHelpers.Objects.RawObject("<script src='/_/js/common.js' type='text/javascript'></script>")
        };

        public int StatusSent { get; set; } = 0;
        protected bool HasResponded => StatusSent != 0;

        public virtual void RespondRaw(string obj, int code = 200)
        {
            StatusSent = code;
            var bytes = System.Text.Encoding.UTF8.GetBytes(obj);
            Context.HTTP.Response.StatusCode = code;
            Context.HTTP.Response.Close(bytes, true);
        }

        public void RespondRaw(string obj, HttpStatusCode code)
            => RespondRaw(obj, (int)code);

        public string LoadRedirectFile(string url, string returnTo = null)
        {
            Context.HTTP.Response.Headers["Location"] = url;
            string file = LoadFile("_redirect.html");
            file = ReplaceMatches(file, new Replacements()
                .Add("url", url)
                .Add("return", returnTo ?? "false"));
            return file;
        }

        protected string LoadFile(string path)
        {
            string proper = Path.Combine(Program.BASE_PATH, "HTTP");
            if (!path.StartsWith("_") && !path.StartsWith("/"))
                proper = Path.Combine(proper, BaseFolder);
            if (path.StartsWith("/"))
                path = path.Substring(1);
            proper = Path.Combine(proper, path);
            return File.ReadAllText(proper, Encoding.UTF8);
        }

        const string matchRegex = "[<$]REPLACE id=['\"](\\S+)['\"]\\/[>$]";
        protected string ReplaceMatches(string input, Replacements replace)
        {
            replace.Add("user", Context.User);
            var REGEX = new Regex(matchRegex);
            var match = REGEX.Match(input);
            while(match != null && match.Success && match.Captures.Count > 0 && match.Groups.Count > 1)
            {
                var key = match.Groups[1].Value;
                if(!replace.TryGetValue(key, out var obj))
                {
                    Program.LogMsg($"Failed to replace '{key}'", Discord.LogSeverity.Warning, $"API:{Context.Path}");
                }
                var value = obj?.ToString() ?? "";
                input = input.Replace(match.Groups[0].Value, value);
                match = REGEX.Match(input);
            }
            return input;
        }

        protected void ReplyFile(string path, int code, Replacements replace = null)
        {
            var f = LoadFile(path);
            string injectedText = "";
            foreach (var x in InjectObjects)
                injectedText += x.ToString();
            injectedText += "<body><REPLACE id='sidebar'/>";
            f = f.Replace("<body>", injectedText);
            replace ??= new Replacements();
            string sN = Sidebar == SidebarType.None ? "" : Sidebar == SidebarType.Global ? "_sidebar.html" : "sidebar.html";
            string sC = "";
            if (!string.IsNullOrWhiteSpace(sN))
                sC = LoadFile(sN);
            replace.Add("sidebar", sC);
            var replaced = ReplaceMatches(f, replace);
            RespondRaw(replaced, code);
        }

        protected void ReplyFile(string path, HttpStatusCode code, Replacements replace = null)
            => ReplyFile(path, (int)code, replace);

        protected void HTTPError(HttpStatusCode code, string title, string message)
        {
            Sidebar = SidebarType.None;
            ReplyFile("_error.html", code, new Replacements()
                .Add("error_code", code)
                .Add("error_message", title)
                .Add("error", message));
        }

        protected string aLink(string url, string display) => $"<a href='{url}'>{display}</a>";

        public virtual void BeforeExecute() { }
        public virtual void ResponseHalted(HaltExecutionException ex) 
        { 
            var er = new ErrorJson(ex.Message);
            if(Context.WantsHTML)
            {
                RespondRaw(er.GetPrettyPage(Context), HttpStatusCode.InternalServerError);
            } else
            {
                var json = JsonConvert.SerializeObject(er);
                RespondRaw(json, HttpStatusCode.InternalServerError);
            }
        }
        public virtual void AfterExecute() { }
    }
}
