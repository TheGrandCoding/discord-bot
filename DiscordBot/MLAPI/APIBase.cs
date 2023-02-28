using DiscordBot.MLAPI;
using DiscordBot.Permissions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public virtual Task RespondRaw(string obj, int code = 200)
        {
            StatusSent = code;
            var bytes = System.Text.Encoding.UTF8.GetBytes(obj);
            Context.HTTP.Response.StatusCode = code;
            Context.HTTP.Response.Close(bytes, true);
            return Task.CompletedTask;
        }
        public Task RespondRaw(string obj, HttpStatusCode code)
            => RespondRaw(obj, (int)code);
        public virtual async Task RespondRedirect(string url, string returnTo = null, int code = 302)
        {
            Context.HTTP.Response.Headers["Location"] = url;
            using var fs = LoadFile("_redirect.html");

            await respondStreamReplacing(fs, code, new Replacements()
                .Add("url", url)
                .Add("return", returnTo ?? "false"));
        }

        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        class StreamCaptureGroup
        {
            public StreamCaptureGroup(string format)
            {
                Format = format;
                Captured = new();
            }
            public string Format { get; set; }

            public StringBuilder Captured { get; set; }

            public bool Matches(char c, StreamCaptureGroup next, 
                out bool groupFullyDone, 
                out bool nestedDone,
                out bool redoNextOne)
            {
                groupFullyDone = true;
                nestedDone = false;
                redoNextOne = false;
                if (Format == "?")
                {
                    if(next != null)
                    {
                        if (next.Matches(c, null, out nestedDone, out _, out _) && nestedDone)
                            return true;
                    }
                    groupFullyDone = false;
                    Captured.Append(c);
                    return true;
                } else if(Format == " ")
                {
                    groupFullyDone = true;
                    if (c == ' ')
                        Captured.Append(c);
                    else
                        redoNextOne = true;
                    return true;
                }
                else
                {
                    if(Format.Contains(c))
                    {
                        Captured.Append(c);
                        return true;
                    }
                    return false;
                }
            }

            private string GetDebuggerDisplay()
            {
                return $"[{Format}] = {Captured}";
            }
            public int Length
            {
                get
                {
                    if (Format == "?")
                        return 1;
                    return 1 + Format.Length + 1;
                }
            }
        }

        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        class StreamReplacement
        {
            public StreamReplacement(string format, object v)
            {
                Format = format;
                Value = v;
                StringBuilder group = null;
                for(int i = 0; i < Format.Length; i++)
                {
                    var c = Format[i];
                    if(group != null)
                    {
                        if(c == ']')
                        {
                            Groups.Add(new(group.ToString()));
                            group = null;
                        } else
                        {
                            group.Append(c);
                            continue;
                        }
                    }
                    if(c == '?')
                    {
                        Groups.Add(new StreamCaptureGroup("?"));
                    } else if(c == '[')
                    {
                        group = new();
                    }
                }
            }
            public string Format { get; set; }
            public object Value { get; set; }

            public int Pointer { get; set; } = 0;
            public int GroupPointer { get; set; } = 0;

            public List<StreamCaptureGroup> Groups { get; set; } = new();

            string getCaptured()
            {
                var sb = new StringBuilder();
                int gp = 0;
                for(int i = 0; i < Pointer; i++)
                {
                    var c = Format[i];
                    if(c == '[' || c == '?')
                    {
                        var group = Groups[gp];
                        sb.Append(group.Captured);
                        gp++;
                        i += group.Length - 1;
                    } else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }

            public string reset()
            {
                var c = getCaptured();
                Pointer = 0;
                for(int gp = 0; gp < GroupPointer; gp++)
                    Groups[gp].Captured.Clear();
                GroupPointer = 0;
                return c;
            }

            public bool Matches(char c, out string captured)
            {
                captured = null;
tryagain:
                char lookingFor = Format[Pointer];
                if(lookingFor == '[' || lookingFor == '?')
                {
                    if (!Groups[GroupPointer].Matches(c, GroupPointer < (Groups.Count - 1) ? Groups[GroupPointer + 1] : null, out var done, out var nested, out bool retry))
                    {
                        captured = reset();
                        return false;
                    }
                    if (done)
                    {
                        Pointer += Groups[GroupPointer].Length;
                        GroupPointer++;
                        if (nested)
                        {
                            Pointer += Groups[GroupPointer].Length;
                            GroupPointer++;
                        }
                        if (retry)
                            goto tryagain;
                    }
                    return true;
                } else
                {
                    if(lookingFor == c)
                    {
                        Pointer++;
                        return true;
                    } else
                    {
                        captured = reset();
                        return false;
                    }
                }
            }

            private string GetDebuggerDisplay()
            {
                var chr = Format.ElementAtOrDefault(Pointer);
                return $"'{Format}' @ {Pointer}={chr}; seen='{getCaptured()}'";
            }
        }

        async Task respondStreamReplacing(Stream fromStream, int code, Replacements reps, params StreamReplacement[] streamReplacements)
        {
            reps.Add("user", Context.User);
            StatusSent = code;
            Context.HTTP.Response.StatusCode = code;
            await copyStreamReplacing(fromStream, Context.HTTP.Response.OutputStream, reps, streamReplacements);
            Context.HTTP.Response.Close();
        }
        async Task copyStreamReplacing(Stream fromStream, Stream toStream, Replacements reps, params StreamReplacement[] replacements)
        {
            var ls = new List<StreamReplacement>(replacements);
            ls.Add(new($"[$<]REPLACE id=['\"]?['\"][ ]/[>$]", reps));
            await copyStreamReplacing(fromStream, toStream, ls.ToArray());
        }
        bool equalChar(char lookingFor, char next)
        {
            if (lookingFor == next) return true;
            if (lookingFor == '<') return next == '$';
            if(lookingFor == '\'') return next == '"';
            if (lookingFor == ' ') return true;
            return false;
        }
        async Task copyStreamReplacing(Stream fromStream, Stream toStream, params StreamReplacement[] replacements)
        {
            char[] buffer = new char[128];
            char[] writebuf = new char[1];
            int writecount = 1;
            int bufferPtr = buffer.Length;

            //var DEBUG = new StringBuilder();

            using(StreamWriter writer = new StreamWriter(toStream))
            using (StreamReader reader = new StreamReader(fromStream))
            {
                async Task<char?> getNext() {
                    if(bufferPtr >= buffer.Length)
                    {
                        bufferPtr = 0;
                        int count = await reader.ReadAsync(buffer, 0, buffer.Length);
                        if (count == 0) return null;
                        // null out buffer
                        for (int i = count; i < buffer.Length; i++)
                            buffer[i] = '\0';
                    }
                    return buffer[bufferPtr++];
                }
                char? next = null;
                int i = 0;
                string towrite = null;
                bool skipThisChar = false;
                int length = -1;
                do
                {
                    i++;
                    next = await getNext();
                    if (!next.HasValue || next.Value == '\0') break;

                    for(int idx = 0; idx < replacements.Length; idx++)
                    {
                        var rep = replacements[idx];
                        if (rep.Matches(next.Value, out var thiswrite))
                        {
                            skipThisChar = true;
                        }
                        if (thiswrite != null && thiswrite.Length > length)
                        {
                            towrite = thiswrite;
                            length = thiswrite.Length;
                        }
                        if (rep.Pointer >= rep.Format.Length)
                        {
                            skipThisChar = true;
                            if (rep.Value is Replacements r)
                            {
                                var id = rep.Groups.FirstOrDefault(x => x.Format == "?");
                                if (r.TryGetValue(id.Captured.ToString(), out var o))
                                    await writer.WriteAsync(o?.ToString() ?? "");
                            }
                            else
                            {
                                await writer.WriteAsync(rep.Value?.ToString() ?? "");
                            }
                            rep.reset();
                            break;
                        }
                    }
                    if (!skipThisChar)
                    {
                        if (towrite != null && towrite.Length > 0)
                            await writer.WriteAsync(towrite);
                        await writer.WriteAsync(next.Value);
                    }
                    skipThisChar = false;
                    length = -1;
                    towrite = null;
                } while (next.HasValue);
            }
            Console.WriteLine("DONE");
        }

        public virtual Task RespondJson(Newtonsoft.Json.Linq.JToken json, int code = 200)
        {
            Context.HTTP.Response.AddHeader("Content-Type", "application/json");
            json ??= Newtonsoft.Json.Linq.JValue.CreateNull();
            return RespondRaw(json.ToString(Program.BOT_DEBUG ? Formatting.Indented : Formatting.None), code);
        }
        public virtual Task RespondError(Classes.APIErrorResponse error, int code = 400)
        {
            Context.HTTP.Response.AddHeader("Content-Type", "application/json");
            return RespondRaw(error.Build().ToString(), code);
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
                Context.HTTP.Response.ContentType = "text/html";
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

        protected Task ReplyFile(string path, HttpStatusCode code, Replacements replace = null)
            => ReplyFile(path, (int)code, replace);

        protected async Task HTTPError(HttpStatusCode code, string title, string message)
        {
            Sidebar = SidebarType.None;
            await ReplyFile("_error.html", code, new Replacements()
                .Add("error_code", code)
                .Add("error_message", title)
                .Add("error", message));
        }

        protected string aLink(string url, string display) => $"<a href='{url}'>{display}</a>";

        public virtual Task BeforeExecute() => Task.CompletedTask;
        public virtual Task ResponseHalted(HaltExecutionException ex) 
        { 
            var er = new ErrorJson(ex.Message);
            if(Context.WantsHTML)
            {
                return RespondRaw(er.GetPrettyPage(Context), HttpStatusCode.InternalServerError);
            } else
            {
                var json = JsonConvert.SerializeObject(er);
                return RespondRaw(json, HttpStatusCode.InternalServerError);
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
