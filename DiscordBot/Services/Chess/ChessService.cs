using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.Chess.Online;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DiscordBot.Services.BuiltIn.BuiltInUsers;

namespace DiscordBot.Services
{
    public class ChessService : Service
    {
        public override bool IsEnabled => true;
        public override bool IsCritical => true;
        public static int PlayerIdMax = 0;
        public static Dictionary<Guid, ChessTimedGame> TimedGames = new Dictionary<Guid, ChessTimedGame>();
        public static OnlineGame CurrentGame = null;
        public static string LoadException;

        public const int OnlineMaxTotal = 5;
        public const int OnlineMaxPlayer = 3;

        public event EventHandler<string> ChangedOccured;
        public event EventHandler<string> MessageNotifiers;

        public Guid AddTimedGame(ChessTimedGame game)
        {
            var id = Guid.NewGuid();
            game.Id = id;
            TimedGames.Add(id, game);
            MessageNotifiers?.Invoke("newGame", id.ToString());
            return id;
        } 
        public void EndTimedGame(ChessTimedGame game)
        {
            MessageNotifiers?.Invoke("endGame", game.Id.ToString());
        }


        /// <summary>
        /// Number of games a Member must play to be eligible to vote; see section 14A(6)(a)
        /// </summary>
        public const int VoterGamesRequired = 2;

        /// <summary>
        /// Number of Justices required for a petition to be allowed.
        /// </summary>
        public const int JusticesRequired = 2;

        /// <summary>
        /// Number of games a Member must play to be eligible for appointment as Moderator
        /// </summary>
        public const int ModeratorGamesRequired = 4;
        /// <summary>
        /// Number of unique opponents a Member must have played against to be eligible for appointment as Moderator
        /// </summary>
        public const int ModeratorOpponentsRequired = 2;

        public const int StartingValue = 500;

        public static Semaphore OnlineLock = new Semaphore(1, 1);
        public static string LatestChessVersion;

        public static ChessDbContext DB() 
        {
            Program.LogMsg(Program.GetStackTrace(), LogSeverity.Info, $"Chs-DB");
            return Program.Services.GetRequiredService<ChessDbContext>();
        }

        public static Dictionary<ChessPlayer, int> GetArbiterElectionResults(ChessDbContext db)
        {
            Dictionary<ChessPlayer, int> results = new Dictionary<ChessPlayer, int>();
            foreach (var player in db.Players.AsQueryable().Where(x => !x.IsBuiltInAccount))
            {
                foreach (var vote in player.ArbVotes)
                {
                    var other = db.Players.AsQueryable().FirstOrDefault(x => x.Id == vote.VoteeId);
                    if (other == null)
                        continue;
                    if (!checkArbiterCandidacy(other).IsSuccess)
                        continue;
                    results[other] = results.GetValueOrDefault(other, 0) + vote.Score;
                }
            }
            return results;
        }

        public Dictionary<int, List<int>> Holidays = new Dictionary<int, List<int>>()
        {
            { 2021, new List<int>() },
            { 2020, new List<int>()
            {
                3,
                52,
                59, // 28th Feb
                87, // 27th march
                // Quarantine automatically added
                129, // 8th May
                143,
                150
            } },
            { 2019, new List<int>() {
                291,
                298,
                354,
                361,
            } },
            {1, new List<int>() },
        };

        public ITextChannel GameChannel;
        public ITextChannel AdminChannel;
        public ITextChannel DiscussionChannel;
        ITextChannel SystemChannel;
        IRole ChsPlayer;
        IRole ChsMod;
        IRole ChsArbiter;
        IRole ChsJustice;
        IRole ChsChiefJustice;

