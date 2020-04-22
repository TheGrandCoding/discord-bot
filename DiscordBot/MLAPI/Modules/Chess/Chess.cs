using Discord;
using DiscordBot.Classes.Chess;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static DiscordBot.Services.ChessService;

namespace DiscordBot.MLAPI.Modules
{
    public partial class Chess : ChessBase
    {
        public static ChessService ChessS;
        public Chess(APIContext context) : base(context, "chess")
        {
            ChessS ??= Program.Services.GetRequiredService<ChessService>();
        }

        void LogAdminAction(string title, string desc, params (string key, string value)[] fields)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle(title);
            builder.WithDescription(desc);
            builder.WithFooter(Context.User.Name, Context.User.FirstValidUser?.GetAvatarUrl());
            foreach (var tuple in fields)
                builder.AddField(tuple.key, tuple.value);
            ChessS.LogAdmin(builder);
        }

        bool didChange = false;

        bool canClassRoomAccn(ChessPlayer player)
        {
            if (player == null)
                return false;
            if(player.Id == BuiltInClassRoomChess)
            {
                if(DateTime.Now.DayOfWeek == DayOfWeek.Friday)
                {
                    var halfTwelve = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                        12, 30, 0);
                    var tenPastOne = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                        13, 20, 0); // allow an extra ten minutes just in case
                    return DateTime.Now >= halfTwelve && DateTime.Now <= tenPastOne;
                }
                return false;
            }
            return doesHavePerm(ChessPerm.AddMatch, player);
        }

        bool doesHavePerm(ChessPerm perm, ChessPlayer user)
        {
            if (perm == ChessPerm.Player)
                return true;
            if (user == null)
                return false;
            return user.Permission.HasFlag(perm);
        }
        
        bool doesHavePerm(ChessPerm perm, Classes.BotUser bUser)
        {
            if (bUser == null)
                return perm == ChessPerm.Player;
            if(SelfPlayer != null)
            {
                if (ChessPerm.ClassRoom.HasFlag(perm) || perm == ChessPerm.ClassRoom)
#if DEBUG
                    return true;
#else
                    return canClassRoomAccn(SelfPlayer);
#endif
                return doesHavePerm(perm, SelfPlayer);
            }
            return false;
        }

        bool doesHavePerm(ChessPerm perm) => doesHavePerm(perm, Context.User);

        public override void BeforeExecute()
        {
            if(!string.IsNullOrWhiteSpace(ChessService.LoadException))
            { // service failed to load
                throw new DiscordBot.MLAPI.Attributes.HaltExecutionException(LoadException);
            }
        }

        public override void AfterExecute()
        {
            if (didChange)
                ChessS.OnSave();
        }

        string suffix(int val)
        {
            string rep = val.ToString();
            switch (string.Join("", rep.TakeLast(2)))
            {
                case "10":
                case "11":
                case "12":
                case "13":
                case "14":
                case "15":
                case "16":
                case "17":
                case "18":
                case "19":
                    return "th";
                default:
                    switch (rep[rep.Length - 1])
                    {
                        case '1':
                            return "st";
                        case '2':
                            return "nd";
                        case '3':
                            return "rd";
                        default:
                            return "th";
                    }
            }
        }

        string getPlayerNameRow(ChessPlayer player, int fridays)
        {
            string warning = addIconsToRow(player, fridays);
            return $"{aLink($"/chess/history?id={player.Id}", $"<label>{player.Name}</label>")}{warning}";
        }

        string loginButton()
        {
            if (Context.User == null)
            {
                return $"<input type='button' value='Login' onclick=\"window.location.replace('/login');\"/>";
            }
            return "";
        }

        string getDiscordLink()
        {
            if (Context.User == null || Context.User.BuiltIn)
                return "";
            if (SelfPlayer == null)
                return "";
            var invite = ChessS.GetInvite(SelfPlayer, Context.User);
            if (invite == null)
                return "";
            didChange = true;
            return  $@"<div id='popupinvite' class='full_popup'>
            <p><strong><a href='{invite.Url}'>Chess / CoA Discord Server</a></strong><br/>
                You are eligible to join the Chess server. This allows you to see games that are played, and may be required for Court appeals
                <br><a href='#' onclick='setHideCookie(this);'>Click here to close</a>
            </p>
            </div>";
        }

        string addIconsToRow(ChessPlayer player, int fridays)
        {
            string warning = "";
            if (doesHavePerm(ChessPerm.ClassRoom) && fridays >= 1 && !player.IsBanned)
            {
                warning += "<img " +
                    "src='https://upload.wikimedia.org/wikipedia/en/thumb/f/fb/Yes_check.svg/240px-Yes_check.svg.png' " +
                    "alt='Mark as present' " +
                    "style='margin-left: 5px' " +
                    $"width='20' height='20' valign='middle' style='margin-left: 5px;' title='Mark Player as present' onclick='doMarkPresent({player.Id});'>";
            }
            if (player.IsBanned)
            {
                warning += "<img " +
                    "src='https://upload.wikimedia.org/wikipedia/en/thumb/4/42/Stop_x_nuvola.svg/800px-Stop_x_nuvola.svg.png' " +
                    "alt='Monitored' " +
                    $"width='20' height='20' valign='middle' style='margin-left: 5px;' title='User is banned'>";
            }
            if (player.RequireGameApproval)
            {
                warning += "<img " +
                    "src='https://upload.wikimedia.org/wikipedia/commons/thumb/6/63/Antu_password-show-on.svg/768px-Antu_password-show-on.svg.png' " +
                    "alt='Monitored' " +
                    "width='20' height='20' valign='middle' style='margin-left: 5px;' title='User is being monitored: Their" +
                        " games require approval before taking effect'>";
            }
            if(player.RequireTiming)
            {
                warning += "<img " +
                    "src='https://upload.wikimedia.org/wikipedia/commons/thumb/8/8c/Clock_and_warning.svg/318px-Clock_and_warning.svg.png' " +
                    "alt='Warning' " +
                    "width='20' height='20' valign='middle' style='margin-left: 5px;' title='User must use chessclock in any games.'>";
            }
            if (player.ActiveNotes.Count > 0 && doesHavePerm(ChessPerm.Moderator))
            {
                warning += "<img " +
                    "src='https://upload.wikimedia.org/wikipedia/commons/thumb/2/24/Warning_icon.svg/630px-Warning_icon.svg.png' " +
                    "alt='Warning' " +
                    "width='20' height='20' valign='middle' style='margin-left: 5px;' title='User has warnings, click name to view'>";
            }
            if (ChessS.GetRelatedPendings(player).Count > 0 && doesHavePerm(ChessPerm.Moderator))
            {
                warning += "<img " +
                    "src='https://upload.wikimedia.org/wikipedia/commons/b/b3/Advancedsettings.png' " +
                    "alt='Approve' " +
                    "style='margin-left: 5px' " +
                    "width='20' height='20' valign='middle' style='margin-left: 5px;' title='Player has games awaiting approval'>";
            }
            return warning;
        }

        [Method("GET"), Path("/chess")]
        //[AllowNonAuthed(ConditionIfAuthed = true)] // Possible privacy implications?
        public void Base()
        {
            string TABLE = "";
            int rank = 1;
            foreach (var player in Players.OrderByDescending(x => x.Rating + x.Modifier).ThenByDescending(x => x.WinRate).ThenByDescending(x => x.Wins + x.Losses))
            {
                if (player.ShouldContinueInLoop && player.Id != ChessService.AIPlayer.Id)
                    continue;
                var lastDate = ChessS.getLastPresentDate(player);
                var fridays = ChessS.FridaysBetween(DateTime.Now, lastDate);
                string color = "";
                if(fridays >= 4)
                {
                    color = "#DF5FFE";
                }
                else if (fridays >= 3)
                {
                    color = "red";
                } else if (fridays >= 2)
                {
                    color = "orange";
                } else if (fridays >= 1)
                {
                    color = "yellow";
                }
                string ROW = $"<tr style='background-color: {color};'>";
                if (player.IsBanned)
                    ROW += $"<td>n/a</td>";
                else
                    ROW += $"<td>{rank}{suffix(rank)}</td>";
                ROW += $"<td>{player.Rating + player.Modifier}</td>";
                ROW += $"<td>{getPlayerNameRow(player, fridays)}</td>";
                ROW += $"<td>{Math.Round(player.WinRate*100)}%</td>";
                ROW += $"<td>{player.Wins} - {player.Losses}</td>";
                TABLE += ROW + "</tr>";
                if(!player.IsBanned)
                    rank++;
            }
            string adminItems = "<div id='admin' style='display: flex; vertical-align: middle; align-items: center; justify-content: center; '>";
            int count = 0;
            if(doesHavePerm(ChessPerm.CreateUser))
            {
                count++;
                adminItems += "<input type='button' value='Create New User' onclick='newuser();' style='margin-left: 5px; width: [[W]]; height: 20px;'>";
            }
            if(doesHavePerm(ChessPerm.Moderator) || doesHavePerm(ChessPerm.Justice))
            {
                count++;
                adminItems += "<input type='button' value='Modify User' onclick='moduser();' style='margin-left: 5px; width: [[W]]; height: 20px;'>";
            }
            if(doesHavePerm(ChessPerm.AddMatch))
            {
                count++;
                adminItems += "<input type='button' value='Add Match' onclick='matchend();' style='width: [[W]]; height: 20px;'>";
            }
            adminItems += "</div>";
            if (count == 0)
            {
                adminItems = "";
            }
            else if (count == 1)
                adminItems = adminItems.Replace("[[W]]", "100%");
            else if (count == 2)
                adminItems = adminItems.Replace("[[W]]", "48%");
            else if (count == 3)
                adminItems = adminItems.Replace("[[W]]", "30%");
            string link = getDiscordLink();
            ReplyFile("base.html", 200, new Replacements() 
                .Add("table", TABLE)
                .Add("admin", adminItems)
                .Add("loginBtn", loginButton())
                .Add("discord_link", link));
        }

        [Method("GET"), Path("/chess/register")]
        public void MultiplePresent()
        {
            if(!doesHavePerm(ChessPerm.ClassRoom)) // don't replace with Attribute - does specific checks
            {
                HTTPError(System.Net.HttpStatusCode.Forbidden, "Cannot Take Register", "Must have permissin and be valid time");
                return;
            }
            string TABLE = "";
            int i = 0;
            foreach(var usr in Players.OrderBy(x => x.Name))
            {
                if (usr.ShouldContinueInLoop || usr.IsBanned)
                    continue;
                string ROW = $"<tr><td>";
                ROW += $"<input id='i-{i++}' type='text' class='text' maxlength='1' value='' placeholder='leave blank if not here' onkeyup='return addPresent(event, this, \"{usr.Id}\");'/>";
                ROW += $"{getPlayerNameRow(usr, 0)}</td>";
                ROW += $"<td>{ChessS.getLastPresentDate(usr, true).ToString("yyyy-MM-dd")}</td>";
                TABLE += ROW + "</tr>";
            }
            ReplyFile("register.html", 200, new Replacements().Add("table", TABLE));
        }

        bool shouldIncludeInRecommend(ChessPlayer player)
        {
            if (player.ShouldContinueInLoop)
                return false;
            if (player.DateLastPresent.HasValue && player.DateLastPresent.Value.DayOfYear == DateTime.Now.DayOfYear)
                return true;
            var val = ChessS.BuildEntries(player, DateTime.Now, true);
            return val.Count > 0;
        }

        [Method("GET"), Path("/chess/recommend")]
        public void RecommendGames()
        {
            string TABLE = "";
            var now = DateTime.Now.DayOfYear;
            var ppresent = Players.Where(shouldIncludeInRecommend);
            var players = ppresent.OrderBy(x => x.Rating).ToList();
            List<int> complete = new List<int>();
            bool debug = Context.Query.Contains("debug");
            var playerOrdered = new List<ChessPlayer>();
            foreach (int start in new int[] {0, 1})
            {
                for(int i = start; i < players.Count; i += 2)
                {
                    playerOrdered.Add(players[i]);
                }
            }
            foreach(var player in playerOrdered)
            {
                string ROW = $"<tr>";
                ROW += $"<td>{getPlayerNameRow(player, 0)}</td>";
                if (complete.Contains(player.Id))
                    continue;
                //complete.Add(player.Id);
                var calculators = new List<ChessWeightedRecommend>();
                foreach(var other in playerOrdered)
                {
                    if (other.Id == player.Id || complete.Contains(other.Id))
                        continue;
                    calculators.Add(new ChessWeightedRecommend(player, other));
                }
                var ordered = calculators.OrderByDescending(x => x.Weight);
                var selected = ordered.FirstOrDefault();
                if(selected == null)
                {
                    ROW += $"<td>No other players</td>";
                } else
                {
                    //complete.Add(selected.Other.Id);
                    ROW += $"<td>{getPlayerNameRow(selected.Other, 0)}</td>";
                }
                if(debug)
                {
                    ROW += "<td><table>";
                    ROW += "<tr><th>Opponent</th><th>Weight</th>";
                    var functions = ChessWeightedRecommend.findCalculators();
                    foreach(var x in functions)
                    {
                        ROW += $"<th>{x.Name.Replace("calc_", "")}</th>";
                    }
                    foreach(var calc in ordered)
                    {
                        ROW += $"<tr><td>{calc.Other.Name}</td>";
                        ROW += $"<td><strong>{calc.Weight}</strong></td>";
                        foreach(var x in functions)
                        {
                            double change = (double)x.Invoke(calc, null);
                            string _cls = change > 0 ? "green" : "red";
                            ROW += $"<td><label style='color: {_cls};'>{change}</label></td>";
                        }
                        ROW += "</tr>";
                    }
                    ROW += "</table></td>";
                }
                TABLE += ROW + "</tr>";
            }
            ReplyFile("recommend.html", 200, new Replacements()
                .Add("table", TABLE)
                .Add("colm", debug ? "<th>debug</th>" : ""));
        }

