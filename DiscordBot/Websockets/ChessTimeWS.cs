using DiscordBot.Classes;
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.Chess.Online;
using DiscordBot.Classes.Chess.TimedOnline;
using DiscordBot.MLAPI;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace DiscordBot.Websockets
{
    public class ChessTimeWS : WebSocketBehavior
    {
        public ChessPlayer Player { get; set; }
        public ChessTimedGame Game { get; set; }

        public PlayerSide Changeable { get; set; }

        protected override void OnClose(CloseEventArgs e)
        {
            Game?.ListeningWS.Remove(this);
        }

        protected override void OnOpen()
        {
            using var db = Program.Services.GetRequiredService<ChessDbContext>();
            try
            {
                if (!Handler.findToken(Context.CookieCollection[AuthToken.SessionToken].Value, out var bUser, out _))
                {
                    Context.WebSocket.Close(CloseStatusCode.Normal, "Authentication failed.");
                    return;
                }
                Player = db.Players.FirstOrDefault(x => x.DiscordAccount == ChessService.cast(bUser.Id));
            } catch 
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Forbidden - must authenticate.");
                return;
            } 
            var id = Guid.Parse(Context.QueryString.Get("id"));
            if(ChessService.TimedGames.TryGetValue(id, out var g))
            {
                Game = g;
                Game.ListeningWS.Add(this);
            }
            if (Game.White.Id == Player.Id)
                Changeable = PlayerSide.White;
            else if (Game.Black.Id == Player.Id)
                Changeable = PlayerSide.Black;
            SendStatus(((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(), true);
        }

        public void SendStatus(long time, bool includeChange = false)
        {
            var jobj = Game.ToJson();
            if(includeChange)
                jobj["change"] = Changeable != PlayerSide.None;
            var pck = new TimedPacket(TimedId.Status, jobj);
            pck.Time = time;
            Send(pck.ToString());
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var jobj = JObject.Parse(e.Data);
            var packet = new TimedPacket(jobj);
            Program.LogMsg($"{packet}", Discord.LogSeverity.Debug, ID);
            if(packet.Id == TimedId.Status)
            {
                SendStatus(((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds(), true);
            } else if(Changeable != PlayerSide.None)
            {
                if(packet.Id == TimedId.Pause)
                {
                    Game.Stop(packet.Time.Value);
                } else if (packet.Id == TimedId.Switch)
                {
                    Game.Switch(packet.Time.Value);
                } else if (packet.Id == TimedId.Start)
                {
                    Game.Start(packet.Time.Value);
                }
            }
        }
    }
}
