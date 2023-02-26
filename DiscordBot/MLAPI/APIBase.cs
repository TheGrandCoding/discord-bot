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
using System.Threading.Tasks;

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
        public void RespondRaw(string obj, HttpStatusCode code)
            => RespondRaw(obj, (int)code);
        public virtual async Task RespondRedirect(string url, string returnTo = null, int code = 302)
        {
            Context.HTTP.Response.Headers["Location"] = url;
            using var fs = LoadFile("_redirect.html");

            await respondStreamReplacing(fs, code, new Replacements()
                .Add("url", url)
                .Add("return", returnTo ?? "false"));
        }

        struct StreamReplacement
        {
            public StreamReplacement(string format, object v)
            {
                Format = format;
                Value = v;
            }
            public string Format { get; set; }
            public StringBuilder ReadKey { get; set; } = new();
            public object Value { get; set; }

            public int Pointer { get; set; } = 0;
        }

        const string replaceText = "<REPLACE id='?' />";

        async Task respondStreamReplacing(Stream fromStream, int code, Replacements reps, params StreamReplacement[] streamReplacements)
        {
            reps.Add("user", Context.User);
            StatusSent = code;
            await copyStreamReplacing(fromStream, Context.HTTP.Response.OutputStream, reps, streamReplacements);
            Context.HTTP.Response.StatusCode = code;
            Context.HTTP.Response.Close();
        }
        async Task copyStreamReplacing(Stream fromStream, Stream toStream, Replacements reps, params StreamReplacement[] replacements)
        {
            var ls = new List<StreamReplacement>(replacements);
            foreach ((var key, var obj) in reps.objs)
            {
                ls.Add(new()
                {
                    Format = $"<REPLACE id='{key}' />",
                    Value = obj
                });
            }
            await copyStreamReplacing(fromStream, toStream, ls.ToArray());
        }
        bool equalChar(char lookingFor, char next)
        {
            if (lookingFor == next) return true;
            if (lookingFor == '<') return next == '$';
            if(lookingFor == '\'') return next == '"';
            return false;
        }
        async Task copyStreamReplacing(Stream fromStream, Stream toStream, params StreamReplacement[] replacements)
        {
            char[] buffer = new char[32];
            char[] writebuf = new char[1];
            int writecount = 1;
            int bufferPtr = 32;

            using (StreamReader reader = new StreamReader(fromStream))
            {
                async Task<char?> getNext() {
                    if(bufferPtr >= buffer.Length)
                    {
                        bufferPtr = 0;
                        int count = await reader.ReadAsync(buffer, 0, buffer.Length);
                        if (count == 0) return null;
                    }
                    return buffer[bufferPtr++];
                }
                async Task write(string s)
                {
                    var b = Encoding.UTF8.GetBytes(s);
                    await toStream.WriteAsync(b, 0, b.Length);
                }
                char? next = null;
                do
                {
                    next = await getNext();

                    bool skipThisChar = false;
                    for(int idx = 0; idx < replacements.Length; idx++)
                    {
                        var rep = replacements[idx];
                        char lookingFor = rep.Format[rep.Pointer];
                        if(lookingFor == '?' && rep.ReadKey != null)
                        {
                            if(equalChar('\'', next.Value))
                            {
                                rep.Pointer++;
                                var key = rep.ReadKey.ToString();
                                if(rep.Value is Replacements r && r.TryGetValue(key, out var o))
                                    await write(o?.ToString() ?? "");
                            } else
                            {
                                skipThisChar = true;
                                rep.ReadKey.Append(next);
                                continue;
                            }
                        }
                        if(equalChar(lookingFor, next.Value))
                        {
                            rep.Pointer++;
                            skipThisChar = true;
                        } else
                        {
                            if (rep.Pointer > 0)
                            {
                                await write(rep.Format.Substring(0, rep.Pointer));
                                rep.Pointer = 0;
                            }
                        }
                        if (rep.Pointer >= rep.Format.Length)
                        {
                            if(rep.Value is not Replacements)
                                await write(rep.Value?.ToString() ?? "");
                            rep.Pointer = 0;
                        }

                    }
                    if (skipThisChar) continue;

                    writebuf[0] = next.Value;
                    var bytes = Encoding.UTF8.GetBytes(writebuf, 0, writecount);
                    await toStream.WriteAsync(bytes, 0, bytes.Length);
                } while (next.HasValue);
            }
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

        protected FileStream LoadFile(string path)
        {
            string proper = Path.Combine(Program.BASE_PATH, "HTTP");
            if (!path.StartsWith("_") && !path.StartsWith("/"))
                proper = Path.Combine(proper, BaseFolder);
            if (path.StartsWith("/"))
                path = path.Substring(1);
            proper = Path.Combine(proper, path);
            try
            {
                return File.OpenRead(proper);
            } catch( FileNotFoundException)
            {
                throw new InvalidOperationException("The file required for this operation was not found");
            }
        }

        protected async Task ReplyFile(string path, int code, Replacements replace = null)
        {
            if(path.EndsWith(".html"))
            {
                using var fs = LoadFile(path);
                string injectedText = "";
                foreach (var x in InjectObjects)
                    injectedText += x.ToString();

                string sN = Sidebar == SidebarType.None ? "" : Sidebar == SidebarType.Global ? "_sidebar.html" : "sidebar.html";
                string sidebar = null;
                if (!string.IsNullOrWhiteSpace(sN))
                {
                    using (var sidefs = LoadFile(sN))
                    using (StreamReader sr = new StreamReader(sidefs))
                        sidebar = sr.ReadToEnd();
                }
                if(sidebar != null)
                {
                    await respondStreamReplacing(fs, code,
                        replace ?? new(),
                        new StreamReplacement("</head>", injectedText + "</head>"),
                        new StreamReplacement("<body>", $"<body>{sidebar}")
                        );
                } else
                {
                    await respondStreamReplacing(fs, code,
                        replace ?? new(),
                        new StreamReplacement("</head>", injectedText + "</head>")
                        );
                }
            } else
            {
                using(var fs = LoadFile(path))
                {
                    await ReplyStream(fs, code);
                }
            }
        }

        protected async Task ReplyStream(Stream stream, int code)
        {
            StatusSent = code;
            Context.HTTP.Response.StatusCode = code;
            await stream.CopyToAsync(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
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

        public virtual Task BeforeExecute() => Task.CompletedTask;
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
        public virtual Task AfterExecute() => Task.CompletedTask;

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
