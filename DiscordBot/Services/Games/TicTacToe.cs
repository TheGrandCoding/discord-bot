using Discord;
using Discord.WebSocket;
using DiscordBot.Utils;
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
        public const string RoleName = "TicTacToe";
        public override void OnReady()
        {
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            Program.Client.ReactionAdded += Client_ReactionAdded;
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        int getCoord(string s)
        {
            switch (s.ToLower()) 
            {
                case "1":
                case "a": return 1;
                case "2":
                case "b": return 2;
                case "3":
                case "c": return 3;
                default: return -1;
            }
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            if (arg.Content.Length != 2)
                return;
            (int x, int y) = (getCoord(arg.Content.Substring(0, 1)), getCoord(arg.Content.Substring(1, 1)));
            if (x == -1 || y == -1)
                return;
            var selected = Games.FirstOrDefault(x => x.Message.Channel.Id == arg.Channel.Id);
            if (selected == null)
                return;
            await Handle(selected, arg.Author as SocketGuildUser, (x, y));
        }

        private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var game = Games.FirstOrDefault(x => x.Message.Id == arg1.Id);
            if (game == null)
                return;
            var user = game.Guild.GetUser(arg3.UserId);
            if (user.IsBot)
                return;
            await game.Message.RemoveReactionAsync(arg3.Emote, arg3.UserId);
            if (arg3.Emote.Name == Emotes.WHITE_CHECK_MARK.Name)
                await newPlayerTryJoin(user, game);
            else if (arg3.Emote.Name == Emotes.ARROWS_COUNTERCLOCKWISE.Name)
                await resendMessage(game);
        }


        async Task resendMessage(TTTGame game)
        {
            var channel = game.Message.Channel;
            var newMsg = await channel.SendMessageAsync(embed: game.ToEmbed());
            await game.Message.DeleteAsync();
            game.Message = newMsg;
        }

        async Task newPlayerTryJoin(SocketGuildUser user, TTTGame game)
        {
            if (game.GetPlayer(user) != Position.Empty)
                return;
            if (game.Crosses == null)
                game.Crosses = user;
            else
                game.Naughts = user;
            await game.Message.ModifyAsync(x => x.Embed = game.ToEmbed());
            var role = game.Guild.Roles.FirstOrDefault(x => x.Name == TTTService.RoleName);
            await user.AddRoleAsync(role);
            await game.Message.Channel.SendMessageAsync($"{user.Mention} has joined as {game.GetPlayer(user)}s");
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
            if (game == null)
                game = Games.FirstOrDefault(x => x.In(arg3.VoiceChannel.Guild));
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
            var position = int.Parse(gameVc.Name.Split('-')[1]);
            var coords = game.GetCoords(position);
            await Handle(game, user, coords);
        }

        async Task Handle(TTTGame game, SocketGuildUser user, (int x, int y) coords)
        {
            if (game.Crosses == null)
            {
                game.Crosses = user;
                await game.Message.Channel.SendMessageAsync($"{user.Mention} has joined as crosses");
            }
            else if (game.Naughts == null && (game.GetPlayer(user) == Position.Empty))
            {
                game.Naughts = user;
                await game.Message.Channel.SendMessageAsync($"{user.Mention} has joined as naughts");
            }
            if (game.Started == false)
            {
                await user.SendMessageAsync($"Game has not yet started! Waiting for a naughts to join.");
                return;
            }
            var player = game.GetPlayer(user);
            if (player != game.Turn)
            {
                await user.SendMessageAsync($"It is not your turn!");
                return;
            }
            if (!game.TryMove(game.GetPosition(coords.x, coords.y), player))
            {
                await user.SendMessageAsync($"That move is not valid.");
                return;
            }
            await game.Message.Channel.SendMessageAsync($"{user.Mention} goes [{coords.x}, {coords.y}]");
            game.Turn = game.Turn == Position.Cross ? Position.Naught : Position.Cross;
            await game.Message.ModifyAsync(x => x.Embed = game.ToEmbed());
            var winner = game.GetWinnerType();
            if (winner != Position.Empty)
            {
                var msg = "";
                if (winner == Position.DRAW)
                    msg = "Game has drawn!";
                else if (winner == Position.Naught)
                    msg = $"{game.Naughts.Mention} has won!";
                else
                    msg = $"{game.Crosses.Mention} has won!";
                await game.Message.Channel.SendMessageAsync(msg,
                    allowedMentions: AllowedMentions.None);
                var role = game.Guild.Roles.FirstOrDefault(x => x.Name == TTTService.RoleName);
                await game.Naughts.RemoveRoleAsync(role);
                await game.Crosses.RemoveRoleAsync(role);
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
        public SocketGuild Guild { get; set; }
        public Position Turn { get; set; } = Position.Cross;
        public IUserMessage Message { get; set; }
        public Position[] Board { get; set; }
        public Dictionary<int, string> Links { get; }
        public int Rows { get; }
        public TTTGame(SocketGuild guild, int rows = 3)
        {
            Guild = guild;
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
        
        public (int x, int y) GetCoords(int position)
        {
            var division = position / Rows;
            return (position - (division * Rows), division);
        }
        public int GetPosition(int x, int y)
        {
            return x + (y * Rows);
        }

        public Position GetPlayer(IUser user)
        {
            if (Crosses?.Id == user.Id)
                return Position.Cross;
            if (Naughts?.Id == user.Id)
                return Position.Naught;
            return Position.Empty;
        }
        public bool Contains(IUser user) => GetPlayer(user) != Position.Empty;
        public bool In(IGuild guild)
        {
            return Guild.Id == guild.Id;
        }
        public bool Started => Naughts != null && Crosses != null;

        public Position[,] Get2dBoard()
        {
            var board2d = new Position[Rows, Rows];
            for (int x = 0; x < Rows; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    board2d[x, y] = Board[GetPosition(x, y)];
                }
            }
            return board2d;
        }

        public Position GetWinnerType()
        {
            var remain = Board.Count(x => x == Position.Empty);
            if (remain == 0)
                return Position.DRAW;
            var board2d = Get2dBoard();
            List<Position> distinct;

            for (int x = 0; x < Rows; x++)
            {
                var col = new List<Position>();
                for (int y = 0; y < Rows; y++)
                {
                    var pos = board2d[x, y];
                    col.Add(pos);
                }
                distinct = col.Distinct().ToList();
                if (distinct.Count == 1 && distinct[0] != Position.Empty)
                    return distinct[0];
            }

            for (int y = 0; y < Rows; y++)
            {
                var row = new List<Position>();
                for (int x = 0; x < Rows; x++)
                {
                    var pos = board2d[x, y];
                    row.Add(pos);
                }
                distinct = row.Distinct().ToList();
                if (distinct.Count == 1 && distinct[0] != Position.Empty)
                    return distinct[0];
            }

            var rightLeft = new List<Position>();
            for(int i = 0; i < Rows; i++)
            {
                var pos = board2d[i, i];
                rightLeft.Add(pos);
            }
            distinct = rightLeft.Distinct().ToList();
            if (distinct.Count == 1 && distinct[0] != Position.Empty)
                return distinct[0];
            var leftRight = new List<Position>();
            for (int i = 0; i < Rows; i++)
            {
                var pos = board2d[Rows - (i+1), i];
                leftRight.Add(pos);
            }
            distinct = leftRight.Distinct().ToList();
            if (distinct.Count == 1 && distinct[0] != Position.Empty)
                return distinct[0];
            return Position.Empty;
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
            if (position >= Board.Length)
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
            builder.Title = $"{getName(Crosses)} vs {getName(Naughts)}";
            var sb = new StringBuilder();
            var board2d = Get2dBoard();
            for(int y = 0; y < Rows; y++)
            {
                var rowSb = new StringBuilder();
                for(int x = 0; x < Rows; x++)
                {
                    var line = getEmbed(board2d[x, y], Links[GetPosition(x, y)]);
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
            if (Started == false)
            {
                builder.WithFooter($"React below to join as {(Crosses == null ? "Crosses" : "Naughts")}");
            } else
            {
                var winner = GetWinnerType();
                if (winner == Position.Empty)
                {
                    builder.Color = Color.Purple;
                    builder.WithFooter($"Game ongoing; react {Emotes.ARROWS_COUNTERCLOCKWISE} to resend message");
                }
                else if (winner == Position.Naught)
                {
                    builder.Color = Color.Orange;
                    builder.WithFooter($"Game finished; {Naughts.GetName()} won.");
                }
                else if (winner == Position.Cross)
                {
                    builder.Color = Color.Blue;
                    builder.WithFooter($"Game finished; {Crosses.GetName()} won.");
                }
                else
                {
                    builder.Color = Color.Red;
                    builder.WithFooter($"Game drawn, no winners - both are equally pathetic.");
                }
            }
            return builder.Build();
        }
    }
    public enum Position
    {
        Empty,
        Naught,
        Cross,
        DRAW
    }
}
