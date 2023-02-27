#if INCLUDE_CHESS
using Discord;
using Discord.Rest;
using DiscordBot.Classes;
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.ServerList;
using DiscordBot.MLAPI.Exceptions;
using DiscordBot.RESTAPI.Functions.HTML;
using DiscordBot.Services;
using DiscordBot.Utils;
using Markdig.Extensions.SelfPipeline;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DiscordBot.Services.ChessService;

namespace DiscordBot.MLAPI.Modules
{
    //[RequireVerifiedAccount]
    public partial class Chess : ChessBase
    {
        public static ChessService ChessS;
        public OauthCallbackService Callbacks { get; set; }
        public Chess(APIContext context) : base(context, "chess")
        {
            ChessS ??= Program.Services.GetRequiredService<ChessService>();
            Callbacks = Program.Services.GetRequiredService<OauthCallbackService>();
        }

        void LogAdminAction(string title, string desc, params (string key, string value)[] fields)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle(title);
            builder.WithDescription(desc);
            if(Context.User.Id == Services.BuiltIn.BuiltInUsers.ChessClass)
            {
                builder.WithFooter(Context.User.VerifiedEmail);
            }
            else
            {
                builder.WithFooter(Context.User.Name, Context.User.FirstValidUser?.GetAvatarUrl());
            }
            foreach (var tuple in fields)
                builder.AddField(tuple.key, tuple.value);
            ChessS.LogAdmin(builder);
        }

        bool didChange = false;

        public override void ResponseHalted(HaltExecutionException ex)
        {
            if (!this.HasResponded)
                base.ResponseHalted(ex);
        }

        public override void BeforeExecute()
        {
            if(!string.IsNullOrWhiteSpace(ChessService.LoadException))
            { // service failed to load
                throw new DiscordBot.MLAPI.HaltExecutionException(LoadException);
            }
            if (Context.User == null)
                return;
            if(Context.User.IsVerified == false && Context.User.Id != ChessS.BuiltInClassUser.Id)
            {
                string url = MLAPI.Modules.MicrosoftOauth.getUrl(Context.User);
                throw new RedirectException(url, "Must verify email.");
            }
            if(Context.Path != "/chess" && Context.Path != "/chess/history")
            {
                if(!Context.User.IsVerified)
                {
                    Context.User.VerifiedEmail = null;
                    string url = MLAPI.Modules.MicrosoftOauth.getUrl(Context.User);
                    if(Context.WantsHTML)
                    {
                        throw new RedirectException(url, "Must verifiy email.");
                    } else
                    {
                        // intentionally don't set Location header
                        // so we can handle this in fetch.
                        await RespondRaw(url, HttpStatusCode.FailedDependency);
                        throw new HaltExecutionException("Must verify email");
                    }
                }
            }
        }

