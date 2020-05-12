using DiscordBot.Classes.ServerList;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public class MLServer : WebSocketBehavior
    {
        public MLServer()
        {
        }
        public MLService Service { get; set; }
        public Server Server { get; set; }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
        }

        protected override void OnOpen()
        {
            var serverId = Context.QueryString["id"];
            var auth = Context.QueryString["auth"];
            if(!Guid.TryParse(serverId, out var id))
            {
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation, "Id not in proper GUID format");
                return;
            }
            if(!Service.Servers.TryGetValue(id, out var server))
            {
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation, "No server found.");
                return;
            }
            if(server.Authentication != auth)
            {
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation, "No server found.");
                return;
            }
            if(server.ActiveSession != null)
            {
                server.ActiveSession.Context.WebSocket.Close(CloseStatusCode.Abnormal, "New session");
            }
            server.ActiveSession = this;
            this.Send("Ok");
        }
    }
}
