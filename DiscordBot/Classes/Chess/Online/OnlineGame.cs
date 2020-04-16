using ChessClient.Classes;
using Discord;
using DiscordBot.Services;
using DiscordBot.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DiscordBot.Classes.Chess.Online
{
    public class OnlineGame : APIObject
    {
        public OnlineGame()
        {
            ChessS = Program.Services.GetRequiredService<ChessService>();
        }
        public ChessConnection White { get; set; }
        public ChessConnection Black { get; set; }
        public List<ChessConnection> Spectators { get; set; } = new List<ChessConnection>();
        public bool UsesAntiCheat { get; set; } = true;
        public int AiDelay = 100; // ms delay applied to AI descision making.
        public bool HasEnded => Ended != null;
        private ChessService ChessS;
        public GameEndCondition Ended;

        public void SendLogStart(string starter)
        {
            if (Message != null)
                return;
            var embed = new Discord.EmbedBuilder();
            if (Program.BOT_DEBUG)
            {
                embed.Title = "Debug Game Started";
                embed.Color = Discord.Color.Red;
            }
            else
            {
                embed.Title = "Online Game Started";
            }
            embed.WithUrl(MLAPI.Handler.LocalAPIUrl + "/chess/online");
            embed.WithDescription($"{starter} has started an cross-network game of chess.\n" +
                $"Click the link title above to view.\n" +
                $"To play or spectate, you will need to [review the Terms]({MLAPI.Handler.LocalAPIUrl}/chess/terms#online), " +
                $"then [download the client](https://github.com/CheAle14/bot-chess/releases/latest/download/ChessInstaller.exe)");
            var msg = ChessS.DiscussionChannel.SendMessageAsync("[...]", embed: embed.Build()).Result;
            Message = msg;
            updateBoard();
        }

        public OtherGame InnerGame;

        public IUserMessage Message { get; set; }

        public List<ChessConnection> GetPlayers()
        {
            var plyers = new List<ChessConnection>();
            if (White != null)
                plyers.Add(White);
            if (Black != null)
                plyers.Add(Black);
            plyers.AddRange(Spectators);
            return plyers;
        }

        public ChessConnection GetPlayer(int id)
        {
            return GetPlayers().FirstOrDefault(x => x.Player?.Id == id);
        }


        public override void LoadJson(JObject json)
        {
        }

        public JObject ToJson(bool withBoard)
        {
            var jobj = ToJson();
            if (!withBoard)
                return jobj;
            return jobj;
        }

        public override JObject ToJson()
        {
            var jobj = new JObject();
            jobj["white"] = White?.Player?.Id ?? 0;
            jobj["black"] = Black?.Player?.Id ?? 0;
            jobj["spectators"] = JToken.FromObject(Spectators.Select(x => x.Player.Id).ToList());
            jobj["wait"] = Black == null ? 0 : (int)(InnerGame.turn == OtherGame.WHITE ? PlayerSide.White : PlayerSide.Black);
            jobj["cheat"] = UsesAntiCheat;
            jobj["fen"] = InnerGame?.generate_fen() ?? "";
            return jobj;
        }

        string getBoard()
        {
            if (InnerGame == null)
                return "Game has not yet start";
            return "```\n" + InnerGame.ascii() + "\n```";
        }

        void sendThread()
        {
            Message?.ModifyAsync(x =>
            {
                x.Content = getBoard();
            });
        }

        public void updateBoard()
        {
            var th = new Thread(sendThread);
            th.Start();
        }

        void winnerLeaderboard(ChessPlayer p1, ChessPlayer p2, bool draw)
        {
            MLAPI.Modules.Chess.addGameEntry(p1, p2, draw, x =>
            {
                return (int)(MLAPI.Modules.Chess.defaultKFunction(x) / 2);
            }, true, out _);
        }

        public void DeclareWinner(GameEndCondition end)
        {
            if (HasEnded)
                return;
            var player = end.Winner;
            var builder = new EmbedBuilder();
            builder.Title = "Online Game Ended";
            bool draw = player == null;
            if(draw == false)
            {
                if(player.Side == PlayerSide.None)
                { // cannot allow Spectator to win.
                    return;
                }
                builder.Description = $"Winner **{player.Player.Name}** declared due to {end.Reason}";
                builder.Color = Color.Green;
            } else
            {
                builder.Description = $"Draw declared due to {end.Reason}";
                builder.Color = Color.Orange;
            }
            ChessPlayer opponent;
            if (player == null)
                player = White;
            if (player.Player.Id == White.Player.Id)
                opponent = Black.Player;
            else
                opponent = White.Player;
            builder.AddField("Player 1", player.Player.Name, true);
            builder.AddField("Player 2", opponent.Name, true);
            if (UsesAntiCheat == false)
                builder.AddField("No Leaderboard", "Anticheat disabled on one or more players", false);
            var chess = Program.Services.GetRequiredService<ChessService>();
            chess.LogChnl(builder, chess.GameChannel);
            chess.GameChannel.SendMessageAsync(getBoard());
            Ended = end;
            if(UsesAntiCheat)
                winnerLeaderboard(player.Player, opponent, draw);
            var jobj = new JObject();
            jobj["id"] = player.Player?.Id ?? 0;
            jobj["reason"] = end.Reason;
            var anyPlayer = ChessService.CurrentGame.GetPlayers().FirstOrDefault();
            anyPlayer?.Broadcast(new Packet(PacketId.GameEnd, jobj));
            ChessService.CurrentGame = null;
        }
    }
}