        public override void AfterExecute()
        {
            if (didChange)
                DB.SaveChanges();
            DB.Dispose();
            if(Context.Method != "GET" && SelfPlayer != null && SelfPlayer.Id == ChessService.BuiltInClassRoomChess)
            {
                Context.User.VerifiedEmail = "@";
            }
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
                    switch (rep[^1])
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

        string getPlayerName(ChessPlayer player)
        {
            if (player == null)
                return null;
            return player.Name;
        }

        string getPlayerNameRow(ChessPlayer player, int fridays)
        {
            string warning = addIconsToRow(player, fridays);
            return $"{aLink($"/chess/history?id={player.Id}", $"<label>{getPlayerName(player)}</label>")}{warning}";
        }

        string loginButton()
        {
            if (Context.User == null)
                return $"<input type='button' value='Login' onclick=\"window.location.href = '/login';\"/>";
            if (Context.User.Id == ChessS.BuiltInClassUser.Id && Context.User.VerifiedEmail != null)
                return $"<input type='button' value='Logout as {Context.User.VerifiedEmail?.Substring(0, Context.User.VerifiedEmail.IndexOf('@')).ToLower()}' onclick=\"window.location.href = '/chess/logout';\"/>";
            return "";
        }

        string getDiscordLink()
        {
            if (Context.User == null || Context.User.ServiceUser)
                return "";
            if (SelfPlayer == null)
                return "";
            var inChs = Program.ChessGuild.GetUser(SelfPlayer.ConnectedAccount);
            if (inChs != null)
                return "";
            var div = new Div("popupInvite", "popup")
            {
                Style = "background-color: red; color: white; border-color: blue;",
                Children =
                {
                    new Paragraph("")
                    {
                        Children =
                        {
                            new StrongText(new Anchor("/chess/invite", "Chess Discord Server")),
                            new RawObject("<br/>"),
                            new RawObject("You can click " + new Anchor("/chess/invite", "this link here") + " to join the Chess Discord server"),
                            new RawObject(", where:")
                        }
                    },
                    new UnorderedList()
                        .AddItem("played games are stored;")
                        .AddItem("any updates to the rules are sent;")
                        .AddItem("any penalties or sanctions are disclosed;")
                        .AddItem("any Court of Appeals hearings are announced, or may take place;"),
                    new Paragraph(new Anchor("#", "Click here to close")
                            {
                                OnClick = "setHideCookie(this);"
                            })
                }
            };
            return div;
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
            if(player.Permission == ChessPerm.Arbiter)
            {
                warning += "<img " +
                    "src='https://masterlist.uk.ms/imgs/arbiter.png' " +
                    "alt='Arbiter' " +
                    "style='margin-left: 5px' " +
                    $"width='18' height='20' valign='middle' style='margin-left: 5px;' title='This player is the Arbiter'>";

            }
            if (player.Permission == ChessPerm.Moderator)
            {
                warning += "<img " +
                    "src='https://masterlist.uk.ms/imgs/moderator.png' " +
                    "alt='Moderator' " +
                    "style='margin-left: 5px' " +
                    $"width='18' height='20' valign='middle' style='margin-left: 5px;' title='This player is a Moderator'>";

            }
            if (player.Permission.HasFlag(ChessPerm.Justice))
            {
                warning += "<img " +
                    "src='https://sweetclipart.com/multisite/sweetclipart/files/legal_scales_black_silhouette.png' " +
                    "alt='Justice' " +
                    "style='margin-left: 5px' " +
                    $"width='20' height='20' valign='middle' style='margin-left: 5px;' title='This player is on the Court of Appeals'>";

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
            if (doesHavePerm(ChessPerm.Moderator) && player.ActiveNotes.Count > 0)
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
        public void Base()
        {
            string TABLE = "";
            int rank = 1;
            foreach (var player in DB.Players.ToList().OrderByDescending(x => x.Rating + x.Modifier).ThenByDescending(x => x.WinRate).ThenByDescending(x => x.Wins + x.Losses))
            {
                if ((player.Removed || player.IsBuiltInAccount) && player.Id != ChessService.AIPlayer.Id)
                    continue;
                var lastDate = ChessS.getLastPresentDate(DB, player);
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
            await ReplyFile("base.html", 200, new Replacements() 
                .Add("table", TABLE)
                .Add("admin", adminItems)
                .Add("loginBtn", loginButton())
                .Add("discord_link", link));
        }

#region Joining Chess Server

        void handleJoinCallback(object sender, object[] args)
        {
            var oauth = new DiscordOauth("guilds.join", Context.GetQuery("code"));
            var response = oauth.JoinToServer(Program.ChessGuild, Context.User).Result;
            if(!response.IsSuccessStatusCode)
            {
                await RespondRaw("Error: " + response.Content.ReadAsStringAsync().Result, response.StatusCode);
            } else
            {
                ChessS.SetPermissionsAThread();
                await RespondRedirect("/chess"), HttpStatusCode.Redirect);
            }
        }

        [Method("GET"), Path("/chess/invite")]
        [RequireChess(ChessPerm.Player)]
        public void JoinChessServer()
        {
            var state = Callbacks.Register(handleJoinCallback);
            var uri = UrlBuilder.Discord()
                .Add("response_type", "code")
                .Add("redirect_uri", Handler.LocalAPIUrl + "/oauth2/discord")
                .Add("state", state)
                .Add("scope", "guilds.join");
            await RespondRedirect(uri), HttpStatusCode.Redirect);
        }
#endregion

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
            foreach(var usr in DB.Players.ToList().OrderBy(x => x.Name))
            {
                if (usr.ShouldContinueInLoop || usr.IsBanned)
                    continue;
                string ROW = $"<tr><td>";
                ROW += $"<input id='i-{i++}' type='text' class='text' maxlength='1' value='' placeholder='leave blank if not here' onkeyup='return addPresent(event, this, \"{usr.Id}\");'/>";
                ROW += $"{getPlayerNameRow(usr, 0)}</td>";
                ROW += $"<td>{ChessS.getLastPresentDate(DB, usr, true):yyyy-MM-dd}</td>";
                TABLE += ROW + "</tr>";
            }
            await ReplyFile("register.html", 200, new Replacements().Add("table", TABLE));
        }

        bool shouldIncludeInRecommend(ChessPlayer player)
        {
            if (player.ShouldContinueInLoop)
                return false;
            if (player.DateLastPresent.HasValue && player.DateLastPresent.Value.DayOfYear == DateTime.Now.DayOfYear)
                return true;
            var val = ChessS.BuildEntries(player, DB, DateTime.Now, true);
            return val.Count > 0;
        }

        [Method("GET"), Path("/chess/logout")]
        public void ClearLoginInfo()
        {
            if(ChessS.BuiltInClassUser.Id == Context.User?.Id)
            { // Special logout case, we just clear the verified email.
                Context.User.IsVerified = false;
                Context.User.VerifiedEmail = null;
                await RespondRedirect("/chess"), HttpStatusCode.Redirect);
            } else
            {
                await RespondRedirect("/logout"), HttpStatusCode.Redirect);
            }
        }

        [Method("GET"), Path("/chess/recommend")]
        public void RecommendGames()
        {
            if(!doesHavePerm(ChessPerm.ClassRoom))
            {
                await RespondRaw("No permissions", 403);
                return;
            }
            string TABLE = "";
            var now = DateTime.Now.DayOfYear;
            var ppresent = DB.Players.ToList().Where(shouldIncludeInRecommend);
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
                    calculators.Add(new ChessWeightedRecommend(DB, player, other));
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
            await ReplyFile("recommend.html", 200, new Replacements()
                .Add("table", TABLE)
                .Add("colm", debug ? "<th>debug</th>" : ""));
        }

#region Chess Clock

#if DEBUG
        const int chessClockMinimum = 10;
#else
        const int chessClockMinimum = 60 * 3;
#endif

        [Method("GET"), Path("/chess/clock")]
        public void Clock(int wsec, int bsec, int wp, int bp)
        {
            if(wsec > (60 * 59) || bsec > (60 * 59))
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "Out of Range", "Time cannot be greater than an hour");
                return;
            }

            if(wsec < chessClockMinimum || bsec < chessClockMinimum)
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "Out of Range", $"Time cannot be less than {Program.FormatTimeSpan(TimeSpan.FromSeconds(chessClockMinimum))}");
                return;
            }
            var game = new ChessTimedGame();
            game.White = DB.Players.FirstOrDefault(x => x.Id == wp);
            game.Black = DB.Players.FirstOrDefault(x => x.Id == bp);
            if(game.White == null || game.Black == null 
                || game.White.ShouldContinueInLoop || game.White.IsBanned
                || game.Black.ShouldContinueInLoop || game.Black.IsBanned)
            {
                HTTPError(HttpStatusCode.BadRequest, "Player", "One of the players selected is unknown");
                return;
            }
            game.WhiteTime = wsec * 1000;
            game.BlackTime = bsec * 1000;
            var id = ChessS.AddTimedGame(game);

            await RespondRedirect($"/chess/clock?id={id}"), HttpStatusCode.Redirect);
        }

        [Method("GET"), Path("/chess/clock")]
        public void Clock(Guid id)
        {
            if(!ChessService.TimedGames.TryGetValue(id, out var game))
            {
                await RespondRaw("No game by that ID", 404);
                return;
            }
            await ReplyFile("chessclock.html", 200, new Replacements()
                .Add("wsId", id));
        }

        [Method("GET"), Path("/chess/clock")]
        public void SetupClock()
        {
            var firstList = GetPlayerList("player1", x => !x.IsBanned);
            var secondList = GetPlayerList("player2", x => !x.IsBanned);
            await ReplyFile("setupChessClock.html", 200, new Replacements()
                .Add("player", SelfPlayer)
                .Add("selectp1", firstList)
                .Add("selectp2", secondList));
        }
#endregion

        [Method("GET"), Path("/chess/ban")]
        public void ChessBan(int id)
        {
            var target = DB.Players.FirstOrDefault(x => x.Id == id);
            if(target == null || target.Removed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Player", "Unknown player with that Id");
                return;
            }
            await ReplyFile("ban.html", 200, new Replacements().Add("target", target));
        }

