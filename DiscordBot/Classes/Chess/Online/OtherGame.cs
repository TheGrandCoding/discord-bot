/*
 * For this file only;
 * Material transcribed from chess.js and used in accordance to the following license:
 * 
 * Copyright (c) 2020, Jeff Hlywa (jhlywa@gmail.com)
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice,
 *    this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 *
 *----------------------------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Classes.Chess.Online
{
    public class OtherGame
    {
        public OtherGame(string fen = null)
        {
            fen = fen ?? DEFAULT_POSITION;
            load(fen);
        }
        public const string BLACK = "b";
        public const string WHITE = "w";

        static int EMPTY = -1;

        public const string PAWN = "p";
        public const string KNIGHT = "k";
        public const string BISHOP = "b";
        public const string ROOK = "r";
        public const string QUEEN = "q";
        public const string KING = "k";

        public int parseAlgebraic(string s)
        {
            if (s.Length != 2)
                throw new ArgumentException("Algebraic format must be 2 letters, eg A2 D5 etc.");
            s = s.ToLower();
            if(!Enum.TryParse<SQUARES>(s, out var r))
            {
                throw new ArgumentException("Algebraic format must be a A-G letter followed by a 1-8 number");
            }
            return (int)r;
        }

        const string SYMBOLS = "pnbrqkPNBRQK";

        public const string DEFAULT_POSITION = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        static readonly string[] POSSIBLE_RESULTS = new string[] { "1-0", "0-1", "1/2-1/2", "*" };

        static readonly Dictionary<string, int[]> PAWN_OFFSETS = new Dictionary<string, int[]>()
        {
            {"b", new int[] {16, 32, 17, 15} },
            {"w", new int[] {-16, -32, -17, -15} }
        };

        static readonly Dictionary<string, int[]> PIECE_OFFSETS = new Dictionary<string, int[]>()
        {
            { "n", new int[] {-18, -33, -31, -14, 18, 33, 31, 14}},
            { "b", new int[] {-17, -15, 17, 15}},
            { "r", new int[] {-16, 1, 16, -1}},
            { "q", new int[] {-17, -16, -15, 1, 17, 16, 15, -1}},
            { "k", new int[] {-17, -16, -15, 1, 17, 16, 15, -1}}
        };

        static readonly int[] ATTACKS = new int[]
        {
           20, 0, 0, 0, 0, 0, 0, 24,  0, 0, 0, 0, 0, 0,20, 0,
            0,20, 0, 0, 0, 0, 0, 24,  0, 0, 0, 0, 0,20, 0, 0,
            0, 0,20, 0, 0, 0, 0, 24,  0, 0, 0, 0,20, 0, 0, 0,
            0, 0, 0,20, 0, 0, 0, 24,  0, 0, 0,20, 0, 0, 0, 0,
            0, 0, 0, 0,20, 0, 0, 24,  0, 0,20, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,20, 2, 24,  2,20, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 2,53, 56, 53, 2, 0, 0, 0, 0, 0, 0,
           24,24,24,24,24,24,56,  0, 56,24,24,24,24,24,24, 0,
            0, 0, 0, 0, 0, 2,53, 56, 53, 2, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,20, 2, 24,  2,20, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0,20, 0, 0, 24,  0, 0,20, 0, 0, 0, 0, 0,
            0, 0, 0,20, 0, 0, 0, 24,  0, 0, 0,20, 0, 0, 0, 0,
            0, 0,20, 0, 0, 0, 0, 24,  0, 0, 0, 0,20, 0, 0, 0,
            0,20, 0, 0, 0, 0, 0, 24,  0, 0, 0, 0, 0,20, 0, 0,
           20, 0, 0, 0, 0, 0, 0, 24,  0, 0, 0, 0, 0, 0,20
        };

        static readonly int[] RAYS = new int[]
        {
         17,  0,  0,  0,  0,  0,  0, 16,  0,  0,  0,  0,  0,  0, 15, 0,
          0, 17,  0,  0,  0,  0,  0, 16,  0,  0,  0,  0,  0, 15,  0, 0,
          0,  0, 17,  0,  0,  0,  0, 16,  0,  0,  0,  0, 15,  0,  0, 0,
          0,  0,  0, 17,  0,  0,  0, 16,  0,  0,  0, 15,  0,  0,  0, 0,
          0,  0,  0,  0, 17,  0,  0, 16,  0,  0, 15,  0,  0,  0,  0, 0,
          0,  0,  0,  0,  0, 17,  0, 16,  0, 15,  0,  0,  0,  0,  0, 0,
          0,  0,  0,  0,  0,  0, 17, 16, 15,  0,  0,  0,  0,  0,  0, 0,
          1,  1,  1,  1,  1,  1,  1,  0, -1, -1,  -1,-1, -1, -1, -1, 0,
          0,  0,  0,  0,  0,  0,-15,-16,-17,  0,  0,  0,  0,  0,  0, 0,
          0,  0,  0,  0,  0,-15,  0,-16,  0,-17,  0,  0,  0,  0,  0, 0,
          0,  0,  0,  0,-15,  0,  0,-16,  0,  0,-17,  0,  0,  0,  0, 0,
          0,  0,  0,-15,  0,  0,  0,-16,  0,  0,  0,-17,  0,  0,  0, 0,
          0,  0,-15,  0,  0,  0,  0,-16,  0,  0,  0,  0,-17,  0,  0, 0,
          0,-15,  0,  0,  0,  0,  0,-16,  0,  0,  0,  0,  0,-17,  0, 0,
        -15,  0,  0,  0,  0,  0,  0,-16,  0,  0,  0,  0,  0,  0,-17
        };

        static readonly Dictionary<string, int> SHIFTS = new Dictionary<string, int>()
        {
            {"p", 0 }, {"n", 1}, {"b", 2}, {"r", 3}, {"q", 4}, {"k", 5}
        };

        public static class FLAGS
        {
            const char NORMAL = 'n';
            const char CAPTURE = 'c';
            const char BIG_PAWN = 'b';
            const char EP_CAPTURE = 'e';
            const char PROMOTION = 'p';
            const char KSIDE_CASTLE = 'k';
            const char QSIDE_CASTLE = 'q';
        }

        public enum BITS
        {
            NORMAL = 1,
            CAPTURE = 2,
            BIG_PAWN = 4,
            EP_CAPTURE = 8,
            PROMOTION = 16,
            KSIDE_CASTLE = 32,
            QSIDE_CASTLE = 64
        }

        const int RANK_1 = 7;
        const int RANK_2 = 6;
        const int RANK_3 = 5;
        const int RANK_4 = 4;
        const int RANK_5 = 3;
        const int RANK_6 = 2;
        const int RANK_7 = 1;
        const int RANK_8 = 0;

        public enum SQUARES
        {
            a8 = 0,
            b8 = 1,
            c8 = 2,
            d8 = 3,
            e8 = 4,
            f8 = 5,
            g8 = 6,
            h8 = 7,
            a7 = 16,
            b7 = 17,
            c7 = 18,
            d7 = 19,
            e7 = 20,
            f7 = 21,
            g7 = 22,
            h7 = 23,
            a6 = 32,
            b6 = 33,
            c6 = 34,
            d6 = 35,
            e6 = 36,
            f6 = 37,
            g6 = 38,
            h6 = 39,
            a5 = 48,
            b5 = 49,
            c5 = 50,
            d5 = 51,
            e5 = 52,
            f5 = 53,
            g5 = 54,
            h5 = 55,
            a4 = 64,
            b4 = 65,
            c4 = 66,
            d4 = 67,
            e4 = 68,
            f4 = 69,
            g4 = 70,
            h4 = 71,
            a3 = 80,
            b3 = 81,
            c3 = 82,
            d3 = 83,
            e3 = 84,
            f3 = 85,
            g3 = 86,
            h3 = 87,
            a2 = 96,
            b2 = 97,
            c2 = 98,
            d2 = 99,
            e2 = 100,
            f2 = 101,
            g2 = 102,
            h2 = 103,
            a1 = 112,
            b1 = 113,
            c1 = 114,
            d1 = 115,
            e1 = 116,
            f1 = 117,
            g1 = 118,
            h1 = 119
        }

        class RookSquare
        {
            public SQUARES square;
            public BITS flag;
            public RookSquare(SQUARES sq, BITS fg)
            {
                square = sq;
                flag = fg;
            }
        }

        static readonly Dictionary<string, RookSquare[]> ROOKS = new Dictionary<string, RookSquare[]>()
        {
            {"w", new RookSquare[]
            {
                new RookSquare(SQUARES.a1, BITS.QSIDE_CASTLE),
                new RookSquare(SQUARES.h1, BITS.KSIDE_CASTLE)
            } },
            {"b", new RookSquare[]
            {
                new RookSquare(SQUARES.a8, BITS.QSIDE_CASTLE),
                new RookSquare(SQUARES.h8, BITS.KSIDE_CASTLE)
            } }
        };

        class Piece
        {
            public string color;
            public string type;
            public Piece(string clr, string ty)
            {
                color = clr;
                type = ty;
            }
        
            public int get_relative_value()
            {
                if (type == PAWN)
                    return 10;
                if (type == KNIGHT || type == BISHOP)
                    return 30;
                if (type == ROOK)
                    return 50;
                if (type == QUEEN)
                    return 90;
                return 900;
            }
        
        }

        [DebuggerDisplay("{DebuggerDisplay,nq}")]
        public struct Move
        {
            public string color;
            public int from;
            public int to;
            public BITS flags;
            public string piece;
            public string promotion;
            public string captured;

            public string DebuggerDisplay {  get
                {
                    return $"{color} {piece}: {algebraic(from)} -> {algebraic(to)} {flags}";
                } }
        }

        class KingsState {
            public KingsState()
            {
                white = EMPTY;
                black = EMPTY;
            }
            int white;
            int black;
            public int this[string key]
            {
                get
                {
                    if (key == WHITE)
                        return white;
                    return black;
                } set
                {
                    if (key == WHITE)
                        white = value;
                    else
                        black = value;
                }
            }
            public KingsState Clone()
            {
                var ks = new KingsState();
                ks.white = white;
                ks.black = black;
                return ks;
            }
        }

        bool truthy(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            return true;
        }

        bool truthy(int i)
        {
            return i != 0;
        }

        bool truthy(object o)
        {
            if (o is int i)
                return truthy(i);
            if (o is string s)
                return truthy(s);
            throw new NotImplementedException($"Unknown type: {o.GetType().FullName}");
        }

        class CastleState : Dictionary<string, int>
        {
            public CastleState()
            {
                Add(WHITE, 0);
                Add(BLACK, 0);
            }
        }

        class HistoryState
        {
            public Move move;
            public KingsState kings;
            public string turn;
            public CastleState castling;
            public int ep_square;
            public int half_moves;
            public int move_number;
        }

        struct FenValidateResult
        {
            public bool Valid => string.IsNullOrWhiteSpace(Error);
            public string Error;
            public FenValidateResult(string e)
            {
                Error = e;
            }
        }

        Piece[] board = new Piece[128];
        KingsState kings = new KingsState();
        public string turn = WHITE;
        CastleState castling = new CastleState();
        int ep_square = EMPTY;
        public int half_moves = 0;
        public int move_number = 1;
        public Dictionary<string, int> positions = new Dictionary<string, int>();
        Stack<HistoryState> history = new Stack<HistoryState>();

        void clear()
        {
            board = new Piece[128];
            kings = new KingsState();
            turn = WHITE;
            castling = new CastleState();
            ep_square = EMPTY;
            half_moves = 0;
            move_number = 1;
            history = new Stack<HistoryState>();
            positions = new Dictionary<string, int>();
        }

        public bool load(string fen)
        {
            var tokens = Regex.Split(fen, @"\s+");
            var position = tokens[0];
            var square = 0;

            if(!validate_fen(fen).Valid)
            {
                return false;
            }

            clear();

            for(int i = 0; i < position.Length; i++)
            {
                var piece = position[i].ToString();
                if(piece == "/")
                {
                    square += 8;
                } else if (is_digit(piece))
                {
                    square += parseInt(piece, 10);
                } else
                {
                    var color = piece[0] < 'a' ? WHITE : BLACK;
                    string s = algebraic(square);
                    if(s.Length != 2)
                    {
                        square++;
                        continue;
                    }
                    put(new Piece(color, piece.ToLower()), (SQUARES)Enum.Parse(typeof(SQUARES), s));
                    square++;
                }
            }

            turn = tokens[1];
            if(tokens[2].Contains("K"))
            {
                castling[WHITE] |= (int)BITS.KSIDE_CASTLE;
            }
            if(tokens[2].Contains("Q"))
            {
                castling[WHITE] |= (int)BITS.QSIDE_CASTLE;
            }
            if(tokens[2].Contains("k"))
            {
                castling[BLACK] |= (int)BITS.KSIDE_CASTLE;
            }
            if(tokens[2].Contains("q"))
            {
                castling[BLACK] |= (int)BITS.QSIDE_CASTLE;
            }

            ep_square = tokens[3] == "-" ? EMPTY : parseAlgebraic(tokens[3]);
            half_moves = parseInt(tokens[4], 10);
            move_number = parseInt(tokens[5], 10);

            return true;
        }

        public void reset()
        {
            load(DEFAULT_POSITION);
        }

        int parseInt(string val, int valBase = 10)
        {
            return Convert.ToInt32(val, valBase);
        }

        bool isNaN(string item)
        {
            return !int.TryParse(item, out _);
        }

        bool isNaN(char chr)
        {
            return isNaN(new string(chr, 1));
        }

        bool isNaN(string[] arr, int index)
        {
            if (arr.Length <= index)
                return false;
            if (index < 0)
                return false;
            var item = arr[index];
            return isNaN(item);
        }

        FenValidateResult validate_fen(string fen)
        {
            var tokens = Regex.Split(fen, @"\s+");
            if (tokens.Length != 6)
                return new FenValidateResult("FEN string must have 6 fields");

            if (isNaN(tokens, 5) || parseInt(tokens[5], 10) <= 0)
                return new FenValidateResult("6th field must be a positive integer");

            if (isNaN(tokens, 4) || parseInt(tokens[4], 10) < 0)
                return new FenValidateResult("5th field most be >= 0");

            var rgx = new Regex(@"^(-|[abcdefgh][36])$");
            if (!rgx.IsMatch(tokens[3]))
                return new FenValidateResult("4th field is invalid.");

            rgx = new Regex(@"^(KQ?k?q?|Qk?q?|kq?|q|-)$");
            if (!rgx.IsMatch(tokens[2]))
                return new FenValidateResult("3rd field is invalid.");

            rgx = new Regex(@"^(w|b)$");
            if (!rgx.IsMatch(tokens[1]))
                return new FenValidateResult("2nd field is invalid");

            var rows = tokens[0].Split("/");
            if (rows.Length != 8)
                return new FenValidateResult("1st field must have 8 rows");

            for (int i = 0; i < rows.Length; i++)
            {
                var sum_fields = 0;
                var previous_was_number = false;
                for (var k = 0; k < rows[i].Length; k++)
                {
                    if (!isNaN(rows[i][k]))
                    {
                        if (previous_was_number)
                        {
                            return new FenValidateResult("1st field invalid (consecutive numbers)");
                        }
                        sum_fields += parseInt(rows[i][k].ToString(), 10);
                        previous_was_number = true;
                    } else
                    {
                        rgx = new Regex(@"^[prnbqkPRNBQK]$");
                        if (!rgx.IsMatch(rows[i][k].ToString()))
                        {
                            return new FenValidateResult("1st field invalid (invalid piece)");
                        }
                        sum_fields += 1;
                        previous_was_number = false;
                    }
                }
                if (sum_fields != 8)
                {
                    return new FenValidateResult("1st field invalid [row wrong length]");
                }
            }
            // This is broken; TODO: look into why.
            /*if (
              (tokens[3][1] == '3' && tokens[1] == "w") ||
              (tokens[3][1] == '6' && tokens[1] == "b")
            )
            {
                return new FenValidateResult("Illegal en-passant square");
            }*/
            return new FenValidateResult();
        }

        public string generate_fen()
        {
            var empty = 0;
            var fen = "";
            for (int i = (int)SQUARES.a8; i <= (int)SQUARES.h1; i++)
            {
                if (board[i] == null)
                {
                    empty++;
                } else
                {
                    if (empty > 0)
                    {
                        fen += empty;
                        empty = 0;
                    }
                    var clr = board[i].color;
                    var piece = board[i].type;
                    fen += clr == WHITE ? piece.ToUpper() : piece.ToLower();
                }

                if (((i + 1) & 0x88) != 0)
                {
                    if (empty > 0)
                    {
                        fen += empty;
                    }
                    if (i != (int)SQUARES.h1)
                    {
                        fen += "/";
                    }
                    empty = 0;
                    i += 8;
                }
            }

            var cflags = "";
            if ((castling[WHITE] ^ (int)BITS.KSIDE_CASTLE) != 0)
            {
                cflags += "K";
            }
            if ((castling[WHITE] ^ (int)BITS.QSIDE_CASTLE) != 0)
            {
                cflags += "Q";
            }
            if ((castling[BLACK] ^ (int)BITS.KSIDE_CASTLE) != 0)
            {
                cflags += "k";
            }
            if ((castling[BLACK] ^ (int)BITS.QSIDE_CASTLE) != 0)
                cflags += "q";

            if (cflags == "")
                cflags = "-";

            var epflags = "-";

            return string.Join(" ",
                new string[] { fen, turn, cflags, epflags, half_moves.ToString(), move_number.ToString() });
        }

        Piece get(SQUARES square)
        {
            var piece = board[(int)square];
            return piece == null ? null : new Piece(piece.color, piece.type);
        }

        bool put(Piece piece, SQUARES square)
        {
            if (SYMBOLS.Contains(piece.type) == false)
                return false;

            if (
              piece.type == KING &&
              !(kings[piece.color] == EMPTY || kings[piece.color] == (int)square)) {
                return false;
            }

            board[(int)square] = new Piece(piece.color, piece.type);
            if(piece.type == KING)
            {
                kings[piece.color] = (int)square;
            }

            return true;
        }

        Piece remove(SQUARES square)
        {
            var piece = get(square);
            board[(int)square] = null;
            if (piece != null && piece.type == KING)
                kings[piece.color] = EMPTY;
            return piece;
        }

        Move build_move(Piece[] board, int from, int to, BITS flags, string promotion = null)
        {
            var move = new Move()
            {
                color = turn,
                from = from,
                to = to,
                flags = flags,
                piece = board[from].type
            };
            if (!string.IsNullOrWhiteSpace(promotion))
            {
                move.flags |= BITS.PROMOTION;
                move.promotion = promotion;
            }
            if (board[to] != null)
            {
                move.captured = board[to].type;
            } else if (flags.HasFlag(BITS.EP_CAPTURE))
            {
                move.captured = PAWN;
            }
            return move;
        }
        public List<Move> generate_moves(Dictionary<string, string> options)
        {
            Action<Piece[], List<Move>, int, int, BITS> add_move = (Piece[] board, List<Move> mvs, int from, int to, BITS flags) =>
            {
                if (board[from].type == PAWN &&
                (rank(to) == RANK_8 || rank(to) == RANK_1))
                {
                    var pieces = new string[] { QUEEN, ROOK, BISHOP, KNIGHT };
                    foreach (var t in pieces)
                    {
                        mvs.Add(build_move(board, from, to, flags, t));
                    }
                }
                else
                {
                    mvs.Add(build_move(board, from, to, flags));
                }
            };
            var moves = new List<Move>();
            var us = turn;
            var them = swap_color(us);
            var second_rank = new Dictionary<string, int>()
            {
                {BLACK, RANK_7 }, {WHITE, RANK_2}
            };
            var first_sq = SQUARES.a8;
            var last_sq = SQUARES.h1;
            var single_square = false;

            var legal = bool.Parse(options.GetValueOrDefault("legal", "false"));

            if (options.TryGetValue("square", out var sqr))
            {
                if (Enum.TryParse<SQUARES>(sqr, out var square))
                {
                    first_sq = square;
                    last_sq = square;
                    single_square = true;
                } else
                {
                    return new List<Move>();
                }
            }

            for (int i = (int)first_sq; i <= (int)last_sq; i++)
            {
                if ((i & 0x88) != 0)
                {
                    i += 7;
                    continue;
                }

                var piece = board[i];
                if (piece == null || piece.color != us)
                    continue;

                if (piece.type == PAWN)
                {
                    var square = i + PAWN_OFFSETS[us][0];
                    if (board[square] == null)
                    {
                        add_move(board, moves, i, square, BITS.NORMAL);

                        square = i + PAWN_OFFSETS[us][1];
                        if (second_rank[us] == rank(i) && board[square] == null)
                        {
                            add_move(board, moves, i, square, BITS.BIG_PAWN);
                        }
                    }
                    for (int j = 2; j < 4; j++)
                    {
                        square = i + PAWN_OFFSETS[us][j];
                        if ((square & 0x88) != 0)
                            continue;

                        if (board[square] != null && board[square].color == them)
                        {
                            add_move(board, moves, i, square, BITS.CAPTURE);
                        } else if (square == ep_square)
                        {
                            add_move(board, moves, i, ep_square, BITS.EP_CAPTURE);
                        }
                    }
                } else
                {
                    int len = PIECE_OFFSETS[piece.type].Length;
                    for (var j = 0; j < len; j++)
                    {
                        var offset = PIECE_OFFSETS[piece.type][j];
                        var square = i;
                        while (true)
                        {
                            square += offset;
                            if ((square & 0x88) != 0) break;

                            if (board[square] == null)
                            {
                                add_move(board, moves, i, square, BITS.NORMAL);
                            }
                            else
                            {
                                if (board[square].color == us) break;
                                add_move(board, moves, i, square, BITS.CAPTURE);
                                break;
                            }

                            if (piece.type == "n" || piece.type == "k") break;
                        }
                    }
                }
            }

            if (!single_square || (int)last_sq == kings[us])
            {
                if ((castling[us] & (int)BITS.KSIDE_CASTLE) != 0)
                {
                    var castling_from = kings[us];
                    var castling_to = castling_from + 2;
                    if (
                        board[castling_from + 1] == null &&
                        board[castling_to] == null &&
                        !attacked(them, kings[us]) &&
                        !attacked(them, castling_from + 1) &&
                        !attacked(them, castling_to)
                        )
                    {
                        add_move(board, moves, kings[us], castling_to, BITS.KSIDE_CASTLE);
                    }
                }
                if ((castling[us] & (int)BITS.QSIDE_CASTLE) != 0)
                {
                    var castling_from = kings[us];
                    var castling_to = castling_from - 2;

                    if (
                          board[castling_from - 1] == null &&
                          board[castling_from - 2] == null &&
                          board[castling_from - 3] == null &&
                          !attacked(them, kings[us]) &&
                          !attacked(them, castling_from - 1) &&
                          !attacked(them, castling_to)
                        )
                    {
                        add_move(board, moves, kings[us], castling_to, BITS.QSIDE_CASTLE);
                    }
                }
            }

            // return quasi-legal moves
            // would allow kings to move into positions of capture
            if (!legal)
                return moves;

            // filter out ILLEGAL moves
            var legal_moves = new List<Move>();
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                //Program.LogMsg($"Testing {i} {move.DebuggerDisplay}", Discord.LogSeverity.Critical, $"CheckValids");
                make_move(move);
                if (!king_attacked(us))
                {
                    legal_moves.Add(move);
                    //Program.LogMsg($"Legal Move {i} {move.DebuggerDisplay}", Discord.LogSeverity.Critical, $"CheckValids");
                } /*else
                {
                    Program.LogMsg($"ILLEGAL MOVE {i} {move.DebuggerDisplay}", Discord.LogSeverity.Critical, $"CheckValids");
                }*/
                undo_move();
            }
            return legal_moves;
        }

        string move_to_san(Move move, bool sloppy)
        {
            var output = "";

            if (move.flags.HasFlag(BITS.KSIDE_CASTLE))
            {
                output = "O-O";
            } else if (move.flags.HasFlag(BITS.QSIDE_CASTLE))
            {
                output = "O-O-O";
            } else
            {
                var disambiguator = get_disambiguator(move, sloppy);
                if (move.piece != PAWN)
                {
                    output += move.piece.ToUpper() + disambiguator;
                }
                if (move.flags.HasFlag(BITS.CAPTURE | BITS.EP_CAPTURE))
                {
                    if (move.piece == PAWN)
                    {
                        output += algebraic(move.from)[0];
                    }
                    output += "x";
                }
                output += algebraic(move.to);
                if (move.flags.HasFlag(BITS.PROMOTION))
                {
                    output += '=' + move.promotion.ToUpper();
                }
            }

            make_move(move);
            if (in_check())
            {
                if (in_checkmate())
                {
                    output += "#";
                } else
                {
                    output += "+";
                }
            }
            undo_move();
            return output;
        }

        string stripped_san(string move)
        {
            return Regex.Replace(
                Regex.Replace(move, "=", ""),
                "[+#]?[?!]*$", "");
        }

        bool attacked(string color, int square)
        {
            var testing = algebraic(square);
            for (int i = (int)SQUARES.a8; i <= (int)SQUARES.h1; i++)
            {
                if ((i & 0x88) != 0)
                {
                    i += 7;
                    continue;
                }

                if (board[i] == null || board[i].color != color)
                    continue;

                var piece = board[i];
                var difference = i - square;
                var index = difference + 119;

                if ((ATTACKS[index] & (1 << SHIFTS[piece.type])) != 0)
                {
                    var attacker = algebraic(i);
                    //Program.LogMsg($"Possibly attacked from {attacker} ({i})", Discord.LogSeverity.Warning, testing);
                    if (piece.type == PAWN)
                    {
                        if (difference > 0)
                            if (piece.color == WHITE)
                                return true;
                            else
                                return false;
                        continue;
                    }

                    if (piece.type == "n" || piece.type == "k") return true;

                    var offset = RAYS[index];
                    var j = i + offset;

                    var blocked = false;
                    while (j != square)
                    {
                        var location = algebraic(j);
                        if (board[j] != null)
                        {
                            //Program.LogMsg($"{attacker}: Testing along {offset}, at {location} is blocked.", Discord.LogSeverity.Warning, testing);
                            blocked = true;
                            break;
                        }
                        //Program.LogMsg($"{attacker}: Testing along {offset}, at {location} is empty.", Discord.LogSeverity.Warning, testing);
                        j += offset;
                    }
                    if (!blocked)
                    {
                        //Program.LogMsg($"{attacker}: Valid attack along {offset}.", Discord.LogSeverity.Warning, testing);
                        return true;
                    }
                }
            }
            //Program.LogMsg($"Not attacked", Discord.LogSeverity.Warning, testing);
            return false;
        }

        bool king_attacked(string color)
        {
            var locInt = kings[color];
            //Program.LogMsg($"{color} king at {algebraic(locInt)} ({locInt})", Discord.LogSeverity.Info, "KingAtk");
            return attacked(swap_color(color), locInt);
        }

        bool in_check()
        {
            return king_attacked(turn);
        }

        public bool in_checkmate()
        {
            return in_check() && generate_moves(new Dictionary<string, string>()).Count == 0;
        }

        public bool in_stalemate()
        {
            return !in_check() && generate_moves(new Dictionary<string, string>()).Count == 0;
        }

        public bool insufficient_material()
        {
            var pieces = new Dictionary<string, int>();
            var bishops = new List<int>();
            var num_pieces = 0;
            var sq_color = 0;

            for (int i = (int)SQUARES.a8; i <= (int)SQUARES.h1; i++)
            {
                sq_color = (sq_color + 1) % 2;
                if ((i & 0x88) != 0)
                {
                    i += 7;
                    continue;
                }

                var piece = board[i];
                if (piece != null)
                {
                    pieces[piece.type] = pieces.GetValueOrDefault(piece.type, 0) + 1;
                    if (piece.type == BISHOP)
                        bishops.Add(sq_color);
                    num_pieces++;
                }
            }

            if (num_pieces == 2)
            {
                return true;
            } else if (
                num_pieces == 3 &&
                (pieces.GetValueOrDefault(BISHOP, 0) == 1 || pieces.GetValueOrDefault(KNIGHT, 0) == 1)
                )
            {
                return true;
            } else if (num_pieces == pieces.GetValueOrDefault(BISHOP, 0) + 2)
            {
                var sum = 0;
                var len = bishops.Count;
                for (int i = 0; i < len; i++)
                {
                    sum += bishops[i];
                }
                if (sum == 0 || sum == len)
                    return true;
            }
            return false;
        }

        string getRepetitionFEN()
        {
            var fenSplit = generate_fen()
                .Split(' ')
                .Take(4);
            return string.Join(" ", fenSplit);
        }

        public void registerMoveMade()
        {
            var fen = getRepetitionFEN();
            positions[fen] = positions.GetValueOrDefault(fen, 0) + 1;
        }

        public bool in_threefold_repetition()
        {
            foreach(var keypair in positions)
            {
                if (keypair.Value >= 3)
                    return true;
            }
            return false;
        }

        void push(Move move)
        {
            history.Push(new HistoryState()
            {
                move = move,
                kings = kings.Clone(),
                turn = turn,
                castling = castling,
                ep_square = ep_square,
                half_moves = half_moves,
                move_number = move_number
            });
            /*Program.LogMsg($"Pushed state now [{string.Join(", ", history.Select(x => $"{algebraic(x.move.from)}{algebraic(x.move.to)}"))}]\r\n" +
                $"Move: {move.DebuggerDisplay}\r\n" +
                $"Kings: {kings[WHITE]} {kings[BLACK]}\r\n" +
                $"Turn: {turn}\r\n" +
                $"Castling: {castling[WHITE]} {castling[BLACK]}\r\n" +
                $"EpSqr: {ep_square}\r\n" +
                $"Half: {half_moves}\r\n" +
                $"MoveNum: {move_number}\r\n" +
                $"Fen: {generate_fen()}", Discord.LogSeverity.Debug, "Push");*/
        }

        public void make_move(Move move)
        {
            var us = turn;
            var them = swap_color(us);
            push(move);

            board[move.to] = board[move.from];
            board[move.from] = null;

            if (move.flags.HasFlag(BITS.EP_CAPTURE))
            {
                if (turn == BLACK)
                {
                    board[move.to - 16] = null;
                } else
                {
                    board[move.to + 16] = null;
                }
            }

            if (move.flags.HasFlag(BITS.PROMOTION))
            {
                board[move.to] = new Piece(us, move.promotion);
            }

            if (board[move.to].type == KING)
            {
                kings[board[move.to].color] = move.to;

                if (move.flags.HasFlag(BITS.KSIDE_CASTLE))
                {
                    var castling_to = move.to - 1;
                    var castling_from = move.to + 1;
                    board[castling_to] = board[castling_from];
                    board[castling_from] = null;
                } else if (move.flags.HasFlag(BITS.QSIDE_CASTLE))
                {
                    var castling_to = move.to + 1;
                    var castling_from = move.to - 2;
                    board[castling_to] = board[castling_from];
                    board[castling_from] = null;
                }

                castling[us] = 0;
            }
            if (truthy(castling[us]))
            {
                for (int i = 0; i < ROOKS[us].Length; i++)
                {
                    if (
                        move.from == (int)ROOKS[us][i].square &&
                        ((castling[us] & (int)ROOKS[us][i].flag) != 0)
                    )
                    {
                        castling[us] ^= (int)ROOKS[us][i].flag;
                        break;
                    }
                }
            }

            if (truthy(castling[them]))
            {
                for (int i = 0; i < ROOKS[them].Length; i++)
                {
                    if (
                        move.to == (int)ROOKS[them][i].square &&
                        ((castling[them] & (int)ROOKS[them][i].flag) != 0)
                    )
                    {
                        castling[them] ^= (int)ROOKS[them][i].flag;
                        break;
                    }
                }
            }

            if (move.flags.HasFlag(BITS.BIG_PAWN))
            {
                if (turn == BLACK)
                {
                    ep_square = move.to - 16;
                } else
                {
                    ep_square = move.to + 16;
                }
            } else
            {
                ep_square = EMPTY;
            }

            if (move.piece == PAWN)
            {
                half_moves = 0;
            } else if (move.flags.HasFlag(BITS.CAPTURE | BITS.EP_CAPTURE))
            {
                half_moves = 0;
            } else
            {
                half_moves++;
            }

            if (turn == BLACK)
            {
                move_number++;
            }
            turn = swap_color(turn);
        }

        public Move? undo_move()
        {
            var old = history.Count == 0 ? null : history.Pop();
            if (old == null)
            {
                Program.LogDebug("Reverted a null state", "UndoMove");
                return null;
            }

            var move = old.move;
            kings = old.kings;
            turn = old.turn;
            castling = old.castling;
            ep_square = old.ep_square;
            half_moves = old.half_moves;
            move_number = old.move_number;

            var us = turn;
            var them = swap_color(us);

            board[move.from] = board[move.to];
            board[move.from].type = move.piece;
            board[move.to] = null;

            if (move.flags.HasFlag(BITS.CAPTURE))
            {
                board[move.to] = new Piece(them, move.captured);
            } else if (move.flags.HasFlag(BITS.EP_CAPTURE))
            {
                int index = us == BLACK ? move.to - 16 : move.to + 16;
                board[index] = new Piece(them, PAWN);
            }

            if (move.flags.HasFlag(BITS.KSIDE_CASTLE | BITS.QSIDE_CASTLE))
            {
                int castling_to, castling_from;
                if (move.flags.HasFlag(BITS.KSIDE_CASTLE))
                {
                    castling_to = move.to + 1;
                    castling_from = move.to - 1;
                } else //if (move.flags.HasFlag(BITS.QSIDE_CASTLE))
                {
                    castling_to = move.to - 2;
                    castling_from = move.to + 1;
                }

                board[castling_to] = board[castling_from];
                board[castling_from] = null;
            }
            /*Program.LogMsg($"Reverted {move.DebuggerDisplay} to [{string.Join(", ", history.Select(x => $"{algebraic(x.move.from)}{algebraic(x.move.to)}"))}]\r\n" +
                $"Kings: {kings[WHITE]} {kings[BLACK]}\r\n" +
                $"Turn: {turn}\r\n" +
                $"Castling: {castling[WHITE]} {castling[BLACK]}\r\n" +
                $"EpSqr: {ep_square}\r\n" +
                $"Half: {half_moves}\r\n" +
                $"MoveNum: {move_number}\r\n" +
                $"Fen: {generate_fen()}", Discord.LogSeverity.Debug, "Push");*/
            return move;
        }

        string get_disambiguator(Move move, bool sloppy)
        {
            var moves = generate_moves(new Dictionary<string, string>()
            {
                {"legal", (!sloppy).ToString() }
            });
            var from = move.from;
            var to = move.to;
            var piece = move.piece;

            var ambiguities = 0;
            var same_rank = 0;
            var same_file = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                var ambig_from = moves[i].from;
                var ambig_to = moves[i].to;
                var ambig_piece = moves[i].piece;

                if (piece == ambig_piece && from != ambig_from && to == ambig_to)
                {
                    ambiguities++;
                    if (rank(from) == rank(ambig_from))
                        same_rank++;
                    if (file(from) == file(ambig_from))
                        same_file++;
                }
            }
            if (ambiguities > 0)
            {
                if (same_rank > 0 && same_file > 0)
                {
                    return algebraic(from);
                } else if (same_file > 0)
                {
                    return algebraic(from).Substring(1, 1);
                } else
                {
                    return algebraic(from).Substring(0, 1);
                }
            }
            return "";
        }

        public string ascii()
        {
            var s = "   +------------------------+\n";
            for (int i = (int)SQUARES.a8; i <= (int)SQUARES.h1; i++)
            {
                if (file(i) == 0)
                {
                    s += " " + "87654321"[rank(i)] + " |";
                }
                if (board[i] == null)
                {
                    s += " . ";
                } else
                {
                    var piece = board[i].type;
                    var color = board[i].color;
                    var symbol = color == WHITE ? piece.ToUpper() : piece.ToLower();
                    s += " " + symbol + " ";
                }

                if(((i + 1) & 0x88) != 0)
                {
                    s += "|\n";
                    i += 8;
                }
            }
            s += "   +------------------------+\n";
            s += "     a  b  c  d  e  f  g  h\n";
            return s;
        }

        // Utility
        public static int rank(int i) => i >> 4;

        public static int file(int i) => i & 15;

        public static string algebraic(int i)
        {
            var f = file(i);
            var r = rank(i);
            if(f > 7 || r > 7)
            { // return hex representation because something went wrong
                return i.ToString("X");
            }
            return "abcdefgh".Substring(f, 1) + "87654321".Substring(r, 1);
        }

        public string swap_color(string c)
        {
            return c == WHITE ? BLACK : WHITE;
        }

        bool is_digit(string c)
        {
            return "0123456789".Contains(c);
        }

        double[,] PAWN_VALUES = new double[,]
        {
            { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
            { 5.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0 },
            { 1.0, 1.0, 2.0, 3.0, 3.0, 2.0, 1.0, 1.0 },
            { 0.5, 0.5, 1.0, 2.5, 2.5, 1.0, 0.5, 0.5 },
            { 0.0, 0.0, 0.0, 2.0, 2.0, 0.0, 0.0, 0.0 },
            { 0.5, -0.5, -1.0, 0.0, 0.0, -1.0, -0.5, 0.5 },
            { 0.5, 1.0, 1.0, -2.0, -2.0, 1.0, 1.0, 0.5 },
            { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }
        };

        double[,] BLACK_PAWN_VALUES = new double[,]
        {
            { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },// 8
            { 0.5, 1.0, 1.0, -2.0, -2.0, 1.0, 1.0, 0.5 },//7
            { 0.5, -0.5, -1.0, 0.0, 0.0, -1.0, -0.5, 0.5 },//6
            { 0.0, 0.0, 0.0, 2.0, 2.0, 0.0, 0.0, 0.0 },//5
            { 0.5, 0.5, 1.0, 2.5, 2.5, 1.0, 0.5, 0.5 },//4
            { 1.0, 1.0, 2.0, 3.0, 3.0, 2.0, 1.0, 1.0 },//3
            { 5.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0 },// 2
            { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }, //1
        };

        double[,] KNIGHT_VALUES = new double[,]
        {
            {-5.0, -4.0, -3.0, -3.0, -3.0, -3.0, -4.0, -5.0},
            {-4.0, -2.0,  0.0,  0.0,  0.0,  0.0, -2.0, -4.0},
            {-3.0,  0.0,  1.0,  1.5,  1.5,  1.0,  0.0, -3.0},
            {-3.0,  0.5,  1.5,  2.0,  2.0,  1.5,  0.5, -3.0},
            {-3.0,  0.0,  1.5,  2.0,  2.0,  1.5,  0.0, -3.0},
            {-3.0,  0.5,  1.0,  1.5,  1.5,  1.0,  0.5, -3.0},
            {-4.0, -2.0,  0.0,  0.5,  0.5,  0.0, -2.0, -4.0},
            {-5.0, -4.0, -3.0, -3.0, -3.0, -3.0, -4.0, -5.0 }
        };

        double[,] BLACK_KNIGHT_VALUES = new double[,]
        {
            {-5.0, -4.0, -3.0, -3.0, -3.0, -3.0, -4.0, -5.0 },//8
            {-4.0, -2.0,  0.0,  0.5,  0.5,  0.0, -2.0, -4.0},//7
            {-3.0,  0.5,  1.0,  1.5,  1.5,  1.0,  0.5, -3.0},//6
            {-3.0,  0.0,  1.5,  2.0,  2.0,  1.5,  0.0, -3.0},//5
            {-3.0,  0.5,  1.5,  2.0,  2.0,  1.5,  0.5, -3.0},//4
            {-3.0,  0.0,  1.0,  1.5,  1.5,  1.0,  0.0, -3.0},//3
            {-4.0, -2.0,  0.0,  0.0,  0.0,  0.0, -2.0, -4.0},//2
            {-5.0, -4.0, -3.0, -3.0, -3.0, -3.0, -4.0, -5.0},//1
        };

        double[,] BISHOP_VALUES = new double[,]
        {
            { -2.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -2.0},
            { -1.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -1.0},
            { -1.0,  0.0,  0.5,  1.0,  1.0,  0.5,  0.0, -1.0},
            { -1.0,  0.5,  0.5,  1.0,  1.0,  0.5,  0.5, -1.0},
            { -1.0,  0.0,  1.0,  1.0,  1.0,  1.0,  0.0, -1.0},
            { -1.0,  1.0,  1.0,  1.0,  1.0,  1.0,  1.0, -1.0},
            { -1.0,  0.5,  0.0,  0.0,  0.0,  0.0,  0.5, -1.0},
            { -2.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -2.0}
        };

        double[,] BLACK_BISHOP_VALUES = new double[,]
        {
            { -2.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -2.0},//8
            { -1.0,  0.5,  0.0,  0.0,  0.0,  0.0,  0.5, -1.0},//7
            { -1.0,  1.0,  1.0,  1.0,  1.0,  1.0,  1.0, -1.0},//6
            { -1.0,  0.0,  1.0,  1.0,  1.0,  1.0,  0.0, -1.0},//5
            { -1.0,  0.5,  0.5,  1.0,  1.0,  0.5,  0.5, -1.0},//4
            { -1.0,  0.0,  0.5,  1.0,  1.0,  0.5,  0.0, -1.0},//3
            { -1.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -1.0},//2
            { -2.0, -1.0, -1.0, -1.0, -1.0, -1.0, -1.0, -2.0},//1
        };

        double[,] ROOK_VALUES = new double[,]
        {
            {  0.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0},
            {  0.5,  1.0,  1.0,  1.0,  1.0,  1.0,  1.0,  0.5},
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},
            {  0.0,   0.0, 0.0,  0.5,  0.5,  0.0,  0.0,  0.0}
        };

        double[,] BLACK_ROOK_VALUES = new double[,]
        {
            {  0.0,   0.0, 0.0,  0.5,  0.5,  0.0,  0.0,  0.0},//8
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},//7
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},//6
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},//5
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},//4
            { -0.5,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -0.5},//3
            {  0.5,  1.0,  1.0,  1.0,  1.0,  1.0,  1.0,  0.5},//2
            {  0.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0},//1
        };

        double[,] QUEEN_VALUES = new double[,]
        {
            { -2.0, -1.0, -1.0, -0.5, -0.5, -1.0, -1.0, -2.0},
            { -1.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -1.0},
            { -1.0,  0.0,  0.5,  0.5,  0.5,  0.5,  0.0, -1.0},
            { -0.5,  0.0,  0.5,  0.5,  0.5,  0.5,  0.0, -0.5},
            {  0.0,  0.0,  0.5,  0.5,  0.5,  0.5,  0.0, -0.5},
            { -1.0,  0.5,  0.5,  0.5,  0.5,  0.5,  0.0, -1.0},
            { -1.0,  0.0,  0.5,  0.0,  0.0,  0.0,  0.0, -1.0},
            { -2.0, -1.0, -1.0, -0.5, -0.5, -1.0, -1.0, -2.0}
        };

        double[,] BLACK_QUEEN_VALUES = new double[,]
        {
            { -2.0, -1.0, -1.0, -0.5, -0.5, -1.0, -1.0, -2.0},//8
            { -1.0,  0.0,  0.5,  0.0,  0.0,  0.0,  0.0, -1.0},//7
            { -1.0,  0.5,  0.5,  0.5,  0.5,  0.5,  0.0, -1.0},//6
            {  0.0,  0.0,  0.5,  0.5,  0.5,  0.5,  0.0, -0.5},//5
            { -0.5,  0.0,  0.5,  0.5,  0.5,  0.5,  0.0, -0.5},//4
            { -1.0,  0.0,  0.5,  0.5,  0.5,  0.5,  0.0, -1.0},//3
            { -1.0,  0.0,  0.0,  0.0,  0.0,  0.0,  0.0, -1.0},//2
            { -2.0, -1.0, -1.0, -0.5, -0.5, -1.0, -1.0, -2.0},//1
        };

        double[,] KING_VALUES = new double[,]
        {
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},
            { -2.0, -3.0, -3.0, -4.0, -4.0, -3.0, -3.0, -2.0},
            { -1.0, -2.0, -2.0, -2.0, -2.0, -2.0, -2.0, -1.0},
            {  2.0,  2.0,  0.0,  0.0,  0.0,  0.0,  2.0,  2.0 },
            {  2.0,  3.0,  1.0,  0.0,  0.0,  1.0,  3.0,  2.0 }
        };

        double[,] BLACK_KING_VALUES = new double[,]
        {
            {  2.0,  3.0,  1.0,  0.0,  0.0,  1.0,  3.0,  2.0 },//8
            {  2.0,  2.0,  0.0,  0.0,  0.0,  0.0,  2.0,  2.0 },//7
            { -1.0, -2.0, -2.0, -2.0, -2.0, -2.0, -2.0, -1.0},//6
            { -2.0, -3.0, -3.0, -4.0, -4.0, -3.0, -3.0, -2.0},//5
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},//4
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},//3
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},//2
            { -3.0, -4.0, -4.0, -5.0, -5.0, -4.0, -4.0, -3.0},//1
        };

        double get_location_value(Piece piece, int square)
        {
            double[,] arr;
            var sqR = rank(square);
            var sqF = file(square);
            if(piece.type == PAWN)
            {
                arr = piece.color == "w" ? PAWN_VALUES : BLACK_PAWN_VALUES;
            } else if (piece.type == KNIGHT)
            {
                arr = piece.color == "w" ? KNIGHT_VALUES : BLACK_KNIGHT_VALUES;

            } else if (piece.type == BISHOP)
            {
                arr = piece.color == "w" ? BISHOP_VALUES : BLACK_BISHOP_VALUES;
            } else if (piece.type == ROOK)
            {
                arr = piece.color == "w" ? ROOK_VALUES : BLACK_ROOK_VALUES;
            } else if (piece.type == QUEEN)
            {
                arr = piece.color == "w" ? QUEEN_VALUES : BLACK_QUEEN_VALUES;
            } else
            {
                arr = piece.color == "w" ? KING_VALUES : BLACK_KING_VALUES;
            }
            return arr[sqR, sqF];
        }

        double get_piece_value(Piece piece, int square)
        {
            double value = piece.get_relative_value() + get_location_value(piece, square);
            return piece.color == WHITE ? value : -value;
        }

        public double get_board_value()
        {
            double TOTAL = 0;
            for (int i = (int)SQUARES.a8; i <= (int)SQUARES.h1; i++)
            {
                if((i & 0x88) != 0)
                {
                    i += 7;
                    continue;
                }
                var piece = board[i];
                if (piece == null)
                    continue;
                TOTAL += get_piece_value(piece, i);
            }
            return TOTAL;
        }
    }
}
