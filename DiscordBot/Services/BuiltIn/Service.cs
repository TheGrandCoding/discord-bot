using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
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
        
        public virtual int Priority => 0;

        public ServiceState State { get; private set; }
        public bool HasFailed => State == ServiceState.Failed;

        public virtual int DefaultTimeout => 10_000;
        public virtual int CloseTimeout => DefaultTimeout / 2;



        public static DateTime? lastDailyTick = null;

        public virtual void OnReady() { }
        public virtual void OnLoaded() { }
        public virtual void OnSave() { }
        public virtual void OnDailyTick() { }
        public virtual void OnClose() { }

        public int CompareTo([AllowNull] Service other)
            => new serviceComparer().Compare(this, other);

        protected static List<Service> zza_services = new List<Service>();

        public static IReadOnlyCollection<TInterface> GetServices<TInterface>()
        {
            var ls = new List<TInterface>();
            foreach(var sv in zza_services)
            {
                if (sv is TInterface ti)
                    ls.Add(ti);
            }
            return ls.ToImmutableArray();
        }

        public static ServiceState GlobalState {  get
            {
                if (doneFunctions.ContainsKey(ServiceState.Close))
                    return ServiceState.Close;
                if (doneFunctions.ContainsKey(ServiceState.Loaded))
                    return ServiceState.Loaded;
                if (doneFunctions.ContainsKey(ServiceState.Ready))
                    return ServiceState.Ready;
                return ServiceState.None;
            } }

        static void sendErrorToOwner(Service failed, string fName, Exception ex)
        {
            var embed = new EmbedBuilder();
            embed.Title = $"{failed.Name} {fName} Error";
            var errorText = ex?.ToString() ?? "null";
            embed.Color = failed.IsCritical ? Color.Red : Color.Orange;
            if(errorText.Length > 2000)
            {
                // send as file.
                var path = Path.Combine(Path.GetTempPath(), "exception.txt");
                File.WriteAllText(path, errorText);
                Program.AppInfo?.Owner.SendFileAsync(path, embed: embed.Build());
            } else
            {
                embed.Description = "```\r\n" + ex?.ToString() + "\r\n```";
                Program.AppInfo?.Owner.SendMessageAsync(embed: embed.Build());
            }
        }

        private static void MarkFailed(Service srv, string fName, Exception ex)
        {
            try
            {
                sendErrorToOwner(srv, fName, ex);
            } catch { }
        }
        protected void MarkFailed(Exception ex)
        {
            this.State = ServiceState.Failed;
            MarkFailed(this, "General", ex);
        }

        static Exception findTrueException(Exception ex)
        {
            if (ex is TargetInvocationException tie)
                return findTrueException(tie.InnerException);
            if (ex is AggregateException ag && ag.InnerExceptions.Count == 1)
                return ag.InnerExceptions.First();
            return ex;
        }

        static Dictionary<ServiceState, bool> doneFunctions = new Dictionary<ServiceState, bool>();
        static void sendFunction(ServiceState state)
        {
            if (doneFunctions.TryGetValue(state, out var d) && d)
                return;
            doneFunctions[state] = true;
            var name = $"On{state}";
            try
            {
                var stop = new Stopwatch();
                foreach (var srv in zza_services)
                {
                    if (srv.HasFailed)
                        continue;
                    if(!srv.IsEnabled)
                    {
                        srv.State = ServiceState.Disabled;
                        continue;
                    }
                    var method = srv.GetType().GetMethod(name);
                    try
                    {
                        if (method.IsOverride())
                        {
                            Program.LogDebug($"Sending {name}", srv.Name);
                            //var source = new CancellationTokenSource();
                            //srv.CancelToken = source.Token;
                            var task = new Task(() =>
                            {
                                try
                                {
                                    method.Invoke(srv, null);
                                }
                                catch (Exception exception)
                                {
                                    Exception ex = findTrueException(exception);
                                    MarkFailed(srv, $"{state}", ex);
                                    Program.LogError(ex, srv.Name);
                                    if (srv.IsCritical)
                                        throw ex;
                                }
                            });
                            stop.Restart();
                            int timeout = state == ServiceState.Close ? srv.CloseTimeout : srv.DefaultTimeout;
                            task.Start();
                            bool completed = task.Wait(timeout);
                            stop.Stop();
                            //source.Cancel(false);
                            if(completed)
                            {
                                Program.LogVerbose($"Finished {name} in {stop.ElapsedMilliseconds}ms", srv.Name);
                            } else
                            {
                                Program.LogWarning($"Failed to complete {name} in {stop.ElapsedMilliseconds}ms; continuing...", srv.Name);
                            }
                            if (state > srv.State)
                                srv.State = state;
                        }
                    }
                    catch (TargetInvocationException tie)
                    {
                        var ex = tie.InnerException;
                        Program.LogError(ex, srv.Name);
                        srv.State = ServiceState.Failed;
                        if (srv.IsCritical)
                            throw ex;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogCritical($"Critical failure on service, must exit: {ex}", name);
                Program.Close(2);
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
            sendFunction(ServiceState.Ready);
        }
    
        public static void SendLoad()
        {
            sendFunction(ServiceState.Loaded);
            if (!doingDailyTick)
                StartDailyTick();
        }

        public static void SendSave() => sendFunction(ServiceState.Save);

        static bool doingDailyTick = false;
        public static void StartDailyTick()
        {
            doingDailyTick = true;
            var th = new Thread(thread);
            th.Start();
        }
        public static void SendClose()
        {
            sendFunction(ServiceState.Close);
        }

        public static void SendDailyTick()
        {
            doneFunctions.Remove(ServiceState.DailyTick);
            lastDailyTick = DateTime.Now;
            sendFunction(ServiceState.DailyTick);
        }

        static void thread()
        {
            var token = Program.GetToken();
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
                    Program.LogInfo($"Waiting for {miliseconds.ToString("#,0.00", nfi)}ms", "DailyTick");
                    Task.Delay(miliseconds, token).Wait(token);
                    SendDailyTick();
                } while (!token.IsCancellationRequested);
            } catch(OperationCanceledException)
            {
                Program.LogWarning("DailyTick thread cancalled, exiting.", "DailyTick");
            }
        }

        protected void Info(string message) => Program.LogInfo(message, Name);
        protected void Warning(string message) => Program.LogWarning(message, Name);
        protected void Error(Exception ex) => Program.LogError(ex, Name);
        protected void Debug(string message) => Program.LogDebug(message, Name);

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

    /// <summary>
    /// Indicates the state of a service
    /// </summary>
    public enum ServiceState
    {
        /// <summary>
        /// No state set.
        /// </summary>
        None,
        /// <summary>
        /// Service has been disabled and will not function
        /// </summary>
        Disabled,
        DailyTick,
        Save,

        /// <summary>
        /// Service has executed the OnReady function without error
        /// </summary>
        Ready,
        /// <summary>
        /// Service has executed the OnLoaded function without error
        /// </summary>
        Loaded,
        /// <summary>
        /// Service has executed the OnClose function without error
        /// </summary>
        Close,

        /// <summary>
        /// Service has failed
        /// </summary>
        Failed
    }
}