#if DEBUG // only want to be able to directly add games in debug config
        [Method("GET"), Path("/chess/previous")]
        public void HistoricGameBase()
        {
            string players = "";
            foreach (var player in DB.Players.ToList().OrderByDescending(x => x.Rating))
            {
                if (player.ShouldContinueInLoop)
                    continue;
                string bannnn = "";
                if (player.IsBanned)
                {
                    bannnn = "disabled class='banned'";
                }
                players += $"<option {bannnn} value=\"{player.Id}\">{getPlayerName(player)}</option>";
            }
            await ReplyFile("debug_addmatch.html", 200, new Replacements().Add("playerlist", players));
        }
#endif

#region Thing
        [Method("POST"), Path("/chess/api/pullr")]
        [RequireValidHTTPAgent(false)]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireVerifiedAccount(false)]
        public void HandleHTTPFilesPr()
        {
            await RespondRaw("Ok");
        }
        static string thing;
        [Method("GET"), Path("/chess/api/pullr")]
        [RequireChess(ChessPerm.ChiefJustice)]
        public void GetThing()
        {
            await RespondRaw(thing ?? "Not set");
        }
#endregion

        [Method("GET"), Path("/chess/api/lawcss")]
        public void LawStyle()
        {
            await ReplyFile("terms/lawstyle.css", 200);
        }

        [Method("GET"), Path("/chess/conduct")]
        public void Regulations(bool raw = false)
        {
            var service = Program.Services.GetRequiredService<LegislationService>();
            if (!service.Laws.TryGetValue("conduct", out var act))
            {
                HTTPError(HttpStatusCode.NotFound, "", "Could not find Conduct Regulations");
                return;
            }
            var page = LegislationService.PageForAct(act, raw);
            await RespondRaw(ReplaceMatches(page, new Replacements()), HttpStatusCode.OK);
        }

        [Method("GET"), Path("/chess/terms")]
        public void TermsAndCons(bool raw = false)
        {
            string mods = "";
            string justices = "<br/>- Chief Justice Alex C<br/>";
            foreach (var player in DB.Players.ToList().Where(x => x.Permission.HasFlag(ChessPerm.Moderator)).OrderBy(x => x.Name))
            {
                mods += $"- {getPlayerName(player)}<br/>";
            }
            foreach(var player in DB.Players.ToList().Where(x => x.Permission == ChessPerm.Justice).OrderBy(x => x.Name))
            {
                justices += $"- Justice {getPlayerName(player)}<br/>";
            }
            var service = Program.Services.GetRequiredService<LegislationService>();
            if(!service.Laws.TryGetValue("terms", out var act))
            {
                HTTPError(HttpStatusCode.NotFound, "", "Could not find Terms and Conditions");
                return;
            }
            var page = LegislationService.PageForAct(act, raw);
            await RespondRaw(ReplaceMatches(page, new Replacements()
                .Add("mods", mods)
                .Add("justices", justices)), HttpStatusCode.OK);
        }

        [Method("GET"), Path("/chess/arbiter")]
        [RequireChess(ChessPerm.Player)]
        public void GetElections()
        {
            int satisfies = ChessService.GetPlayedAgainst(SelfPlayer, ChessService.VoterGamesRequired).Count;
            if(satisfies < ChessService.VoterGamesRequired)
            {
                await RespondRaw(new HTMLPage()
                {
                    Children =
                    {
                        new PageHeader(),
                        new PageBody()
                        {
                            Children =
                            {
                                new Paragraph($"You are not able to vote; " + new Anchor("/chess/terms#14A-6-a", "please see the Terms and Conditions for eligibility conditions"))
                            }
                        }
                    }
                }, 403);
            }
            string table = "";
            string ineligible = "";
            foreach(var player in DB.Players.ToList().OrderBy(x => x.Name))
            {
                if (player.ShouldContinueInLoop || player.Id == SelfPlayer.Id)
                    continue;
                var result = checkArbiterCandidacy(player);
                if (result.IsSuccess)
                {
                    string ROW = "<tr>";
                    ROW += $"<td>{player.Name}</td>";
                    int current = SelfPlayer.ArbVotes.FirstOrDefault(x => x.VoteeId == player.Id)?.Score ?? 0;
                    for (int i = -2; i <= 2; i++)
                    {
                        ROW += $"<td><input type='checkbox' {(i == current ? "checked" : "")} onclick='setVote({player.Id}, {i})'></td>";
                    }
                    table += ROW + "</tr>";
                } else
                {
                    SelfPlayer.ArbVotes.RemoveAll(x => x.VoteeId == player.Id);
                    didChange = true;
                    ineligible += new ListItem($"<strong>{player.Name}:</strong> {result.ErrorReason}");
                }
            }
            await ReplyFile("election.html", 200, new Replacements()
                .Add("table", table)
                .Add("inelig", ineligible)
                .Add("arbiter", DB.Players.FirstOrDefault(x => x.Permission.HasFlag(ChessPerm.Arbiter)).Name)
                );
        }

        [Method("PUT"), Path("/chess/api/elect")]
        [RequireChess(ChessPerm.Player)]
        public void SetVote(int id, int value)
        {
            if(ChessService.GetPlayedAgainst(SelfPlayer, ChessService.VoterGamesRequired).Count < ChessService.VoterGamesRequired)
            {
                await RespondRaw($"You are unable to vote; must have played at least {ChessService.VoterGamesRequired} games", 403);
                return;
            }
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            var result = checkArbiterCandidacy(player);
            if (!result.IsSuccess)
            {
                await RespondRaw("Invalid: " + result.ErrorReason, 404);
                return;
            }
            value = Math.Clamp(value, -2, 2);
            SelfPlayer.ArbVotes.RemoveAll(x => x.VoteeId == id);
            SelfPlayer.ArbVotes.Add(new ArbiterVote(SelfPlayer)
            {
                Score = value
            });
            didChange = true;
            await RespondRaw("");
        }

        [Method("GET"), Path("/chess/match")]
        [RequireChess(ChessPerm.AddMatch)]
        public void MatchBase()
        {
            string players = "";
            foreach (var player in DB.Players.AsQueryable().OrderByDescending(x => x.Rating).ToList())
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
            await ReplyFile("match.html", 200, new Replacements()
                .Add("playerlist", players));
        }

        List<DateTime> getDatesForHistory()
        {
            var dates = new List<DateTime>();
            DateTime start = ChessS.GetFridayOfThisWeek();
            do
            {
                if (!ChessS.IsHoliday(start))
                { 
                    dates.Add(start);
                }
                start = start.AddDays(-7);
            } while (dates.Count < 6);
            return dates;
        }

        [Method("GET"), Path("/chess/history")]
        public void UserHistory(int id, bool full = false)
        {
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
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
            bool ADMIN = doesHavePerm(ChessPerm.ChiefJustice);
            string TABLE = "";
            List<DateTime> dates;
            if(full)
            {
                dates = new List<DateTime>();
                foreach (var game in DB.Games)
                    dates.Add(game.Timestamp);
                dates = dates.Distinct(new Classes.DateEquality()).OrderBy(x => x).ToList();
            } else
            {
                dates = getDatesForHistory();
            }
            foreach (var date in dates)
            {
                var anyGames = DB.GetGamesOnDate(player.Id, date).ToList();
                var entries = anyGames.Where(x => x.IsApproved).ToList();
                var modPending = new List<ChessGame>();
                var playerPending = new List<ChessGame>();
                if (doesHavePerm(ChessPerm.Moderator))
                    modPending = anyGames.Where(x => x.NeedsModApproval).ToList();
                foreach(var game in anyGames)
                {
                    if (game.IsApproved)
                        continue;
                    if (game.NeedsModApproval)
                        continue;

                    if (game.NeedsWinnerApproval && game.WinnerId == SelfPlayer.Id)
                        playerPending.Add(game);
                    else if (game.NeedsLoserApproval && game.LoserId == SelfPlayer.Id)
                        playerPending.Add(game);
                }

                int total = entries.Count + modPending.Count + playerPending.Count;
                string DATE = $"<tr><th rowspan='{total + 2}'>{date.DayOfWeek} {date.ToShortDateString()}</th></tr><tr>";
                if (entries.Count == 0)
                {
                    DATE += $"<td colspan='2'>No games played</td></tr>";
                } else
                {
                    int count = 0;
                    foreach (var entry in entries)
                    {
                        count++;
                        var against = entry.WinnerId == player.Id ? entry.Loser : entry.Winner;
                        var state = entry.Draw ? "Draw" : entry.WinnerId == player.Id ? "Winner" : "Loser";
                        DATE += $"<td>{(getPlayerName(against) ?? "unknown")}</td><td>{state}";
                        if (ADMIN)
                        {
                            DATE += $"<input type='button' value='Remove' style='margin-left:5px;' onclick='dispute(\"{entry.Id}\");' />";
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
                var pending = new List<ChessGame>();
                pending.AddRange(modPending);
                pending.AddRange(playerPending);
                foreach(var thing in pending)
                {
                    bool modOrPlayer = thing.NeedsModApproval;
                    string ROW = $"<tr style='background-color: {(modOrPlayer ? "orange" : "blue")};'>";
                    var against = thing.WinnerId == player.Id 
                        ? (thing.Loser ?? DB.Players.FirstOrDefault(x => x.Id == thing.LoserId)) 
                        : (thing.Winner ?? DB.Players.FirstOrDefault(x => x.Id == thing.WinnerId));
                    var type = thing.Draw ? "Draw" : thing.WinnerId == player.Id ? "Won" : "Loss";
                    ROW += $"<td>{getPlayerName(against)}</td>";
                    ROW += $"<td>{type} ";
                    string approveStr = $"<input type='button' value='Approve' onclick='approveGame(\"{thing.Id}\");'/>";
                    if (thing.NeedsModApproval)
                    {
                        if(thing.WinnerId == SelfPlayer.Id || thing.LoserId == SelfPlayer.Id)
                        {
                            approveStr = "<span class='label label-error'>Cannot approve own game</span>";
                        } else if(!doesHavePerm(ChessPerm.Moderator))
                        {
                            approveStr = "";
                        }
                    } else
                    {
                        if(thing.NeedsLoserApproval == false && thing.LoserId == SelfPlayer.Id)
                        {
                            approveStr = $"<span class='label label-error'>Game must be approved by {against.Name}</span>";
                        } else if (thing.NeedsWinnerApproval == false && thing.WinnerId == SelfPlayer.Id)
                        {
                            approveStr = $"<span class='label label-error'>Game must be approved by {against.Name}</span>";
                        }
                    }
                    ROW += approveStr;
                    ROW += "</td>";
                    TABLE += ROW + "</tr>";
                }
            }


            List<ChessNote> notes = new List<ChessNote>();
            var lastDate = ChessS.getLastPresentDate(DB, player);
            var lastDiff = DateTime.Now - lastDate;
            if (lastDiff.TotalDays > 14)
            {
                var note = new ChessNote(null, player, $"No games played in {(int)lastDiff.TotalDays} days", 1);
                notes.Add(note);
            }
            notes.AddRange(player.ActiveNotes);

            string WARNINGS = "";
            if (notes.Count > 0 && doesHavePerm(ChessPerm.Moderator))
            {
                WARNINGS = $"<div id='warnings' style='border: 3px orange solid;background-color: #ff6666;width: 100%;margin: 5px;padding: 2px;'><ul>";
                foreach (var note in notes)
                {
                    WARNINGS += $"<li><strong>{note.Text}</strong>";
                    if (note.OperatorId != 0)
                    {
                        var oper = DB.Players.FirstOrDefault(x => x.Id == note.OperatorId);
                        WARNINGS += $" ({oper.Name})";
                    }
                    var difff = note.ExpiresAt - DateTime.Now;
                    WARNINGS += $"; expires in {(int)difff.TotalDays} days</li>";
                }
                WARNINGS += "</ul></div>";
            }

            await ReplyFile("history.html", 200, new Replacements()
                .Add("table", TABLE)
                .Add("target", player)
                .Add("warnings", WARNINGS));
        }

#region Modify User
        TableRow _modGetHeaders()
        {
            var row = new TableRow();
            row.WithHeader("Name");
            row.WithHeader("Score");
            if (doesHavePerm(ChessPerm.Arbiter))
                row.WithHeader("Remove");
            if (doesHavePerm(ChessPerm.Moderator))
                row.WithHeader("Add Note");
            row.WithHeader("Monitored");
            row.WithHeader("Requires Chessclock");
            row.WithHeader("Banned");
            if (doesHavePerm(ChessPerm.Arbiter))
                row.WithHeader("Moderator");
            return row;
        }

        TableData _modName(ChessPlayer user)
        {
            if (doesHavePerm(ChessPerm.Moderator))
                return new TableData(new Input("button", user.Name)
                {
                    OnClick = $"changename({user.Id}, '{user.Name}');"
                });
            return new TableData(new Anchor($"/chess/history?id={user.Id}", user.Name));
        }
        TableData _modScoreRating(ChessPlayer user)
        {
            var data = new TableData("");
            if(Context.HasPerm(Perms.Bot.Developer.SetActualChessRating))
            {
                data.Children.Add(new Input("button", $"{user.Rating}")
                {
                    OnClick = $"change('{user.Id}');"
                });
            } else
            {
                data.Children.Add(new Label(user.Rating.ToString()));
            }

            if (doesHavePerm(ChessPerm.Moderator))
            {
                data.Children.Add(new Input("button", $"{(user.Modifier > 0 ? "+" : "")}{user.Modifier}")
                {
                    OnClick = $"changemod('{user.Id}');"
                });
            } else
            {
                data.Children.Add(new Label($"{(user.Modifier > 0 ? "+" : "")}{user.Modifier}"));
            }
            return data;
        }
        TableData _modRemove(ChessPlayer user)
        {
            if (!doesHavePerm(ChessPerm.Arbiter))
                return null;
            return new TableData("")
            {
                Children =
                {
                    new Input("button", user.Removed ? "Add" : "Remove")
                    {
                        OnClick = $"remove(\"{user.Name}\", \"{user.Id}\");"
                    }
                }
            };
        }
        TableData _modAddNote(ChessPlayer user)
        {
            if (!doesHavePerm(ChessPerm.Moderator))
                return null;
            return new TableData("")
            {
                Children =
                {
                    new Input("button", "Add")
                    {
                        OnClick = $"addNote('{user.Id}');"
                    }
                }
            };
        }
        TableData _modMonitor(ChessPlayer user)
        {
            if (!doesHavePerm(ChessPerm.Moderator))
                return new TableData(user.RequireGameApproval ? "Yes" : "No");
            return new TableData("")
            {
                Children =
                {
                    new Input("checkbox", "")
                    {
                        OnClick = $"toggleMonitor('{user.Id}')",
                        Checked = user.RequireGameApproval
                    }
                }
            };
        }
        TableData _modTiming(ChessPlayer user)
        {
            if (!doesHavePerm(ChessPerm.Moderator))
                return new TableData(user.RequireTiming ? "Yes" : "No");
            return new TableData("")
            {
                Children =
                {
                    new Input("checkbox")
                    {
                        OnClick = $"toggleTime('{user.Id}')",
                        Checked = user.RequireTiming
                    }
                }
            };
        }
        TableData _modBan(ChessPlayer user)
        {
            var data = new TableData("");
            if(doesHavePerm(ChessPerm.Arbiter) || doesHavePerm(ChessPerm.ChiefJustice))
            {
                data.Children.Add(new Input("checkbox")
                {
                    OnClick = (user.IsBanned ? "unBanUser" : "banUser") + $"('{user.Id}')",
                    ReadOnly = (user.IsBanned == false && doesHavePerm(ChessPerm.Moderator) == false)
                });
            } else if (doesHavePerm(ChessPerm.Moderator))
            {
                data.Children.Add(new Input("checkbox")
                {
                    OnClick = user.IsBanned ? "" : $"banUser('{user.Id}')",
                    Checked = user.IsBanned,
                    ReadOnly = user.IsBanned,
                });
            }
            return data;
        }
        TableData _modModerator(ChessPlayer user)
        {
            if (!doesHavePerm(ChessPerm.Arbiter))
                return null;
            var result = checkModeratorCandidacy(user);
            if(result.IsSuccess)
            {
                return new TableData("")
                {
                    Children =
                {
                    new Input("checkbox", id: user.Id.ToString())
                    {
                        Checked = user.Permission == ChessPerm.Moderator,
                        OnClick = $"modUser(this)"
                    }
                    .WithTag("data-name", user.Name)
                }
                };
            } else
            {
                return new TableData($"<abbr title='{result.ErrorReason}'>Not eligible</abbr>");
            }
        }


        [Method("GET"), Path("/chess/userlist")]
        [RequireChess(ChessPerm.Moderator, OR = "permission")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.SetActualChessRating, OR = "permission")]
        public void ModifyUser()
        {
            var TABLE = new Table();
            TABLE.Children.Add(_modGetHeaders());
            foreach (var user in DB.Players.AsQueryable().OrderByDescending(x => x.Rating).ToList())
            {
                var ROW = new TableRow();
                if(user.Removed)
                {
                    if (doesHavePerm(ChessPerm.Arbiter))
                        ROW.Class = "removed";
                    else
                        continue;
                }
                var datas = new List<TableData>()
                {
                    _modName(user),
                    _modScoreRating(user),
                    _modRemove(user),
                    _modAddNote(user),
                    _modMonitor(user),
                    _modTiming(user),
                    _modBan(user),
                    _modModerator(user),
                }.Where(x => x != null);
                ROW.Children.AddRange(datas);
                TABLE.Children.Add(ROW);
            }
            await ReplyFile("user.html", 200, new Replacements().Add("table", TABLE));
        }

#endregion

        [Method("GET"), Path("/chess/account")]
        [RequireVerifiedAccount(false)]
        [RequireApproval(false)]
        [RequireAuthentication(false)]
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
                {"coa", doesHavePerm(ChessPerm.ChiefJustice).ToString() }
            };
            string TEXT = "";
            foreach(var keypair in dict)
            {
                TEXT += $"<li><strong>{keypair.Key}</strong>: {keypair.Value}</li>";
            }
            await ReplyFile("account.html", 200, new Replacements().Add("list", TEXT));
        }

        [Method("GET"), Path("/chess/perms")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.ConnectChess, OR = "permission")]
        [RequireChess(ChessPerm.Justice, OR = "permission")]
        public void UserPermissions()
        {
            string TABLE = "";
            foreach(var player in DB.Players.AsQueryable().OrderBy(x => x.Id).ToList())
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
            await ReplyFile("perms.html", 200, new Replacements().Add("table", TABLE));
        }

        [Method("PUT"), Path("/chess/api/connect")]
        [RequireChess(ChessPerm.ChiefJustice, OR = "permission")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.ConnectChess, OR = "permission")]
        public void ConnectPlayer(int chessId, ulong discord)
        {
            if(Context.User == null)
            {
                await RespondRaw("Must be logged in", 403);
                return;
            }
            var player = DB.Players.FirstOrDefault(x => x.Id == chessId);
            if(player == null)
            {
                await RespondRaw("Unknown player", 404);
                return;
            }
            if(player.IsBuiltInAccount)
            {
                await RespondRaw("Account is built-in and cannot be modified", 403);
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
                    await RespondRaw("Unknown discord Id", 404);
                    return;
                }
                player.ConnectedAccount = discord;
            }
            didChange = true;
            await RespondRaw("Set");
        }

        [Method("PUT"), Path("/chess/api/setperm")]
        [RequireChess(ChessPerm.ChiefJustice)]
        public void SetPermPlayer(int id, int role)
        {
            var perm = (ChessPerm)role;
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                await RespondRaw("Unknown player", 404);
                return;
            }
            if(player.IsBuiltInAccount)
            {
                await RespondRaw("This account is built-in and cannot be changed", 403);
                return;
            }
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Member Permission Changed";
            builder.Description = $"{player.Name}'s permissions have been changed:\n" +
                $"{player.Permission} -> {perm}";
            ChessS.LogAdmin(builder);
            player.Permission = perm;
            didChange = true;
            await RespondRaw("Ok");
            ChessS.SetPermissionsAThread();
        }

        [Method("PUT"), Path("/chess/api/note")]
        [RequireChess(ChessPerm.Moderator)]
        public void AddNewNote(int id, string note, int expires = 31)
        {
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                await RespondRaw("Unknown player", 404);
                return;
            }
            if(expires > 31 || expires < 1)
            {
                await RespondRaw("Invalid expiration date");
                return;
            } 
            if(Context.User == null)
            {
                await RespondRaw("You must be logged in to do that", 403);
                return;
            }
            LogAdminAction("Note Added", note, ("Against", player.Name), ("Expires", $"{expires} days"));
            var thing = new ChessNote(SelfPlayer, player, note, expires);
            player.Notes.Add(thing);
            didChange = true;
            await RespondRaw("Added");
        }

        [Method("GET"), Path("/chess/api/lastscore")]
        public void ForceSetScores()
        {
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                HTTPError(System.Net.HttpStatusCode.Forbidden, "No permissions", "You are not allowed to do that");
                return;
            }
            foreach(var usrs in DB.Players)
            {
                usrs.SetScoreOnDay(usrs.Rating, DateTime.Now);
            }
            didChange = true;
            await RespondRedirect("/chess"));
        }

        [Method("PUT"), Path("/chess/api/player")]
        public void AddNewPlayer(string name)
        {
            if (!doesHavePerm(ChessPerm.CreateUser))
            {
                await RespondRaw("Error: You do not have permission to do that", 403);
                return;
            }
            var existing = DB.Players.FirstOrDefault(x => x.Name == name);
            if (existing != null)
            {
                await RespondRaw("User already exists", 400);
                return;
            }
            var player = new ChessPlayer()
            {
                Name = name,
                Losses = 0,
                Wins = 0,
                Rating = StartingValue,
            };
            DB.Players.Add(player);
            LogAdminAction("Account Created", player.Name);
            didChange = true;
            await RespondRaw("Ok");
        }

        [Method("PUT"), Path("/chess/api/present")]
        public void MarkAsPresent(int id)
        {
            if (!doesHavePerm(ChessPerm.ClassRoom))
            {
                await RespondRaw("No permission", 403);
                return;
            }
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if (player == null || player.Removed)
            {
                await RespondRaw("No player", 404);
                return;
            }
            player.DateLastPresent = DateTime.Now;
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle("Player Marked Present")
                .WithDescription($"{Context.User.Name} marks {player.Name} as present");
            ChessS.LogAdmin(builder);
            await RespondRaw("Ok");
        }

        [Method("PUT"), Path("/chess/api/changename")]
        public void ChangePlayerName(int id, string newName)
        {
            if (!doesHavePerm(ChessPerm.Justice))
            {
                await RespondRaw("Error: You do not have permission to do that", 403);
                return;
            }
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if (player == null || player.Removed)
            {
                await RespondRaw("Player not found", 404);
                return;
            }
            if (player.ShouldContinueInLoop)
            {
                await RespondRaw("That account cannot have its name changed", 403);
                return;
            }
            player.Notes.Add(new ChessNote(SelfPlayer, player, $"Changed name from {player.Name} to {newName}", 8));
            player.Name = newName;
            didChange = true;
            await RespondRaw("Updated");
        }

        [Method("PUT"), Path("/chess/api/remove")]
        public void RemoveUserLeaderboard(int id)
        {
            if(!doesHavePerm(ChessPerm.RemoveUser))
            {
                await RespondRaw("No permission", 403);
                return;
            }
            var usr = DB.Players.FirstOrDefault(x => x.Id == id);
            if(usr == null)
            {
                await RespondRaw("User not found", 404);
                return;
            }
            if(usr.IsBuiltInAccount)
            {
                await RespondRaw("Account is built-in and cannot be removed", 400);
                return;
            }
            if(usr.Removed && usr.IsBanned)
            {
                await RespondRaw("User is banned: They must first be unbanned.", 403);
                return;
            }
            usr.Removed = !usr.Removed;
            if(usr.Removed)
            {
                LogAdminAction("User Removed", usr.Name);
            }
            else
            {
                usr.Rating = StartingValue;
                usr.Wins = 0;
                usr.Losses = 0;
                usr.Modifier = 0;
                LogAdminAction("User Rejoins", usr.Name);
            }
            await RespondRaw("User toggled", 200);
            didChange = true;
        }

        [Method("PUT"), Path("/chess/api/score")]
        [RequirePermNode(DiscordBot.Perms.Bot.Developer.SetActualChessRating)]
        public void ModifyUserScore(int id, int value)
        {
            var usr = DB.Players.FirstOrDefault(x => x.Id == id);
            if (usr == null)
            {
                await RespondRaw("User not found", 404);
                return;
            }
            if(usr.ShouldContinueInLoop)
            {
                await RespondRaw("Account score cannot be changed");
                return;
            }
            if(value == usr.Rating)
            {
                await RespondRaw("Values are identical");
                return;
            }
            int old = usr.Rating;
            LogAdminAction("Score Manually Set", "Technical modifcation to rating", ("Player", usr.Name), ("Old", old.ToString()), ("New", value.ToString()));
            usr.Rating = value;
            var note = new ChessNote(SelfPlayer, usr, $"Rating set to {value} from {old} ({value - old})", 14);
            usr.Notes.Add(note);
            didChange = true;
            await RespondRaw("Updated");
        }

        [Method("PUT"), Path("/chess/api/scoremod")]
        [RequireChess(ChessPerm.Moderator)]
        public void ModifyUserScoreMod(int id, int value)
        {
            var usr = DB.Players.FirstOrDefault(x => x.Id == id);
            if (usr == null)
            {
                await RespondRaw("User not found", 404);
                return;
            }
            if (usr.ShouldContinueInLoop)
            {
                await RespondRaw("Account score cannot be changed");
                return;
            }
            int old = usr.Modifier;
            LogAdminAction("Score Modifier Set", "Modifier made to player's rating", ("Player", usr.Name), ("Old", old.ToString()), ("New", value.ToString()));
            usr.Modifier = value;
            didChange = old != value;
            await RespondRaw("Updated");
        }

        [Method("PUT"), Path("/chess/api/dispute")]
        public void DisputeGamePlayed(int gameId)
        {
            var entry = DB.Games.FirstOrDefault(x => x.Id == gameId);
            if(entry == null)
            {
                await RespondRaw("Unable to find game entry.", 404);
                return;
            }
            var winner = entry.Winner;
            var opposition = entry.Loser;
            winner.Notes.Add(new ChessNote(SelfPlayer, winner, $"Removed {(entry.Draw ? "draw" : "win")} against {opposition.Name}", 8));
            opposition.Notes.Add(new ChessNote(SelfPlayer, opposition, $"Removed {(entry.Draw ? "draw" : "loss")} against {winner.Name}", 8));
            LogAdminAction("Game Removed", $"Date: {entry.Timestamp.ToShortDateString()}", 
                ("P1", winner.Name), ("P2", opposition.Name), ("State", entry.Draw ? "Draw" : "P1 won"));
            if(entry.Draw)
            {
                winner.Losses--;
                opposition.Losses--;
            } else
            {
                winner.Wins--;
                opposition.Losses--;
            }
            await RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/monitor")]
        public void ToggleMonitor(int id)
        {
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                await RespondRaw("Unknown player", 404);
                return;
            }
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                await RespondRaw("No permission", 403);
                return;
            }
            if(player.RequireGameApproval)
            { // only justices may remove approval
                if(!doesHavePerm(ChessPerm.Justice))
                {
                    await RespondRaw("Only Justices of the Court of Appeals may remove monitor from players", 403);
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
            await RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/time")]
        public void ToggleRequireTiming(int id)
        {
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if (player == null)
            {
                await RespondRaw("Unknown player", 404);
                return;
            }
            if (!doesHavePerm(ChessPerm.ChiefJustice))
            {
                await RespondRaw("No permission", 403);
                return;
            }
            player.RequireTiming = !player.RequireTiming;
            didChange = true;
            await RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/approve")]
        public void ApproveMonitorGame(int id)
        {
            var game = DB.Games.FirstOrDefault(x => x.Id == id);
            if(game == null)
            {
                await RespondRaw("Unknown game", 404);
                return;
            }
            if(game.NeedsModApproval)
            {
                if(!doesHavePerm(ChessPerm.Moderator))
                {
                    await RespondRaw("Game must be approved by a Moderator, and you are not that.", 403);
                    return;
                }
                game.ApprovalGiven |= ApprovedBy.Moderator;
            } else if (game.NeedsWinnerApproval && game.WinnerId == SelfPlayer.Id)
            {
                game.ApprovalGiven |= ApprovedBy.Winner;
            } else if (game.NeedsLoserApproval && game.LoserId == SelfPlayer.Id)
            {
                game.ApprovalGiven |= ApprovedBy.Loser;
            }
            if(game.IsApproved)
            {
                int p1Was = game.Winner.Rating;
                int p2Was = game.Loser.Rating;
                game.Winner.Rating += game.WinnerChange;
                game.Loser.Rating += game.LoserChange;
                if(game.Draw)
                    game.Winner.Losses++;
                else
                    game.Winner.Wins++;
                game.Loser.Losses++;
                LogAdminAction("Game Approved", $"Game has been approved by {SelfPlayer.Name}",
                    ("Date", game.Timestamp.ToShortDateString()),
                    ("State", game.Draw ? "Draw" : "P1 Won"),
                    (game.Draw ? "P1" : "Winner", game.Winner.Name + "\r\nRating changed by " + game.WinnerChange.ToString()),
                    (game.Draw ? "P2" : "Loser", game.Loser.Name + "\r\nRating changed by " + game.LoserChange.ToString())
                    );
                ChessS.LogEntry(game);
            }
            didChange = true;
            await RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/ban")]
        public void BanUser(int id, string reason, string expires)
        {
            if(!doesHavePerm(ChessPerm.Moderator))
            {
                await RespondRaw("No permissions", 403);
                return;
            }
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                await RespondRaw("unknown player", 404);
                return;
            }
            if(string.IsNullOrWhiteSpace(reason) || reason.Length  > 256 || reason.Length < 16)
            {
                await RespondRaw("Reason invalid: empty or length too short or too long", 400);
                return;
            }
            if(string.IsNullOrWhiteSpace(expires) || expires.Length != 10 || expires.Contains('-') == false)
            {
                await RespondRaw("Expirery date invalid: incorrect format; require 'yyyy-MM-dd'", 400);
                return;
            }
            var split = expires.Split('-');
            var expiresAt = new DateTime(int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]));
            if(expiresAt.DayOfYear <= DateTime.Now.DayOfYear || expiresAt.Year < DateTime.Now.Year)
            {
                await RespondRaw("Expirery date cannot be before now", 400);
                return;
            }
            if(expiresAt.DayOfWeek != DayOfWeek.Friday)
            {
                await RespondRaw("Expirery must be on a Friday", 400);
                return;
            }
            if(player.IsBanned)
            {
                await RespondRaw("Player is already banned", System.Net.HttpStatusCode.Conflict);
                return;
            }

            var ban = new ChessBan(player, SelfPlayer);
            ban.Reason = reason;
            ban.ExpiresAt = expiresAt;
            player.Bans.Add(ban);
            didChange = true;
            ChessS.LogAdmin(ban);
            await RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/moderator")]
        public void ModUser(int id)
        {
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                await RespondRaw("Error: player does not exist", 404);
                return;
            }
            if(!(player.Permission == ChessPerm.Moderator || player.Permission == ChessPerm.Player))
            {
                await RespondRaw($"Error: that player has permissions that you are unable to remove or modify: {player.Permission}", 400);
                return;
            }
            if(player.Permission == ChessPerm.Moderator)
            {
                player.Permission = ChessPerm.Player;
                didChange = true;
                LogAdminAction("Moderator Fired", $"The Arbiter has removed **{player.Name}** as a Moderator.");
            } else
            {
                var result = checkModeratorCandidacy(player);
                if(!result.IsSuccess)
                {
                    await RespondRaw($"Unable: {result.ErrorReason}", 400);
                    return;
                }
                player.Permission = ChessPerm.Moderator;
                didChange = true;
                LogAdminAction("Moderator Appointed", $"The Arbiter has appointed **{player.Name}** as a Moderator");
            }
            await RespondRaw("");
        }

        [Method("PUT"), Path("/chess/api/register")]
        public void RegisterMultipleUsers(string list)
        {
            if(!doesHavePerm(ChessPerm.ClassRoom))
            {
                await RespondRaw("No permission", 403);
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
                    await RespondRaw($"Could not parse id '{item}' as int", 400);
                    return;
                }
            }
            foreach(var id in ids)
            {
                var player = DB.Players.FirstOrDefault(x => x.Id == id);
                if(player == null)
                {
                    await RespondRaw($"Could not find player with id {id}", 400);
                    return;
                }
                if(player.ShouldContinueInLoop || player.IsBanned)
                {
                    await RespondRaw($"{player.Name} cannot be marked as present, they are removed, banned or built in", 400);
                    return;
                }
                player.DateLastPresent = DateTime.Now;
            }
            didChange = true;
            await RespondRaw("");
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


        public static ChessGame createGameEntry(ChessPlayer winP, ChessPlayer lossP, bool draw, Func<ChessPlayer, int> kFunction, bool onlineGame, out int httpCode)
        {
            if (kFunction == null)
                kFunction = defaultKFunction;
            int winnerRating = (int)Math.Round(getRating(winP, lossP, draw ? 0.5d : 1.0d, kFunction));
            int loserRating = (int)Math.Round(getRating(lossP, winP, draw ? 0.5d : 0.0d, kFunction));
            winP.DateLastPresent = null; // so it auto-calculates
            lossP.DateLastPresent = null;

            var game = new ChessGame(winP, lossP)
            {
                Draw = draw,
                WinnerChange = winnerRating - winP.Rating,
                LoserChange = loserRating - lossP.Rating,
                Timestamp = DateTime.Now,
            };

            httpCode = 201;
            if (winP.RequireGameApproval || lossP.RequireGameApproval || winP.RequireTiming || lossP.RequireTiming)
            {
                game.ApprovalNeeded |= ApprovedBy.Moderator;
                if (winP.RequireGameApproval || lossP.RequireGameApproval)
                    httpCode = 204;
                else // require timing
                    httpCode = 202;
            }
            else if(onlineGame)
            {
                game.ApprovalNeeded |= ApprovedBy.Loser | ApprovedBy.Winner;
                httpCode = 203;
                ChessS.LogGame(game);
            }
            else
            {
                winP.Rating = winnerRating;
                lossP.Rating = loserRating;
                if (game.Draw)
                    winP.Losses++;
                else
                    winP.Wins++;
                lossP.Losses++;
                ChessS.LogEntry(game);
            }
            winP.SetScoreOnDay(winP.Rating, DateTime.Now);
            lossP.SetScoreOnDay(lossP.Rating, DateTime.Now);
            return game;
        }

        [Method("GET"), Path("/chess/api/testmatch")]
        [RequireChess(ChessPerm.Player)]
        public void PretendMatch(int winner, int loser, bool draw)
        {
            var winP = DB.Players.FirstOrDefault(x => x.Id == winner);
            var lossP = DB.Players.FirstOrDefault(x => x.Id == loser);
            if (winP == null)
            {
                await RespondRaw("Unknown winner", 404);
                return;
            }
            if (lossP == null)
            {
                await RespondRaw("Unknown loser", 404);
                return;
            }
            if (winP.Id == lossP.Id)
            {
                await RespondRaw("Winner and loser are identical", 400);
                return;
            }
            if (winP.IsBuiltInAccount || lossP.IsBuiltInAccount)
            {
                await RespondRaw("One of those Accounts is built-in, so cannot be given a match", 400);
                return;
            }
            if (winP.IsBanned)
            {
                await RespondRaw($"{winP.Name} is currently banned.", 403);
                return;
            }
            if (lossP.IsBanned)
            {
                await RespondRaw($"{lossP.Name} is currently banned", 403);
                return;
            }
            int winnerRating = (int)Math.Round(getRating(winP, lossP, draw ? 0.5d : 1.0d, defaultKFunction));
            int loserRating = (int)Math.Round(getRating(lossP, winP, draw ? 0.5d : 0.0d, defaultKFunction));
            await RespondRaw($"<p>{winP.Name}: {winP.Rating} -> <strong>{winnerRating}</strong></p>" +
                $"<p>{lossP.Name}: {lossP.Rating} -> <strong>{loserRating}</strong></p>");
        }

        bool canAddMatch(ChessPlayer win, ChessPlayer loss, bool external)
        {
            return doesHavePerm(ChessPerm.AddMatch)
                || (external 
                    &&  win.Id == SelfPlayer.Id || loss.Id == loss.Id);
        }

        [Method("PUT"), Path("/chess/api/match")]
        public void AddNewMatch(int winner, int loser, bool draw = false, bool external = false)
        {
            var winP = DB.Players.FirstOrDefault(x => x.Id == winner);
            var lossP = DB.Players.FirstOrDefault(x => x.Id == loser);
            if(winP == null)
            {
                await RespondRaw("Unknown winner", 404);
                return;
            }
            if(lossP == null)
            {
                await RespondRaw("Unknown loser", 404);
                return;
            }
            if(winP.Id == lossP.Id)
            {
                await RespondRaw("Winner and loser are identical", 400);
                return;
            }
            if(winP.IsBuiltInAccount || lossP.IsBuiltInAccount)
            {
                await RespondRaw("One of those Accounts is built-in, so cannot be given a match", 400);
                return;
            }
            if(winP.IsBanned)
            {
                await RespondRaw($"{winP.Name} is currently banned.", 403);
                return;
            }
            if(lossP.IsBanned)
            {
                await RespondRaw($"{lossP.Name} is currently banned", 403);
                return;
            }
            if(!canAddMatch(winP, lossP, external))
            {
                await RespondRaw($"You do not have permission to add that match.");
                return;
            }
            var game = createGameEntry(winP, lossP, draw, null, external, out int httpCode);
            if (game.NeedsWinnerApproval && game.WinnerId == SelfPlayer.Id)
                game.ApprovalGiven |= ApprovedBy.Winner;
            else if (game.NeedsLoserApproval && game.LoserId == SelfPlayer.Id)
                game.ApprovalGiven |= ApprovedBy.Loser;
            DB.Games.Add(game);
            didChange = true;
            await RespondRaw("Updated", httpCode);
        }


#endregion
    }
}
#endif