#region Chess Clock
        [Method("GET"), Path("/chess/clock")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void Clock(int seconds = 300, int black = 300)
        {
            if(seconds > (60 * 59) || black > (60 * 59))
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "Out of Range", "Time cannot be greater than an hour");
                return;
            }
            if(seconds < (60 * 3) || black < (60 * 3))
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "Out of Range", "Time cannot be less than 3 minutes");
                return;
            }
            int w_minutes = 0;
            while(seconds >= 60)
            {
                w_minutes += 1;
                seconds -= 60;
            }
            int b_minutes = 0;
            while(black >= 60)
            {
                b_minutes += 1;
                black -= 60;
            }
            ReplyFile("chessclock.html", 200, new Replacements()
                .Add("w_minutes", w_minutes)
                .Add("w_seconds", seconds)
                .Add("b_minutes", b_minutes)
                .Add("b_seconds", black));
        }
#endregion

        [Method("GET"), Path("/chess/ban")]
        public void ChessBan(int id)
        {
            var target = Players.FirstOrDefault(x => x.Id == id);
            if(target == null || target.Removed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Player", "Unknown player with that Id");
                return;
            }
            ReplyFile("ban.html", 200, new Replacements().Add("target", target));
        }

#if DEBUG // only want to be able to directly add games in debug config
        [Method("GET"), Path("/chess/previous")]
        public void HistoricGameBase()
        {
            string players = "";
            foreach (var player in Players.OrderByDescending(x => x.Rating))
            {
                if (player.ShouldContinueInLoop)
                    continue;
                string bannnn = "";
                if (player.IsBanned)
                {
                    bannnn = "disabled class='banned'";
                }
                players += $"<option {bannnn} value=\"{player.Id}\">{player.Name}</option>";
            }
            ReplyFile("debug_addmatch.html", 200, new Replacements().Add("playerlist", players));
        }

        [Method("PUT"), Path("/chess/api/previous")]
        public void AddHistoricGame(int p1, int p2, string date, bool draw)
        {
            var player1 = Players.FirstOrDefault(x => x.Id == p1);
            var player2 = Players.FirstOrDefault(x => x.Id == p2);
            var dSplit = date.Split('-');
            var dateTime = new DateTime(int.Parse(dSplit[0]), int.Parse(dSplit[1]), int.Parse(dSplit[2]));
            player1.SetGameOnDay(player2, draw ? ChessGameStatus.Draw : ChessGameStatus.Loss, dateTime);
            didChange = true;
            RespondRaw("Ok");
        }
