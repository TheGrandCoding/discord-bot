using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.Attributes;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
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

        public virtual void OnReady(IServiceProvider services) { }
        public virtual void OnLoaded(IServiceProvider services) { }
        public virtual void OnSave() { }
        public virtual void OnDailyTick() { }
        public virtual void OnClose() { }

        public int CompareTo([AllowNull] Service other)
            => new serviceComparer().Compare(this, other);

        protected static List<Service> zza_services = new List<Service>();

        public static IReadOnlyCollection<TInterface> GetServices<TInterface>()
        {
            var ls = new List<TInterface>();
            foreach (var sv in zza_services)
            {
                if (sv is TInterface ti)
                    ls.Add(ti);
            }
            return ls.ToImmutableArray();
        }
        public static IReadOnlyCollection<Service> GetServices()
            => zza_services.ToImmutableArray();

        static bool global_failed;
        public static ServiceState GlobalState { get
            {
                if (global_failed)
                    return ServiceState.Failed;
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
            if (errorText.Length > 4000)
            {
                // send as file.
                var path = Path.Combine(Path.GetTempPath(), "exception.txt");
                File.WriteAllText(path, errorText);
                Program.SendLogFileAsync(path, embed: embed.Build()).Wait();
            } else
            {
                embed.Description = "```\r\n" + ex?.ToString() + "\r\n```";
                Program.SendLogMessageAsync(embed: embed.Build()).Wait();
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
                using var scope = Program.GlobalServices.CreateScope();
                var stop = new Stopwatch();
                foreach (var srv in zza_services)
                {
                    if (srv.HasFailed)
                    {
                        Program.LogInfo($"Skipping {state} for {srv.Name} due because it is marked Failed.", "Service");
                        continue;
                    }
                    if (!srv.IsEnabled)
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
                                    if(method.GetParameters().Length == 0)
                                    {
                                        method.Invoke(srv, null);
                                    } else
                                    {
                                        method.Invoke(srv, new[] { scope.ServiceProvider });
                                    }
                                }
                                catch (Exception exception)
                                {
                                    Exception ex = findTrueException(exception);
                                    MarkFailed(srv, $"{state}", ex);
                                    Program.LogError(ex, srv.Name);
                                    srv.State = ServiceState.Failed;
                                    if (srv.IsCritical)
                                        throw ex;
                                }
                            });
                            stop.Restart();
                            bool completed;
                            task.Start();
                            completed = task.Wait(state == ServiceState.Close ? srv.CloseTimeout : srv.DefaultTimeout);
                            stop.Stop();
                            //source.Cancel(false);
                            if (completed)
                            {
                                Program.LogVerbose($"Finished {name} in {stop.ElapsedMilliseconds}ms", srv.Name);
                            } else
                            {
                                Program.LogWarning($"Failed to complete {name} in {stop.ElapsedMilliseconds}ms", srv.Name);
                                if(state != ServiceState.Close)
                                { // wait for it to finish anyway
                                    stop.Start();
                                    task.Wait(Program.GetToken());
                                    stop.Stop();
                                    Program.LogWarning($"Completed {name} in {stop.ElapsedMilliseconds}ms", srv.Name);
                                }
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
                global_failed = true;
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

            var serviceBaseType = typeof(Service);
            foreach (var service in zza_services)
            {
                var type = service.GetType();
                var properties = type.GetProperties().Where(x => serviceBaseType.IsAssignableFrom(x.PropertyType));
                foreach (var property in properties)
                {
                    var value = zza_services.First(x => x.GetType().FullName == property.PropertyType.FullName);
                    if (property.CanWrite)
                    {
                        Console.WriteLine($"Injecting {property.Name} with {property.PropertyType.Name} for {service.Name}");
                        property.SetValue(service, value);
                    }
                }

            }


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

        [Cron("4", "0")]
        public static void SendDailyTick()
        {
            doneFunctions.Remove(ServiceState.DailyTick);
            lastDailyTick = DateTime.Now;
            sendFunction(ServiceState.DailyTick);
        }

        struct schedueledJobs
        {

            public MethodInfo Method { get; set; }
            public Service Service { get; set; }
            public CronSchedule Schedule { get; set; }

            public void Invoke(int hour, int minute)
            {
                var meth = Method;
                var srv = Service;
                Task.Run(() =>
                {
                    try
                    {
                        object[] arr = new object[] { };
                        var param = meth.GetParameters();
                        if(param.Length == 1)
                        {
                            if (param[0].ParameterType == typeof(int))
                                arr = new object[] { hour };
                        } else if(param.Length == 2)
                        {
                            if (param.All(x => x.ParameterType == typeof(int)))
                                arr = new object[] { hour, minute };
                        }


                        if (meth.IsStatic)
                        {
                            meth.Invoke(null, arr);
                        } else
                        {
                            meth.Invoke(srv, arr);
                        }
                    } catch(Exception ex)
                    {
                        if(srv != null)
                        {
                            srv.ErrorToOwner(ex, "Cron-" + meth.Name);
                        }
                    }
                });
            }
            
            public DateTime Next { get; set; }
        }

        static void insertCron(LinkedList<schedueledJobs> queue, schedueledJobs newJob)
        {
            jobsLock.Wait();
            try
            {
                if (queue.Count == 0)
                {
                    queue.AddFirst(newJob);
                    return;
                }
                LinkedListNode<schedueledJobs> node = queue.First;
                do
                {
                    if(node.Value.Next > newJob.Next)
                    {
                        queue.AddBefore(node, newJob);
                        return;
                    } else
                    {
                        node = node.Next;
                    }
                } while (node != null);
                queue.AddLast(newJob);
            }
            finally
            {
                jobsLock.Release();
            }
        }
        static schedueledJobs pop(LinkedList<schedueledJobs> queue)
        {
            jobsLock.Wait();
            try
            {
                var x = queue.First;
                queue.RemoveFirst();
                return x.Value;
            } finally
            {
                jobsLock.Release();
            }
        }

        static void handleCronSearch(LinkedList<schedueledJobs> list, Type type, Service srv)
        {
            var methods = new List<MethodInfo>();
            if (srv != null)
                methods.AddRange(type.GetMethods());
            methods.AddRange(type.GetMethods(BindingFlags.Static | BindingFlags.Public));

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<CronAttribute>();
                if (attr != null)
                {
                    var sch = new CronSchedule(attr.Hour, attr.Minute);
                    insertCron(list, new schedueledJobs()
                    {
                        Method = method,
                        Service = srv,
                        Schedule = sch,
                        Next = sch.GetNext()
                    });
                }
            }
        }

        static void fetchCrons()
        {
            foreach(var srv in zza_services)
            {
                var type = srv.GetType();
                handleCronSearch(currentJobs, type, srv);
            }
            handleCronSearch(currentJobs, typeof(Service), null);
        }

        static SemaphoreSlim jobsLock = new(1);
        static LinkedList<schedueledJobs> currentJobs = new();
        static void thread()
        {
            fetchCrons();
            var token = Program.GetToken();
            var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = " ";
            try
            {
                do
                {
                    var nextJob = pop(currentJobs);

                    var now = DateTime.Now;
                    var then = nextJob.Next;
                    var diff = then - now;
                    var miliseconds = (int)Math.Floor(diff.TotalMilliseconds);
                    if (miliseconds >= 500)
                    {
                        if(miliseconds > Time.Ms.Hour)
                            Program.LogInfo($"Waiting for {miliseconds.ToString("#,0.00", nfi)}ms until {then:hh:mm} for {nextJob.Method.Name}", "JobSchedule");
                        Task.Delay(miliseconds, token).Wait(token);
                    }
                    nextJob.Invoke(then.Hour, then.Minute);
                    nextJob.Next = nextJob.Schedule.GetNext(nextJob.Next);
                    insertCron(currentJobs, nextJob);
                } while (!token.IsCancellationRequested);
            } catch (OperationCanceledException)
            {
                Program.LogWarning("DailyTick thread cancalled, exiting.", "DailyTick");
            }
        }

        protected void schedule(CronSchedule sch, MethodInfo info)
        {
            jobsLock.Wait();
            try
            {
                foreach(var x in currentJobs)
                {
                    if (x.Method == info)
                        throw new ArgumentException($"That method is already scheduled");
                }
            }
            finally
            {
                jobsLock.Release();
            }
            insertCron(currentJobs, new()
            {
                Method = info,
                Schedule = sch,
                Service = this,
                Next = sch.GetNext()
            });
        }
        protected void schedule(CronSchedule sch, Action func)
        {
            schedule(sch, func.Method);
        }

        protected void Info(string message, string source = null) => Program.LogInfo(message, Name + (source == null ? "" : ":" + source));
        protected void Warning(string message, string source = null) => Program.LogWarning(message, Name + (source == null ? "" : ":" + source));
        protected void Error(Exception ex, string source = null) => Program.LogError(ex, Name + (source == null ? "" : ":" + source));
        protected void Error(string exMessage, string source = null) => Program.LogError(exMessage, Name + (source == null ? "" : ":" + source));
        protected void ErrorToOwner(Exception ex, string source = null)
        {
            Error(ex, source);
            MarkFailed(this, source ?? "Log", ex);
        }
        protected void Debug(string message, string source = null) => Program.LogDebug(message, Name + (source == null ? "" : ":" + source));

        protected string EnsureConfiguration(string configKey)
        {
            var s = Program.Configuration[configKey];
            return s ?? throw new ArgumentNullException(nameof(configKey), $"Missing configuration value '{configKey}' for {Name}");
        }

        static Type[] getReliedOnServices(Service x)
        {
            var type = x.GetType();
            var ls = (type.GetCustomAttribute<RequireServiceAttribute>()
                             ?? new RequireServiceAttribute()).Types.ToList();

            var serviceBaseType = typeof(Service);
            foreach (var property in type.GetProperties())
            {
                var pType = property.PropertyType;
                if (serviceBaseType.IsAssignableFrom(pType) && !ls.Any(y => y.Name == pType.Name))
                    ls.Add(pType);
            }

            return ls.ToArray();
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
                var aAttribute = getReliedOnServices(x);
                var bAttribute = getReliedOnServices(y);

                if (aAttribute.Contains(yType))
                {
                    return "y".CompareTo("x");
                }
                if (bAttribute.Contains(xType))
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

    public class CronValue
    {
        public CronValue(string text)
        {
            Text = text;
        }
        public string Text { get; set; }

        public bool Matches(int value)
        {
            if (Text == "*") return true;
            if (int.TryParse(Text, out var x))
                return x == value;
            if(Text.Contains('/'))
            {
                var after = Text.Split('/')[1];
                if (!int.TryParse(after, out var interval))
                    throw new ArgumentException($"Cronvalue invalid: '{Text}'");
                return (value % interval) == 0;
            }
            if(Text.Contains('-'))
            {
                var sp = Text.Split('-');
                var start = int.Parse(sp[0]);
                var end = int.Parse(sp[1]);
                return start >= value && value < end;
            }
            if(Text.Contains(','))
            {
                var sp = Text.Split(',');
                return sp.Any(x => int.Parse(x) == value);
            }
            return false;
        }
    }

    public class CronSchedule
    {
        public CronValue Hour { get; set; }
        public CronValue Minute { get; set; }

        public CronSchedule(string text)
        {
            var sp = text.Split(' ');
            Hour = new CronValue(sp[0]);
            Minute = new CronValue(sp[1]);
        }
        public CronSchedule(string hour, string minute)
        {
            Hour = new CronValue(hour);
            Minute = new CronValue(minute);
        }

        public DateTime GetNext(DateTime? start = null)
        {
            var now = start ?? DateTime.Now;
            int startday = now.DayOfYear;
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            do
            {
                now = now.AddMinutes(1);
                if (Hour.Matches(now.Hour) && Minute.Matches(now.Minute))
                    return now;
            } while ((now.DayOfYear - startday) < 2);
            return now;
        }
    }

    public class CronAttribute : Attribute
    {
        public string Hour { get; set; }
        public string Minute { get; set; }
        public CronAttribute(string hour = "*", string minute = "*")
        {
            Hour = hour;
            Minute = minute;
        }
    }
}
