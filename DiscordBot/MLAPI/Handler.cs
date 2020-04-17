using Discord;
using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DiscordBot.MLAPI
{
    public class Handler
    {
        public static HttpListener Server { get; set; }
        static Thread listenThread;

#if WINDOWS
        public const string LocalAPIUrl = "http://localhost:8887";
#else
        public const string LocalAPIUrl = "https://ml-api.uk.ms";
#endif


        public static void Start()
        {
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
    
        static void buildEndpoints()
        {
            Endpoints = new Dictionary<string, List<APIEndpoint>>();
            var q = from t in Assembly.GetExecutingAssembly().GetTypes()
                    where t.IsClass  && t.IsSubclassOf(typeof(APIBase))
                    select t;
            foreach(var module in q)
            {
                var methods = module.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach(var func in methods)
                {
                    var method = func.GetCustomAttribute<MethodAttribute>();
                    var path = func.GetCustomAttribute<PathAttribute>();
                    if (method == null || path == null)
                        continue;
                    var api = new APIEndpoint();
                    api.Method = method.Method.Method;
                    api.Path = path;
                    api.Function = func;
                    api.Module = module;
                    var prec = api.Function.GetCustomAttributes<APIPrecondition>().ToList();
                    var parentPrec = module.GetCustomAttributes<APIPrecondition>();
                    if (parentPrec != null && parentPrec.Count() > 0)
                    {
                        foreach (var p in parentPrec)
                        {
                            var child = prec.FirstOrDefault(x => x.ToString() == p.ToString());
                            if ((child != null && !child.CanChildOverride(p)) || child == null)
                            {
                                prec.Remove(child);
                                prec.Add(p);
                            }
                        }
                    }
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
            }
            foreach (var keypair in Endpoints)
                Program.LogMsg($"Loaded {keypair.Value.Count} {keypair.Key} endpoints", source:"API", sev:LogSeverity.Debug);
        }

        static bool findToken(string t, out BotUser user, out AuthToken token)
        {
            token = null;
            user = null;
            foreach(var u in Program.Users)
            {
                foreach(var a in u.Tokens)
                {
                    if(a.Value == t)
                    {
                        user = u;
                        token = a;
                        return true;
                    }
                }
            }
            return false;
        }

        static APIContext parseContext(HttpListenerContext request)
        {
            APIContext context = new APIContext(request);
            string strToken = null;
            var cookie = request.Request.Cookies[AuthToken.SessionToken];
            strToken ??= cookie?.Value;
            strToken ??= request.Request.QueryString.Get(AuthToken.SessionToken);
            strToken ??= request.Request.Headers.Get($"X-{AuthToken.SessionToken.ToUpper()}");

            if (findToken(strToken, out var user, out var t))
            {
                context.User = user;
                context.Token = t;
            }
            return context;
        }

        static bool isValidConnection(IPEndPoint endpoint)
        {
            if (endpoint.Port != 8887)
                return false;
            var str = endpoint.ToString();
            return str.StartsWith("127.0.0.1") || str.StartsWith("192.168.1");
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
                    if (!isValidConnection(req.Request.LocalEndPoint))
                    {
                        Program.LogMsg($"Invalid REST connection: {req.Request.LocalEndPoint}: {req.Request.QueryString}", source:"RESTHandler", sev:LogSeverity.Warning);
                        req.Response.StatusCode = 400;
                        req.Response.Close();
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
                    Program.LogMsg(ex.ToString(), source:"REST-New", sev:LogSeverity.Error);
                    exceptions++;
                    if (exceptions > 2)
                    {
                        Program.LogMsg($"Maximum retries met, API handler shutting down", source:"REST-Listener", sev:LogSeverity.Critical);
                        Listening = false;
                        break;
                    }
                }
            }
        }

        static string[] streamableTypes = new string[]
        {
            "text/plain", "application/json"
        };
        static string getLoggable(APIContext context)
        {
            string basic = @$"
================================================
Id: {context.Id}
Method: {context.Method}
Date: {DateTime.Now}
Path: {context.Request.Url.PathAndQuery}
IP: {context.Request.RemoteEndPoint.Address}";
            if (context.User != null)
                basic += $"\r\nUser: {context.User.Id}/{context.User.Name}";
            foreach(var x in context.Request.Headers.AllKeys)
            {
                var header = context.Request.Headers[x];
                basic += $"\r\nh:{x}: {header}";
            }
            if(context.Request.HasEntityBody && streamableTypes.Any(x => context.Request.ContentType.StartsWith(x)))
            {
                string thing = "\r\n>>>>>>\r\n";
                thing += context.Body;
                thing += "\r\n<<<<<<";
                if (thing.Length > 10)
                    basic += thing;
            }
            return basic;
        }

        class RequestLogger
        {
            APIContext context;
            string logFolder;
            string logFile;
            bool logged = false;
            public RequestLogger(APIContext c)
            {
                context = c;
                logFolder = Path.Combine(Program.BASE_PATH, "APILogs");
                logFile = Path.Combine(logFolder, $"{DateTime.Now.ToString("yyyy-MM-dd")}.txt");
                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);
            }
            public void Write(string text)
            {
                File.AppendAllText(logFile, text);
                logged = true;
            }
            public void Append(string text)
            {
                if (!logged)
                    return;
                File.AppendAllText(logFile, text);
            }
        }

        static void handleRequest(APIContext context)
        {
            context.Id = Guid.NewGuid();
            var idSplit = context.Id.ToString().Split('-');
            var logger = new RequestLogger(context);
            Func<ErrorJson, int, string> sendError = (ErrorJson reply, int code) =>
            {
                var str = Newtonsoft.Json.JsonConvert.SerializeObject(reply, Newtonsoft.Json.Formatting.None);
                logger.Append($"\r\nResult: {code}\r\nMore: {str}");
                var bytes = System.Text.Encoding.UTF8.GetBytes(str);
                context.HTTP.Response.StatusCode = code;
                context.HTTP.Response.Close(bytes, true);
                return "";
            };

            if(!context.Path.Contains("/_/"))
                logger.Write(getLoggable(context));

            List<APIEndpoint> endpoints;
            if(!Endpoints.TryGetValue(context.Method, out endpoints))
            {
                logger.Append($"\r\nResult: 400\r\nMore: Method unrecognised ({context.Method})");
                sendError(new ErrorJson("Method is not recognised"), 400);
                return;
            }

            var seeker = new CommandFinder(context, endpoints);
            if(!seeker.Search())
            {
                var list = new List<ErrorItem>();
                foreach (var er in seeker.Errors)
                {
                    list.Add(new ErrorItem(er.Command?.fullInfo(), er.ErrorReason));
                }
                sendError(new ErrorJson(list), 400);
                return;
            }

            var found = seeker.BestFind;
            var commandBase = found.CommandBase;
            if (found.RequiresAuthentication && context.User == null)
            {
                if (context.Method == HttpMethod.Get.Method)
                {
                    commandBase.Context.HTTP.Response.Headers["Location"] = "/login";
                    try
                    {
                        var cookie = new Cookie("Redirect", context.HTTP.Request.Url.PathAndQuery);
                        cookie.Path = "/";
                        cookie.Expires = DateTime.Now.AddSeconds(60);
                        commandBase.Context.HTTP.Response.Cookies.Add(cookie);
                    }
                    catch { }
                    commandBase.Context.HTTP.Response.StatusCode = 307;
                    commandBase.Context.HTTP.Response.Close();
                    logger.Append($"\r\nResult: 307\r\nMore: Authentication required");
                }
                else
                {
                    commandBase.RespondRaw("Authentication is required for this endpoint", HttpStatusCode.Forbidden);
                }
                return;
            }
            commandBase.Context.Endpoint = found.Command;
            try
            {
                commandBase.BeforeExecute();
            }
            catch (Attributes.HaltExecutionException ex)
            {
                Program.LogMsg(ex, $"{context.Id}");
                commandBase.ResponseHalted(ex);
                logger.Append($"\r\nResult: 500\r\nMore: {ex.Message}");
                return;
            }
            try
            {
                found.Command.Function.Invoke(commandBase, found.Arguments.ToArray());
                logger.Append($"\r\nResult: {commandBase.StatusSent}");
            }
            catch (Exception ex)
            {
                try
                {
                    sendError(new ErrorJson(ex.Message), 500);
                } catch { }
                Program.LogMsg(ex, "Handler");
            }
            commandBase.AfterExecute();
        }
    }
}
