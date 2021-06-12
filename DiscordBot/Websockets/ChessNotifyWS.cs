#if INCLUDE_CHESS
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{

    /// <summary>
    /// Communicates clients looking at '/chess' to reload if an update occurs
    /// </summary>
    public class ChessNotifyWS : WebSocketBehavior
    {
        public ChessService Service { get; set; }

        protected override void OnOpen()
        {
            Program.LogMsg($"Opened Notify", source: $"ChsNf-{ID}");
            Service.ChangedOccured += Service_ChangedOccured;
            Service.MessageNotifiers += Service_MessageNotifiers;
            foreach(var game in ChessService.TimedGames.Values)
            {
                if (game.Ended)
                    continue;
                Send($"newGame:{game.Id}");
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Program.LogMsg($"Closed Notify", source: $"ChsNf-{ID}");
            Service.ChangedOccured -= Service_ChangedOccured;
            Service.MessageNotifiers -= Service_MessageNotifiers;
        }
        protected override void OnMessage(MessageEventArgs e)
        {
            Program.LogMsg($"Message Notify: {e.Data}", source: $"ChsNf-{ID}");
            Sessions.Broadcast(e.Data);
        }

        private void Service_ChangedOccured(object sender, string e)
        {
            Send("reload:" + e);
        }

        private void Service_MessageNotifiers(object sender, string e)
        {
            Send($"{sender}:{e}");
        }
    }
}
#endif