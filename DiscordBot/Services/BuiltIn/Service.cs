using Discord;
using Discord.WebSocket;
using DiscordBot.Utils;
using RestSharp.Deserializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

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
        public bool HasFailed { get; private set; }
        public virtual int Priority => 0;

        public virtual void OnReady() { }
        public virtual void OnLoaded() { }
        public virtual void OnSave() { }

        public int CompareTo([AllowNull] Service other)
            => this.Name.CompareTo(other.Name);

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
                    if (!srv.IsEnabled)
                        continue;
                    var method = srv.GetType().GetMethod(name);
                    try
                    {
                        if (method.IsOverride())
                        {
                            Program.LogMsg($"Sending {name}", LogSeverity.Debug, srv.Name);
                            stop.Restart();
                            method.Invoke(srv, null);
                            stop.Stop();
                            Program.LogMsg($"Finished {name} in {stop.ElapsedMilliseconds}ms", Discord.LogSeverity.Verbose, srv.Name);
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
                Environment.Exit(2);
                return;
            }
        }

        public static void SendReady(List<Service> _servs)
        {
            zza_services = _servs.OrderBy(x => x.Priority).ToList();
            sendFunction("OnReady");
        }
    
        public static void SendLoad()
        {
            sendFunction("OnLoaded");
        }

        public static void SendSave() => sendFunction("OnSave");
    }
}
