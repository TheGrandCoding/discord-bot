using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.Chess.CoA;
using DiscordBot.Classes.Chess.Online;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class ChessService : SavedService
    {
        public override bool IsEnabled => true;
        public override bool IsCritical => true;
        public static int PlayerIdMax = 0;
        public static List<ChessPlayer> Players = new List<ChessPlayer>();
        public static List<CoAHearing> Hearings = new List<CoAHearing>();
        public static List<ChessPendingGame> PendingGames = new List<ChessPendingGame>();
        public static OnlineGame CurrentGame = null;
        public static Dictionary<ulong, IInvite> Invites = new Dictionary<ulong, IInvite>();
        public static string LoadException;

        public const int ElectableModerators = 2;
        public const int OnlineMaxTotal = 5;
        public const int OnlineMaxPlayer = 3;

        public static Semaphore OnlineLock = new Semaphore(1, 1);
        public static string LatestChessVersion;

        public static Dictionary<ChessPlayer, int> GetModElectionResults()
        {
            Dictionary<ChessPlayer, int> results = new Dictionary<ChessPlayer, int>();
            foreach (var player in Players.Where(x => !x.IsBuiltInAccount && !x.IsBanned))
            {
                foreach (var vote in player.ModVotePreferences)
                {
                    var other = Players.FirstOrDefault(x => x.Id == vote.Key);
                    if (other == null)
                        continue;
                    if (!meetsCandidacyRequirements(other))
                        continue;
                    results[other] = results.GetValueOrDefault(other, 0) + vote.Value;
                }
            }
            return results;
        }

        public ChessGameStatus SwapStatePerspective(ChessGameStatus state)
        {
            switch (state)
            {
                case ChessGameStatus.Loss:
                    return ChessGameStatus.Won;
                case ChessGameStatus.Won:
                    return ChessGameStatus.Loss;
                default:
                    return state;
            }
        }

        public Dictionary<int, List<int>> Holidays = new Dictionary<int, List<int>>()
        {
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
        IRole ChsJustice;
        IRole ChsChiefJustice;

        public static bool meetsCandidacyRequirements(ChessPlayer player)
        {
            int total = player.Wins + player.Losses;
            if (total < 5)
                return false;
            if (player.Permission.HasFlag(ChessPerm.Justice))
                return false;
            if (player.Permission.HasFlag(ChessPerm.Elected) == false && player.Permission.HasFlag(ChessPerm.Moderator))
                return false; // they are an appointed/permenant mod
            if (player.ConnectedAccount == 0)
                return false; // no connected Discord account
            if (player.Bans.Count > 0)
                return false;
            if (player.WithdrawnModVote)
                return false;
            return true;
        }
        public void PopulateDiscordObjects()
        {
            Program.ChessGuild.CurrentUser.ModifyAsync(x => x.Nickname = "Court Clerk");
            GameChannel = Program.ChessGuild.GetTextChannel(671379228045869076);
            AdminChannel = Program.ChessGuild.GetTextChannel(671379272832647228);
            DiscussionChannel = Program.ChessGuild.GetTextChannel(659708597298528260);
            SystemChannel = Program.ChessGuild.GetTextChannel(660065903291006977);
            //
            ChsPlayer = Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Member");
            ChsMod = Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Moderator");
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
        }

        public void LogEntry(ChessPendingGame game)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Pending Game";
            builder.WithColor(Color.DarkOrange);
            builder.Description = "Because one or both players are monitored, a Moderator must approve this game as valid";
            builder.AddField("P1", $"{game.Player1.Name}\n{game.Player1.Rating} + {game.P1_Change}", true);
            builder.AddField("P2", $"{game.Player2.Name}\n{game.Player2.Rating} + {game.P2_Change}", true);
            builder.AddField("Draw?", game.Draw ? "Yes" : "No", true);
            LogChnl(builder, AdminChannel);
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

        public IInvite GetInvite(ChessPlayer player, Classes.BotUser usr)
        {
            var usrInChess = Program.ChessGuild.GetUser(usr.Id);
            if (usrInChess != null)
                return null;
            if (Invites.TryGetValue(usr.Id, out var existing))
                return existing;
            var created = SystemChannel.CreateInviteAsync(0, 0, true, true, new RequestOptions() { AuditLogReason = $"For {player.Name}; {usr.Id}" }).Result;
            Invites.Add(usr.Id, created);
            LogEntry(created, player);
            return created;
        }

        public void LogEntry(ChessPlayer player, ChessEntry entry, ChessPlayer opposition = null, bool isApproval = false)
        {
            if (opposition == null)
                opposition = Players.FirstOrDefault(x => x.Id == entry.againstId);
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Game Added";
            builder.WithColor(Color.Blue);
            string play = $"{player.Name}\nScore: {entry.selfWas} -> **{player.Rating}**";
            string opp = $"{opposition.Name}\nScore: {entry.otherWas} -> **{opposition.Rating}**";
            if (entry.State == ChessGameStatus.Draw)
            {
                builder.WithColor(Color.Green);
                builder.WithDescription($"Game was a **draw**");
                builder.AddField("P1", play, true);
                builder.AddField("P2", opp, true);
            }
            else if (entry.State == ChessGameStatus.Won)
            {
                builder.AddField("Winner", play, true);
                builder.AddField("Loser", opp, true);
            }
            else if (entry.State == ChessGameStatus.Loss)
            {
                builder.AddField("Winner", play, true);
                builder.AddField("Loser", opp, true);
            }
            if (isApproval)
            {
                builder.AddField($"Approved From Before", "Game occured in past, now approved");
            }
            LogChnl(builder, GameChannel);
        }

        public List<ChessPendingGame> GetRelatedPendings(ChessPlayer p)
        {
            return PendingGames.Where(x => x != null && (x.Player1?.Id == p.Id || x.Player2?.Id == p.Id)).ToList();
        }

        public void LogAdmin(EmbedBuilder builder)
        {
            LogChnl(builder, AdminChannel);
        }

        public void LogAdmin(ChessBan ban)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "User Banned";
            builder.AddField("Target", ban.Against.Name, true);
            builder.AddField("Operator", ban.GivenBy.Name, true);
            builder.AddField("Reason", ban.Reason, false);
            builder.AddField("Expires At", ban.ExpiresAt.ToString("yyyy-MMM-dd"), true);
            builder.AddField("Duration", Math.Round((ban.ExpiresAt - DateTime.Now).TotalDays / 7, 2).ToString() + " weeks", true);
            if (ban.Against.Bans.Count > 1)
            {
                builder.AddField("Previous", $"This is the player's {ban.Against.Bans.Count}th ban");
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

        public DateTime getLastPresentDate(ChessPlayer player, bool doLastPlayed = false)
        {
            if (player.DateLastPresent.HasValue && doLastPlayed == false)
            {
                return player.DateLastPresent.Value;
            }
            DateTime lastPlayed = DateTime.MinValue;
            foreach(var otherPlayer in Players)
            {
                foreach(var entry in otherPlayer.Days)
                {
                    if(entry.Date > lastPlayed)
                    {
                        var any = entry.Entries.Any(x => otherPlayer.Id == player.Id || x.againstId == player.Id);
                        if (any)
                            lastPlayed = entry.Date;
                    }
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

        public const int BuiltInClassRoomBotUser = 3;
        public const int BuiltInClassRoomChess = -10;
        public const int BuiltInAIUser = 15;
        public const int BuiltInAIChess = -15;
        public BotUser BuiltInCoAUser;
        public BotUser BuiltInClassUser;
        public static ChessPlayer AIPlayer;
        void SetBuiltInRoles()
        {
            var chiefJustice = ulong.Parse(Program.Configuration["chess:chief:id"]);
            var chiefName = Program.Configuration["chess:chief:name"];
            var uu = Players.FirstOrDefault(x => x.Name == chiefName || x.ConnectedAccount == chiefJustice);
            uu.Permission = ChessPerm.CourtOfAppeals;

            BuiltInClassUser = Program.GetUserOrDefault(BuiltInClassRoomBotUser);
            if (BuiltInClassUser == null)
            {
                BuiltInClassUser = new Classes.BotUser(BuiltInClassRoomBotUser);
                BuiltInClassUser.OverrideName = "Friday Lunch";
                Program.Users.Add(BuiltInClassUser);
            }
            BuiltInClassUser.Tokens = new List<AuthToken>()
            {
                new AuthToken(AuthToken.LoginPassword, "fridayclassroom")
            };
            BuiltInClassUser.VerifiedEmail = null; // force re-authenticate
            BuiltInClassUser.BuiltIn = true;
            var classRoom = Players.FirstOrDefault(x => x.Name == "Friday Lunch" && x.ConnectedAccount == BuiltInClassUser.Id);
            if (classRoom == null)
            {
                classRoom = new ChessPlayer();
                classRoom.Name = "Friday Lunch";
                classRoom.IsBuiltInAccount = true;
                classRoom.Id = BuiltInClassRoomChess;
                classRoom.ConnectedAccount = BuiltInClassUser.Id;
                Players.Add(classRoom);
            }
            classRoom.Permission = ChessPerm.ClassRoom;

            BuiltInCoAUser = Program.GetUserOrDefault(5);
            if (BuiltInCoAUser == null)
            {
                BuiltInCoAUser = new Classes.BotUser(5);
                BuiltInCoAUser.OverrideName = "Court of Appeals";
                Program.Users.Add(BuiltInCoAUser);
            }
            BuiltInCoAUser.BuiltIn = true;
            var court = Players.FirstOrDefault(x => x.Name == "Court of Appeals" && x.ConnectedAccount == BuiltInCoAUser.Id);
            if (court == null)
            {
                court = new ChessPlayer();
                court.Name = "Court of Appeals";
                court.IsBuiltInAccount = true;
                court.Id = -11;
                court.ConnectedAccount = BuiltInCoAUser.Id;
                Players.Add(court);
            }
            court.Permission = ChessPerm.CourtOfAppeals;

            var aiuser = Program.GetUserOrDefault(BuiltInAIUser);
            if (aiuser == null)
            {
                aiuser = new BotUser(BuiltInAIUser);
                aiuser.OverrideName = "AI Player";
                Program.Users.Add(aiuser);
            }
            AIPlayer = Players.FirstOrDefault(x => x.Id == BuiltInAIChess);
            if (AIPlayer == null)
            {
                AIPlayer = new ChessPlayer();
                AIPlayer.Name = "AI";
                AIPlayer.IsBuiltInAccount = true;
                AIPlayer.Id = BuiltInAIChess;
                AIPlayer.ConnectedAccount = aiuser.Id;
                Players.Add(AIPlayer);
            }

            foreach (var p in Players)
            {
                if ((int)p.Permission == 18)
                {
                    p.Permission = ChessPerm.Justice;
                }
            }

        }

        void threadSetPerms()
        {
            SetConnectedRoles();
            foreach (var h in Hearings)
                h.SetChannelPermissions();
        }

        public void SetPermissionsAThread()
        {
            var th = new Thread(threadSetPerms);
            th.Start();
        }

        void SetConnectedRoles()
        {
            foreach (var player in Players)
            {
                if (player.IsBuiltInAccount)
                    continue;
                if (player.ConnectedAccount > 0)
                {
                    var chsServer = Program.ChessGuild.GetUser(player.ConnectedAccount);
                    if (chsServer != null)
                    {
                        chsServer?.AddRoleAsync(ChsPlayer);
                        if (player.Permission.HasFlag(ChessPerm.Moderator))
                        {
                            chsServer?.AddRoleAsync(ChsMod);
                        }
                        else
                        {
                            chsServer?.RemoveRoleAsync(ChsMod);
                        }
                        if (player.Permission.HasFlag(ChessPerm.Justice))
                        {
                            chsServer?.AddRoleAsync(ChsJustice);
                        }
                        else
                        {
                            chsServer?.RemoveRoleAsync(ChsJustice);
                        }
                        if (player.Permission == ChessPerm.CourtOfAppeals)
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

        void SendRatingChanges()
        {
            if (Program.DailyValidateFailed())
                return;
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle("Leaderboard Changes");
            builder.WithColor(Color.Orange);
            foreach (var usr in Players.OrderByDescending(x => x.Rating))
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
                var gamesPlayed = BuildEntries(usr, yesturday, false);
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

        void SetIds()
        {
            foreach (var x in Hearings)
                x.SetIds();
            foreach (var x in Hearings)
            {
                x.SetAppealHearing();
            }
            foreach (var x in PendingGames)
                x.SetIds();
        }

        void SetNickNames()
        {
            foreach (var p in Players)
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

        void CheckExpiredNotes()
        {
            foreach (var p in Players)
            {
                List<ChessNote> toRemove = new List<ChessNote>();
                foreach (var note in p.Notes)
                {
                    var expiry = note.Date.AddDays(note.DaysExpire + 90);
                    if (expiry < DateTime.Now)
                    {
                        toRemove.Add(note);
                    }
                }
                foreach (var x in toRemove)
                    p.Notes.Remove(x);
            }
        }

        void RemoveExpiredPending()
        {
            var toRemove = new List<ChessPendingGame>();
            foreach (var game in PendingGames)
            {
                var diff = DateTime.Now - game.RecordedAt;
                if (diff.TotalDays > 3)
                {
                    var emb = new EmbedBuilder();
                    emb.Title = $"Pending Game Rejected";
                    emb.Description = "Game was not approved in time, so has been refused";
                    emb.AddField("P1", game.Player1.Name, true);
                    emb.AddField("P2", game.Player2.Name, true);
                    emb.AddField("Draw?", game.Draw ? "Yes" : "No", true);
                    LogAdmin(emb);
                    toRemove.Add(game);
                }
            }
            foreach (var x in toRemove)
                PendingGames.Remove(x);
        }

        void setAutomatic(ChessPlayer player, int changeModBy, string reason)
        {
            int old = player.Rating + player.Modifier;
            int newR = player.Rating + (player.Modifier + changeModBy);
            int diff = newR - old;
            string text = diff >= 0 ? $"+{diff}" : $"{diff}";
            player.Modifier = player.Modifier + changeModBy;
            player.Notes.Add(new ChessNote(BuiltInCoAUser, $"{text}: {reason}"));
            LogAdmin(new EmbedBuilder()
                .WithTitle("Automatic Deduction")
                .WithDescription($"{player.Name} rating {text}, to {newR}\nReason: {reason}"));
        }

        void CheckLastDatePlayed()
        {
            if (Program.DailyValidateFailed())
                return;
            foreach (var player in Players)
            {
                if (player.IsBuiltInAccount)
                    continue;
                if (player.Rating <= 100)
                    continue;
                var lastPresent = getLastPresentDate(player);
                var lastPlayed = getLastPresentDate(player, true); // will ignore last present.
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
                if(sent)
                    Thread.Sleep(1500);
            }
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

        void fixIdIssue()
        {
            var p = Players.FirstOrDefault(x => x.Id == 0);
            if (p == null)
                return;
            p.Id = ++PlayerIdMax;
            foreach (var player in Players)
            {
                foreach (var day in player.Days)
                {
                    foreach (var entry in day.Entries)
                    {
                        if (entry.againstId == 0)
                            entry.againstId = p.Id;
                    }
                }
                if (player.ModVotePreferences.TryGetValue(0, out var vote))
                {
                    player.ModVotePreferences.Remove(0);
                    player.ModVotePreferences[p.Id] = vote;
                }
            }
        }

        void setOnlineTokens()
        {
            foreach(var chs in Players)
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
                Holidays[day.Year].Add(day.DayOfYear);
                day = day.AddDays(1);
            } while (day < afterNow);
        }

        public void setElectedModerators()
        {
            var winners = GetModElectionResults().Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(ElectableModerators);
            var existing = Players.Where(x => x.Permission == ChessPerm.ElectedMod);
            string elected = "";
            string removed = "";
            foreach (var person in winners)
            {
                if (existing.Contains(person.Key))
                { // already a mod, so we dont need to do anything.
                }
                else
                { // not a mod, but they should be.
                    person.Key.Permission = ChessPerm.ElectedMod;
                    elected += person.Key.Name + "\r\n";
                }
            }
            var winnerKeys = winners.Select(x => x.Key);
            foreach (var person in existing)
            {
                if (winnerKeys.Contains(person))
                { // they are a mod, and should be.
                }
                else
                { // they are a mod, but shouldnt.
                    person.Permission = ChessPerm.Player;
                    removed += person.Name + "\r\n";
                }
            }
            if (!(string.IsNullOrWhiteSpace(elected) && string.IsNullOrWhiteSpace(removed)))
            {
                var builder = new EmbedBuilder();
                builder.Title = "Moderator Election";
                builder.Description = "Due to changes in votes, the below changes have occured.\r\n" +
                    "Current Moderators therefore are:\r\n" + string.Join(", ", winners.Select(x => x.Key.Name));
                if (!string.IsNullOrWhiteSpace(elected))
                    builder.AddField("Elected", elected, true);
                if (!string.IsNullOrWhiteSpace(removed))
                    builder.AddField("Removed", removed, true);
                DiscussionChannel.SendMessageAsync(embed: builder.Build());
            }
        }

        public ChessPlayer GetPlayer(int id) => Players.FirstOrDefault(x => x.Id == id);

        public List<ChessEntry> BuildEntries(ChessPlayer player, DateTime date, bool ignoreOnline)
        {
            var lst = new List<ChessEntry>();
            var day = player.Days.FirstOrDefault(x => x.Date.DayOfYear == date.DayOfYear && x.Date.Year == date.Year);
            if (day != null)
            {
                lst.AddRange(day.Entries);
            }
            // now we go through every other player..
            foreach (var other in Players)
            {
                if (other.Id == player.Id)
                    continue;
                day = other.Days.FirstOrDefault(x => x.Date.DayOfYear == date.DayOfYear && x.Date.Year == date.Year);
                if (day != null)
                { // we're goin to need to swap 'Against' around, because against is currently us..
                    foreach (var entry in day.Entries)
                    {
                        if (entry.againstId != player.Id)
                            continue;
                        if (entry.onlineGame && ignoreOnline)
                            continue;
                        var newEntry = new ChessEntry()
                        {
                            againstId = other.Id,
                            State = SwapStatePerspective(entry.State),
                            onlineGame = entry.onlineGame,
                            Id = entry.Id,
                            otherWas = entry.otherWas,
                            selfWas = entry.selfWas
                        };
                        lst.Add(newEntry);
                    }
                }
            }
            return lst;
        }

        class chessSave
        {
            public List<ChessPlayer> players;
            public List<CoAHearing> hearings;
            public List<ChessPendingGame> pending;
            public Dictionary<ulong, string> invites;
        }

        public override void OnLoaded()
        {
            PopulateDiscordObjects();
            try
            {
                var content = ReadSave("{}");
                Invites = new Dictionary<ulong, IInvite>();
                if (content.StartsWith("["))
                {
                    Players = JsonConvert.DeserializeObject<List<ChessPlayer>>(content);
                    Hearings = new List<CoAHearing>();
                    PendingGames = new List<ChessPendingGame>();
                    Invites = new Dictionary<ulong, IInvite>();
                }
                else
                {
                    var save = JsonConvert.DeserializeObject<chessSave>(content);
                    Players = save.players ?? new List<ChessPlayer>();
                    Hearings = save.hearings ?? new List<CoAHearing>();
                    PendingGames = save.pending ?? new List<ChessPendingGame>();
                    Invites = new Dictionary<ulong, IInvite>();
                    save.invites = save.invites ?? new Dictionary<ulong, string>();
                    if (save.invites.Count > 0)
                    {
                        var INVITES = Program.ChessGuild.GetInvitesAsync().Result;
                        foreach (var keypair in save.invites)
                        {
                            var invite = INVITES.FirstOrDefault(x => x.Id == keypair.Value);
                            if (invite != null)
                                Invites.Add(keypair.Key, invite);
                        }
                    }
                }
                setQuarantine();
                SetBuiltInRoles();
                CheckLastDatePlayed();
                SendRatingChanges();
                setElectedModerators();
                SetConnectedRoles();
                SetIds();
                SetNickNames();
                CheckExpiredNotes();
                RemoveExpiredPending();
                fixIdIssue();
                setOnlineTokens();
                try
                {
                    getChessOnlineVersion();
                }
                catch (Exception ex)
                {
                    Program.LogMsg("ChessService", ex);
                }
                try
                {
                    foreach (var h in Hearings)
                        h.SetChannelPermissions();
                }
                catch (Exception ex)
                {
                    Program.LogMsg("ChessService2", ex);
                }
                OnSave();
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

        public override string GenerateSave()
        {
            var dict = new Dictionary<ulong, string>();
            foreach (var item in Invites)
                dict.Add(item.Key, item.Value.Id);
            var save = new chessSave()
            {
                players = Players,
                hearings = Hearings,
                pending = PendingGames,
                invites = dict
            };
            var content = JsonConvert.SerializeObject(save);
            if (content.Length < 500)
            {
                Program.LogMsg("Refusing to save chess content, since its below threshhold:\n" + content, Discord.LogSeverity.Critical, "ChessService-Save");
                return null;
            }
            return content;
        }

        public override void OnReady()
        {
            Program.Client.UserJoined += Client_UserJoined;
        }

        private async Task Client_UserJoined(SocketGuildUser arg)
        {
            if (arg.Guild.Id == Program.ChessGuild.Id)
            {
                var chessUser = Players.FirstOrDefault(x => x.ConnectedAccount == arg.Id);
                if (chessUser == null)
                {
                    var r = await arg.SendMessageAsync("Error joining Chess Court of Appeals\nYou are not permitted entry");
                    Thread.Sleep(1500);
                    await arg.KickAsync("No connected account on LeaderBoard");
                }
                else
                {
                    SetConnectedRoles();
                    foreach (var h in Hearings)
                        h.SetChannelPermissions();
                    await arg.ModifyAsync(x => x.Nickname = chessUser.Name);
                }
            }
        }
    }
}
