using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.Games
{
    public class TTTService : Service
    {
        public List<TTTGame> Games { get; set; } = new List<TTTGame>();
        public override void OnReady()
        {
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
        }

        private async Task Client_UserVoiceStateUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if (arg1.IsBot || !(arg1 is SocketGuildUser user))
                return;
            if (!(TTTGame.IsTTTVoice(arg2.VoiceChannel) || TTTGame.IsTTTVoice(arg3.VoiceChannel)))
                return;
            if (!TTTGame.IsTTTVoice(arg3.VoiceChannel))
                return; // left the channel.
            var game = Games.FirstOrDefault(x => x.Contains(arg1));
            try
            {
                await Handle(game, user, arg2.VoiceChannel, arg3.VoiceChannel);
            } catch(Exception ex)
            {
                Program.LogMsg("VCTTT", ex);
            } finally
            {
                await user.ModifyAsync(x => x.Channel = arg2.VoiceChannel);
            }
        }

        async Task Handle(TTTGame game, SocketGuildUser user, SocketVoiceChannel before, SocketVoiceChannel gameVc)
        {
            if(game.Crosses == null)
            {
                game.Crosses = user;
            } else if (game.Naughts == null)
            {
                game.Naughts = user;
            }
            var position = int.Parse(gameVc.Name.Split('-')[1]);
            var player = game.Naughts.Id == user.Id ? Position.Naught : Position.Cross;
            if (!game.TryMove(position, player))
            {
                await user.SendMessageAsync($"That move is not valid.");
                return;
            }
            await game.Message.ModifyAsync(x => x.Embed = game.ToEmbed());
            var winner = game.GetWinner();
            if(winner != null)
            {
                await game.Message.Channel.SendMessageAsync($"{winner.Mention} has won!",
                    allowedMentions: AllowedMentions.None);
                Games.Remove(game);
            }

        }

    }

    public class TTTGame
    {
        public static bool IsTTTVoice(IVoiceChannel voice)
        {
            if (voice == null)
                return false;
            return voice.Name.StartsWith("ttt");
        }
        public SocketGuildUser Naughts { get; set; }
        public SocketGuildUser Crosses { get; set; }
        public IUserMessage Message { get; set; }
        public Position[] Board { get; set; }
        public Dictionary<int, string> Links { get; }
        public int Rows { get; }
        public TTTGame(SocketGuild guild, int rows = 3)
        {
            Links = new Dictionary<int, string>();
            var channels = guild.VoiceChannels.Where(x => x.Name.StartsWith("ttt-"));
            foreach(var channel in channels)
            {
                var split = channel.Name.Split('-');
                Links[int.Parse(split[1])] = "https://discord.gg/" + split[2];
            }
            Rows = rows;
            Board = new Position[rows * rows];
            for(int x = 0; x < Board.Length; x++)
            {
                Board[x] = Position.Empty;
            }
        }
        
        public bool Contains(IUser user) => Naughts?.Id == user.Id || Crosses?.Id == user.Id || false;
        public bool Started => Naughts != null && Crosses != null;

        public Position[,] Get2dBoard()
        {
            var board2d = new Position[Rows, Rows];
            for (int x = 0; x < Rows; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    board2d[x, y] = Board[x + y];
                }
            }
            return board2d;
        }

        public Position GetWinnerType()
        {
            var board2d = Get2dBoard();

            for(int x = 0; x < Rows; x++)
            {
                Position player = Position.Empty;
                for(int y = 0; y < Rows; y++)
                {
                    var pos = board2d[x, y];
                    if(pos != Position.Empty)
                    {
                        if(player == Position.Empty)
                        {
                            player = pos;
                        } else
                        {
                            player = Position.Empty;
                            break;
                        }
                    }
                }
                if (player != Position.Empty)
                    return player;
            }

            for (int y = 0; y < Rows; y++)
            {
                Position player = Position.Empty;
                for (int x = 0; x < Rows; x++)
                {
                    var pos = board2d[x, y];
                    if (pos != Position.Empty)
                    {
                        if (player == Position.Empty)
                        {
                            player = pos;
                        }
                        else
                        {
                            player = Position.Empty;
                            break;
                        }
                    }
                }
                if (player != Position.Empty)
                    return player;
            }

            Position diagonal = Position.Empty;
            for(int i = 0; i < Rows; i++)
            {
                var pos = board2d[i, i];
                if(pos != Position.Empty)
                {
                    if(diagonal == Position.Empty)
                    {
                        diagonal = pos;
                    } else
                    {
                        diagonal = Position.Empty;
                        break;
                    }
                }
            }
            if (diagonal != Position.Empty)
                return diagonal;
            for (int i = 0; i < Rows; i++)
            {
                var pos = board2d[Rows - (i+1), i];
                if (pos != Position.Empty)
                {
                    if (diagonal == Position.Empty)
                    {
                        diagonal = pos;
                    }
                    else
                    {
                        diagonal = Position.Empty;
                        break;
                    }
                }
            }
            return diagonal;
        }

        public SocketGuildUser GetWinner()
        {
            var type = GetWinnerType();
            if (type == Position.Empty)
                return null;
            return type == Position.Cross ? Crosses : Naughts;
        }

        string ToAscii(Position pos)
        {
            if (pos == Position.Cross)
                return "X";
            if (pos == Position.Naught)
                return "O";
            return " ";

        }
        public string GetAscii()
        {
            var sb = new StringBuilder();
            for(int i = 0; i < Board.Length; i++)
            {
                var chr = ToAscii(Board[i]);
                sb.Append(" ");
                sb.Append(chr);
                sb.Append(" ┃");
                if((i + 1) % Rows == 0)
                {
                    for (int x = 0; x < Rows - 1; x++)
                        sb.Append($"━━━╋");
                    sb.Append("━━━\r\n");
                }
            }
            return sb.ToString();
        }
        public override string ToString()
        {
            return GetAscii();
        }
        public bool TryMove(int position, Position player)
        {
            if (position >= Rows)
                return false;
            if (Board[position] != Position.Empty)
                return false;
            Board[position] = player;
            return true;
        }
        string getEmbed(Position pos, string link)
        {
            var sq = pos == Position.Empty
                ? "⬛"
                : pos == Position.Naught
                    ? "🟧" : "🟦";
            if(pos == Position.Empty)
                return $"[{sq}{sq}{sq}]({link})";
            return $"{sq}{sq}{sq}";
        }
        public Embed ToEmbed()
        {
            var builder = new EmbedBuilder();
            Func<IGuildUser, string> getName = x => x?.Nickname ?? x?.Username ?? "tbd";
            builder.Title = $"{getName(Naughts)} vs {getName(Crosses)}";
            var sb = new StringBuilder();
            var board2d = Get2dBoard();
            for(int y = 0; y < Rows; y++)
            {
                var rowSb = new StringBuilder();
                for(int x = 0; x < Rows; x++)
                {
                    var line = getEmbed(board2d[x, y], Links[x + y]);
                    rowSb.Append(line);
                    if (x != Rows - 1)
                        rowSb.Append("⬜");
                }
                sb.Append(rowSb.ToString() + "\r\n");
                sb.Append(rowSb.ToString() + "\r\n");
                sb.Append(rowSb.ToString() + "\r\n");
                if(y != Rows - 1)
                {
                    sb.Append(new string('⬜', (Rows * Rows) + (Rows - 1)));
                    sb.Append("\r\n");
                }
            }
            builder.Description = sb.ToString();
            var winner = GetWinnerType();
            builder.Color = winner == Position.Empty ? Color.Red
                : winner == Position.Naught ? Color.Orange : Color.Blue;
            return builder.Build();
        }
    }
    public enum Position
    {
        Empty,
        Naught,
        Cross
    }
}