        public static MiscResult checkArbiterCandidacy(ChessPlayer player)
        {
            if (player == null)
                return MiscResult.FromError("Player does not exist");
            if (player.DismissalReason != null)
                return MiscResult.FromError("Dismissed by CoA: " + player.DismissalReason);
            if (player.Permission.HasFlag(ChessPerm.Justice))
                return MiscResult.FromError("Current serving Justice");
            if (player.Permission == ChessPerm.Moderator)
                return MiscResult.FromError("Current serving Moderator");
            var playersPlayedAgainst = GetPlayedAgainst(player);
            if (playersPlayedAgainst.Count < 10)
                return MiscResult.FromError("Less than ten games played");
            if (playersPlayedAgainst.Distinct().Count() < 4)
                return MiscResult.FromError("Less than four unique opponents played");
            return MiscResult.FromSuccess();
        }
        public static MiscResult checkModeratorCandidacy(ChessPlayer player)
        {
            if (player == null)
                return MiscResult.FromError("Player does not exist");
            if (player.Permission.HasFlag(ChessPerm.Justice))
                return MiscResult.FromError($"They are a Justice of the Court of Appeals");
            if (player.Permission == ChessPerm.Arbiter)
                return MiscResult.FromError($"You are already a Moderator by virtue of being the Arbiter.");
            if (player.DismissalReason != null)
                return MiscResult.FromError("Dismissed by CoA: " + player.DismissalReason);
            if (player.IsBanned)
                return MiscResult.FromError($"They are currently banned");
            var games = ChessService.GetPlayedAgainst(player);
            if (games.Count < ChessService.ModeratorGamesRequired)
                return MiscResult.FromError($"They have played less than {ModeratorGamesRequired} games");
            if (games.Distinct().Count() < ModeratorOpponentsRequired)
                return MiscResult.FromError($"They have played against less than {ModeratorOpponentsRequired} other players");
            return MiscResult.FromSuccess();
        }
        
        public void PopulateDiscordObjects()
        {
            GameChannel = Program.ChessGuild.GetTextChannel(671379228045869076);
            AdminChannel = Program.ChessGuild.GetTextChannel(671379272832647228);
            DiscussionChannel = Program.ChessGuild.GetTextChannel(659708597298528260);
            SystemChannel = Program.ChessGuild.GetTextChannel(660065903291006977);
            //
            ChsPlayer = Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Member");
            ChsMod = Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Moderator");
            ChsArbiter = Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Arbiter");
            ChsJustice = Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Justice");
            ChsChiefJustice = Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Chief Justice");
        }

        public void LogChnl(EmbedBuilder builder, ITextChannel chnl)
        {
            builder.WithCurrentTimestamp();
#if DEBUG
            builder.WithFooter((builder.Footer?.Text ?? "") + " DEBUG");
#endif
            var embed = builder.Build();
            var thRead = new Thread(() => actualLogOnThread(embed, chnl));
            thRead.Start();
        }

        async void actualLogOnThread(Embed embed, ITextChannel chnl)
        {
            try
            {
                await chnl.SendMessageAsync("", false, embed);
            }
            catch (Exception ex)
            {
                Program.LogMsg("ChessActualLog", ex);
            }
            try
            {
                ChangedOccured?.Invoke(this, embed.Title);
            } catch (Exception ex)
            {
                Program.LogMsg("ChessRaiseChange", ex);
            }
        }

        public List<ChessGame> GetRelatedPendings(ChessPlayer player)
        {
            using var db = DB();
            return db.GetGamesWith(player).Where(x => x.ApprovalNeeded != ApprovedBy.None && (x.ApprovalNeeded != x.ApprovalGiven))
                .ToList();
        }
        
        
        public void LogGame(ChessGame game)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Chess Game";
            builder.WithColor(Color.Blue);
            builder.Description = "";
            if(game.ApprovalNeeded.HasFlag(ApprovedBy.Moderator))
                builder.Description = "Because one or both players are monitored, a Moderator must approve this game as valid";
            if (game.ApprovalNeeded.HasFlag(ApprovedBy.Winner))
                builder.Description += $"Because this game occured 'third-party', both players must approve this game as valid";
            builder.AddField("P1", $"{game.Winner.Name}\n{game.Winner.Rating} + {game.WinnerChange}", true);
            builder.AddField("P2", $"{game.Winner.Name}\n{game.Winner.Rating} + {game.LoserChange}", true);
            builder.AddField("Draw?", game.Draw ? "Yes" : "No", true);
            LogChnl(builder, game.ApprovalNeeded == ApprovedBy.None ? GameChannel : AdminChannel);
        }

