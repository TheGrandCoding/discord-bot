using Discord;
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.Chess.CoA;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DiscordBot.Services.ChessService;

namespace DiscordBot.MLAPI.Modules
{
    public class CoA : ChessBase
    {
        const string Invite = "https://discord.gg/pRN4Fa7";
        public CoA(APIContext c) : base(c, "chess/coa")
        {
        }
        public override void BeforeExecute()
        {
            if (SelfPlayer == null)
                throw new HaltExecutionException("You must be both logged in, and have a connected Chess profile to do that");
            if (SelfPlayer.IsBuiltInAccount)
                throw new HaltExecutionException("This account cannot do that");
        }
        public override void AfterExecute()
        {
            if(mustSave)
            {
                ChessService.SendSave();
            }
        }
        public static IRole Member => Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Member");
        public static IRole Moderator => Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Moderator");
        public static IRole Justice => Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Justice");
        public static IRole ChiefJustice => Program.ChessGuild.Roles.FirstOrDefault(x => x.Name == "Chief Justice");
        public const int JudgesToCertify = 2;

        bool mustSave = false;

        bool HasPerms(ChessPerm perm)
        {
            if (SelfPlayer == null)
                return perm == ChessPerm.Player;
            return SelfPlayer.Permission.HasFlag(perm);
        }

        [Path("/chess/coa"), Method("GET")]
        public void Base()
        {
            var TABLE = "";
            var chief = HasPerms(ChessPerm.CourtOfAppeals);
            foreach(var hear in Hearings)
            {
                if (hear.IsRequested)
                    continue;
                string ROW = $"<tr>";
                ROW += $"<td>";
                if (hear.HasFinished)
                    ROW += "<del>";
                ROW += $"{aLink($"/chess/coa/hearing?num={hear.CaseNumber}", hear.CaseStr)}";
                if (hear.HasFinished)
                    ROW += $"</del><br/><strong>{hear.Verdict}</strong>";
                ROW += $"</td>";
                ROW += $"<td>{hear.Title}</td>";
                if(hear.Justices.Length == 0)
                {
                    if(chief)
                    {
                        ROW += $"<td>{aLink("/chess/coa/justices?num=" + hear.CaseNumber.ToString(), "None; select panel")}</td>";
                    } else
                    {
                        ROW += "<td>TBD</td>";
                    }
                } else
                {
                    ROW += "<td><ul>";
                    foreach(var justice in hear.Justices)
                    {
                        ROW += "<li>";
                        if(justice.Permission == ChessPerm.CourtOfAppeals)
                        {
                            ROW += "Chief ";
                        }
                        ROW += "Justice " + justice.Name;
                        ROW += "</li>";
                    }
                    ROW += "</ul></td>";
                }
                TABLE += ROW + "</tr>";
            }

            var REQUESTS = "";
            if(HasPerms(ChessPerm.Justice))
            {
                foreach(var hear in Hearings)
                {
                    if (!hear.IsRequested)
                        continue;
                    string ROW = "<tr>";
                    ROW += $"<td>{hear.Plaintiff.Name}</td>";
                    ROW += $"<td>{hear.Defendant.Name}</td>";
                    ROW += $"<td>{hear.Description}</td>";
                    ROW += $"<td>";
                    if(hear.Justices.Length == 0)
                    {
                        ROW += aLink("/chess/coa/grant?num=" + hear.CaseNumber.ToString(), "None; click to vote to go ahead");
                    } else
                    {
                        if(hear.Justices.Select(x => x.Id).Contains(SelfPlayer.Id))
                        {
                            ROW += string.Join("<br/>", hear.Justices.Select(x => x.Name));
                        } else
                        {
                            ROW += aLink("/chess/coa/grant?num=" + hear.CaseNumber.ToString(), $"Approve, with:<br/>{string.Join("<br/>", hear.Justices.Select(x => x.Name))}");
                        }
                        ROW += "</td>";
                    }
                    REQUESTS += ROW + "</tr>";
                }
            } else
            {
                REQUESTS = $"<tr><td colspan='3'>You do not have permission to see pending cases</td></tr>";
            }
            ReplyFile("base.html", 200, new Replacements().Add("table", TABLE).Add("pending", REQUESTS));
        }

