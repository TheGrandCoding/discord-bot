using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.MLAPI.Exceptions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI
{
    public class Handler
    {
        public static HttpListener Server { get; set; }
        static Thread listenThread;

#if WINDOWS
        public const string LocalAPIDomain = "localhost";
        public const string LocalAPIUrl = "http://localhost:8887";
#else
        public const string LocalAPIDomain = "uk.ms";
        public const string LocalAPIUrl = "https://ml-api." + LocalAPIDomain;
#endif
        public static void Start()
        {
            if (Server != null)
                return;
            Server = new HttpListener();
            Server.Prefixes.Add("http://+:8887/");
            Server.Start();
            m_listening = true;
            listenThread = new Thread(listenLoop);
            listenThread.Start();
            buildEndpoints();
        }

        static bool m_listening;
        public static bool Listening 
        { 
            get
            {
                return m_listening;
            } 
            set
            {
                m_listening = value;
                if (Server == null && value)
                    Start();
                if (value == false)
                {
                    try
                    {
                        Server.Stop();
                    } catch { }
                    Server = null;
                    try
                    {
                        listenThread.Abort();
                    }
                    catch { }
                    listenThread = null;
                }
            }
        } 

        public static Dictionary<string, List<APIEndpoint>> Endpoints = new Dictionary<string, List<APIEndpoint>>();
        public static List<APIModule> Modules { get; set; } = new List<APIModule>();

        static void buildEndpoints()
        {
            Endpoints = new Dictionary<string, List<APIEndpoint>>();
            var q = from t in Assembly.GetExecutingAssembly().GetTypes()
                    where t.IsClass  && t.IsSubclassOf(typeof(APIBase))
                    select t;
            foreach(var module in q)
            {
                var mod = new APIModule();
                mod.Name = module.GetCustomAttribute<NameAttribute>()?.Text ?? module.Name;
                mod.Summary = module.GetCustomAttribute<SummaryAttribute>()?.Text ?? null;
                mod.Type = module;
                foreach (var prec in module.GetCustomAttributes<APIPrecondition>())
                    mod.Preconditions.Add(prec);
                var methods = module.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach(var func in methods)
                {
                    var method = func.GetCustomAttribute<MethodAttribute>();
                    var path = func.GetCustomAttribute<PathAttribute>();
                    if (method == null || path == null)
                        continue;
                    var name = func.GetCustomAttribute<NameAttribute>();
                    var api = new APIEndpoint(method.Method.Method, path);
                    var regexs = func.GetCustomAttributes<RegexAttribute>();
                    foreach (var keypair in regexs)
                        api.Regexs[keypair.Name] = keypair.Regex;
                    api.Name = name?.Text ?? func.Name;
                    api.Summary = func.GetCustomAttribute<SummaryAttribute>()?.Text ?? null;
                    api.Function = func;
                    api.Module = mod;
                    mod.Endpoints.Add(api);
                    var prec = api.Function.GetCustomAttributes<APIPrecondition>().ToList();
                    api.Preconditions = prec.ToArray();
                    if (Endpoints.ContainsKey(api.Method))
                    {
                        Endpoints[api.Method].Add(api);
                    }
                    else
                    {
                        Endpoints.Add(api.Method, new List<APIEndpoint>()
                        {
                            api
                        });
                    }
                }
                Modules.Add(mod);
            }
            foreach (var keypair in Endpoints)
                Program.LogDebug($"Loaded {keypair.Value.Count} {keypair.Key} endpoints", "API");
        }

        public static bool findSession(string t, out BotUser user, out AuthSession session)
        {
            session = null;
            user = null;
            foreach(var u in Program.Users)
            {
                foreach(var a in u.Sessions)
                {
                    if(a.Token == t)
                    {
                        user = u;
                        session = a;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool findToken(string t, out BotUser user, out AuthToken token)
        {
            token = null;
            user = null;
            foreach (var u in Program.Users)
            {
                foreach (var a in u.Tokens)
                {
                    if (a.Value == t)
                    {
                        user = u;
                        token = a;
                        return true;
                    }
                }
            }
            return false;
        }

        static string getAnyValue(HttpListenerContext request, string key, out Cookie cookie)
        {
            string strToken = null;
            cookie = request.Request.Cookies[key];
            strToken ??= cookie?.Value;
            strToken ??= request.Request.QueryString.Get(key);
            strToken ??= request.Request.Headers.Get($"X-{key}");
            return strToken;
        }

        static APIContext parseContext(HttpListenerContext request)
        {
            APIContext context = new APIContext(request);
            string strToken = getAnyValue(request, AuthSession.CookieName, out var cookie);

            if (findSession(strToken, out var user, out var s))
            {
                context.User = user;
                context.Session = s;
                if(cookie == null)
                    request.Response.AppendCookie(new Cookie(AuthSession.CookieName, strToken)
                    {
                        Expires = DateTime.Now.AddHours(3)
                    });
            } else
            {
                strToken = getAnyValue(request, "api-key", out cookie);
                if(findToken(strToken, out user, out var t))
                {
                    context.User = user;
                    context.Token = t;
                }
            }

            return context;
        }

        public static async Task<AuthSession> GenerateNewSession(BotUser user, string ip, string userAgent, bool? forceApproved = null)
        {
            var s = new AuthSession(ip, userAgent, forceApproved ?? (user?.ApprovedIPs ?? new List<string>()).Contains(ip));
            if (ip == "localhost" || ip == "127.0.0.1" || ip == "::1")
                s.Approved = true;
            user.Sessions.Add(s);
            Program.Save();
            if (s.Approved)
                return s;

            EmbedBuilder getBuilder(bool redactIp)
            {
                var embed = new EmbedBuilder();
                embed.Title = "New IP Detected";
                embed.Description = "A login has been attempted by an unknown IP address to the MLAPI website through your account.\r\n" +
                    "Please approve the login below.";
                embed.AddField("IP", redactIp ? "||<redacted>||" : ip, true);
                embed.AddField("User-Agent", userAgent, true);
                return embed;
            }
            var embed = getBuilder(false);



            var components = new ComponentBuilder();
            components.WithButton("Approve", "true", ButtonStyle.Success);
            components.WithButton("Deny", "false", ButtonStyle.Danger);

            var msg = await user.FirstValidUser.SendMessageAsync(embed: embed.Build(), components: components.Build());

            var service = Program.Services.GetRequiredService<MessageComponentService>();
            service.Register(msg, async x =>
            {
                var result = bool.Parse(x.ComponentId);
                if (result)
                {
                    s.Approved = true;
                    user.ApprovedIPs.Add(ip);
                }
                else
                {
                    user.Sessions.Remove(s);
                }
                Program.Save();
                await x.Interaction.UpdateAsync(m =>
                {
                    m.Embeds = new[] { getBuilder(result).Build() };
                    m.Content = "This login has been " + (result ? "approved\r\nThe IP address has been whitelisted, and now redacted." : "rejected");
                });
            }, doSave: false);

            return s;
        }

        static bool isValidConnection(IPEndPoint endpoint)
        {
            if (endpoint.Port != 8887)
                return false;
            var str = endpoint.ToString();
            return str.StartsWith("127.0.0.1") || str.StartsWith("192.168.") || str.Contains("::1");
        }

        static void listenLoop()
        {
            int exceptions = 0;
            HttpListenerContext req = null;
            while (Listening)
            {
                try
                {
                    req = Server.GetContext();
                    var context = parseContext(req);
                    if (req.Request.RawUrl == "/favicon.ico")
                    {
                        req.Response.StatusCode = 301;
                        req.Response.Close();
                        continue;
                    }
                    if(context.Headers.TryGetValue("Origin", out var origin))
                    {
                        if (origin != LocalAPIUrl && context.Path.StartsWith("/api/tracker"))
                        {
                            foreach (string key in req.Request.Headers.Keys)
                            {
                                var val = req.Request.Headers[key];
                                if (key.StartsWith("Access-Control-"))
                                {
                                    req.Response.AddHeader(key.Replace("Request", "Allow"), val);
                                }
                            }
                            req.Response.AddHeader("Access-Control-Allow-Origin", "*");
                            if(context.Method == "OPTIONS")
                            {
                                req.Response.StatusCode = 200;
                                req.Response.Close();
                                continue;
                            }
                        }
                    }
                    if (!isValidConnection(req.Request.LocalEndPoint))
                    {
                        Program.LogWarning($"Invalid REST connection: {req.Request.LocalEndPoint}: {req.Request.Url.PathAndQuery}", "RESTHandler");
                        req.Response.StatusCode = 400;
                        req.Response.Close();
                        continue;
                    }
                    if(context.Session != null && context.Session.Approved == false)
                    {
                        Program.LogDebug($"Unapproved session access: {req.Request.LocalEndPoint}: {req.Request.Url.PathAndQuery}", "REST-" + context.IP);
                        var bytes = System.Text.Encoding.UTF8.GetBytes("You are logging in from a new IP address.\r\n" +
                            "You will need to approve this session through the DM the bot has just sent you.");
                        req.Response.StatusCode = 200;
                        req.Response.Close(bytes, false);
                        continue;
                    }
                    handleRequest(context);
                    exceptions = 0;
                }
                catch (ThreadAbortException)
                {
                    // thread is closing, already logged - no need to catch again
                    break;
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "REST-New");
                    exceptions++;
                    if (exceptions > 2)
                    {
                        Program.LogCritical($"Maximum retries met, API handler shutting down", "REST-Listener");
                        Listening = false;
                        break;
                    }
                }
            }
        }

        public static object logLock = new object();

        class RequestLogger
        {
            APIContext context;
            string logFolder;
            string logFile;
            bool logged = false;
            public APILogEntry Entry { get; set; }
            public RequestLogger(APIContext c)
            {
                Entry = new APILogEntry(c);
                context = c;
                logFolder = Path.Combine(Program.BASE_PATH, "APILogs");
                logFile = Path.Combine(logFolder, $"{DateTime.Now.ToString("yyyy-MM-dd")}.txt");
                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);
            }
            public void End(HttpStatusCode code, params string[] more) 
            {
                Entry.End(code, more);
                if (!logged)
                    return;
                lock (logLock)
                {
                    using var fs = File.Open(logFile, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    using var reader = new StreamReader(fs);
                    using var writer = new StreamWriter(fs);
                    string line;
                    string desired = $"Id: {Entry.Id}";
                    do
                    {
                        if (reader.EndOfStream)
                            return;
                        line = reader.ReadLine();
                    } while (line.Trim() != desired);
                    do
                    {
                        line = reader.ReadLine();
                    } while (!line.StartsWith("===") && !reader.EndOfStream);
                    writer.WriteLine("Result: " + ((int)code).ToString());
                    foreach (var x in more)
                        writer.WriteLine("More: " + x);
                }
            }
            public void Write()
            {
                logged = true;
                lock(logLock)
                {
                    File.AppendAllText(logFile, Entry.ToString());
                }
            }
        }

        static void redirect(RequestLogger logger, APIContext context, string url)
        {
            context.HTTP.Response.Headers["Location"] = url;
            try
            {
                var bs = new APIBase(context, "/");
                string current = context.Request.Url.PathAndQuery;
                if (context.User != null)
                    context.User.RedirectUrl = current;
                context.HTTP.Response.AppendCookie(new Cookie("redirect", current)
                {
                    Expires = DateTime.Now.AddDays(1),
                    Path = "/"
                });
                bs.RespondRaw(bs.LoadRedirectFile(url, current), HttpStatusCode.TemporaryRedirect);
            }
            catch { }
            logger.End(HttpStatusCode.TemporaryRedirect, url);
        }

        static void handleRequest(APIContext context)
        {
            context.Id = Guid.NewGuid();
            Console.WriteLine($"{(context?.Id.ToString() ?? "null")}: {(context.Request?.RemoteEndPoint?.ToString() ?? "null")} {(context?.Request?.Url.ToString() ?? "null")}");
            var idSplit = context.Id.ToString().Split('-');
            var logger = new RequestLogger(context);
            Func<ErrorJson, int, string> sendError = (ErrorJson reply, int code) =>
            {
                var str = Newtonsoft.Json.JsonConvert.SerializeObject(reply, Newtonsoft.Json.Formatting.None);
                logger.End((HttpStatusCode)code, str);
                if(context.WantsHTML)
                {
                    str = reply.GetPrettyPage(context);
                } else if (!context.WantsJson)
                {
                    str = reply.GetSimpleText();
                }
                var bytes = System.Text.Encoding.UTF8.GetBytes(str);
                context.HTTP.Response.StatusCode = code;
                context.HTTP.Response.Close(bytes, true);
                return "";
            };

            if(!(context.Path.Contains("/_/") || context.Path.StartsWith("/api/tracker/")))
                logger.Write();

            List<APIEndpoint> endpoints;
            if(!Endpoints.TryGetValue(context.Method, out endpoints))
            {
                logger.End((HttpStatusCode)400, $"Method unrecognised ({context.Method})");
                sendError(new ErrorJson("Method is not recognised"), 400);
                return;
            }

            var seeker = new CommandFinder(context, endpoints);
            if(!seeker.Search())
            {
                var list = new List<ErrorItem>();
                var mustRedirectTo = new List<string>();
                foreach (var er in seeker.Errors)
                {
                    var redirects = er.Exceptions.Where(x => x is RedirectException).Select(x => x as RedirectException);
                    foreach (var thing in redirects)
                        if (!mustRedirectTo.Contains(thing.URL))
                            mustRedirectTo.Add(thing.URL);
                    list.Add(new ErrorItem(er.Command, er.ErrorReason));
                }
                if(mustRedirectTo.Count == 1)
                { // every error is solely about not being logged in
                    redirect(logger, context, mustRedirectTo[0]);
                    return;
                }
                sendError(new ErrorJson(list), 400);
                return;
            }

            var found = seeker.BestFind;
            var commandBase = found.CommandBase;
            var exceptions = found.Exceptions;
            var failures = exceptions.Where(x => x.CompleteFailure).ToList();
            if(failures.Count > 0)
            {
                commandBase.RespondRaw($"{string.Join(", ", failures.ToString())}", 500);
                return;
            }
            var redirectEx = exceptions.FirstOrDefault(x => x is RedirectException) as RedirectException;
            if(redirectEx != null)
            {
                redirect(logger, context, redirectEx.URL);
                return;
            }
            commandBase.Context.Endpoint = found.Command;
            try
            {
                commandBase.BeforeExecute();
            }
            catch (RedirectException ex)
            {
                redirect(logger, context, ex.URL);
                logger.End(HttpStatusCode.TemporaryRedirect, ex.Message);
                return;
            }
            catch(ReqHandledException ex)
            {
                logger.End((HttpStatusCode)commandBase.StatusSent);
                return;
            }
            catch (HaltExecutionException ex)
            {
                Program.LogError(ex, $"{context.Id}");
                commandBase.ResponseHalted(ex);
                logger.End(HttpStatusCode.InternalServerError, ex.Message);
                return;
            }
            try
            {
                found.Command.Function.Invoke(commandBase, found.Arguments.ToArray());
                logger.End((HttpStatusCode)commandBase.StatusSent);
            }
            catch (TargetInvocationException outer)
            {
                Exception ex = outer.InnerException;
                try
                {
                    sendError(new ErrorJson(ex.Message), 500);
                } catch { }
                Program.LogError(ex, "Handler");
            } catch (Exception ex)
            {
                try
                {
                    sendError(new ErrorJson(ex.Message), 500);
                }
                catch { }
                Program.LogError(ex, "ExHandler");
            }
            commandBase.AfterExecute();
            Console.WriteLine($"{context.Id}: Returned {commandBase.StatusSent}");
        }

        static APIEndpoint find(MethodInfo method)
        {
            foreach (var verbKp in Handler.Endpoints)
            {
                foreach (var ep in verbKp.Value)
                {
                    if (ep.Function.Equals(method))
                        return ep;
                }
            }
            return null;
        }
        public static string RelativeLink(MethodInfo method, params object[] args)
        {
            APIEndpoint endpoint = find(method);
            var path = endpoint.GetFormattablePath();
            return string.Format(path, args);

        }

    }
}
