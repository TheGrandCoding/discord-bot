using Discord;
using DiscordBot.Classes;
using DiscordBot.Commands.Modules;
using DiscordBot.MLAPI;
using DiscordBot.MLAPI.Modules.TimeTracking;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public class StatisticsWS : WebSocketBehavior
    {
        string _ip = null;
        public string IP
        {
            get
            {
                _ip ??= Context.Headers["X-Forwarded-For"] ?? Context.UserEndPoint.Address.ToString();
                return _ip;
            }
        }
        public int ListeningTo { get; private set; }

        protected override void OnOpen()
        {
            var id = Context.QueryString.Get("id");
            if(int.TryParse(id, out var iii))
            {
                if(!StatsModule.AllStats.TryGetValue(iii, out var stats))
                {
                    Context.WebSocket.Close(CloseStatusCode.Normal, "No stats exists by that id");
                } else
                {
                    ListeningTo = iii;
                    Send(stats.GetJson().ToString());
                }
            } else
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "ID was not passed in query string");
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Program.LogDebug(e.Data, $"TTWS-{IP}");
        }
    }
}