        void LogEntry(IInvite invite, ChessPlayer player)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Invite Created";
            builder.WithCurrentTimestamp();
            builder.AddField("For", player.Name, true);
            builder.AddField("Code", invite.Code, true);
            LogChnl(builder, SystemChannel);
        }

        public string GetInvite(ChessPlayer player, Classes.BotUser usr)
        {
            var usrInChess = Program.ChessGuild.GetUser(usr.Id);
            if (usrInChess != null)
                return null;
            using var db = DB();
            var existing = db.Invites.FirstOrDefault(x => x.Id == cast(usr.Id));
            if (existing != null)
                return existing.Code;
            var created = SystemChannel.CreateInviteAsync(0, 0, true, true, new RequestOptions() { AuditLogReason = $"For {player.Name}; {usr.Id}" }).Result;
            db.Invites.Add(new ChessInvite()
            {
                Id = cast(usr.Id),
                Code = created.Code
            });
            db.SaveChanges();
            LogEntry(created, player);
            return created.Code;
        }

        public void LogEntry(ChessGame entry)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Game Added";
            builder.WithColor(Color.Blue);
            string play = $"{entry.Winner.Name}\nRating: +{entry.WinnerChange} = **{entry.Winner.Rating}**";
            string opp = $"{entry.Loser.Name}\nScore: {entry.LoserChange} = **{entry.Loser.Rating}**";
            if (entry.Draw)
            {
                builder.WithColor(Color.Green);
                builder.WithDescription($"Game was a **draw**");
                builder.AddField("P1", play, true);
                builder.AddField("P2", opp, true);
            }
            else
            {
                builder.AddField("Winner", play, true);
                builder.AddField("Loser", opp, true);
            }
            if (entry.ApprovalNeeded != ApprovedBy.None)
            {
                builder.AddField($"Approved From Before", "Game occured in past, now approved");
            }
            LogChnl(builder, GameChannel);
        }

        public void LogAdmin(EmbedBuilder builder)
        {
            LogChnl(builder, AdminChannel);
        }

        public void LogAdmin(ChessBan ban)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "User Banned";
            builder.AddField("Target", ban.Target.Name, true);
            builder.AddField("Operator", ban.OperatorId.ToString(), true);
            builder.AddField("Reason", ban.Reason, false);
            builder.AddField("Expires At", ban.ExpiresAt.ToString("yyyy-MMM-dd"), true);
            builder.AddField("Duration", Math.Round((ban.ExpiresAt - DateTime.Now).TotalDays / 7, 2).ToString() + " weeks", true);
            if (ban.Target.Bans.Count > 1)
            {
                builder.AddField("Previous", $"This is the player's {ban.Target.Bans.Count}th ban");
            }
            builder.WithColor(Color.Red);
            LogAdmin(builder);
        }

        public DateTime GetFridayOfWeek(DateTime now)
        {
            int direction = (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Saturday) ? -1 : 1;
            while (now.DayOfWeek != DayOfWeek.Friday)
                now = now.AddDays(direction);
            return now;
        }

        public DateTime GetFridayOfThisWeek() => GetFridayOfWeek(DateTime.Now);

        public DateTime getLastPresentDate(ChessDbContext db, ChessPlayer player, bool doLastPlayed = false)
        {
            if (player.DateLastPresent.HasValue && doLastPlayed == false)
            {
                return player.DateLastPresent.Value;
            }
            DateTime lastPlayed = DateTime.MinValue;
            var gamesInvolving = db.GetCurrentGamesWith(player);
            foreach(var entry in gamesInvolving)
            {
                if(entry.Timestamp > lastPlayed)
                {
                    lastPlayed = entry.Timestamp;
                }
            }
            return lastPlayed;
        }

        public int FridaysBetween(DateTime first, DateTime second)
        {
            if (first == DateTime.MinValue || second == DateTime.MinValue)
                return -1;
            if (first > second)
            {
                var timeFirst = new DateTime(first.Year, first.Month, first.Day);
                var timeSecond = new DateTime(second.Year, second.Month, second.Day);

                var dayDifference = (int)timeFirst.Subtract(timeSecond).TotalDays;
                return Enumerable
                    .Range(1, dayDifference)
                    .Select(x => timeSecond.AddDays(x))
                    .Count(x => x.DayOfWeek == DayOfWeek.Friday && !Holidays[x.Year].Contains(x.DayOfYear));
            }
            else if (first == second)
            {
                return 0;
            }
            else
            {
                return FridaysBetween(second, first);
            }
        }

        [Obsolete]
        public const int BuiltInClassRoomChess = -10;
        [Obsolete]
        public const int BuiltInAIChess = -15;
        public BotUser BuiltInCoAUser;
        public BotUser BuiltInClassUser;
        public static ChessPlayer AIPlayer;

        public static long cast(ulong v)
        {
            unchecked
            {
                return (long)v;
            }
        }
        
        void SetBuiltInRoles(ChessDbContext db)
        {
            var chiefJustice = ulong.Parse(Program.Configuration["chess:chief:id"]);
            var chiefName = Program.Configuration["chess:chief:name"];
            var uu = db.Players.AsQueryable().FirstOrDefault(x => x.Name == chiefName || x.DiscordAccount == cast(chiefJustice));
            if(uu == null)
            {
                uu = new ChessPlayer()
                {
                    Name = chiefName,
                    DiscordAccount = cast(chiefJustice),
                };
                db.Players.Add(uu);
            }
            uu.Permission = ChessPerm.ChiefJustice;

            BuiltInClassUser = Program.GetUserOrDefault(ChessClass);
            if (BuiltInClassUser == null)
            {
                BuiltInClassUser = new Classes.BotUser(ChessClass);
                BuiltInClassUser.OverrideName = "Friday Lunch";
                Program.Users.Add(BuiltInClassUser);
            }
            BuiltInClassUser.VerifiedEmail = null;
            BuiltInClassUser.IsVerified = false;
            BuiltInClassUser.Tokens = new List<AuthToken>()
            {
                new AuthToken(AuthToken.LoginPassword, PasswordHash.HashPassword("fridayclassroom"))
            };
            BuiltInClassUser.OverrideDiscriminator = 1;
            BuiltInClassUser.ServiceUser = true;
            var classRoom = db.Players.FirstOrDefault(x => x.Name == "Friday Lunch" && x.DiscordAccount == cast(BuiltInClassUser.Id));
            if (classRoom == null)
            {
                classRoom = new ChessPlayer();
                classRoom.IsBuiltInAccount = true;
                classRoom.ConnectedAccount = BuiltInClassUser.Id;
                db.Players.Add(classRoom);
            }
            classRoom.Name = "Friday Lunch";
            classRoom.Permission = ChessPerm.ClassRoom;

            BuiltInCoAUser = Program.GetUserOrDefault(ChessCoA);
            if (BuiltInCoAUser == null)
            {
                BuiltInCoAUser = new Classes.BotUser(ChessCoA);
                BuiltInCoAUser.OverrideName = "Court of Appeals";
                Program.Users.Add(BuiltInCoAUser);
            }
            BuiltInCoAUser.VerifiedEmail = "@";
            BuiltInCoAUser.IsVerified = true;
            BuiltInCoAUser.OverrideDiscriminator = 1;
            BuiltInCoAUser.OverrideName = "Court of Appeals";
            BuiltInCoAUser.ServiceUser = true;
            var coaToken = BuiltInCoAUser.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
            if(coaToken == null)
            {
                coaToken = new AuthToken(AuthToken.LoginPassword, 24);
                BuiltInCoAUser.Tokens.Add(coaToken);
            } else
            {
                coaToken.Regenerate(24);
            }

            var court = db.Players.FirstOrDefault(x => x.Name == "Court of Appeals" && x.DiscordAccount == cast(BuiltInCoAUser.Id));
            if (court == null)
            {
                court = new ChessPlayer();
                court.Name = "Court of Appeals";
                court.IsBuiltInAccount = true;
                court.ConnectedAccount = BuiltInCoAUser.Id;
                db.Players.Add(court);
            }
            court.Permission = ChessPerm.CourtOfAppeals;
            
            var aiuser = Program.GetUserOrDefault(ChessAI);
            if (aiuser == null)
            {
                aiuser = new BotUser(ChessAI);
                aiuser.OverrideName = "AI Player";
                Program.Users.Add(aiuser);
            }
            AIPlayer = db.Players.FirstOrDefault(x => x.Name == "AI" && x.IsBuiltInAccount);
            if (AIPlayer == null)
            {
                AIPlayer = new ChessPlayer();
                AIPlayer.Name = "AI";
                AIPlayer.IsBuiltInAccount = true;
                AIPlayer.ConnectedAccount = aiuser.Id;
                db.Players.Add(AIPlayer);
            }
            db.SaveChanges();
        }

        void threadSetPerms()
        {
            using var db = DB();
            SetConnectedRoles(db);
        }

        public void SetPermissionsAThread()
        {
            var th = new Thread(threadSetPerms);
            th.Start();
        }

        void SetConnectedRoles(ChessDbContext db)
        {
            foreach (var player in db.Players.AsQueryable().Where(x => !x.IsBuiltInAccount))
            {
                if (player.ConnectedAccount > 0)
                {
                    var chsServer = Program.ChessGuild.GetUser(player.ConnectedAccount);
                    if (chsServer != null)
                    {
                        if(!player.Removed)
                        {
                            chsServer?.AddRoleAsync(ChsPlayer);
                        }
                        else
                        {
                            chsServer?.RemoveRoleAsync(ChsPlayer);
                        }
                        if (player.Permission.HasFlag(ChessPerm.Moderator))
                        {
                            chsServer?.AddRoleAsync(ChsMod);
                        }
                        else
                        {
                            chsServer?.RemoveRoleAsync(ChsMod);
                        }
                        if(player.Permission == ChessPerm.Arbiter)
                        {
                            chsServer?.AddRoleAsync(ChsArbiter);
                        } else
                        {
                            chsServer?.RemoveRoleAsync(ChsArbiter);
                        }
                        if (player.Permission.HasFlag(ChessPerm.Justice))
                        {
                            chsServer?.AddRoleAsync(ChsJustice);
                        }
                        else
                        {
                            chsServer?.RemoveRoleAsync(ChsJustice);
                        }
                        if (player.Permission == ChessPerm.ChiefJustice)
                        {
                            chsServer?.AddRoleAsync(ChsChiefJustice);
                        }
                        else
                        {
                            chsServer?.RemoveRoleAsync(ChsChiefJustice);
                        }
                    }
                }
            }
        }

        void SendRatingChanges(ChessDbContext db)
        {
            if (Program.DailyValidateFailed())
                return;
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle("Leaderboard Changes");
            builder.WithColor(Color.Orange);
            foreach (var usr in db.Players.AsQueryable().Where(x => !x.IsBuiltInAccount).ToList().OrderByDescending(x => x.Rating))
            {
                // since this is happening on saturday morning..
                var yesturday = DateTime.Now.AddDays(-1);
                var score = usr.GetScoreOnDay(yesturday);
                int diffb = 0;
                int weekBefore = 0;
                do
                {
                    diffb -= 7;
                    weekBefore = usr.GetScoreOnDay(yesturday.AddDays(diffb));
                } while (weekBefore == 0 && diffb > -100);
                var gamesPlayed = BuildEntries(usr, db, yesturday, false);
                if (gamesPlayed.Count > 0 && score > 0)
                { // dont bother adding them if no games were played
                    if (weekBefore == 0)
                    { // never playedd last week
                        builder.AddField(usr.Name, $"{score}\nN/A Previous");
                    }
                    else
                    {
                        int diff = score - weekBefore;
                        if (diff > 0)
                        {
                            builder.AddField(usr.Name, $"{score}\n <:small_green_triangle:668750922536452106> {diff}");
                        }
                        else if (diff < 0)
                        {
                            builder.AddField(usr.Name, $"{score}\n :small_red_triangle_down: {Math.Abs(diff)}");
                        }
                        else
                        {
                            builder.AddField(usr.Name, $"{score}\n :small_orange_diamond: N/C");
                        }
                    }
                }
            }
            if (builder.Fields.Count > 0)
                LogChnl(builder, GameChannel);
        }

        void SetNickNames(ChessDbContext db)
        {
            foreach (var p in db.Players)
            {
                if (p.IsBuiltInAccount)
                    continue;
                if (p.ConnectedAccount <= 0)
                    continue;
                var usr = Program.ChessGuild.GetUser(p.ConnectedAccount);
                if (usr == null)
                    continue;
                usr.ModifyAsync(x => x.Nickname = p.Name);
            }
        }

        void CheckExpiredNotes(ChessDbContext db)
        {
            foreach (var p in db.Players)
            {
                List<ChessNote> toRemove = new List<ChessNote>();
                foreach (var note in db.GetNotesAgainst(p.Id))
                {
                    var expiry = note.GivenAt.AddDays(note.ExpiresInDays + 90);
                    if (expiry < DateTime.Now)
                    {
                        toRemove.Add(note);
                    }
                }
                foreach (var x in toRemove)
                    db.Notes.Remove(x);
            }
            db.SaveChanges();
        }

        void RemoveExpiredPending(ChessDbContext db)
        {
            var toRemove = new List<ChessGame>();
            foreach (var game in db.Games.AsQueryable().Where(x => x.ApprovalNeeded != ApprovedBy.None && x.ApprovalGiven != x.ApprovalNeeded))
            {
                var diff = DateTime.Now - game.Timestamp;
                if (diff.TotalDays > 3)
                {
                    var emb = new EmbedBuilder();
                    emb.Title = $"Pending Game Rejected";
                    emb.Description = $"Game on {game.Timestamp:yyyy/MM/dd hh:mm:ss} was not approved in time, so has been refused";
                    emb.AddField("P1", game.Winner.Name, true);
                    emb.AddField("P2", game.Loser.Name, true);
                    emb.AddField("Draw?", game.Draw ? "Yes" : "No", true);
                    LogAdmin(emb);
                    toRemove.Add(game);
                }
            }
            foreach (var x in toRemove)
                db.Games.Remove(x);
            if (toRemove.Count > 0)
                db.SaveChanges();
        }

        void setAutomatic(ChessPlayer player, int changeModBy, string reason)
        {
            int old = player.Rating + player.Modifier;
            int newR = player.Rating + (player.Modifier + changeModBy);
            int diff = newR - old;
            string text = diff >= 0 ? $"+{diff}" : $"{diff}";
            using var db = DB();
            player.Modifier = player.Modifier + changeModBy;
            var note = new ChessNote()
            {
                GivenAt = DateTime.Now,
                ExpiresInDays = 14,
                OperatorId = 0,
                TargetId = player.Id,
                Target = player,
                Text = $"{text}: {reason}"
            };
            player.Notes.Add(note);
            LogAdmin(new EmbedBuilder()
                .WithTitle("Automatic Deduction")
                .WithDescription($"{player.Name} rating {text}, to {newR}\nReason: {reason}"));
        }

        void CheckLastDatePlayed(ChessDbContext db)
        {
            if (Program.DailyValidateFailed())
                return;
            bool any = false;
            foreach (var player in db.Players.AsQueryable().Where(x => !x.IsBuiltInAccount && x.Rating > 100))
            {
                var lastPresent = getLastPresentDate(db, player);
                var lastPlayed = getLastPresentDate(db, player, true); // will ignore last present.
                var presentFridays = FridaysBetween(lastPresent, DateTime.Now);
                if (lastPresent == DateTime.MinValue)
                    presentFridays = 0;
                var playedFridays = FridaysBetween(lastPlayed, DateTime.Now);
                if (lastPlayed == DateTime.MinValue)
                    playedFridays = 0;
                bool sent = false;
                if (presentFridays >= 3)
                {
                    sent = true;
                    setAutomatic(player, -15, $"Not present consc. three weeks (last {lastPresent.ToShortDateString()})");
                }
                if (playedFridays >= 3)
                {
                    sent = true;
                    setAutomatic(player, -5, $"Not played consc. three weeks (last {lastPlayed.ToShortDateString()})");
                }
                any = any || sent;
                if(sent)
                    Thread.Sleep(1500);
            }
            if (any)
                db.SaveChanges();
        }

        void getChessOnlineVersion()
        {
            var client = Program.Services.GetRequiredService<HttpClient>();
            var r = client.GetAsync("https://api.github.com/repos/CheAle14/bot-chess/releases/latest").Result;
            if (r.IsSuccessStatusCode)
            {
                var jobj = Newtonsoft.Json.Linq.JObject.Parse(r.Content.ReadAsStringAsync().Result);
                LatestChessVersion = jobj["tag_name"].ToObject<string>();
                Program.LogMsg("Latest chess version: " + LatestChessVersion, LogSeverity.Critical, "");
            }
            else
            {
                LatestChessVersion = "v0.0";
            }
        }

        void setOnlineTokens(ChessDbContext db)
        {
            foreach(var chs in db.Players.AsQueryable().Where(x => x.DiscordAccount != cast(0)))
            {
                var usr = Program.GetUserOrDefault(chs.ConnectedAccount);
                if (usr == null)
                    continue;
                var token = usr.Tokens.FirstOrDefault(x => x.Name == "onlinechesstoken");
                if (token != null)
                    chs.VerifyOnlineReference = token.Value;
            }
        }

        void setQuarantine()
        {
            var day = new DateTime(2020, 03, 27);
            var afterNow = DateTime.Now.AddDays(30);
            do
            {
                if(!Holidays[day.Year].Contains(day.DayOfYear))
                {
                    // recognise issue with looping through it, but memory issues if the bot keeps calling this function.
                    Holidays[day.Year].Add(day.DayOfYear);
                }
                day = day.AddDays(1);
            } while (day < afterNow);
        }

        public void setElectedArbiter(ChessDbContext db)
        {
            var winner = GetArbiterElectionResults(db).Where(x => x.Value > 0).OrderByDescending(x => x.Value).FirstOrDefault().Key;
            var existing = db.Players.FirstOrDefault(x => x.Permission == ChessPerm.Arbiter);

            if (winner?.Id != existing?.Id)
            {
                if(winner != null)
                    winner.Permission = winner.Permission | ChessPerm.Arbiter; // add flag
                if(existing != null)
                    existing.Permission = existing.Permission & ~ChessPerm.Arbiter; // remove flag
                var builder = new EmbedBuilder();
                builder.Title = "Arbiter Election";
                builder.Description = "Due to changes in votes, the below changes have occured.";
                builder.AddField("Arbiter", $"**{winner?.Name ?? "N/A"}**", true);
                builder.AddField("Removed", $"*No longer arbiter*\r\n{existing?.Name ?? "N/A"}", true);
                DiscussionChannel.SendMessageAsync(embed: builder.Build());
            }
        }

        public List<ChessGame> BuildEntries(ChessPlayer player, ChessDbContext db, DateTime date, bool ignoreOnline)
        {
            var games = db.GetGamesOnDate(player.Id, date);
            if (ignoreOnline)
                return games
                    .Where(x => x.ApprovalNeeded == ApprovedBy.None || x.ApprovalNeeded == ApprovedBy.Moderator)
                    .ToList();
            return games.ToList();
        }
        public static List<int> GetPlayedAgainst(ChessPlayer p, int stopAt = int.MaxValue)
        {
            List<int> ids = new List<int>();
            using var db = DB();
            var gamesInvolving = db.GetCurrentGamesWith(p); 
            foreach(var x in gamesInvolving)
            {
                if (x.WinnerId == p.Id)
                    ids.Add(x.LoserId);
                else
                    ids.Add(x.WinnerId);
                if (ids.Count > stopAt)
                    break;
            }
            return ids;
        }

        public override void OnLoaded()
        {
            PopulateDiscordObjects();
            try
            {
                OnDailyTick();
            }
            catch (Exception ex)
            {
                Program.LogMsg($"Failed to load Chess", ex);
                LoadException = ex.Message;
                try
                {
                    LogAdmin(new EmbedBuilder()
                        .WithTitle("Failed to Start")
                        .WithColor(Color.Red)
                        .WithDescription($"```\n{ex}```"));
                }
                catch { }
            }
        }

        public override void OnDailyTick()
        {
            using var db = DB();
            setQuarantine();
            SetBuiltInRoles(db);
            CheckLastDatePlayed(db);
            SendRatingChanges(db);
            setElectedArbiter(db);
            SetConnectedRoles(db);
            SetNickNames(db);
            CheckExpiredNotes(db);
            RemoveExpiredPending(db);
            setOnlineTokens(db);
            try
            {
                getChessOnlineVersion();
            }
            catch (Exception ex)
            {
                Program.LogMsg("ChessService", ex);
            }
            db.SaveChanges();
        }

        public override void OnReady()
        {
            Program.Client.UserJoined += Client_UserJoined;
        }

        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            if (arg.Guild.Id == Program.ChessGuild.Id)
            {
                using var db = DB();
                var chessUser = db.Players.FirstOrDefault(x => x.DiscordAccount == cast(arg.Id));
                if (chessUser == null)
                {
                    var r = await arg.SendMessageAsync("Error joining Chess Court of Appeals\nYou are not permitted entry");
                    Thread.Sleep(1500);
                    await arg.KickAsync("No connected account on LeaderBoard");
                }
                else
                {
                    SetConnectedRoles(db);
                    await arg.ModifyAsync(x => x.Nickname = chessUser.Name);
                }
            }
        }
    }
}