        [Method("GET"), Path("/chess/coa/newappeal")]
        public void NewAppeal()
        {
            string players = "";
            foreach(var p in Players.OrderBy(x => x.Name))
            {
                if (p.IsBuiltInAccount)
                    continue;
                if (p.Id == SelfPlayer.Id)
                    continue;
                if (p.ConnectedAccount == 0)
                    continue;
                players += $"<option value=\"{p.Id}\">{p.Name}</option>";
            }
            string TABLE = "";
            foreach(var hear in Hearings)
            {
                if (!hear.HasFinished)
                    continue;
                if(hear.Plaintiff.Id == SelfPlayer.Id || hear.Defendant.Id == SelfPlayer.Id)
                {
                    string ROW = "<tr>";
                    ROW += $"<td>{hear.CaseStr}</td>";
                    ROW += $"<td>{hear.Title}</td>";
                    ROW += $"<td>{hear.Description}</td>";
                    ROW += $"<td>{hear.Verdict}</td>";
                    ROW += $"<th><input type='button' value='Appeal' onclick='appealCase(\"{hear.CaseNumber}\");'/></td>";
                    TABLE += ROW + "</tr>";
                }
            }
            ReplyFile("newappeal.html", 200, new Replacements()
                .Add("players", players)
                .Add("plaintiff", SelfPlayer.Name)
                .Add("table", TABLE)
                .Add("chief", Players.FirstOrDefault(x => x.Name == "Alex C").Id));
        }

