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
                Program.LogMsg($"Loaded {keypair.Value.Count} {keypair.Key} endpoints", "API", LogSeverity.Debug);
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
            var cookie = request.Request.Cookies["token"];
            strToken ??= cookie?.Value;
            strToken ??= request.Request.QueryString.Get("token");
            strToken ??= request.Request.Headers.Get("X-TOKEN");

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
                        Program.LogMsg($"Invalid REST connection: {req.Request.LocalEndPoint}: {req.Request.QueryString}", "RESTHandler", LogSeverity.Warning);
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
                    Program.LogMsg(ex.ToString(), "REST-New", LogSeverity.Error);
                    exceptions++;
                    if (exceptions > 2)
                    {
                        Program.LogMsg($"Maximum retries met, API handler shutting down", $"REST-Listener", LogSeverity.Critical);
                        Listening = false;
                        break;
                    }
                }
            }
        }

        static string getLoggable(APIContext context)
        {
            return @$"Id: {context.Id}
Date: {DateTime.Now}
Path: {context.Request.Url.PathAndQuery}
IP: {context.Request.RemoteEndPoint.Address}";
        }

        static void handleRequest(APIContext context)
        {
            context.Id = Guid.NewGuid();
            var idSplit = context.Id.ToString().Split('-');
            string logFolder = Path.Combine(Program.BASE_PATH, "APILogs", idSplit[0], idSplit[1], idSplit[2], idSplit[3]);
            string logFile = Path.Combine(logFolder, idSplit[4] + ".txt");
            string linkFile = Path.Combine(Program.BASE_PATH, "APILogs", $"{DateTime.Now.ToString("yyyy-MM-dd")}.log");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);
            Func<ErrorJson, int, string> sendError = (ErrorJson reply, int code) =>
            {
                var str = Newtonsoft.Json.JsonConvert.SerializeObject(reply, Newtonsoft.Json.Formatting.None);
                File.AppendAllText(logFile, $"\r\nResult: {code}\r\nMore: {str}");
                var bytes = System.Text.Encoding.UTF8.GetBytes(str);
                context.HTTP.Response.StatusCode = code;
                context.HTTP.Response.Close(bytes, true);
                return "";
            };

            File.WriteAllText(logFile, getLoggable(context));
            File.AppendAllText(linkFile, $"\r\n{context.Id}");

            List<APIEndpoint> endpoints;
            if(!Endpoints.TryGetValue(context.Method, out endpoints))
            {
                File.AppendAllText(logFile, $"\r\nResult: 400\r\nMore: Method unrecognised ({context.Method})");
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
                    File.AppendAllText(logFile, $"\r\nResult: 307\r\nMore: Authentication required");
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
                File.AppendAllText(logFile, $"\r\nResult: 500\r\nMore: {ex.Message}");
                return;
            }
            found.Command.Function.Invoke(commandBase, found.Arguments.ToArray());
            File.AppendAllText(logFile, $"\r\nResult: {commandBase.StatusSent}");
            commandBase.AfterExecute();
        }
    }
}
