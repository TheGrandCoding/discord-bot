/*
The MIT License (MIT)

Copyright (c) 2014 Aaron Mell

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

#if INCLUDE_CHESS
using DiscordBot.Classes.Chess.Online;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using static DiscordBot.Classes.Chess.Online.OtherGame;

namespace DiscordBot.Classes.Chess
{
    public class AIEngine
    {
        OtherGame game;
        ChessAIPlayer player;
        public AIEngine(ChessAIPlayer p, OtherGame g)
        {
            CalculateToDepth = p.RunDepth;
            player = p;
            game = g;
            _principalVariation = new Move[100];
        }

        internal event EventHandler<BestMoveFoundEventArgs> BestMoveFound;

        private debugLogger _logger = new debugLogger();
        private const int CheckMateScore = 10001;
        private bool _stopRaised;
        private int _timeToMove;
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private int _nodeCount;
        private int _checkTimeInterval;

        private Dictionary<ulong, Tuple<int, int, Move, int>> _transpositionTable;

#region Internal Properties
        /// <summary>
        /// If set to true, continue to calculate until stop has been issued by the program
        /// </summary>
        internal bool InfiniteTime { get; set; } = false;

        /// <summary>
        /// If set to a positive value, calculate the current position to this depth.
        /// </summary>
        internal int CalculateToDepth { get; set; }

        /// <summary>
        /// If set to a positive value, this amount of time in ms is allowed to make the move
        /// </summary>
        internal int MoveTime { get; set; } = 10 * 1000;

        /// <summary>
        /// If set to a positive value, search for the mate in this number of moves. 
        /// </summary>
        internal int MateDepth { get; set; }

        /// <summary>
        /// If set to a positive value, search a maximum of this number of nodes
        /// </summary>
        internal int MaxNodes { get; set; }

        /// <summary>
        /// If set to a positive value, this is the amount of time in mswhite has remaining
        /// </summary>
        internal int WhiteTime { get; set; }

        /// <summary>
        /// If set to a positive value, this is the amount of time in ms black has remaining
        /// </summary>
        internal int BlackTime { get; set; }

        /// <summary>
        /// If set to a positive value, this is the amount of time in ms added to each white move.
        /// </summary>
        internal int WhiteIncrementTime { get; set; }

        /// <summary>
        /// If set to a positive value, this is the amount of time in ms added to each black move
        /// </summary>
        internal int BlackIncrementTime { get; set; }

        /// <summary>
        /// If set to a positive value, this is the number of moves remaining until the time controls change. 
        /// </summary>
        internal int MovesUntilNextTimeControl { get; set; }
#endregion

        private Move[] _principalVariation;

        internal void Calculate()
        {
            _stopRaised = false;
            _stopwatch.Restart();
            _nodeCount = 0;
            _checkTimeInterval = 100000;

            _logger.InfoFormat("Entered calculate");
            Task.Factory.StartNew(() =>
            {
                _logger.InfoFormat("Task started, searching moves");
                Move? bestMove = null;
                var _moveData = game.generate_moves(new Dictionary<string, string>()
                {
                    {"legal", "true" }
                });
                int legalMoves = _moveData.Count;
                _logger.InfoFormat($"Found {legalMoves} legal moves.");
                if (legalMoves == 1)
                {
                    bestMove = _moveData[0];
                    _logger.InfoFormat($"Forced to move {bestMove.Value.DebuggerDisplay}");
                }
                else if (legalMoves == 0)
                {
                    _logger.InfoFormat("No legal moves found");
                    bestMove = null;
                }
                else if (legalMoves > 1)
                {
                    int maxDepth = GetMaxDepth();
                    _logger.InfoFormat($"Max depth is {maxDepth}");
                    SetTimeToMove();
                    _logger.InfoFormat($"Time permitted for move: {_timeToMove}");
                    var depthSearched = 0;
                    for (var currentDepth = 1; currentDepth <= maxDepth; currentDepth++)
                    {
                        depthSearched++;
                        var searchResult = new SearchResult();
                        _logger.InfoFormat($"Starting search at depth {currentDepth}");
                        NegaMaxAlphaBetaRecursive(searchResult, 0, currentDepth, int.MinValue + 1, int.MaxValue - 1, player.Side == PlayerSide.White);
                        _principalVariation[0] = searchResult.Move.GetValueOrDefault();
                        _logger.InfoFormat($"info depth {currentDepth} cp {searchResult.Score} time {_stopwatch.ElapsedMilliseconds}");
                        if (searchResult.Score > CheckMateScore - currentDepth ||
                            searchResult.Score < -(CheckMateScore - currentDepth))
                        {
                            bestMove = searchResult.Move.Value;
                            break;
                        }

                        if (_stopRaised || IsTimeUp() || IsNodeCountExceeded())
                        {
                            _logger.InfoFormat("Exiting because end conditions met");
                            if (bestMove.HasValue == false)
                                bestMove = searchResult.Move;

                            break;
                        }
                        bestMove = searchResult.Move;
                    }
                    _logger.InfoFormat("Looped all moves.");
                    Array.Clear(_principalVariation, 0, depthSearched + 1);
                }
                OnBestMoveFound(new BestMoveFoundEventArgs
                {
                    BestMove = bestMove
                });
                _stopwatch.Stop();
            }).ContinueWith(task =>
            {
                Debug.Assert(task.Exception != null);

                _logger.ErrorFormat($"Exception Occured while caclulating. Exception: {task.Exception.InnerException}");

                _stopwatch.Stop();
            }, TaskContinuationOptions.OnlyOnFaulted);
        }


        protected virtual void OnBestMoveFound(BestMoveFoundEventArgs e)
        {
            var handler = BestMoveFound;

            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void NegaMaxAlphaBetaRecursive(SearchResult searchResult, int ply, int depth, int alpha, int beta, bool side)
        {
            if (depth <= 0)
            {
                _nodeCount++;
                searchResult.Score = (int)(game.get_board_value() * (side ? 1 : -1));
                return;
            }
            if (game.in_threefold_repetition())
            {
                searchResult.Score = 0;
                return;
            }

            var bestValue = int.MinValue + 1;
            Move? bestMove = null;
            var alphaOriginal = alpha;
            var movesFound = 0;
            var moves = game.generate_moves(new Dictionary<string, string>()
            {
                {"legal", "true" }
            });
            foreach (var move in moves)
            {
                game.make_move(move);
                movesFound++;
                NegaMaxAlphaBetaRecursive(searchResult, ply + 1, depth - 1, -beta, -alpha, !side);
                var value = -(searchResult.Score);
                game.undo_move();
                if (--_checkTimeInterval < 0 && IsTimeUp())
                {
                    searchResult.Score = 0;
                    return;
                }

                if (value > bestValue)
                {
                    bestValue = value;
                    bestMove = move;
                }

                if (value > alpha)
                {
                    alpha = value;
                    _principalVariation[ply] = bestMove.Value;
                }

                if (alpha >= beta)
                    break;
            }

            if(game.half_moves >= 100)
            {
                bestValue = 0;
                bestMove = null;
            } else if (movesFound != 0)
            {

            } else if (game.in_checkmate())
            {
                searchResult.Move = null;
                searchResult.Score = (side ? 1 : -1) * (CheckMateScore - ply);
            } else
            {
                bestValue = 0;
                bestMove = null;
            }

            var flag = 0;
            if(bestValue <= alphaOriginal)
            {
                flag = 2;
            } else if (bestValue >= beta)
            {
                flag = 1;
            }

            searchResult.Move = bestMove;
            searchResult.Score = bestValue;
        }

        private void SetTimeToMove()
        {
            if (InfiniteTime || MaxNodes > 0 || MateDepth > 0 || CalculateToDepth > 0)
            {
                _timeToMove = int.MaxValue;
                return;
            }

            if (MoveTime > 0)
            {
                _timeToMove = MoveTime;
                return;
            }

            var remainingMoves = 80 - game.move_number;

            if (remainingMoves < 20)
                remainingMoves = 20;

            var remainingTime = game.turn == "w" ? WhiteTime + WhiteIncrementTime : BlackTime + BlackIncrementTime;

            _timeToMove = remainingTime / remainingMoves;
        }

        internal int GetMaxDepth()
        {
            if (CalculateToDepth > 0)
                return CalculateToDepth;
            if (MateDepth > 0)
                return MateDepth;

            return InfiniteTime ? int.MaxValue : 16;
        }

        private bool IsNodeCountExceeded()
        {
            if (MaxNodes > 0)
                return _nodeCount > MaxNodes;

            return false;
        }
        private bool IsTimeUp()
        {
            if (_stopwatch.ElapsedMilliseconds > _timeToMove)
            {
                return true;
            }

            _checkTimeInterval = 100000;
            return false;
        }
    }

    internal class SearchResult
    {
        internal Move? Move { get; set; }
        internal int Score { get; set; }
    }

    public class BestMoveFoundEventArgs : EventArgs
    {
        public Move? BestMove { get; set; }
    }

    internal class debugLogger
    {
        private void Log(string s)
        {
            Console.BackgroundColor = ConsoleColor.Green;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[{DateTime.Now.ToString("hh:mm:ss.fff")}] {s}");
            Console.BackgroundColor = ConsoleColor.Black;
        }
        internal void InfoFormat(string s) => Log("[INFO] " + s);
        internal void ErrorFormat(string s) => Log("[ERROR] " + s);
    }
}
#endif