#endif
#region Thing
        [Method("POST"), Path("/chess/api/pullr")]
        [RequireValidHTTPAgent(false)]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void HandleHTTPFilesPr()
        {
            RespondRaw("Ok");
        }
        static string thing;
        [Method("GET"), Path("/chess/api/pullr")]
        [RequireChess(ChessPerm.CourtOfAppeals)]
        public void GetThing()
        {
            RespondRaw(thing ?? "Not set");
        }
#endregion

        [Method("GET"), Path("/chess/api/lawcss")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void LawStyle()
        {
            ReplyFile("terms/lawstyle.css", 200);
        }

        [Method("GET"), Path("/chess/conduct")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void Regulations()
        {
            ReplyFile("terms/conduct.html", 200);
        }

        [Method("GET"), Path("/chess/terms")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void TermsAndCons()
        {
            string mods = "";
            string justices = "";
            foreach (var player in Players.Where(x => x.Permission == ChessPerm.Moderator || x.Permission == ChessPerm.ElectedMod).OrderBy(x => x.Name))
            {
                mods += $"<uli>{player.Name}</uli>";
            }
            foreach(var player in Players.Where(x => x.Permission == ChessPerm.Justice).OrderBy(x => x.Name))
            {
                justices += $"<uli>Justice {player.Name}</uli>";
            }
            ReplyFile("terms/terms.html", 200, new Replacements()
                .Add("mods", mods)
                .Add("justices", justices));
        }

        [Method("GET"), Path("/chess/moderators")]
        [RequireChess(ChessPerm.Player)]
        public void GetElections()
        {
            if(SelfPlayer.IsBanned || SelfPlayer.IsBuiltInAccount)
            {
                HTTPError(HttpStatusCode.Forbidden, "Not Able to Vote", "You are unable to vote");
                return;
            }
            if (SelfPlayer.Removed)
            {
                if (SelfPlayer.Permission.HasFlag(ChessPerm.Moderator) || SelfPlayer.Permission.HasFlag(ChessPerm.Justice))
                {
                } else { 
                    RespondRaw("Invalid player: removed from leaderboard", 400);
                    return;
                }
            }
            string table = "";
            foreach(var player in Players.OrderBy(x => x.Name))
            {
                if (player.ShouldContinueInLoop || player.Id == SelfPlayer.Id)
                    continue;
                if (!meetsCandidacyRequirements(player))
                {
                    SelfPlayer.ModVotePreferences.Remove(player.Id);
                    didChange = true;
                    continue;
                }
                string ROW = "<tr>";
                ROW += $"<td>{player.Name}</td>";
                int current = SelfPlayer.ModVotePreferences.GetValueOrDefault(player.Id, 0);
                for(int i = -2; i <= 2; i++)
                {
                    ROW += $"<td><input type='checkbox' {(i == current ? "checked" : "")} onclick='setVote({player.Id}, {i})'></td>";
                }
                table += ROW + "</tr>";
            }
            string mods = "";
            foreach(var player in ChessService.Players.Where(x => x.Permission.HasFlag(ChessPerm.ElectedMod)))
            {
                mods += $"<li>{player.Name}</li>";
            }
            ReplyFile("election.html", 200, new Replacements()
                .Add("table", table)
                .Add("moderators", mods)
                .Add("numMods", ChessService.ElectableModerators));
        }

        [Method("PUT"), Path("/chess/api/elect")]
        [RequireChess(ChessPerm.Player)]
        public void SetVote(int id, int value)
        {
            if (SelfPlayer.IsBanned || SelfPlayer.IsBuiltInAccount)
            {
                HTTPError(HttpStatusCode.Forbidden, "Not Able to Vote", "You are unable to vote");
                return;
            }
            if (SelfPlayer.Removed)
            {
                if (SelfPlayer.Permission.HasFlag(ChessPerm.Moderator) || SelfPlayer.Permission.HasFlag(ChessPerm.Justice))
                {
                }
                else
                {
                    RespondRaw("Invalid player: removed from leaderboard", 400);
                    return;
                }
            }
            var player = Players.FirstOrDefault(x => x.Id == id);
            if (player == null || player.ShouldContinueInLoop|| player.IsBanned)
            {
                RespondRaw("Invalid player: banned, built in or removed or not exists.", 404);
                return;
            }
            value = Math.Clamp(value, -2, 2);
            SelfPlayer.ModVotePreferences[id] = value;
            didChange = true;
            RespondRaw("");
        }

        [Method("GET"), Path("/chess/match")]
        [RequireChess(ChessPerm.AddMatch)]
        public void MatchBase()
        {
            string players = "";
            foreach (var player in Players.OrderByDescending(x => x.Rating))
            {
                if (player.ShouldContinueInLoop)
                    continue;
                string bannnn = "";
                if(player.IsBanned)
                {
                    bannnn = "disabled class='banned'";
                }
                players += $"<option {bannnn} value=\"{player.Id}\">{player.Name}</option>";
            }
            ReplyFile("match.html", 200, new Replacements()
                .Add("playerlist", players));
        }

        [Method("GET"), Path("/chess/history")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void UserHistory(int id, bool full = false)
        {
            var player = Players.FirstOrDefault(x => x.Id == id);
            if (player == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Unknown player", "Id given did not match any known player.");
                return;
            }
            if(player.IsBuiltInAccount)
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "Invalid Account", "Player is a built-in account so cannot be modified");
                return;
            }
            if(player.Removed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Account Removed", "That account has been removed from the Leaderboard");
                return;
            }
            bool ADMIN = doesHavePerm(ChessPerm.CourtOfAppeals);
            string TABLE = "";
            DateTime start = ChessS.GetFridayOfThisWeek();
            var dates = new List<DateTime>()
            {
#if DEBUG
                DateTime.Now,
#endif
                start,
                start.AddDays(7 * -1),
                start.AddDays(7 * -2),
                start.AddDays(7 * -3),
                start.AddDays(7 * -4),
                start.AddDays(7 * -5)
            };
            if(full)
            {
                dates = new List<DateTime>();
                foreach(var p in Players)
                {
                    foreach(var d in p.Days)
                    {
                        if (p.Id == player.Id)
                        {
                            dates.Add(d.Date);
                            continue;
                        }
                        if (d.Entries.FirstOrDefault(x => x.againstId == player.Id) != null)
                            dates.Add(d.Date);
                    }
                }
                dates = dates.Distinct(new Classes.DateEquality()).OrderBy(x => x).ToList();
            }
            foreach (var date in dates)
            {
                var entries = ChessS.BuildEntries(player, date, ignoreOnline:false);
                var pending = new List<ChessPendingGame>();
                if(doesHavePerm(ChessPerm.Moderator))
                {
                    pending = ChessService.PendingGames.Where(x => (x.Player1Id == player.Id || x.Player2Id == player.Id) && x.RecordedAt.DayOfYear == date.DayOfYear).ToList();
                }
                string DATE = $"<tr><th rowspan='{entries.Count + 2 + pending.Count}'>{date.DayOfWeek} {date.ToShortDateString()}</th></tr><tr>";
                if (entries.Count == 0)
                {
                    DATE += $"<td colspan='2'>No games played</td></tr>";
                } else
                {
                    int count = 0;
                    foreach (var entry in entries)
                    {
                        count++;
                        var against = ChessS.GetPlayer(entry.againstId);
                        DATE += $"<td{(entry.onlineGame ? " class='online'" : "")}>{(against?.Name ?? "unknown")}</td><td>{entry.State}";
                        if (ADMIN)
                        {
                            DATE += $"<input type='button' value='Remove' style='margin-left:5px;' onclick='dispute(\"{date.DayOfYear}\", \"{date.Year}\", \"{player.Id}\", \"{entry.Id}\");' />";
                        }
                        DATE += "</td></tr><tr>";
                    }
                    int scr = player.GetScoreOnDay(date);
                    if (scr == 0)
                    {
                        DATE += $"<td colspan='2'>Unknown final score</td></tr>";
                    }
                    else
                    {
                        DATE += $"<td colspan='2'>Score at end: <strong>{scr}</strong></td></tr>";
                    }
                }
                TABLE += DATE;
                foreach(var thing in pending)
                {
                    string ROW = $"<tr style='background-color: orange;'>";
                    var against = thing.Player1Id == player.Id ? thing.Player2 : thing.Player1;
                    string type = "";
                    if (thing.Draw)
                        type = "Draw";
                    else if (thing.Player1Id == player.Id)
                        type = "Won";
                    else
                        type = "Loss";
                    ROW += $"<td>{against.Name}</td>";
                    ROW += $"<td>{type} ";
                    if (thing.Player1Id == SelfPlayer.Id || thing.Player2Id == SelfPlayer.Id)
                    {
                        ROW += "<span class='label label-error'>No Approve: Conflict</span>";
                    } else
                    {
                        ROW += $"<input type='button' value='Approve' onclick='approveGame(\"{thing.Reference}\");'/>";
                    }
                    ROW += "</td>";
                    TABLE += ROW + "</tr>";
                }
            }


            List<ChessNote> notes = new List<ChessNote>();
            var lastDate = ChessS.getLastPresentDate(player);
            var lastDiff = DateTime.Now - lastDate;
            if (lastDiff.TotalDays > 14)
            {
                notes.Add(new ChessNote(null, $"No games played in {(int)lastDiff.TotalDays} days"));
            }
            notes.AddRange(player.ActiveNotes);

            string WARNINGS = "";
            if (notes.Count > 0 && doesHavePerm(ChessPerm.Moderator))
            {
                WARNINGS = $"<div id='warnings' style='border: 3px orange solid;background-color: #ff6666;width: 100%;margin: 5px;padding: 2px;'><ul>";
                foreach (var note in notes)
                {
                    WARNINGS += $"<li><strong>{note.Note}</strong>";
                    if (note.Author != null)
                        WARNINGS += $" ({note.Author.Name})";
                    var dateExpires = note.Date.AddDays(note.DaysExpire);
                    var difff = dateExpires - DateTime.Now;
                    WARNINGS += $"; expires in {(int)difff.TotalDays} days</li>";
                }
                WARNINGS += "</ul></div>";
            }

            ReplyFile("history.html", 200, new Replacements()
                .Add("table", TABLE)
                .Add("target", player)
                .Add("warnings", WARNINGS));
        }

        [Method("GET"), Path("/chess/userlist")]
        [RequireChess(ChessPerm.Moderator, OR = "permission")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.SetActualChessRating, OR = "permission")]
        public void ModifyUser()
        {
            string TABLE = "";
            foreach (var user in Players.OrderByDescending(x => x.Rating))
            {
                string ROW = "<tr";
                if(user.Removed)
                {
                    if (doesHavePerm(ChessPerm.CourtOfAppeals))
                        ROW += " class='removed'";
                    else
                        continue;
                }
                ROW += ">";
                ROW += $"<td><input type='button' value='{user.Name}' onclick=\"changename({user.Id}, '{user.Name}');\"/>" +
                    $"{aLink($"/chess/history?id={user.Id}", " [History]")}</td>";
                ROW += $"<td>" +
                    $"<input type='button' value='{user.Rating}' onclick='change(\"{user.Id}\");'/>  " +
                    $"<input type='button' value='{user.Modifier}' onclick='changemod(\"{user.Id}\");'/>" +
                    $"</td>";
                ROW += $"<td><input type='button' value='{(user.Removed ? "Add" : "Remove")}' onclick='remove(\"{user.Name}\", \"{user.Id}\");'/></td>";
                ROW += $"<td><input type='button' value='Add' onclick='addNote({user.Id});'/></td>";
                ROW += $"<td><input type='checkbox' {(user.RequireGameApproval ? "checked" : "")} onclick='toggleMonitor(\"{user.Id}\");'/></td>";
                ROW += $"<td><input type='checkbox' {(user.RequireTiming ? "checked" : "")} onclick='toggleTime(\"{user.Id}\");'/></td>";
                string banOnClick = "";
                if (user.IsBanned)
                { // only CoA can unban
                    if(doesHavePerm(ChessPerm.CourtOfAppeals))
                    {
                        banOnClick = $"unBanUser('{user.Id}');";
                    } else
                    {
                        banOnClick = "alert(\\\"Only the Court can order a ban be rescinded!\\\");";
                    }
                } else
                {
                    banOnClick = $"banUser(\"{user.Id}\")";
                }
                ROW += $"<td><input type='checkbox' {(user.IsBanned ? "checked" : "")} onclick='{banOnClick}'/></td>";
                TABLE += ROW + "</tr>";
            }
            ReplyFile("user.html", 200, new Replacements().Add("table", TABLE));
        }

        [Method("GET"), Path("/chess/account")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void GetAccountInfo()
        {
            var accn = SelfPlayer;
            Dictionary<string, string> dict = new Dictionary<string, string>()
            {
                {"IP", Context.Request.Cookies["X-Forwarded-For"]?.Value ?? "unknown" },
                {"LoggedIn", Context.User?.Name ?? "not logged in" },
                {"ChessAccount", accn?.Name ?? "none" },
                {"ChessPerm", (accn?.Permission ?? ChessPerm.Player).ToString() },
                {"addMatch", doesHavePerm(ChessPerm.AddMatch).ToString() },
                {"mod", doesHavePerm(ChessPerm.Moderator).ToString() },
                {"justice", doesHavePerm(ChessPerm.Justice).ToString() },
                {"coa", doesHavePerm(ChessPerm.CourtOfAppeals).ToString() }
            };
            string TEXT = "";
            foreach(var keypair in dict)
            {
                TEXT += $"<li><strong>{keypair.Key}</strong>: {keypair.Value}</li>";
            }
            ReplyFile("account.html", 200, new Replacements().Add("list", TEXT));
        }

        [Method("GET"), Path("/chess/perms")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.ConnectChess, OR = "permission")]
        [RequireChess(ChessPerm.Justice, OR = "permission")]
        public void UserPermissions()
        {
            string TABLE = "";
            foreach(var player in Players.OrderBy(x => x.Id))
            {
                string ROW = "<tr>";
                ROW += $"<td>{player.Id}</td>";
                ROW += $"<td>{player.Name}</td>";
                ROW += $"<td><input type='text' onclick='setUser(\"{player.Id}\", this);' value='{player.ConnectedAccount}'/></td>";
                ROW += $"<td><select onchange='setRole(\"{player.Id}\", this);'>";
                foreach(var value in Enum.GetValues(typeof(ChessPerm)))
                {
                    var name = Enum.GetName(typeof(ChessPerm), value);
                    ROW += $"<option value='{(int)value}'{(player.Permission == (ChessPerm)value ? " selected" : "")}>{name}</option>";
                }
                ROW += "</select></td>";
                TABLE += ROW + "</tr>";
            }
            ReplyFile("perms.html", 200, new Replacements().Add("table", TABLE));
        }

        [Method("PUT"), Path("/chess/api/connect")]
        [RequireChess(ChessPerm.CourtOfAppeals, OR = "permission")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.ConnectChess, OR = "permission")]
        public void ConnectPlayer(int chessId, ulong discord)
        {
            if(Context.User == null)
            {
                RespondRaw("Must be logged in", 403);
                return;
            }
            var player = Players.FirstOrDefault(x => x.Id == chessId);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            if(player.IsBuiltInAccount)
            {
                RespondRaw("Account is built-in and cannot be modified", 403);
                return;
            }
            if(discord == 0)
            {
                player.ConnectedAccount = 0;

            } else
            {
                var accn = Program.GetUserOrDefault(discord);
                if(accn == null)
                {
                    RespondRaw("Unknown discord Id", 404);
                    return;
                }
                player.ConnectedAccount = discord;
            }
            didChange = true;
            RespondRaw("Set");
        }

        [Method("PUT"), Path("/chess/api/setperm")]
        [RequireChess(ChessPerm.CourtOfAppeals)]
        public void SetPermPlayer(int id, int role)
        {
            var perm = (ChessPerm)role;
            var player = Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            if(player.IsBuiltInAccount)
            {
                RespondRaw("This account is built-in and cannot be changed", 403);
                return;
            }
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Member Permission Changed";
            builder.Description = $"{player.Name}'s permissions have been changed:\n" +
                $"{player.Permission} -> {perm}";
            ChessS.LogAdmin(builder);
            player.Permission = perm;
            didChange = true;
            RespondRaw("Ok");
            ChessS.SetPermissionsAThread();
        }

        [Method("PUT"), Path("/chess/api/note")]
        public void AddNewNote(int id, string note, int expires = 31)
        {
            var player = Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            if(expires > 31 || expires < 1)
            {
                RespondRaw("Invalid expiration date");
                return;
            } 
            if(Context.User == null)
            {
                RespondRaw("You must be logged in to do that", 403);
                return;
            }
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                RespondRaw("You dont have permission to do that", 403);
                return;
            }
            LogAdminAction("Note Added", note, ("Against", player.Name), ("Expires", $"{expires} days"));
            player.Notes.Add(new ChessNote(Context.User, note, expires));
            didChange = true;
            RespondRaw("Added");
        }

        [Method("GET"), Path("/chess/api/lastscore")]
        public void ForceSetScores()
        {
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                HTTPError(System.Net.HttpStatusCode.Forbidden, "No permissions", "You are not allowed to do that");
                return;
            }
            foreach(var usrs in Players)
            {
                usrs.SetScoreOnDay(usrs.Rating, DateTime.Now);
            }
            didChange = true;
            RespondRaw(LoadRedirectFile("/chess"));
        }

        [Method("PUT"), Path("/chess/api/player")]
        public void AddNewPlayer(string name)
        {
            if (!doesHavePerm(ChessPerm.CreateUser))
            {
                RespondRaw("Error: You do not have permission to do that", 403);
                return;
            }
            var existing = Players.FirstOrDefault(x => x.Name == name);
            if (existing != null)
            {
                RespondRaw("User already exists", 400);
                return;
            }
            var player = new ChessPlayer()
            {
                Name = name,
                Losses = 0,
                Wins = 0,
                Rating = 100
            };
            Players.Add(player);
            LogAdminAction("Account Created", player.Name);
            didChange = true;
            RespondRaw("Ok");
        }

        [Method("PUT"), Path("/chess/api/present")]
        public void MarkAsPresent(int id)
        {
            if (!doesHavePerm(ChessPerm.ClassRoom))
            {
                RespondRaw("No permission", 403);
                return;
            }
            var player = Players.FirstOrDefault(x => x.Id == id);
            if (player == null || player.Removed)
            {
                RespondRaw("No player", 404);
                return;
            }
            player.DateLastPresent = DateTime.Now;
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle("Player Marked Present")
                .WithDescription($"{Context.User.Name} marks {player.Name} as present");
            ChessS.LogAdmin(builder);
            RespondRaw("Ok");
        }

        [Method("PUT"), Path("/chess/api/changename")]
        public void ChangePlayerName(int id, string newName)
        {
            if (!doesHavePerm(ChessPerm.Justice))
            {
                RespondRaw("Error: You do not have permission to do that", 403);
                return;
            }
            var player = Players.FirstOrDefault(x => x.Id == id);
            if (player == null || player.Removed)
            {
                RespondRaw("Player not found", 404);
                return;
            }
            if (player.ShouldContinueInLoop)
            {
                RespondRaw("That account cannot have its name changed", 403);
                return;
            }
            player.Notes.Add(new ChessNote(Context.User, $"Changed name from {player.Name} to {newName}", 8));
            player.Name = newName;
            didChange = true;
            RespondRaw("Updated");
        }

        [Method("PUT"), Path("/chess/api/remove")]
        public void RemoveUserLeaderboard(int id)
        {
            if(!doesHavePerm(ChessPerm.RemoveUser))
            {
                RespondRaw("No permission", 403);
                return;
            }
            var usr = Players.FirstOrDefault(x => x.Id == id);
            if(usr == null)
            {
                RespondRaw("User not found", 404);
                return;
            }
            if(usr.IsBuiltInAccount)
            {
                RespondRaw("Account is built-in and cannot be removed", 400);
                return;
            }
            if(usr.Removed && usr.IsBanned)
            {
                RespondRaw("User is banned: They must first be unbanned.", 403);
                return;
            }
            usr.Removed = !usr.Removed;
            if(usr.Removed)
            {
                LogAdminAction("User Removed", usr.Name);
            }
            else
            {
                usr.Rating = 100;
                usr.Days = new List<ChessDay>();
                foreach(var otherUser in ChessService.Players)
                {
                    foreach(var otherDay in otherUser.Days)
                    {
                        int v = otherDay.Entries.RemoveAll(x => x.againstId == usr.Id);
                        if(v > 0)
                        {
                            Program.LogMsg($"Removed {v} games between {otherUser.Name} and rejoined {usr.Name}", LogSeverity.Info, "UserRejoin");
                        }
                    }
                }
                usr.Wins = 0;
                usr.Losses = 0;
                LogAdminAction("User Rejoins", usr.Name);
            }
            RespondRaw("User toggled", 200);
            didChange = true;
        }

        [Method("PUT"), Path("/chess/api/score")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.SetActualChessRating)]
        public void ModifyUserScore(int id, int value)
        {
            var usr = Players.FirstOrDefault(x => x.Id == id);
            if (usr == null)
            {
                RespondRaw("User not found", 404);
                return;
            }
            if(usr.ShouldContinueInLoop)
            {
                RespondRaw("Account score cannot be changed");
                return;
            }
            int old = usr.Rating;
            LogAdminAction("Score Manually Set", "Technical modifcation to rating", ("Player", usr.Name), ("Old", old.ToString()), ("New", value.ToString()));
            usr.SetRating(value, Context.User, "manually set via website");
            didChange = old != value;
            RespondRaw("Updated");
        }

        [Method("PUT"), Path("/chess/api/scoremod")]
        [RequireChess(ChessPerm.Moderator)]
        public void ModifyUserScoreMod(int id, int value)
        {
            var usr = Players.FirstOrDefault(x => x.Id == id);
            if (usr == null)
            {
                RespondRaw("User not found", 404);
                return;
            }
            if (usr.ShouldContinueInLoop)
            {
                RespondRaw("Account score cannot be changed");
                return;
            }
            int old = usr.Modifier;
            LogAdminAction("Score Modifier Set", "Modifier made to player's rating", ("Player", usr.Name), ("Old", old.ToString()), ("New", value.ToString()));
            usr.Modifier = value;
            didChange = old != value;
            RespondRaw("Updated");
        }

        [Method("PUT"), Path("/chess/api/dispute")]
        public void DisputeGamePlayed(int day, int year, int user, int entryId)
        {
            var player = Players.FirstOrDefault(x => x.Id == user);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            ChessEntry entry = null;
            ChessPlayer opposition = null;
            DateTime? date = null;
            ChessGameStatus STATE = ChessGameStatus.Draw;
            foreach(var _player in ChessService.Players)
            {
                foreach(var _day in _player.Days)
                {
                    if (_day.Date.Year != year)
                        continue;
                    if (_day.Date.DayOfYear != day)
                        continue;
                    entry = _day.Entries.FirstOrDefault(x => x.Id == entryId);
                    if(entry != null)
                    {
                        opposition = _player.Id == player.Id ? Players.FirstOrDefault(x => x.Id == entry.againstId) : _player;
                        STATE = _player.Id == player.Id ? entry.State : ChessS.SwapStatePerspective(entry.State);
                        date = _day.Date;
                        _day.Entries.Remove(entry);
                        didChange = true;
                        break; // out of _day loop
                    }
                }
                if(entry != null)
                {
                    break;
                }
            }
            if(entry == null || opposition == null || date == null)
            {
                RespondRaw("Unable to find game entry.", 404);
                return;
            }
            player.Notes.Add(new ChessNote(Context.User, $"Removed {STATE} against {opposition.Name}", 8));
            opposition.Notes.Add(new ChessNote(Context.User, $"Removed {ChessS.SwapStatePerspective(STATE)} against {player.Name}", 8));
            LogAdminAction("Game Removed", $"Date: {date.Value.ToShortDateString()}" +
                $"{(entry.onlineGame ? "\r\nOnline: yes" : "")}", 
                ("P1", player.Name), ("P2", opposition.Name), ("State", STATE.ToString()));
            if(STATE == ChessGameStatus.Won)
            {
                player.Wins--;
                opposition.Losses--;
            } else if (STATE == ChessGameStatus.Loss)
            {
                player.Losses--;
                opposition.Wins--;
            }
            RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/monitor")]
        public void ToggleMonitor(int id)
        {
            var player = Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                RespondRaw("No permission", 403);
                return;
            }
            if(player.RequireGameApproval)
            { // only justices may remove approval
                if(!doesHavePerm(ChessPerm.Justice))
                {
                    RespondRaw("Only Justices of the Court of Appeals may remove monitor from players", 403);
                    return;
                }
                player.RequireGameApproval = false;
                LogAdminAction("Monitor Removed", "Removed RequireGameApproval from " + player.Name);
            } else
            { // any mod/justice can set monitor - so we are already authed
                player.RequireGameApproval = true;
                LogAdminAction("Monitor Added", $"Player {player.Name} is now monitored\n" +
                    $"Any game involving them must be manually approved by a Moderator or Justice.\n" +
                    $"Until that happens, the rating change will not apply to either player");
            }
            didChange = true;
            RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/time")]
        public void ToggleRequireTiming(int id)
        {
            var player = Players.FirstOrDefault(x => x.Id == id);
            if (player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            if (!doesHavePerm(ChessPerm.CourtOfAppeals))
            {
                RespondRaw("No permission", 403);
                return;
            }
            player.RequireTiming = !player.RequireTiming;
            didChange = true;
            RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/approve")]
        public void ApproveMonitorGame(string reference)
        {
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                RespondRaw("No permission", 403);
                return;
            }
            var pending = PendingGames.FirstOrDefault(x => x.Reference == reference);
            if(pending == null)
            {
                RespondRaw("Unknown game", 404);
                return;
            }
            int p1Was = pending.Player1.Rating;
            int p2Was = pending.Player2.Rating;
            pending.Player1.Rating += pending.P1_Change;
            pending.Player2.Rating += pending.P2_Change;
            var e = pending.Player1.SetGameOnDay(pending.Player2, pending.Draw ? ChessGameStatus.Draw : ChessGameStatus.Won, pending.RecordedAt);
            e.selfWas = p1Was;
            e.otherWas = p2Was;
            LogAdminAction("Approved Game", $"{Context.User.Name} approves game: {pending.Player1.Name} v {pending.Player2.Name} Draw:{pending.Draw}",
                ("P1 Score", $"{pending.P1_StartScore} + {pending.P1_Change} = {pending.Player1.Rating}"),
                ("P2 Score", $"{pending.P2_StartScore} + {pending.P2_Change} = {pending.Player2.Rating}")
                );
            if(pending.Draw)
            {
                pending.Player1.Losses++;
                pending.Player2.Losses++;
            } else
            {
                pending.Player1.Wins++;
                pending.Player2.Losses++;
            }
            ChessS.LogEntry(pending.Player1, e, pending.Player2);
            didChange = true;
            PendingGames.Remove(pending);
            RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/ban")]
        public void BanUser(int id, string reason, string expires)
        {
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                RespondRaw("No permissions", 403);
                return;
            }
            var player = Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                RespondRaw("unknown player", 404);
                return;
            }
            if(string.IsNullOrWhiteSpace(reason) || reason.Length  > 256 || reason.Length < 16)
            {
                RespondRaw("Reason invalid: empty or length too short or too long", 400);
                return;
            }
            if(string.IsNullOrWhiteSpace(expires) || expires.Length != 10 || expires.Contains('-') == false)
            {
                RespondRaw("Expirery date invalid: incorrect format; require 'yyyy-MM-dd'", 400);
                return;
            }
            var split = expires.Split('-');
            var expiresAt = new DateTime(int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]));
            if(expiresAt.DayOfYear <= DateTime.Now.DayOfYear || expiresAt.Year < DateTime.Now.Year)
            {
                RespondRaw("Expirery date cannot be before now", 400);
                return;
            }
            if(expiresAt.DayOfWeek != DayOfWeek.Friday)
            {
                RespondRaw("Expirery must be on a Friday", 400);
                return;
            }
            if(player.IsBanned)
            {
                RespondRaw("Player is already banned", System.Net.HttpStatusCode.Conflict);
                return;
            }

            var ban = new ChessBan(player, SelfPlayer);
            ban.Reason = reason;
            ban.ExpiresAt = expiresAt;
            player.Bans.Add(ban);
            didChange = true;
            ChessS.LogAdmin(ban);
            RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/register")]
        public void RegisterMultipleUsers(string list)
        {
            if(!doesHavePerm(ChessPerm.ClassRoom))
            {
                RespondRaw("No permission", 403);
                return;
            }
            var splt = list.Split(',');
            List<int> ids = new List<int>();
            foreach(var item in splt)
            {
                if(int.TryParse(item, out int id))
                {
                    ids.Add(id);
                } else
                {
                    RespondRaw($"Could not parse id '{item}' as int", 400);
                    return;
                }
            }
            foreach(var id in ids)
            {
                var player = Players.FirstOrDefault(x => x.Id == id);
                if(player == null)
                {
                    RespondRaw($"Could not find player with id {id}", 400);
                    return;
                }
                if(player.ShouldContinueInLoop || player.IsBanned)
                {
                    RespondRaw($"{player.Name} cannot be marked as present, they are removed, banned or built in", 400);
                    return;
                }
                player.DateLastPresent = DateTime.Now;
            }
            didChange = true;
            RespondRaw("");
        }