        [Method("GET"), Path("/chess/coa/justices")]
        public void ViewJudges(int num)
        {
            if(!HasPerms(ChessPerm.CourtOfAppeals))
            {
                HTTPError(System.Net.HttpStatusCode.Forbidden, "Not Chief Justice", "Only the Chief Justice may appoint justices to cases");
                return;
            }
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num); 
            if(hearing.IsRequested)
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "Not started", "Case has not yet been started");
                return;
            }
            var existing = hearing.Justices.Select(x => x.Id);
            string justices = "";
            int count = 0;
            int TOTAL_JUSTICE = Players.Where(x => !x.ShouldContinueInLoop && x.Permission.HasFlag(ChessPerm.Justice)
                && x.Id != hearing.Plaintiff.Id && x.Id != hearing.Defendant.Id).Count();
            foreach(var p in Players.OrderBy(x => x.Name))
            {
                if (p.IsBuiltInAccount)
                    continue;
                if(p.Permission.HasFlag(ChessPerm.Justice))
                {
                    string warn = "";
                    if (hearing.Plaintiff.Id == p.Id || hearing.Defendant.Id == p.Id)
                    {
                        if (TOTAL_JUSTICE >= 3)
                            continue;
                        warn = "class='warn' ";
                    }
                    justices += $"<option {warn}{(existing.Contains(p.Id) ? $"sel{count++}" : "")} value=\"{p.Id}\">{p.Name}</option>";
                }
            }
            string list1 = justices.Replace("sel0", "selected").Replace("sel1", "").Replace("sel2", "");
            string list2 = justices.Replace("sel1", "selected").Replace("sel2", "").Replace("sel0", "");
            string list3 = justices.Replace("sel2", "selected").Replace("sel0", "").Replace("sel1", "");
            ReplyFile("justices.html", 200, new Replacements().Add("list1", list1).Add("list2", list2).Add("list3", list3).Add("hearing", hearing));
        }

        [Method("GET"), Path("/chess/coa/grant")]
        public void JusticeGrantAppeal(int num)
        {
            if(HasPerms(ChessPerm.Justice))
            {
                var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
                if(hearing == null)
                {
                    HTTPError(System.Net.HttpStatusCode.NotFound, "Unknown case", "Could not find a hearing with that Case Number");
                    return;
                }
                if (!hearing.IsRequested)
                { // already started
                    HTTPError(System.Net.HttpStatusCode.BadRequest, "Already Started", "That case has already been started");
                    return;
                }
                if(!hearing.Justices.Select(x => x.Id).Contains(SelfPlayer.Id))
                {
                    var ls = hearing.Justices.ToList();
                    ls.Add(SelfPlayer);
                    hearing.Justices = ls.ToArray();
                    mustSave = true;
                    if(hearing.Justices.Length >= JudgesToCertify || SelfPlayer.Permission == ChessPerm.CourtOfAppeals)
                    { // granted, by two justices or the chief justice
                        hearing.Justices = new ChessPlayer[0]; // reset, to be enpanelled
                        hearing.Category = Program.ChessGuild.CreateCategoryChannelAsync(hearing.Title).Result;
                        hearing.GeneralChnl = Program.ChessGuild.CreateTextChannelAsync("general", x =>
                        {
                            x.CategoryId = hearing.Category.Id;
                            x.Topic = "Where main arguments and witlessness evidence may be presented";
                            x.SlowModeInterval = 60;
                        }).Result;
                        hearing.JusticesChnl = Program.ChessGuild.CreateTextChannelAsync("justices", x =>
                        {
                            x.CategoryId = hearing.Category.Id;
                            x.Topic = "Channel for justices empanelled for this Hearing";
                        }).Result;
                        hearing.JusticesChnl.AddPermissionOverwriteAsync(ChiefJustice, Program.WritePerms); // temporary
                        hearing.Opened = DateTime.Now;
                        hearing.SetChannelPermissions();
                        foreach(var usr in new ChessPlayer[] { hearing.Plaintiff, hearing.Defendant})
                        {
                            var inChess = Program.ChessGuild.GetUser(usr.ConnectedAccount);
                            if(inChess == null)
                            {
                                var anyUser = Program.Client.GetUser(usr.ConnectedAccount);
                                if(anyUser == null)
                                {
                                    hearing.GeneralChnl.SendMessageAsync($"{usr.Name} does not have a recognised Discord account, so there will be some difficulty here..");
                                } else
                                {
                                    anyUser.SendMessageAsync("A Chess Court of Appeals hearing involving you has been approved.\n" +
                                        $"{hearing.Title} -- {hearing.Description}\n" +
                                        $"Invite to server to argue your case: {Invite}\n" +
                                        $"Website to call witnesses etc: {Handler.LocalAPIUrl}/chess/coa/hearing?num={hearing.CaseNumber}");
                                }
                            } else
                            {
                                inChess.SendMessageAsync($"An appeal involving you has been granted its certification\n" +
                                    $"{hearing.Title} -- {hearing.Description}\n" +
                                    $"You may call witnesses via the online website: {Handler.LocalAPIUrl}/chess/coa/hearing?num={hearing.CaseNumber}\n" +
                                    $"Channel: {hearing.GeneralChnl.Mention}");
                            }
                        }
                    }
                }
            }
            LoadRedirectFile("/chess/coa");
        }

        (string k, object o)[] sideBarObjects(CoAHearing hearing, params (string key, object o)[] args)
        {
            List<(string key, object o)> objects = args.ToList();
            string type = "";
            if (hearing.Justices.Length == 1)
            {
                type = "Solo";
            }
            else if (hearing.Justices.Length == 3)
            {
                type = "Panel";
            }
            else
            {
                type = "En banc";
            }
            string justices = string.Join(", ", hearing.Justices.Select(x => x.Name));
            if (hearing.IsRequested)
                justices = $"<label color='red'>Case awaiting certification/approval from {JudgesToCertify} justices</label>";
            string out1 = "";
            string out2 = "";
            if(hearing.HasFinished)
            {
                out1 = "Outcome";
                out2 = "<strong>" + hearing.Verdict + "</strong>";
            }
            objects.Add(("outcome1", out1));
            objects.Add(("outcome2", out2));
            objects.Add(("type", type));
            objects.Add(("justices", justices));
            objects.Add(("hearing", hearing));
            objects.Add(("plaintiff", hearing.Plaintiff.Name));
            objects.Add(("defendant", hearing.Defendant.Name));
            objects.Add(("opened", hearing.Opened.ToString("ddd, dd MMMM yyyy")));
            return objects.ToArray();
        }

        [Method("GET"), Path("/chess/coa/hearing")]
        public void ViewHearing(int num)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if (hearing == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Hearing unknown", "Could not find a Court of Appeals hearing with that case number");
                return;
            }
            string witnesses = "";
            if(hearing.Witnesses.Count == 0)
            {
                witnesses = "<tr><td colspan='2'>No witnesses called</td></tr>";
            }
            foreach (var wit in hearing.Witnesses)
            {
                witnesses += $"<tr>" +
                    $"<td>{aLink($"/chess/coa/testimony?num={hearing.CaseNumber}&witness={wit.Witness.Id}", wit.Witness.Name)}</td>" +
                    $"<td>{Enum.GetName(typeof(CalledBy), wit.CalledByWho)}</td></tr>";
            }
            if(hearing.CanCallWitnesses(SelfPlayer))
            {
                witnesses += $"<tr><td colspan='2'>{aLink("/chess/coa/witness?num=" + num.ToString(), "Call new witness")}</td></tr>";
            }
            string judgeRulings = "";
            if(hearing.Justices.Select(x => x.Id).Contains(SelfPlayer.Id))
            {
                judgeRulings = $"<input type='button' onclick='window.location.href=\"/chess/coa/ruling?num={hearing.CaseNumber}\"' value='Reach Verdict'/>";
            }
            string appeal = "";
            if(hearing.AppealHearing != null)
            {
                appeal = $"<p><a href='/chess/coa/hearing?num={hearing.AppealHearing.CaseNumber}'>Appealed to #{hearing.AppealHearing.CaseStr}</a></p>";
            } else
            {
                if(hearing.IsAppealRequested)
                {
                    if (SelfPlayer.Permission == ChessPerm.CourtOfAppeals)
                        appeal = $"<input type='button' onclick='grantAppeal({hearing.CaseNumber});' value='As Chief Justice: Grant En Banc Appeal'/>";
                    else
                        appeal = "<p>Appeal Requested</p>";
                } else
                {
                    if (SelfPlayer.Id == hearing.Plaintiff.Id || SelfPlayer.Id == hearing.Defendant.Id)
                        appeal = "<p><a href='/chess/coa/newappeal'>Select in table to appeal this case</a></p>";
                }
            }
            Sidebar = SidebarType.Local;
            ReplyFile("hearing.html", 200, new Replacements(hearing)
                .Add("witnesses", witnesses)
                .Add("judge", judgeRulings)
                .Add("appeal", appeal));
        }

        [Method("GET"), Path("/chess/coa/testimony")]
        public void Testimony(int num, int witness)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Hearing", "Case Number not found");
                return;
            }
            var witnessObj = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == witness);
            if(witnessObj == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Witness", "Witness number unknown");
                return;
            }
            string moveNext = "";
            if(witnessObj.CanMoveNextStage(SelfPlayer, out bool powers))
            {
                moveNext = $"<input type='button' onclick='moveNext();' value='Rest Questioning {(powers ? "[Using judge powers]" : "")}'/>";
            }
            string testimony = "";
            if(hearing.CanCallWitnesses(SelfPlayer))
            { // only those in case can view
                var messages = witnessObj.Channel.GetMessagesAsync().FlattenAsync().Result;
                foreach(var msg in messages.OrderBy(x => x.CreatedAt))
                {
                    string name = msg.Author.Username;
                    string cls = "";
                    if(msg.Author.IsBot || msg.Author.IsWebhook)
                    {
                        if(msg.Author.Id == Program.Client.CurrentUser.Id)
                        {
                            name = "Court Clerk";
                            cls = "bot";
                        } else
                        {
                            continue;
                        }
                    }
                    string MSG = $"<div class='{cls}' id='{msg.Id}'>";
                    MSG += $"<span title='{msg.CreatedAt}'>{name}</span>";
                    MSG += $"<label>{msg.Content.Replace("\n", "<br/>")}</label>";
                    if(msg.Attachments.Count > 0)
                    {
                        MSG += "<hr><em>Attachments:</em><ul>";
                        foreach(var attch in msg.Attachments)
                        {
                            MSG += $"<li>{aLink(attch.Url, attch.Filename)}</li>";
                        }
                        MSG += "</ul>";
                    }
                    testimony += MSG + "</div>";
                }
                if (string.IsNullOrWhiteSpace(testimony))
                    testimony = "<p class='error'>There are no messages currently</p>";
            } else
            {
                testimony = "<p class='error'>You do not have permission to view the testimony of this person at this time</p>";
            }
            Sidebar = SidebarType.Local;
            ReplyFile("testimony.html", 200, new Replacements(hearing)
                .Add("testimony", testimony)
                .Add("advStage", moveNext)
                .Add("witness", witnessObj.Witness));
        }

        [Method("GET"), Path("/chess/coa/witness")]
        public void WitnessView(int num)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Hearing", "Could not find hearing");
                return;
            }
            string callFor = "";
            if(SelfPlayer.Id == hearing.Plaintiff.Id)
            {
                callFor = "Plaintiff";
            } else if (SelfPlayer.Id == hearing.Defendant.Id)
            {
                callFor = "Defendant";
            } else if (hearing.Justices.Select(x => x.Id).Contains(SelfPlayer.Id))
            {
                callFor = "Judges";
            } else
            {
                HTTPError(System.Net.HttpStatusCode.Forbidden, "Cannot call", "You have no standing to call witnesses in this hearing<br/>" +
                    "Only the plaintiff, defendant or the Justices in the case may call witnesses");
                return;
            }
            string players = "";
            foreach(var p in Players.OrderBy(x => x.Name))
            {
                if (p.IsBuiltInAccount)
                    continue;
                string text = p.Name;
                if (p.Id == SelfPlayer.Id)
                    continue;
                if (p.Id == hearing.Plaintiff.Id)
                    text = "[Plaintiff] " + text;
                if (p.Id == hearing.Defendant.Id)
                    text = "[Defendant] " + text;
                if (hearing.Justices.Select(x => x.Id).Contains(p.Id))
                    text = "[Judge] " + text;
                var existing = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == p.Id);
                if (existing != null)
                    continue;
                string cls = "";
                if (p.ConnectedAccount == 0)
                    cls = "red";
                else
                    cls = "green";
                players += $"<option class='{cls}' value=\"{p.Id}\">{text}</option>";
            }
            ReplyFile("witness.html", 200, new Replacements()
                .Add("hearing", hearing)
                .Add("witness", players)
                .Add("for", callFor));
        }

        class RulingRow
        {
            public ChessPlayer Dismiss;
            public ChessPlayer Uphold;
            public ChessPlayer Remand;
            public ChessPlayer Overturn;
            public ChessPlayer Other;
            public ChessPlayer Undecided;
            public override string ToString()
            {
                string ROW = "<tr>";
                foreach(var player in new ChessPlayer[] { Dismiss, Uphold, Remand, Overturn, Other, Undecided})
                {
                    ROW += "<td>" + (player?.Name ?? "") + "</td>";
                }
                return ROW + "</tr>";
            }
        }

        class RulingTable
        {
            public List<RulingRow> Rows = new List<RulingRow>();
            public RulingRow SetValue(string type, ChessPlayer player)
            {
                var fields = typeof(RulingRow).GetFields();
                var ofType = fields.FirstOrDefault(x => x.Name == type);
                foreach (var row in Rows)
                {
                    if (ofType.GetValue(row) == null)
                    {
                        ofType.SetValue(row, player);
                        return row;
                    }
                }
                var next = new RulingRow();
                ofType.SetValue(next, player);
                Rows.Add(next);
                return next;
            }
            public void AddDismiss(ChessPlayer player) => SetValue("Dismiss", player);
            public void AddUphold(ChessPlayer player) => SetValue("Uphold", player);
            public void AddRemand(ChessPlayer player) => SetValue("Remand", player);
            public void AddOverturn(ChessPlayer player) => SetValue("Overturn", player);
            public void AddOther(ChessPlayer player) => SetValue("Other", player);
            public override string ToString()
            {
                string TABLE = "";
                foreach (var row in Rows)
                    TABLE += row.ToString();
                return TABLE;
            }
        }

        [Method("GET"), Path("/chess/coa/ruling")]
        public void JusticeSeeRuling(int num)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Hearing", "Unknown hearing");
                return;
            }
            if(SelfPlayer == null || !hearing.Justices.Select(x => x.Id).Contains(SelfPlayer.Id))
            {
                HTTPError(System.Net.HttpStatusCode.Forbidden, "Not justice", "Not justice (in general, or in this case)");
                return;
            }
            var TABLE = new RulingTable();
            foreach(var judge in hearing.Justices)
            {
                TABLE.SetValue(hearing.GetJusticeVote(judge), judge);
            }
            Sidebar = SidebarType.Local;
            ReplyFile("ruling.html", 200, new Replacements(hearing)
                .Add("judges", TABLE.ToString()));
        }

        CalledBy findCalledBy(CoAHearing hearing)
        {
            if (SelfPlayer.Id == hearing.Plaintiff.Id)
                return CalledBy.Plaintiff;
            if (SelfPlayer.Id == hearing.Defendant.Id)
                return CalledBy.Defendant;
            if (hearing.Justices.Select(x => x.Id).Contains(SelfPlayer.Id))
                return CalledBy.Justices;
            return CalledBy.NONE;
        }

        [Method("PUT"), Path("/chess/coa/call")]
        public void CallSpecificWitness(int num, int witness)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                RespondRaw("Hearing not found", 404);
                return;
            }
            var player = Players.FirstOrDefault(x => x.Id == witness);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            if(hearing.Justices.Length == 0)
            {
                RespondRaw("Justices have not been selected for this case yet", 400);
                return;
            }
            if (hearing.HasFinished)
            {
                RespondRaw("This case has already finished", 400);
                return;
            }
            var existing = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == witness);
            if(existing != null)
            {
                RespondRaw("Witness has already been called", 309);
                return;
            }
            if(hearing.CanCallWitnesses(SelfPlayer) == false)
            {
                RespondRaw("You cannot call witnesses", 403);
                return;
            }
            var chnl = Program.ChessGuild.CreateTextChannelAsync(player.Name, x =>
            {
                x.CategoryId = hearing.Category.Id;
                x.Topic = "Witness testimony of " + player.Name + "; called by " + SelfPlayer.Name;
            }).Result;
            existing = new CoAWitness(player, findCalledBy(hearing), chnl);
            existing.Hearing = hearing;
            hearing.Witnesses.Add(existing);
            existing.SetPermissions();
            existing.Stage = 0;
            existing.SendEmbed();
            mustSave = true;
            RespondRaw("Called");
            var witUsr = Program.Client.GetUser(existing.Witness.ConnectedAccount);
            witUsr?.SendMessageAsync(embed: new EmbedBuilder()
                .WithTitle("Chess Court of Appeals")
                .WithDescription($"You have been called as a witness\n" +
                    $"For: **{hearing.Title}** -- {hearing.Description}\n" +
                    $"Called by: {SelfPlayer.Name}\n" +
                    $"Channel: {existing.Channel.Mention}\n[Invite to CoA server]({Invite})")
                .AddField("Penalty", "Failure to appear may constitute an offence, which could result in discretionary sanctions").Build());
        }

        [Method("PUT"), Path("/chess/coa/request")]
        public void RequestAppeal(int def, string reason)
        {
            var player = Players.FirstOrDefault(x => x.Id == def);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            if(reason.Length > 256)
            {
                RespondRaw("Reason too long", 400);
                return;
            }
            var hearing = new CoAHearing(SelfPlayer, player, reason);
            Hearings.Add(hearing);
            mustSave = true;
            RespondRaw("Opened");
            var usr = Program.Client.GetUser(player.ConnectedAccount);
            usr?.SendMessageAsync(embed: new EmbedBuilder()
                .WithTitle("Chess Court of Appeals")
                .WithDescription($"{SelfPlayer.Name} has filed with the CoA with an appeal **against** you.\n" +
                $"The appeal will require certification from {JudgesToCertify} or more justices\n" +
                $"If the appeal goes forth, you will be able to call witnesses through the online portal")
                .WithUrl($"{Handler.LocalAPIUrl}/chess/coa/hearing?num=" + hearing.CaseNumber).Build());
            var chs = Program.Services.GetRequiredService<ChessService>();
            chs.DiscussionChannel.SendMessageAsync(embed: new EmbedBuilder()
                .WithTitle("Chess Court of Appeals")
                .WithDescription($"Appeal currently pending approval: {hearing.Title}")
                .WithUrl($"{Handler.LocalAPIUrl}/chess/coa/hearing?num=" + hearing.CaseNumber).Build());
        }

        [Method("PUT"), Path("/chess/coa/select")]
        public void SelectJudges(int num, int first, int second, int third)
        {
            if(!HasPerms(ChessPerm.CourtOfAppeals))
            {
                RespondRaw("No perms", 403);
                return;
            }
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                RespondRaw("Unknown case number", 404);
                return;
            }
            if(hearing.HasFinished)
            {
                RespondRaw("Hearing has already finished", 400);
                return;
            }
            var j1 = Players.FirstOrDefault(x => x.Id == first);
            var j2 = Players.FirstOrDefault(x => x.Id == second);
            var j3 = Players.FirstOrDefault(x => x.Id == third);
            var selected = new ChessPlayer[] { j1, j2, j3 }.Distinct(new ChessPlayerComparer()).ToArray();
            if(selected.Length != 3)
            {
                RespondRaw("Duplicate justices present, must select 3 distinct", System.Net.HttpStatusCode.BadRequest);
                return;
            }
            foreach(var judge in selected)
            {
                if (judge == null)
                {
                    RespondRaw($"Judge not found", 404);
                    return;
                } else if (judge.Permission.HasFlag(ChessPerm.Justice) == false) 
                {
                    RespondRaw("Person not a judge: " + judge.Name, 400);
                    return;
                }
            }
            hearing.Justices = selected;
            hearing.SetChannelPermissions();
            int withUsers = 0;
            foreach(var jst in hearing.Justices)
            {
                var chs = Program.ChessGuild.GetUser(jst.ConnectedAccount);
                if(chs == null)
                {
                    var anyuser = Program.Client.GetUser(jst.ConnectedAccount);
                    anyuser?.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("Chess Court of Appeals")
                        .WithDescription($"**{hearing.Title}**:\n" +
                        $"> {hearing.Description} \n\n" +
                        $"You have been selected to act as a Judge to hear the above case\n" +
                        $"[Invite to Court of Appeals]({Invite})").Build());
                } else
                {
                    chs.SendMessageAsync($"You have been selected, as a Justice, to hear {hearing.Title}\nChannel: {hearing.GeneralChnl.Mention}");
                    withUsers += 1;
                }
            }
            hearing.JusticesChnl.SendMessageAsync($"{Program.ChessGuild.EveryoneRole.Mention}, justices have been selected to hear this case.\n" +
                $"The case may now begin");
            RespondRaw("Changed");
            mustSave = true;
            if(withUsers == hearing.Justices.Length)
            { // all judges in, so Chief can go
                hearing.JusticesChnl.RemovePermissionOverwriteAsync(ChiefJustice);
            }
        }

        [Method("PUT"), Path("/chess/coa/stage")]
        public void AdvanceNextStage(int num, int wit)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.HasFinished)
            {
                RespondRaw("Hearing has finished", 400);
                return;
            }
            var witObj = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == wit);
            if (witObj == null)
            {
                RespondRaw("Unknown witness", 404);
                return;
            }
            if(witObj.CanMoveNextStage(SelfPlayer, out var withPowers))
            {
                if(hearing.Justices.Select(x => x.Id).Contains(SelfPlayer.Id) && !(SelfPlayer.Id == hearing.Plaintiff.Id || SelfPlayer.Id == hearing.Defendant.Id))
                { // judges need to vote to move on, since there are multiple
                    if (witObj.JusticesVotedAdvance.Contains(SelfPlayer.Id))
                    {
                        RespondRaw("You have already voted to move testimony forward", System.Net.HttpStatusCode.Conflict);
                        return;
                    } else
                    { // note on move/rule: if overriding plaintiff/defendant, we use rule
                        mustSave = true;
                        witObj.JusticesVotedAdvance.Add(SelfPlayer.Id);
                        if (witObj.JusticesVotedAdvance.Count >= hearing.JudgeRulingMinimum)
                        { 
                            witObj.AdvanceNextStage();
                            if(witObj.IsFinishedTestimony)
                            {
                                witObj.Channel.SendMessageAsync("Testimony has been conclued.\nWitness is dismissed");
                            } else
                            {
                                witObj.Channel.SendMessageAsync($"Justices have {(withPowers ? "ruled to move" : "voted to move")} testimony forwards\n" +
                                    $"{witObj.CurrentlyQuestioning} now questioning the witnesses");
                                System.Threading.Thread.Sleep(500);
                                witObj.SendEmbed();
                            }
                            hearing.JusticesChnl.SendMessageAsync($"Justice **{SelfPlayer.Name}** joins majority {(withPowers ? "ruling" : "vote")} to move testimony of {witObj.Witness.Name} forward");
                        } else
                        {
                            hearing.JusticesChnl.SendMessageAsync($"Justice **{SelfPlayer.Name}** votes to{(withPowers ? " forcefully" : "")} move testimony of {witObj.Witness.Name} foward ({witObj.JusticesVotedAdvance.Count}/{hearing.JudgeRulingMinimum})");
                        }
                        RespondRaw("");
                    }
                } else
                { // non-justices can simply move it of their own accord
                    mustSave = true;
                    witObj.AdvanceNextStage();
                    if(witObj.IsFinishedTestimony)
                    {
                        witObj.Channel.SendMessageAsync($"Testimony of witness {witObj.Witness.Name} has been concluded\n" +
                            $"The witness is dismissed");
                    } else
                    {
                        witObj.Channel.SendMessageAsync($"{SelfPlayer.Name} has {(witObj.Stage <= 1 ? "paused" : "concluded")} their testimony\n" +
                            $"{witObj.CurrentlyQuestioning} now questioning the witness");
                    }
                    RespondRaw("");
                }
            } else
            {
                RespondRaw("No permission to do that", 403);
            }
        }
    
        [Method("PUT"), Path("/chess/coa/rule")]
        public void JusticeDoRule(int num, string type)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.HasFinished)
            {
                RespondRaw("Hearing has already finished", 400);
                return;
            }
            hearing.RemoveJusticeVotes(SelfPlayer);
            if(hearing.AddVote(SelfPlayer, type))
            {
                mustSave = true;
                RespondRaw("Ok");
                if(hearing.HasReachedMajority(out string verdict, out var majority))
                {
                    hearing.Verdict = verdict;
                    hearing.JusticesChnl.SendMessageAsync($"Justice **{SelfPlayer.Name}** causes majority for {verdict}");
                    hearing.GeneralChnl.SendMessageAsync($"Case Concluded", embed: new EmbedBuilder()
                        .WithTitle("Court of Appeals")
                        .WithDescription($"The Court has reached a majority verdict: **{verdict}**")
                        .AddField("Majority", "- " + string.Join("\n- ", majority.Select(x => x.Name)))
                        .AddField("Minority", "- " + string.Join("\n- ", hearing.GetVoteCount(x => x != verdict).Select(x => x.Name)))
                        .WithCurrentTimestamp()
                        .Build());
                    foreach(var witObj in hearing.Witnesses)
                    {
                        if(witObj.Stage < 5)
                        {
                            witObj.Stage = 5; // set it beyond
                            witObj.Channel.SendMessageAsync("Witness testimony concluded since the overall appeal was " + verdict);
                        }
                    }
                    hearing.SetChannelPermissions();
                } else
                {
                    hearing.JusticesChnl.SendMessageAsync($"Justice **{SelfPlayer.Name}** votes that this Court reaches a verdict of {type}");
                }
            } else
            {
                RespondRaw("Error occured, maybe you are not a justice?", 400);
                return;
            }
        }
    
        [Method("PUT"), Path("/chess/coa/allow_enbanc")]
        public void PermitEnBancAppeal(int num)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(!hearing.IsAppealRequested)
            {
                RespondRaw("No appeal requested", 404);
                return;
            }
            if(SelfPlayer.Permission != ChessPerm.CourtOfAppeals)
            {
                RespondRaw("Only the Chief Justice may do that", 403);
                return;
            }
            var newHear = new CoAHearing(hearing.Plaintiff, hearing.Defendant, $"Appeal {hearing.Verdict} ruling from #{hearing.CaseStr}");
            newHear.Justices = Players.Where(x => !x.IsBuiltInAccount && x.Permission.HasFlag(ChessPerm.Justice)).ToArray();
            newHear.GeneralChnl = hearing.GeneralChnl;
            newHear.JusticesChnl = hearing.JusticesChnl;
            newHear.Category = hearing.Category;
            newHear.Witnesses = hearing.Witnesses;
            newHear.AnAppealOf = hearing;
            hearing.AppealHearing = newHear;
            Hearings.Add(newHear);
            mustSave = true;
            newHear.ClearChannelPermissions(); // just clean it up
            newHear.SetChannelPermissions();
            newHear.GeneralChnl.SendMessageAsync("This case has been appealed to be heard by the whole Court.\nThese channels will now be used by the appeal case");
            newHear.JusticesChnl.SendMessageAsync($"An appeal should **not** be a brand new trial.\n" +
                $"Instead, this Court should evaluate descisions made by the previous Justices and determine whether any errors in logic were made\n" +
                $"This Court should not be calling any new witnesses as all the applicable facts of the case should be established");
            foreach(var player in new ChessPlayer[] { hearing.Plaintiff, hearing.Defendant})
            {
                if (player.Id == SelfPlayer.Id)
                    continue;
                if(player.ConnectedAccount > 0)
                {
                    var usr = Program.GetUserOrDefault(player.ConnectedAccount);
                    if (usr == null)
                        continue;
                    var builder = new EmbedBuilder();
                    builder.WithTitle("Court of Appeals");
                    builder.WithDescription($"A case you were involved in has been appealed to be heard *en banc*");
                    builder.AddField("What this means", "The Court should review the ruling and any descisions made by the previous Justices\n" +
                        "Then they should come to a ruling on whether to uphold or overturn the verdict");
                    if (Program.ChessGuild.GetUser(player.ConnectedAccount) == null)
                        builder.AddField("Invite", $"To server: {Invite}");
                    usr.SendMessageAsync(embed: builder.Build());
                }
            }
        }

        [Method("PUT"), Path("/chess/coa/enbanc")]
        public void AppealPriorCase(int num)
        {
            var hearing = Hearings.FirstOrDefault(x => x.CaseNumber == num);
            if(hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.AppealHearing != null || hearing.IsAppealRequested)
            {
                RespondRaw($"Case has already been appealed, or requested to be appealed", 409);
                return;
            }
            if(hearing.Defendant.Id != SelfPlayer.Id && hearing.Plaintiff.Id != SelfPlayer.Id)
            {
                RespondRaw("Only the plaintiff or defendant may appeal", 403);
                return;
            }
            hearing.IsAppealRequested = true;
            mustSave = true;
            ulong chiefId = ulong.Parse(Program.Configuration["chess:chief:id"]);
            var usr = Program.Client.GetUser(chiefId);
            usr.SendMessageAsync(embed: new EmbedBuilder()
                .WithTitle("Court of Appeals")
                .WithDescription($"[Case #{hearing.CaseStr}]({Handler.LocalAPIUrl}/chess/coa/hearing?num={hearing.CaseNumber})'s " +
                    $"verdict of {hearing.Verdict} has requsted to be appealed by {SelfPlayer.Name}")
                .Build());
            RespondRaw("");
        }
    }
}
