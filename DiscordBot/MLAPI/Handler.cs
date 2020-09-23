﻿using Discord;
using DiscordBot.Classes;
using DiscordBot.MLAPI.Exceptions;
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
            return str.StartsWith("127.0.0.1") || str.StartsWith("192.168.");
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
            Console.WriteLine($"{context.Id}: {context.Request.RemoteEndPoint} {context.Request.Url}");
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

            if(!context.Path.Contains("/_/"))
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
                Program.LogMsg(ex, $"{context.Id}");
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
                Program.LogMsg(ex, "Handler");
            } catch (Exception ex)
            {
                try
                {
                    sendError(new ErrorJson(ex.Message), 500);
                }
                catch { }
                Program.LogMsg(ex, "ExHandler");
            }
            commandBase.AfterExecute();
            Console.WriteLine($"{context.Id}: Returned {commandBase.StatusSent}");
        }
    }
}
