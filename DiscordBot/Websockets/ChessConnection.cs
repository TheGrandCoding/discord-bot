using ChessClient.Classes;
using ChessClient.Classes.Chess;
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.Chess.Online;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;
using static DiscordBot.Services.ChessService;

namespace DiscordBot.WebSockets
{
    public class ChessConnection : WebSocketBehavior
    {
        public ChessPlayer Player { get; set; }
        public PlayerSide Side { get; set; }
        static ChessService ChessS;

        static string folder = null;
        public static void log(string message)
        {
            message = $"[{DateTime.Now.ToString("hh:mm:ss.fff")}] " + message;
            folder = folder ?? Path.Combine(Program.BASE_PATH, "ChessO", "Games", DateTime.Now.DayOfYear.ToString("000"));
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "log.txt");
            File.AppendAllText(file, message.Replace("\r\n", "\n") + "\r\n");
        }

        public bool ExpectDemand { get; set; }
        public int DemandsSent { get; set; } = 0;

        protected static bool checkingGameEnd = false;
        protected static AutoResetEvent finishedChecks = new AutoResetEvent(false);

        GameEndCondition _innerCheck()
        {
            var inner = ChessService.CurrentGame.InnerGame;
            if (inner.half_moves >= 100)
            { // counts number of moves made without a pawn moving or capture
                return GameEndCondition.Draw($"No captures or pawn movements made in 50 moves");
            }
            if(inner.in_checkmate())
            {
                var opposite = inner.swap_color(inner.turn);
                var player = opposite == "w" ? CurrentGame.White : CurrentGame.Black;
                return GameEndCondition.Win(player, "In check with no legal moves");
            }
            if(inner.in_stalemate())
            {
                return GameEndCondition.Draw("Not in check but no legal moves");
            }
            if(inner.insufficient_material())
            {
                return GameEndCondition.Draw("Insufficient material to cause checkmate");
            }
            if(inner.in_threefold_repetition())
            {
                return GameEndCondition.Draw("Board repeated thrice");
            }
            return GameEndCondition.NotEnded();
        }

        public void CheckGameEnd()
        {
            checkingGameEnd = true;
            var result = _innerCheck();
            if(result.IsOver)
            {
                var jobj = new JObject();
                jobj["reason"] = result.Reason;
                jobj["id"] = result.Winner?.Player?.Id ?? 0;
                Broadcast(new ChessPacket(PacketId.GameEnd, jobj));
            }
            checkingGameEnd = false;
            finishedChecks.Set();
        }

        public virtual void Send(ChessPacket p)
        {
            log($"{REF} <<< {p.ToString()}");
            this.Send(p.ToString());
        }

        public void Broadcast(ChessPacket p)
        {
            log($"{REF} === {p.ToString()}");
            string data = p.ToString();
            foreach(var pl in ChessService.CurrentGame.GetPlayers())
            {
                if(pl is ChessAIPlayer ai)
                {
                    ai.recievePacket(p);
                } else
                {
                    try
                    {
                        pl.Send(data);
                    }
                    catch(Exception ex) 
                    {
                        Program.LogMsg($"{ex}", Discord.LogSeverity.Warning, $"Send-{pl.ID}-{pl.Player?.Name ?? ""}");
                    }
                }
            }
        }

        public void LoadJson(JObject json)
        { // should not load this.
        }

