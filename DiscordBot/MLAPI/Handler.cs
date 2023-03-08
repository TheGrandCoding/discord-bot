using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.MLAPI.Exceptions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
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
        public const string LocalAPIDomain = "cheale14.com";
        public const string LocalAPIUrl = "https://mlapi." + LocalAPIDomain;
#endif
        public static void Start()
        {
            if (Server != null)
                return;
            Server = new HttpListener();
            Server.Prefixes.Add("http://+:8887/");
            Server.Start();
            m_listening = true;
            buildEndpoints();
            Task.Run(async () =>
            {
                try
                {
                    await listenLoop();
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "Handler");
                }
            });
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
            bool failed = false;
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
                    if(!func.ReturnType.IsAssignableTo(typeof(Task)))
                    {
                        Program.LogError($"MLAPI method {module.Name}.{func.Name}({string.Join(", ", func.GetParameters().Select(x => x.Name))}) does not return Task", "Handler");
                        failed = true;
                        continue;
                    }
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
            if(failed)
            {
                Program.LogCritical("Refusing to allow bot start due to malformed endpoints", "API");
                Program.Close(1);
                throw new ArgumentException("Invalid MLAPI functions");
            }
            foreach (var keypair in Endpoints)
                Program.LogDebug($"Loaded {keypair.Value.Count} {keypair.Key} endpoints", "API");
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

        static APIContext parseContext(HttpListenerContext request, IServiceScope scope)
        {
            APIContext context = new APIContext(request, scope);
            string strToken = getAnyValue(request, BotDbAuthSession.CookieName, out var cookie);

            var session = context.BotDB.GetSessionAsync(strToken).Result;
            if (session != null)
            {
                context.User = session.User;
                context.Session = session;
                if(cookie == null)
                    request.Response.AppendCookie(new Cookie(BotDbAuthSession.CookieName, strToken)
                    {
                        Expires = DateTime.Now.AddHours(3)
                    });
            } else
            {
                strToken = getAnyValue(request, "api-key", out cookie);
                var autht = context.BotDB.GetTokenAsync(strToken).Result;
                if(autht != null)
                {
                    context.User = autht.User;
                    context.Token = autht;
                }
            }

            return context;
        }


        public struct holdInfo
        {
            public string token;
            public string ip;
        }
        public static ConcurrentDictionary<ulong, holdInfo> holding = new ConcurrentDictionary<ulong, holdInfo>();

        public static EmbedBuilder getBuilder(BotDbAuthSession s, bool redactIp)
        {
            var embed = new EmbedBuilder();
            if (s == null)
            {
                embed.Title = "[Session removed]";
                return embed;
            }
            embed.Title = "New IP Detected";
            embed.Description = "A login has been attempted by an unknown IP address to the MLAPI website through your account.\r\n" +
                "Please approve the login below.";
            embed.AddField("IP", redactIp ? "||<redacted>||" : s.IP, true);
            embed.AddField("User-Agent", s.UserAgent, true);
            return embed;
        }
        public static async Task<BotDbAuthSession> GenerateNewSession(BotDbUser user, string ip, string userAgent, BotDbContext db, bool? forceApproved = null, bool logoutOthers = false)
        {
            if (string.IsNullOrWhiteSpace(user.Connections.DiscordId))
                forceApproved = true;
            if(forceApproved.HasValue == false)
            {
                forceApproved = ip == "localhost" || 
                                ip == "127.0.0.1" || 
                                ip == "::1" || 
                                Program.BOT_DEBUG || 
                                user.ApprovedIPs.Any(x => x.IP == ip);
            }
            var s = await db.GenerateNewSession(user, ip, userAgent, logoutOthers, forceApproved);
            if (s.Approved)
                return s;

            var embed = getBuilder(s, false);



            var components = new ComponentBuilder();
            components.WithButton("Approve", $"internal:app:{user.Id}:true", ButtonStyle.Success);
            components.WithButton("Deny", $"internal:app:{user.Id}:false", ButtonStyle.Danger);
            holding[user.Id] = new holdInfo() { token = s.Token, ip = ip };
            await user.Connections.Discord.SendMessageAsync(embed: embed.Build(), components: components.Build());

            return s;
        }

        public static async Task SetNewLoginSession(APIContext context, BotDbUser user, bool? forceApproved = null, bool purgeOtherSessions = false)
        {
            // password valid, we need to log them in.
            var session = await GenerateNewSession(user, context.IP, context.Request.UserAgent ?? "none", context.BotDB, forceApproved, purgeOtherSessions);
            // to prevent logging out any other devices, we'll maintain the same token value.
            context.HTTP.Response.Cookies.Add(new Cookie(BotDbAuthSession.CookieName, session.Token, "/")
            {
                Expires = DateTime.Now.AddDays(60)
            });
        }
        static bool isValidConnection(IPEndPoint endpoint)
        {
            if (endpoint.Port != 8887)
                return false;
            var str = endpoint.ToString();
            return str.StartsWith("127.0.0.1") 
                || str.StartsWith("192.168.") 
                || str.StartsWith("172.17.0.1")
                || str.Contains("::1");
        }

        static async Task innerLoop(HttpListenerContext req)
        {
            using var scopedServices = Program.GlobalServices.CreateScope();
            var context = parseContext(req, scopedServices);
            if (req.Request.RawUrl == "/favicon.ico")
            {
                req.Response.StatusCode = 301;
                req.Response.Close();
                return;
            }
            if (context.Headers.TryGetValue("Origin", out var origin))
            {
                if (origin != LocalAPIUrl && (context.Path.StartsWith("/api/tracker") || context.Path.StartsWith("/ocr")))
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
                    if (context.Method == "OPTIONS")
                    {
                        req.Response.StatusCode = 200;
                        req.Response.Close();
                        return;
                    }
                }
            }
            if (!isValidConnection(req.Request.LocalEndPoint))
            {
                Program.LogWarning($"Invalid REST connection: {req.Request.LocalEndPoint}: {req.Request.Url.PathAndQuery}", "RESTHandler");
                req.Response.StatusCode = 400;
                req.Response.Close();
                return;
            }
            if (context.Session != null && context.Session.Approved == false)
            {
                Program.LogDebug($"Unapproved session access: {req.Request.LocalEndPoint}: {req.Request.Url.PathAndQuery}", "REST-" + context.IP);
                var bytes = System.Text.Encoding.UTF8.GetBytes("You are logging in from a new IP address.\r\n" +
                    "You will need to approve this session through the DM the bot has just sent you.");
                req.Response.StatusCode = 200;
                req.Response.Close(bytes, false);
                return;
            }
            try
            {
                await handleRequest(context);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "HandleRequest");
            }
        }

        static async Task listenLoop()
        {
            int exceptions = 0;
            HttpListenerContext req = null;
            var token = Program.GetToken();
            while (Listening)
            {
                try
                {
                    req = await Server.GetContextAsync().WaitAsync(token);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await innerLoop(req);
                            exceptions = 0;
                        } catch(Exception ex)
                        {
                            Program.LogError(ex, "REST-Execute");
                            exceptions++;
                        }
                    });
                }
                catch (OperationCanceledException)
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

        static async Task redirect(RequestLogger logger, APIContext context, string url)
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
                bs.Context.DisposeDB();
                await bs.RespondRedirect(url, current, 307);
            }
            catch { }
            logger.End(HttpStatusCode.TemporaryRedirect, url);
        }

        static async Task handleRequest(APIContext context)
        {
            using var services = Program.GlobalServices.CreateScope();
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
                    await redirect(logger, context, mustRedirectTo[0]);
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
                await commandBase.RespondRaw($"{string.Join(", ", failures.ToString())}", 500);
                return;
            }
            var redirectEx = exceptions.FirstOrDefault(x => x is RedirectException) as RedirectException;
            if(redirectEx != null)
            {
                await redirect(logger, context, redirectEx.URL);
                return;
            }
            commandBase.Context.Endpoint = found.Command;
            try
            {
                await commandBase.BeforeExecute().ConfigureAwait(false);
            }
            catch (RedirectException ex)
            {
                await redirect(logger, context, ex.URL);
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
                commandBase.ResponseHalted(ex).Wait();
                logger.End(HttpStatusCode.InternalServerError, ex.Message);
                return;
            }
            try
            {
                var task = found.Command.Function.Invoke(commandBase, found.Arguments.ToArray()) as Task;
                await task.WaitAsync(Program.GetToken()).ConfigureAwait(false);
                logger.End((HttpStatusCode)commandBase.StatusSent);
            } catch(OperationCanceledException)
            {
                return;
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
            } finally
            {
                try
                {
                    await commandBase.AfterExecute();
                } catch(Exception ex)
                {
                    Program.LogError(ex, "AfterExHandler");
                }
                try
                {
                    commandBase.Context.DisposeDB();
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "AfterExDispose");
                }
            }
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
