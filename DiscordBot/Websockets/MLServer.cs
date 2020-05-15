using DiscordBot.Classes.ServerList;
using DiscordBot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
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

        void ReplyError(string e, bool close = false)
        {
            var jobj = new JObject();
            jobj["close"] = close;
            jobj["reason"] = e;
            Send(new MLPacket(PacketId.Error, jobj).ToString());
        }

        protected override void OnClose(CloseEventArgs e)
        {
            if(Server != null)
                Server.ActiveSession = null;
            base.OnClose(e);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            if (Server != null)
                Server.ActiveSession = null;
            base.OnError(e);
        }

        protected void HandleMessage(MLPacket packet)
        {
            if(packet.Id == PacketId.SetPlayers)
            {
                var players = new List<Player>();
                foreach(JToken child in packet.Content)
                {
                    var reader = child.CreateReader();
                    var p = Player.FromJson(reader);
                    players.Add(p);
                }
                Server.Players = players;
            } else if (packet.Id == PacketId.AddPlayer)
            {
                var reader = packet.Content.CreateReader();
                var player = Player.FromJson(reader);
                Server.Players.Add(player);
            } else if (packet.Id == PacketId.PatchPlayer)
            {
                var hwid = packet.Content["hwid"].ToObject<string>();
                var payload = packet.Content["value"];
                if(payload == null)
                {
                    Server.Players.RemoveAll(x => x.HWID == hwid);
                } else
                {
                    var ply = Server.Players.FirstOrDefault(x => x.HWID == hwid);
                    if(ply == null)
                    {
                        ReplyError($"Cannot PATCH player with name '{hwid}' as none exists");
                    } else
                    {
                        var reader = payload.CreateReader();
                        var player = Player.FromJson(reader);
                        ply.Name = player.Name;
                        ply.Latency = player.Latency;
                        ply.Score = player.Score;
                    }
                }
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if(!e.IsText)
                return;
            var jobj = JObject.Parse(e.Data);
            var packet = new MLPacket(jobj["id"].ToObject<PacketId>(), jobj["content"]);
            Service.Lock.WaitOne();
            try
            {
                HandleMessage(packet);
            } catch (Exception ex)
            {
                Program.LogMsg(ex, $"ML:{Server.Id}");
                try
                {
                    ReplyError(ex.Message, true);
                } catch { }
                this.Context.WebSocket.Close(CloseStatusCode.Abnormal, $"Error");
            } finally
            {
                Service.Lock.Release();
            }
        }

        protected override void OnOpen()
        {
            var serverId = Context.QueryString["id"];
            var auth = Context.QueryString["auth"];
            if(!Guid.TryParse(serverId, out var id))
            {
                ReplyError("Id not in proper GUID format", true);
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation);
                return;
            }
            if(!Service.Servers.TryGetValue(id, out var server))
            {
                ReplyError("No server found.", true);
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation);
                return;
            }
            if(server.Authentication != auth)
            {
                ReplyError("No server found.", true);
                Context.WebSocket.Close(CloseStatusCode.PolicyViolation);
                return;
            }
            if(server.ActiveSession != null)
            {
                server.ActiveSession.ReplyError($"New session created", true);
                server.ActiveSession.Context.WebSocket.Close(CloseStatusCode.Abnormal, "New session");
            }
            server.ActiveSession = this;
            Server = server;
            this.Send("Ok");
        }
    }
}
