#if INCLUDE_CHESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChessClient.Classes;
using ChessClient.Classes.Chess;
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.Chess.Online;
using DiscordBot.Services;
using DiscordBot.WebSockets;
using Newtonsoft.Json.Linq;
using static DiscordBot.Classes.Chess.Online.OtherGame;

namespace DiscordBot.Classes.Chess
{
    public class ChessAIPlayer : ChessConnection
    {
        /// <summary>
        /// Depth to evaluate positions to, where 0=random move.
        /// </summary>
        public int RunDepth { get; set; } = -1;

        public PlayerSide Opponent => (PlayerSide)((int)Side ^ 0b11);



        DateTime startEvalTime = DateTime.Now;
        int positions = 0;
        Move minmaxRoot(int depth, OtherGame game)
        {
            var available_moves = game.generate_moves(new Dictionary<string, string>()
            {
                {"legal", "true" }
            });
            double bestMove = -9999;
            Move? bestMoveFound = null;

            for(int i = 0; i < available_moves.Count; i++)
            {
                var gameMove = available_moves[i];
                game.make_move(gameMove);
                var value = alphaBeta(game, double.MinValue, double.MaxValue, RunDepth-1);
                game.undo_move();
                if(value >= bestMove)
                {
                    bestMove = value;
                    bestMoveFound = gameMove;
                }
            }
            return bestMoveFound.Value;
        }

        double quiesce(OtherGame game, double alpha, double beta)
        {
            positions++;
            if(positions % 100 == 0)
            {
                Program.LogDebug($"Evaluate {positions.ToString("000000")}", "Quiesce");
            }
            var stand_pat = game.get_board_value();
            if (stand_pat >= beta)
                return beta;
            if (alpha < stand_pat)
                alpha = stand_pat;
            var moves = game.generate_moves(new Dictionary<string, string>()
            {
                {"legal", "true" }
            });
            foreach(var mv in moves)
            {
                if (string.IsNullOrWhiteSpace(mv.captured))
                    continue;
                game.make_move(mv);
                var score = -quiesce(game, -beta, -alpha);
                game.undo_move();
                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }
            return alpha;
        }

        double alphaBeta(OtherGame game, double alpha, double beta, int depthLeft)
        {
            double bestscore = double.MinValue;
            if (depthLeft == 0)
                return quiesce(game, alpha, beta);
            var moves = game.generate_moves(new Dictionary<string, string>()
            {
                {"legal", "true" }
            });
            foreach(var mv in moves)
            {
                game.make_move(mv);
                var score = -alphaBeta(game, -beta, -alpha, depthLeft - 1);
                game.undo_move();
                if (score >= beta)
                    return score;
                if(score > bestscore)
                {
                    bestscore = score;
                    if (score > alpha)
                        alpha = score;
                }
            }
            return bestscore;
        }


        Move selectBestMove(List<Move> moves)
        {
            var g = ChessService.CurrentGame.InnerGame;
            if(RunDepth == 0)
            {
                return moves[Program.RND.Next(0, moves.Count)];
            } else 
            {
                startEvalTime = DateTime.Now;
                positions = 0;
                var bestMove = minmaxRoot(RunDepth, g);
                var end = DateTime.Now;
                var diff = end - startEvalTime;
                Program.LogInfo($"Evaluated {RunDepth} depth, {positions} positions {Side} in {diff} time.", "ASDAQ");
                return bestMove;
            }
        }

        int simpleToComplex(string s)
        {
            switch (s)
            {
                case "r": return 1;
                case "n": return 2;
                case "b": return 3;
                case "q": return 4;
                case "k": return 5;
                default:
                    return 0;
            }
        }

        void aiMakeMove(Move mv)
        {
            engine.BestMoveFound -= Engine_BestMoveFound;
            Program.LogInfo($"Selected move {mv.DebuggerDisplay}", $"AI-{Side}");
            ChessService.CurrentGame.InnerGame.make_move(mv);
            ChessService.CurrentGame.InnerGame.registerMoveMade(); // keeps track of actual proper moves made for repetition checking
            ChessService.CurrentGame.updateBoard();
            var jobj = new JObject();
            jobj["from"] = OtherGame.algebraic(mv.from).ToUpper();
            jobj["to"] = OtherGame.algebraic(mv.to).ToUpper();
            if (!string.IsNullOrWhiteSpace(mv.promotion))
                jobj["promote"] = simpleToComplex(mv.promotion);
            Broadcast(new ChessPacket(PacketId.MoveMade, jobj));
            CheckGameEnd();
        }

        AIEngine engine;
        public void MakeAIMove()
        {
            if (ChessService.CurrentGame.InnerGame.turn != Side.ToString()[0].ToString().ToLower())
                return;
            Thread.Sleep(ChessService.CurrentGame.AiDelay);
            if(checkingGameEnd)
            {
                finishedChecks.WaitOne();
            }
            if (ChessService.CurrentGame.HasEnded)
                return;

            engine = new AIEngine(this, ChessService.CurrentGame.InnerGame);
            engine.BestMoveFound += Engine_BestMoveFound;
            engine.Calculate();

            /*var moves = ChessService.CurrentGame.InnerGame.generate_moves(new Dictionary<string, string>()
                {
                    {"legal", "true" }
                });
            if (moves.Count == 0)
            {
                var opp = Side == PlayerSide.White ? ChessService.CurrentGame.Black : ChessService.CurrentGame.White;
                ChessService.CurrentGame.DeclareWinner(GameEndCondition.Win(opp, "AI checkmated"));
                return;
            }
            var mv = selectBestMove(moves);*/
        }

        private void Engine_BestMoveFound(object sender, BestMoveFoundEventArgs e)
        {
            var move = e.BestMove;
            if(move.HasValue)
            {
                aiMakeMove(move.Value);
            } else
            { // found nothing - checkmate.
                var opp = Side == PlayerSide.White ? ChessService.CurrentGame.Black : ChessService.CurrentGame.White;
                ChessService.CurrentGame.DeclareWinner(GameEndCondition.Win(opp, "AI checkmated"));
            }
        }

        void handlePacket(object obj)
        {
            if (!(obj is ChessPacket ping))
                return;
            if(ping.Id == PacketId.MoveMade)
            {
                MakeAIMove();
            }
        }

        public void recievePacket(ChessPacket p)
        {
            var th = new Thread(handlePacket);
            th.Start(p);
        }

        public override void Send(ChessPacket p)
        {
            recievePacket(p);
        }

        public void HasJoined()
        {
            doJoin();
        }
    }
}
#endif