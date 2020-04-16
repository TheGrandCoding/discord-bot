using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

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

        public virtual void OnReady(DiscordSocketClient client) { }
        public virtual void OnLoaded() { }
        public virtual void OnSave() { }

        public int CompareTo([AllowNull] Service other)
            => this.Name.CompareTo(other.Name);

        static List<Service> services;

        static void sendFunction(string name, Action<Service> action)
        {
            try
            {
                foreach (var srv in services)
                {
                    try
                    {
                        action(srv);
                    }
                    catch (Exception ex)
                    {
                        Program.LogMsg(ex, srv.Name);
                        if (srv.IsCritical)
                            throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMsg($"Critical failure on service, must exit.", name, Discord.LogSeverity.Critical);
                Environment.Exit(2);
                return;
            }
        }

        public static void SendReady()
        {
            services = ReflectiveEnumerator.GetEnumerableOfType<Service>(null).ToList();
            sendFunction("Ready", x => x.OnReady(Program.Client));
        }
    
        public static void SendLoad()
        {
            sendFunction("Load", x => x.OnLoaded());
        }    
    }
}