#region Match Adding and Rating Calculations
        public static int defaultKFunction(ChessPlayer a) => getKFunction(a.Wins + a.Losses);

        static int getKFunction(int total)
        {
            if (total <= 3)
                return 40;
            if (total > 3 && total <= 6)
                return 30;
            if (total > 6 && total <= 10)
                return 20;
            return 10;
        }

        static double getExpectedRating(ChessPlayer a, ChessPlayer b)
        {
            var denom = 1 + (Math.Pow(10, (b.Rating - a.Rating) / 400d));
            return 1 / denom;
        }

        static double getRating(ChessPlayer a, ChessPlayer b, double actualScore, Func<ChessPlayer, int> kFunction)
        {
            var update = kFunction(a) * (actualScore - getExpectedRating(a, b));
            return a.Rating + update;
        }


        public static void addGameEntry(ChessPlayer winP, ChessPlayer lossP, bool draw, Func<ChessPlayer, int> kFunction, bool onlineGame, out int httpCode)
        {
            if (kFunction == null)
                kFunction = defaultKFunction;
            int winnerRating = (int)Math.Round(getRating(winP, lossP, draw ? 0.5d : 1.0d, kFunction));
            int loserRating = (int)Math.Round(getRating(lossP, winP, draw ? 0.5d : 0.0d, kFunction));
            winP.DateLastPresent = null; // so it auto-calculates
            lossP.DateLastPresent = null;

            httpCode = 201;
            if (winP.RequireGameApproval || lossP.RequireGameApproval || winP.RequireTiming || lossP.RequireTiming)
            {
                var pend = new ChessPendingGame(winP, lossP, DateTime.Now, winnerRating - winP.Rating, loserRating - lossP.Rating);
                pend.OnlineGame = onlineGame;
                pend.Draw = draw;
                PendingGames.Add(pend);
                ChessS.LogEntry(pend);
                if (winP.RequireGameApproval || lossP.RequireGameApproval)
                    httpCode = 204;
                else // require timing
                    httpCode = 202;
            }
            else
            {
                int p1Was = winP.Rating;
                int p2Was = lossP.Rating;
                winP.Rating = winnerRating;
                lossP.Rating = loserRating;
                var e = winP.SetGameOnDay(lossP, draw ? ChessGameStatus.Draw : ChessGameStatus.Won, DateTime.Now);
                e.onlineGame = onlineGame;
                e.selfWas = p1Was;
                e.otherWas = p2Was;
                ChessS.LogEntry(winP, e, lossP);
                if (draw)
                {
                    winP.Losses++; // Draw is a loss for both sides
                    lossP.Losses++;
                }
                else
                {
                    winP.Wins++;
                    lossP.Losses++;
                }
            }

            winP.SetScoreOnDay(winP.Rating, DateTime.Now);
            lossP.SetScoreOnDay(lossP.Rating, DateTime.Now);

        }

        [Method("GET"), Path("/chess/api/testmatch")]
        public void PretendMatch(int winner, int loser, bool draw)
        {
            var winP = Players.FirstOrDefault(x => x.Id == winner);
            var lossP = Players.FirstOrDefault(x => x.Id == loser);
            if (winP == null)
            {
                RespondRaw("Unknown winner", 404);
                return;
            }
            if (lossP == null)
            {
                RespondRaw("Unknown loser", 404);
                return;
            }
            if (winP.Id == lossP.Id)
            {
                RespondRaw("Winner and loser are identical", 400);
                return;
            }
            if (winP.IsBuiltInAccount || lossP.IsBuiltInAccount)
            {
                RespondRaw("One of those Accounts is built-in, so cannot be given a match", 400);
                return;
            }
            if (winP.IsBanned)
            {
                RespondRaw($"{winP.Name} is currently banned.", 403);
                return;
            }
            if (lossP.IsBanned)
            {
                RespondRaw($"{lossP.Name} is currently banned", 403);
                return;
            }
            int winnerRating = (int)Math.Round(getRating(winP, lossP, draw ? 0.5d : 1.0d, defaultKFunction));
            int loserRating = (int)Math.Round(getRating(lossP, winP, draw ? 0.5d : 0.0d, defaultKFunction));
            RespondRaw($"<p>{winP.Name}: {winP.Rating} -> <strong>{winnerRating}</strong></p>" +
                $"<p>{lossP.Name}: {lossP.Rating} -> <strong>{loserRating}</strong></p>");
        }

        [Method("PUT"), Path("/chess/api/match")]
        [RequireChess(ChessPerm.AddMatch)]
        public void AddNewMatch(int winner, int loser, bool draw = false)
        {
            var winP = Players.FirstOrDefault(x => x.Id == winner);
            var lossP = Players.FirstOrDefault(x => x.Id == loser);
            if(winP == null)
            {
                RespondRaw("Unknown winner", 404);
                return;
            }
            if(lossP == null)
            {
                RespondRaw("Unknown loser", 404);
                return;
            }
            if(winP.Id == lossP.Id)
            {
                RespondRaw("Winner and loser are identical", 400);
                return;
            }
            if(winP.IsBuiltInAccount || lossP.IsBuiltInAccount)
            {
                RespondRaw("One of those Accounts is built-in, so cannot be given a match", 400);
                return;
            }
            if(winP.IsBanned)
            {
                RespondRaw($"{winP.Name} is currently banned.", 403);
                return;
            }
            if(lossP.IsBanned)
            {
                RespondRaw($"{lossP.Name} is currently banned", 403);
                return;
            }
            addGameEntry(winP, lossP, draw, null, false, out int httpCode);
            didChange = true;
            RespondRaw("Updated", httpCode);
        }
#endregion
    }
}