        void handleClose(string reason)
        {
            log($"Socket connection closed: {reason}");
            Program.LogMsg($"Closed: {reason}", Discord.LogSeverity.Critical, $"{(Player?.Name ?? "")}-{this.ID}");
            if(Player != null && ChessService.CurrentGame != null)
            { // no point sending message if player never joined, or game not started
                var jobj = new JObject();
                jobj["id"] = Player.Id;
                Broadcast(new ChessPacket(PacketId.UserDisconnected, jobj));
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            handleClose($"{e.Code}: {e.Reason}");
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            handleClose($"Error: {e.Exception}");
        }

        public JObject ToJson()
        {
            var jobj = new JObject();
            jobj["id"] = Player.Id;
            jobj["name"] = Player.Name;
            jobj["side"] = (int)Side;
            return jobj;
        }

        public ChessConnection()
        {
            ChessS ??= Program.Services.GetRequiredService<ChessService>();
        }

        string getPieceSimple(int fromComplex)
        {
            if (fromComplex == 0)
                return "p"; // pawn
            else if (fromComplex == 1)
                return "r"; // rook
            else if (fromComplex == 2)
                return "n"; // knight
            else if (fromComplex == 3)
                return "b"; // bishop
            else if (fromComplex == 4)
                return "q"; // queen
            return "k"; // king
        }

        void handleInGame(ChessPacket ping)
        {
            if(ping.Id == PacketId.MoveRequest)
            {
                var r = new OtherGame.Move();
                var g = CurrentGame;
                string sFrom = ping.Content["from"].ToObject<string>();
                string sTo = ping.Content["to"].ToObject<string>();
                if(!(Side == PlayerSide.White && g.InnerGame.turn == "w" || Side == PlayerSide.Black && g.InnerGame.turn == "b"))
                {
                    Send(new ChessPacket(PacketId.MoveRequestRefuse, new MoveRefuse($"{sFrom} -> {sTo}", $"It is not your turn").ToJson()));
                    return;
                }
                r.from = g.InnerGame.parseAlgebraic(sFrom);
                r.to = g.InnerGame.parseAlgebraic(sTo);
                var promVal = ping.Content["promote"];
                if (promVal != null)
                    r.promotion = getPieceSimple(promVal.ToObject<int>());

                var legalMoves = g.InnerGame.generate_moves(new Dictionary<string, string>() { { "legal", "true" }, { "square", r.from.ToString() } });
                OtherGame.Move? legalMove = null;
                foreach(var mv in legalMoves)
                {
                    if(mv.from == r.from && mv.to == r.to)
                    {
                        if(!string.IsNullOrWhiteSpace(mv.promotion) && !string.IsNullOrWhiteSpace(r.promotion))
                        {
                            if (mv.promotion != r.promotion)
                                continue;
                        }
                        if(!string.IsNullOrWhiteSpace(mv.promotion) && string.IsNullOrWhiteSpace(r.promotion))
                        {
                            if (mv.promotion != "q")
                                continue;
                        }
                        if(string.IsNullOrWhiteSpace(mv.promotion) && !string.IsNullOrWhiteSpace(r.promotion))
                        {
                            continue;
                        }
                        legalMove = mv;
                        break;
                    }
                }
                if(legalMove.HasValue == false)
                {
                    Send(new ChessPacket(PacketId.MoveRequestRefuse, new MoveRefuse($"{sFrom} -> {sTo}", $"No legal move to that location").ToJson()));
                    return;
                }
                g.InnerGame.make_move(legalMove.Value);
                g.InnerGame.registerMoveMade();
                Program.LogMsg("Player made move", Discord.LogSeverity.Error, "Player");
                ChessService.CurrentGame.updateBoard();
                Broadcast(new ChessPacket(PacketId.MoveMade, ping.Content));
                CheckGameEnd();
            } else if (ping.Id == PacketId.IdentRequest)
            {
                var id = ping.Content["id"].ToObject<int>();
                var player = CurrentGame.GetPlayer(id);
                var jobj = ping.Content;
                jobj["player"] = player?.ToJson() ?? JObject.FromObject(null);
                Send(new ChessPacket(PacketId.PlayerIdent, jobj));
            } else if (ping.Id == PacketId.RequestScreen)
            {
                if(Player.Permission.HasFlag(ChessPerm.Moderator))
                {
                    var player = CurrentGame.GetPlayer(ping.Content["id"].ToObject<int>());
                    if(player != null)
                    {
                        player.ExpectDemand = true;
                        player.Send(new ChessPacket(PacketId.DemandScreen, new JObject()));
                    }
                }
            } else if (ping.Id == PacketId.RequestGameEnd)
            {
                var id = ping.Content["id"].ToObject<int>();
                var player = id == 0 ? null : ChessService.CurrentGame.GetPlayer(id);
                var end = id == 0 ? GameEndCondition.Draw($"A-Drawn by {Player.Name}") : GameEndCondition.Win(player, $"A-Won by {Player.Name}");
                ChessService.CurrentGame.DeclareWinner(end);
            } else if (ping.Id == PacketId.RequestProcesses)
            {
                if (Player.Permission.HasFlag(ChessPerm.Moderator))
                {
                    var player = CurrentGame.GetPlayer(ping.Content["id"].ToObject<int>());
                    if (player != null)
                    {
                        player.ExpectDemand = true;
                        player.Send(new ChessPacket(PacketId.DemandProcesses, new JObject()));
                    }
                }
            } else if (ping.Id == PacketId.ResignRequest)
            {
                ChessConnection opponent;
                if(Side == PlayerSide.White)
                {
                    opponent = ChessService.CurrentGame.Black;
                } else if (Side == PlayerSide.Black)
                {
                    opponent = ChessService.CurrentGame.White;
                } else
                { // spectators can't resign.
                    return;
                }
                ChessService.CurrentGame.DeclareWinner(GameEndCondition.Win(opponent, "Resigned"));
            } else if (ping.Id == PacketId.RequestRevertMove)
            {
                if(Player.Permission.HasFlag(ChessPerm.Moderator))
                {
                    CurrentGame.InnerGame.undo_move();
                    Broadcast(new ChessPacket(PacketId.MoveReverted, ping.Content));
                }
            }
        }

        void handleMessage(ChessPacket ping)
        {
            if (ChessService.CurrentGame == null || ChessService.CurrentGame.HasEnded)
            {
                Send(new ChessPacket(PacketId.GameEnd, new JObject()));
                return;
            }
            if (Player != null)
            {
                handleInGame(ping);
                return;
            }
            using var db = DB();
            if(ping.Id == PacketId.ConnRequest)
            {
                Send(new ChessPacket(PacketId.Log, ping.Content));
                var token = ping.Content["token"].ToObject<string>();
                bool usesAntiCheat = ping.Content["cheat"].ToObject<bool>();
                Player = db.Players.FirstOrDefault(x => x.VerifyOnlineReference == token);
                if (Player == null)
                {
                    Context.WebSocket.Close(CloseStatusCode.InvalidData, "Player token invalid");
                    return;
                }
                if(Player.IsBuiltInAccount)
                {
                    Context.WebSocket.Close(CloseStatusCode.Normal, "Built in account");
                    return;
                }
                if(Player.IsBanned)
                {
                    Context.WebSocket.Close(CloseStatusCode.Normal, "Banned");
                    return;
                }
                if (Player.Removed)
                {
                    Context.WebSocket.Close(CloseStatusCode.Normal, "Removed from Leaderboard");
                    return;
                }
                var existing = ChessService.CurrentGame.GetPlayer(Player.Id);
                if(existing != null)
                {
                    this.Side = existing.Side;
                    try
                    {
                        existing.Sessions.CloseSession(existing.ID, CloseStatusCode.PolicyViolation, "You joined with another client; replacing");
                    }
                    catch { }
                    if(this.Side == PlayerSide.White)
                    {
                        ChessService.CurrentGame.White = this;
                        if (!usesAntiCheat)
                            ChessService.CurrentGame.UsesAntiCheat = false;
                    } else if (this.Side == PlayerSide.Black)
                    {
                        ChessService.CurrentGame.Black = this;
                        if (!usesAntiCheat)
                            ChessService.CurrentGame.UsesAntiCheat = false;
                    } else
                    {
                        ChessService.CurrentGame.Spectators.RemoveAll(x => x.Player?.Id == Player.Id);
                        ChessService.CurrentGame.Spectators.Add(this);
                    }
                    doJoin();
                    return;
                }
                var type = ping.Content["mode"].ToObject<string>();
                if (type == "join")
                {
                    if (CurrentGame.White == null)
                    {
                        CurrentGame.White = this;
                        Side = PlayerSide.White;
                        if (!usesAntiCheat)
                            ChessService.CurrentGame.UsesAntiCheat = false;
                        ChessS.LogChnl(new Discord.EmbedBuilder()
                            .WithTitle("White Player")
                            .WithDescription("Joined: " + Player.Name)
                            .WithColor(usesAntiCheat ? Discord.Color.Green : Discord.Color.Red)
                            ,
                            ChessS.DiscussionChannel);
                    }
                    else if (CurrentGame.Black == null)
                    {
                        Side = PlayerSide.Black;
                        CurrentGame.Black = this;
                        CurrentGame.InnerGame = new OtherGame();
                        ChessS.LogChnl(new Discord.EmbedBuilder()
                            .WithTitle("Black Player")
                            .WithDescription("Joined: " + Player.Name)
                            .WithColor(usesAntiCheat ? Discord.Color.Green : Discord.Color.Red)
                            ,
                            ChessS.DiscussionChannel);
                    }
                    else
                    {
                        Context.WebSocket.Close(CloseStatusCode.Normal, "Game is full");
                        return;
                    }
                }
                else if (type == "spectate")
                {
                    CurrentGame.Spectators.Add(this);
                    ChessS.LogChnl(new Discord.EmbedBuilder()
                        .WithTitle("Spectator").WithDescription("Joined: " + Player.Name),
                        ChessS.DiscussionChannel);
                }
                doJoin();
            }
        }
        protected void doJoin()
        {
            ChessService.CurrentGame.updateBoard(); // since board is not initialised until both players are in.
            var conn = new ConnectionBroadcast(this, Side == PlayerSide.None ? "spectate" : "join");
            Broadcast(new ChessPacket(PacketId.ConnectionMade, conn.ToJson()));
            foreach (var p in CurrentGame.GetPlayers())
            {
                if (p.ID == this.ID)
                    continue;
                var jobj = new JObject();
                jobj["id"] = p.Player.Id;
                jobj["player"] = p?.ToJson() ?? JObject.FromObject(null);
                Send(new ChessPacket(PacketId.PlayerIdent, jobj));
                System.Threading.Thread.Sleep(500);
            }
            if(Player.Permission.HasFlag(ChessPerm.Moderator) && Side == PlayerSide.None)
            { // must be a Mod/Justice AND must be Spectating - not playing
                System.Threading.Thread.Sleep(500);
                Send(new ChessPacket(PacketId.NotifyAdmin, new JObject()));
            }
            System.Threading.Thread.Sleep(500);
            Broadcast(new ChessPacket(PacketId.GameStatus, CurrentGame.ToJson((CurrentGame.InnerGame?.move_number ?? 0) > 1)));
            if (ChessService.CurrentGame.InnerGame == null)
                return; // black hasnt joined yet
            if(ChessService.CurrentGame.InnerGame.move_number == 1 && 
                ChessService.CurrentGame.InnerGame.turn == OtherGame.WHITE)
            { // moves havnt been made yet
                if(ChessService.CurrentGame.White is ChessAIPlayer ai)
                { // and the ai needs to make their turn
                    if(ChessService.CurrentGame.Black is ChessAIPlayer)
                    { // increase delay since thats an oof.
                        ChessService.CurrentGame.AiDelay = 5000;
                    }
                    System.Threading.Thread.Sleep(500); // delay for packets to be sent
                    ai.MakeAIMove();
                }
            }
        }

        string REF => $"{(Player?.Name ?? "")}-{this.ID}";

        protected override void OnMessage(MessageEventArgs e)
        {
            Program.LogMsg($"Getting Lock..", Discord.LogSeverity.Critical, $"Con::{REF}");
            if (!OnlineLock.WaitOne(6 * 1000))
            {
                Program.LogMsg($"Failed Lock..", Discord.LogSeverity.Critical, $"Con::{REF}");
                Context.WebSocket.Close(CloseStatusCode.ServerError, "Unable to get lock");
                return;
            }
            Program.LogMsg($"Got Lock..", Discord.LogSeverity.Critical, $"Con::{REF}");
            try
            {
                log($"{REF} >>> {e.Data}");
                Program.LogMsg($"{e.Data}", Discord.LogSeverity.Critical, REF);
                var jobj = JObject.Parse(e.Data);
                var packet = new ChessPacket(jobj);
                handleMessage(packet);
            } catch (Exception ex)
            {
                Program.LogMsg($"ChessCon:{Player?.Name ?? "na"}", ex);
            } finally
            {
                OnlineLock.Release();
                Program.LogMsg($"Released Lock..", Discord.LogSeverity.Critical, $"Con::{REF}");
            }
        }
    }
}
