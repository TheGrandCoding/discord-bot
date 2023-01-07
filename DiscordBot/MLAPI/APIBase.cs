using DiscordBot.MLAPI;
using DiscordBot.Permissions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI
{
/*#if LINUX
    [RequireServerName("ml-api." + Handler.LocalAPIDomain, OR = "domain")]
    [RequireServerName("mlapi.cheale14.com", OR = "domain")]
    [RequireServerName("mlapi.cheale14.com:8887", OR = "domain")]
#else
    [RequireServerName("localhost")]
#endif*/
    [RequireServerName(null)]
    public class APIBase
    {
        public APIBase(APIContext context, string path)
        {
            if(path.StartsWith('/') && path != "/")
            {
                Program.LogWarning($"{this.GetType().Name}'s path should not begin with a '/'.", "API");
                path = path[1..];
            }
            Context = context;
            BaseFolder = path;
        }
        private string BaseFolder { get; set; }
        public APIContext Context { get; set; }

        public bool HasNode(string perm) => Context.HasPerm(perm);

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
            new Classes.HTMLHelpers.Objects.Script("/_/js/common.js")
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

        public virtual void RespondJson(Newtonsoft.Json.Linq.JToken json, int code = 200)
        {
            Context.HTTP.Response.AddHeader("Content-Type", "application/json");
            json ??= Newtonsoft.Json.Linq.JValue.CreateNull();
            RespondRaw(json.ToString(Program.BOT_DEBUG ? Formatting.Indented : Formatting.None), code);
        }
        public virtual void RespondError(Classes.APIErrorResponse error, int code = 400)
        {
            Context.HTTP.Response.AddHeader("Content-Type", "application/json");
            RespondRaw(error.Build().ToString(), code);
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
            try
            {
                return File.ReadAllText(proper, Encoding.UTF8);
            } catch( FileNotFoundException)
            {
                throw new InvalidOperationException("The file required for this operation was not found");
            }
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
                    Program.LogWarning($"Failed to replace '{key}'", $"API:{Context.Path}");
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
            f = f.Replace("</head>", injectedText + "</head>");
            f = f.Replace("<body>", "<body><REPLACE id='sidebar'/>");
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

        protected string RelativeLink(MethodInfo method, params object[] args)
            => Handler.RelativeLink(method, args);
        protected string RelativeLink(Action method, params object[] args)
            => RelativeLink(method.Method, args);
        protected string RelativeLink<T>(Action<T> method, params object[] args)
            => RelativeLink(method.Method, args);
        protected string RelativeLink<T1, T2>(Action<T1, T2> method, params object[] args)
            => RelativeLink(method.Method, args);
        protected string RelativeLink<T1, T2, T3>(Action<T1, T2, T3> method, params object[] args)
            => RelativeLink(method.Method, args);
        protected string RelativeLink<T1, T2, T3, T4>(Action<T1, T2, T3, T4> method, params object[] args)
            => RelativeLink(method.Method, args);

        protected string RelativeLink<T>(Func<T> method, params object[] args)
            => RelativeLink(method.Method, args);
        protected string RelativeLink<T1, T2>(Func<T1, T2> method, params object[] args)
            => RelativeLink(method.Method, args);
        protected string RelativeLink<T1, T2, T3>(Func<T1, T2, T3> method, params object[] args)
            => RelativeLink(method.Method, args);
        protected string RelativeLink<T1, T2, T3, T4>(Func<T1, T2, T3, T4> method, params object[] args)
            => RelativeLink(method.Method, args);
    }

    [RequireScope(null)] // scope is determined per-request.
    [RequireApproval]
    [RequireAuthentication]
    public class AuthedAPIBase : APIBase
    {
        public AuthedAPIBase(APIContext context, string path) : base(context, path)
        {
        }
    }
}
