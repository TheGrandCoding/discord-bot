using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public abstract class Service : IComparable<Service>
    {
        public virtual string Name => this.GetType().Name;
        /// <summary>
        /// A critical service that errors during an event causes bot to fail and exit.
        /// </summary>
        public virtual bool IsCritical => false;
        public virtual bool IsEnabled => true;
        public bool HasFailed { get; protected set; }
        
        public virtual int Priority => 0;

        public virtual int DefaultTimeout => 10_000;
        public virtual int CloseTimeout => DefaultTimeout / 2;

        public static DateTime? lastDailyTick = null;

        protected CancellationToken CancelToken { get; private set; }

        public virtual void OnReady() { }
        public virtual void OnLoaded() { }
        public virtual void OnSave() { }
        public virtual void OnDailyTick() { }
        public virtual void OnClose() { }

        public int CompareTo([AllowNull] Service other)
            => new serviceComparer().Compare(this, other);

        protected static List<Service> zza_services;

        static Dictionary<string, bool> doneFunctions = new Dictionary<string, bool>();
        static void sendFunction(object obj)
        {
            if(!(obj is string name))
            {
                Program.LogMsg($"sendFunction recieved non-string input: {obj.GetType()}", LogSeverity.Warning);
                return;
            }
            if (doneFunctions.TryGetValue(name, out var d) && d)
                return;
            doneFunctions[name] = true;
            try
            {
                var stop = new Stopwatch();
                foreach (var srv in zza_services)
                {
                    if (!srv.IsEnabled || srv.HasFailed)
                        continue;
                    var method = srv.GetType().GetMethod(name);
                    try
                    {
                        if (method.IsOverride())
                        {
                            Program.LogMsg($"Sending {name}", LogSeverity.Debug, srv.Name);
                            //var source = new CancellationTokenSource();
                            //srv.CancelToken = source.Token;
                            var task = new Task(() =>
                            {
                                try
                                {
                                    method.Invoke(srv, null);
                                }
                                catch (TargetInvocationException tie)
                                {
                                    var ex = tie.InnerException;
                                    Program.LogMsg(ex, srv.Name);
                                    srv.HasFailed = true;
                                    if (srv.IsCritical)
                                        throw ex;
                                }
                            });
                            stop.Restart();
                            int timeout = name == "OnClose" ? srv.CloseTimeout : srv.DefaultTimeout;
                            task.Start();
                            bool completed = task.Wait(timeout);
                            stop.Stop();
                            //source.Cancel(false);
                            if(completed)
                            {
                                Program.LogMsg($"Finished {name} in {stop.ElapsedMilliseconds}ms", Discord.LogSeverity.Verbose, srv.Name);
                            } else
                            {
                                Program.LogMsg($"Failed to complete {name} in {stop.ElapsedMilliseconds}ms; continuing...", LogSeverity.Warning, srv.Name);
                            }
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        var ex = tie.InnerException;
                        Program.LogMsg(ex, srv.Name);
                        srv.HasFailed = true;
                        if (srv.IsCritical)
                            throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMsg($"Critical failure on service, must exit: {ex}", Discord.LogSeverity.Critical, name);
                try
                {
                    var exStr = Program.Clamp(ex.ToString(), 1990);
                    Program.Client.GetUser(144462654201790464)
                        .SendMessageAsync($"```\r\n{exStr}\r\n```");
                } catch { }
                Environment.Exit(2);
                return;
            }
        }

        public static void SendReady(List<Service> _servs)
        {
            var backup = _servs.FirstOrDefault(x => x is BackupService);
            _servs.Remove(backup);
            _servs.Sort(new serviceComparer());
            zza_services = new List<Service>();
            zza_services.Add(backup);
            zza_services.AddRange(_servs);
            sendFunction("OnReady");
        }
    
        public static void SendLoad()
        {
            sendFunction("OnLoaded");
            if (cancel == null)
                StartDailyTick();
        }

        public static void SendSave() => sendFunction("OnSave");

        static CancellationTokenSource cancel;
        public static void StartDailyTick()
        {
            cancel = new CancellationTokenSource();
            var th = new Thread(thread);
            th.Start();
        }
        public static void SendClose()
        {
            cancel.Cancel();
            sendFunction("OnClose");
        }

        public static void SendDailyTick()
        {
            doneFunctions.Remove("OnDailyTick");
            lastDailyTick = DateTime.Now;
            sendFunction("OnDailyTick");
        }

        static void thread()
        {
            var token = cancel.Token;
            var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = " ";
            try
            {
                do
                {
                    var now = DateTime.Now;
                    var then = new DateTime(now.Year, now.Month, now.Day, 4, 0, 0); // 4 am
                    if (then <= now)
                        then = then.AddDays(1);
                    var diff = then - now;
                    var miliseconds = (int)Math.Floor(diff.TotalMilliseconds);
                    if (miliseconds < 2500)
                        miliseconds = 2500;
                    Program.LogMsg($"Waiting for {miliseconds.ToString("#,0.00", nfi)}ms", LogSeverity.Info, "DailyTick");
                    Task.Delay(miliseconds, token).Wait(token);
                    SendDailyTick();
                } while (!token.IsCancellationRequested);
            } catch(OperationCanceledException)
            {
                Program.LogMsg("DailyTick thread cancalled, exiting.", LogSeverity.Warning, "DailyTick");
            }
        }


        class serviceComparer : IComparer<Service>
        {
            public int Compare([AllowNull] Service x, [AllowNull] Service y)
            {
                // -1 -> x precedes y
                // 0 -> same
                // 1 -> x follows y
                if (x == null && y == null)
                    return 0;
                if (x == null)
                    return -1;
                if (y == null)
                    return 1;
                var xType = x.GetType();
                var yType = y.GetType();
                var aAttribute = xType.GetCustomAttribute<RequireServiceAttribute>() ?? new RequireServiceAttribute();
                var bAttribute = yType.GetCustomAttribute<RequireServiceAttribute>() ?? new RequireServiceAttribute();

                if (aAttribute.Types.Contains(yType))
                {
                    return "y".CompareTo("x");
                }
                if (bAttribute.Types.Contains(xType))
                {
                    return "x".CompareTo("y");
                }
                return 0;
            }
        }
    }
}
