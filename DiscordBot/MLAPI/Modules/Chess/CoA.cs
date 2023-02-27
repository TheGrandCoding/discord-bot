#if INCLUDE_CHESS
using DiscordBot.Classes.Chess;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscordBot.MLAPI.Modules
{
    [RequireVerifiedAccount]
    [RequireChess(Classes.Chess.ChessPerm.Player)]
    public class CoA : ChessBase
    {
        public CoA(APIContext c) : base(c, "chess/coa") 
        {
            ChesssInstance = Program.Services.GetRequiredService<ChessService>();
            Sidebar = SidebarType.Local;
            InjectObjects.Add(new PageLink("stylesheet", "text/css", "/_/css/chessCOA.css"));
        }
        public ChessService ChesssInstance { get; set; }

        public override void AfterExecute()
        {
            if(Context.Method != "GET" && (StatusSent > 100 && StatusSent < 400))
            {
                DB.SaveChanges();
            }
        }

#region Browser Endpoints

        [Method("GET"), Path("/chess/cases")]
        public void ListCases()
        {
            var awaitingWrit = new Table()
            {
                Children =
                {
                    new TableRow()
                        .WithHeader("Title")
                        .WithHeader("Filed")
                        .WithHeader("Brief")
                }
            };
            var receivedWrit = new Table()
            {
                Children =
                {
                    new TableRow()
                    .WithHeader("Title")
                    .WithHeader("Filed")
                    .WithHeader("Commenced")
                }
            };
            var receivedOutcome = new Table()
            {
                Children =
                {
                    new TableRow()
                    .WithHeader("Title")
                    .WithHeader("Decided")
                    .WithHeader("Holding")
                }
            };
            Func<AppealsHearing, string> getAnchor = x => new Anchor($"/chess/cases/{x.Id}", x.Title) + "<br/>" +
                (x.IsArbiterCase ? "Arbiter" : "Court of Appeals");
            foreach(var hearing in DB.Appeals)
            {
                if(hearing.Holding != null)
                {
                    receivedOutcome.Children.Add(new TableRow()
                        .WithCell(getAnchor(hearing))
                        .WithCell(hearing.Concluded.Value.ToString("dd/MM/yyyy"))
                        .WithCell(hearing.Holding));
                } else if (hearing.Commenced.HasValue)
                {
                    receivedWrit.Children.Add(new TableRow()
                        .WithCell(getAnchor(hearing))
                        .WithCell(hearing.Filed.ToString("dd/MM/yyyy"))
                        .WithCell(hearing.Commenced.Value.ToShortDateString()));
                } else
                {
                    awaitingWrit.Children.Add(new TableRow()
                        .WithCell(getAnchor(hearing))
                        .WithCell(hearing.Filed.ToString("dd/MM/yyyy"))
                        .WithCell(new Anchor($"/chess/cases/{hearing.Id}/motions/00", hearing.Motions.FirstOrDefault()?.MotionType ?? "none")));
                }
            }
            Sidebar = SidebarType.None;
            await ReplyFile("base.html", 200, new Replacements()
                .Add(nameof(awaitingWrit), awaitingWrit)
                .Add(nameof(receivedWrit), receivedWrit)
                .Add(nameof(receivedOutcome), receivedOutcome));
        }

        [Method("GET"), Path("/chess/coa_pass")]
        [RequireChess(ChessPerm.ChiefJustice)]
        public void Password()
        {
            var service = Program.Services.GetRequiredService<OauthCallbackService>();
            var thing = service.Register(coaLogin, Context.User.Id);
            await RespondRaw($"<a href='/oauth2/misc?state={thing}'>Open in incognite.</a>");
        }

        void coaLogin(object sender, object[] args)
        {
            Login.SetLoginSession(Context, this.ChesssInstance.BuiltInCoAUser);
            await RespondRedirect("/chess"), System.Net.HttpStatusCode.Redirect);
        }

#region View Hearing

        string getHearingOutcome(AppealsHearing h)
        {
            string outcom = "";
            if(h.Ruling != null)
            {
                string who = h.IsArbiterCase ? "Arbiter" : "Court";
                outcom = $"<p>The {who}'s ruling on this case:</p><iframe class='coaDoc' src='/chess/cases/{h.Id}/ruling'></iframe>";
            } else if(h.isClerkOnCase(SelfPlayer))
            {
                outcom = new Paragraph(new Anchor($"/chess/cases/{h.Id}/newruling", "Submit new ruling"));
            }
            return outcom;
        }
        string getHearingActions(AppealsHearing h)
        {
            if (h.Holding == null || !h.IsArbiterCase)
                return "";
            var appeals = DB.Appeals.Any(x => x.AppealOf == h.Id);
            if (appeals)
                return "";
            var rel = h.Members.FirstOrDefault(x => x.MemberId == SelfPlayer.Id)?.Relation ?? Relation.None;
            if (rel.HasFlag(Relation.Claimant) || rel.HasFlag(Relation.Respondent))
                return new Paragraph(new Input("button", "Appeal this Hearing")
                {
                    OnClick = $"AppealsHearing({h.Id});"
                });
            return "";

        }

        [Method("GET")]
        [Path("/chess/cases/{n}")]
        [Regex("n", @"\d{1,4}(?!\d*\/)")]
        public void ViewHearingInfo(int n)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if(hearing == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Case Number", "Could not find a petition by that assigned case number");
                return;
            }

            var motions = new Table()
            {
                Children =
                {
                    new TableRow()
                        .WithHeader("Filed On")
                        .WithHeader("Filed By")
                        .WithHeader("Type")
                        .WithHeader("Result")
                }
            };

            foreach(var motion in hearing.Motions)
            {
                motions.Children.Add(new TableRow()
                    .WithCell(new Anchor($"/chess/cases/{hearing.Id}/motions/{motion.Id}", motion.Filed.ToString("dd/MM/yyyy, hh:mm:ss")))
                    .WithCell(motion.Movant.Name)
                    .WithCell(motion.MotionType)
                    .WithCell(motion.Holding ?? "No ruling on motion yet") 
                    );
            }

            var exhibits = new Table()
            {
                Children =
                {
                    new TableRow()
                        .WithHeader("Number")
                        .WithHeader("Filed By")
                        .WithHeader("Filed On")
                }
            };

            var index = 0;
            foreach(var exhibit in hearing.Exhibits)
            {
                exhibit.Attachment ??= DB.AppealsAttachments.FirstOrDefault(x => x.Id == exhibit.AttachmentId);
                var player = DB.Players.FirstOrDefault(x => x.Id == exhibit.Attachment.FiledBy);
                exhibits.Children.Add(new TableRow()
                    .WithCell(new Anchor($"/chess/cases/{hearing.Id}/exhibit/{exhibit.AttachmentId}", $"#{++index}"))
                    .WithCell(hearing.getRelationToCase(player) + " " + player.Name)
                    .WithCell(hearing.Filed.ToString("yyyy/MM/dd hh:mm:ss"))
                );
            }

            var witnesses = new Table()
            {
                Children =
                {
                    new TableRow()
                    .WithHeader("Name")
                    .WithHeader("")
                }
            };
            foreach(var witness in hearing.Witnesses)
            {
                witnesses.Children.Add(new TableRow()
                    .WithCell(new Anchor($"/chess/cases/{hearing.Id}/witness/{witness.Witness.Id}", witness.Witness.Name))
                    .WithCell(witness.ConcludedOn.HasValue ? witness.ConcludedOn.Value.ToString("dd/MM/yyyy") : "Remains ongoing")
                );
            }

            if(hearing.Holding == null)
            {
                motions.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableHeader(new Anchor($"/chess/cases/{hearing.Id}/newmotion", "File a new motion"))
                        {
                            ColSpan = "4"
                        }
                    }
                });
                if(hearing.Concluded.HasValue == false)
                {
                    if(hearing.getRelationToCase(SelfPlayer) != "Outsider")
                    {
                        exhibits.Children.Add(new TableRow()
                        {
                            Children =
                            {
                                new TableHeader(new Anchor($"/chess/cases/{hearing.Id}/newexhibit", "Submit a new exhibit"))
                                {
                                    ColSpan = "3"
                                }
                            }
                        });
                    }
                    if(hearing.Commenced.HasValue && hearing.CanCallWitness(SelfPlayer))
                    {
                        witnesses.Children.Add(new TableRow()
                        {
                            Children =
                            {
                                new TableHeader(new Anchor($"/chess/cases/{hearing.Id}/newwitness", "Call a new witness"))
                                {
                                    ColSpan = "2"
                                }
                            }
                        });
                    }
                }
            }

            await ReplyFile("hearing.html", 200, new Replacements(hearing)
                .Add("actions", getHearingActions(hearing))
                .Add("outcomes", getHearingOutcome(hearing))
                .Add("motions", motions)
                .Add("exhibits", exhibits)
                .Add("notice", (hearing.AppealOf.HasValue && hearing.Motions.First().Attachments.Count == 0) ? "<p class='warn'>You must upload a document to the Motion below which explains why the Court should agree to hear your appeal</p>" : "")
                .Add("witnesses", witnesses)
            );
        }
#endregion

        [Method("GET")]
        [Path("/chess/cases/{n}/motions/{mn}")]
        [Regex("n", @"\d{1,4}")]
        [Regex("mn", @"\d{1,2}(?!\/)")]
        public void ViewMotionInfo(int n, int mn)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if(hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            var motion = hearing.Motions.FirstOrDefault(x => x.Id ==mn);
            if(motion == null)
            {
                await RespondRaw("Unknown motion", 404);
                return;
            }

            var attachments = new Div();
            int attachmentIndex = 0;
            foreach(var attch in motion.Attachments)
            {
                attch.Attachment ??= DB.AppealsAttachments.FirstOrDefault(x => x.Id == attch.AttachmentId);
                var player = DB.Players.FirstOrDefault(x => x.Id == attch.Attachment.FiledBy);
                string relation = hearing.getRelationToCase(player);
                var div = new Div(id: attch.AttachmentId.ToString(), cls: "file file-" + relation.ToLower())
                {
                    Children =
                    {
                        new Paragraph($"Filed by {relation} {player.Name}")
                    }
                };
                if(hearing.isClerkOnCase(SelfPlayer))
                {
                    div.Children.Add(new Input("button", hearing.Sealed ? "Unseal" : "Seal")
                    {
                        OnClick = $"toggleSeal({attachmentIndex});"
                    });
                }
                div.Children.Add(new RawObject($"<iframe src='/chess/cases/{n}/motions/{mn}/{attch.AttachmentId}'></iframe>"));
                attachments.Children.Add(div);
            }
            var holding = new RawObject("");
            if(motion.Holding != null)
            {
                string cls;
                if (motion.Denied)
                    cls = "denied";
                else if (motion.Granted)
                    cls = "granted";
                else
                    cls = "";
                holding = new RawObject($"<p class='{cls}'><strong>{motion.Holding}</strong> on {motion.HoldingDate:dd/MM/yyyy}</p>");
            }

            string cj = hearing.isClerkOnCase(SelfPlayer)
                ? "<input type='button' class='cjdo' onclick='doThing()' value='Submit holding'>"
                : "";
            await ReplyFile("motion.html", 200, new Replacements(hearing)
                .Add("files", attachments)
                .Add("motion", motion)
                .Add("mholding", holding)
                .Add("chief", cj)
                .IfElse("canadd", motion.Granted || motion.Denied, "display: none;", "display: block")
                .Add("cjDoUrl", $"/chess/api/cases/{n}/motions/{mn}/holding")
                .Add("newpath", $"cases/{n}/motions/{mn}/files"));
        }

        [Method("GET")]
        [Path("/chess/cases/{n}/exhibits/{ex}")]
        [Regex("n", @"\d{1,4}")]
        [Regex("ex", @"\d{1,2}(?!\/)")]
        public void ViewExhibitInfo(int n, int ex)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            var exhibit = hearing.Exhibits.FirstOrDefault(x => x.AttachmentId == ex);
            if(exhibit == null)
            {
                await RespondRaw("Unknown exhibit", 404);
                return;
            }
            await ReplyFile("exhibit.html", 200, new Replacements(hearing)
                .Add("exhibit", exhibit)
                .Add("index", ex)
                .Add("iframe", $"<iframe src='/chess/cases/{n}/exhibit/{ex}'></iframe>"));
        }

        [Method("GET")]
        [Path("/chess/cases/{n}/witnesses/{id}")]
        [Regex("n", @"\d{1,4}")]
        [Regex("id", @"\d{1,3}(?!\/)")]
        public void ViewWitnessInfo(int n, int id)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if(hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.Sealed)
            {
                await RespondRaw("Hearing has been Ordered sealed by the Court of Appeals.", System.Net.HttpStatusCode.UnavailableForLegalReasons);
                return;
            }
            var witness = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == id);
            if(witness == null)
            {
                await RespondRaw("Unknown witness", 404);
                return;
            }
            await ReplyFile("witness.html", 200, new Replacements(hearing)
                .Add("witness", witness)
                .Add("concluded", witness.ConcludedOn.HasValue 
                    ? $"Witness testimony was closed/concluded on {witness.ConcludedOn.Value.ToString("dd/MM/yyyy")}"
                    : "Testimony remains on going")
            );
        }

        [Method("GET"), Path("/chess/new")]
        public void ViewCreateHearing(string type)
        {
            if (!(type == "coa" || type == "arbiter"))
                return;
            Sidebar = SidebarType.None;
            var multiSelect = new Div(cls: "playerList");
            var playerTable = new Table()
            {
                Children =
                {
                    new TableRow()
                        .WithHeader("Player Name")
                        .WithHeader("File against?")
                        .WithHeader("In their official capacity?")
                }
            };
            multiSelect.Children.Add(playerTable);
            int i = 0;
            foreach(var p in DB.Players.AsQueryable().Where(x => x.Id != SelfPlayer.Id && !x.IsBuiltInAccount).OrderByDescending(x => x.Rating).ToList())
            {
                if(type == "arbiter")
                {
                    if (p.Permission != ChessPerm.Moderator)
                        continue;
                }
                var tRow = new TableRow(id:p.Id.ToString(), cls: "playerListElement " + ((i % 2 == 0) ? "even" : "odd"));
                tRow.Id = p.Id.ToString();
                tRow.WithCell(p.Name);
                tRow.WithCell(new Input("checkbox", id: $"cb-{p.Id}")
                {
                    OnClick = "setText();"
                });
                tRow.WithCell(new Input("checkbox", id: $"ct-{p.Id}")
                {
                    Disabled = p.Permission == ChessPerm.Player,
                    OnClick = p.Permission == ChessPerm.Player ? "" : "setText();"
                });
                playerTable.Children.Add(tRow);
                i++;
            }
            await ReplyFile($"new_{type}.html", 200, new Replacements()
                .Add("players", multiSelect));
        }

        [Method("GET")]
        [Path("/chess/cases/{n}/newmotion")]
        [Regex("n", @"\d{1,4}")]
        public void ViewCreateMotion(int n)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.Holding != null)
            {
                await RespondRaw("Petition has been concluded, no motions may be added.", 400);
                return;
            }
            var list = new Select(name: "types");
            var motionType = typeof(Motions);
            var fields = motionType.GetFields();
            foreach(var type in fields)
            {
                if (type.Name == nameof(Motions.WritOfCertiorari))
                    continue;
                list.Add((string)type.GetValue(null), type.Name);
            }
            await ReplyFile("newmotion.html", 200, new Replacements(hearing)
                .Add("types", list));
        }

        [Method("GET")]
        [Path("/chess/cases/{n}/newexhibit")]
        [Regex("n", @"\d{1,4}")]
        public void ViewCreateExhibit(int n)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            if (hearing.Holding != null)
            {
                await RespondRaw("Petition has been concluded, no exhibits may be added.", 400);
                return;
            }
            if(hearing.getRelationToCase(SelfPlayer) == "Outside")
            {
                await RespondRaw("You are unrelated to this case.", 403);
                return;
            }
            await ReplyFile("newexhibit.html", 200, new Replacements(hearing));
        }

        [Method("GET")]
        [Path("/chess/cases/{n}/newwitness")]
        [Regex("n", @"\d{1,4}")]
        public void ViewCreateWitness(int n)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            if (hearing.Holding != null)
            {
                await RespondRaw("Petition has been concluded, no further witnesses may be called.", 400);
                return;
            }
            var existingWitnesses = new OptionGroup("Already called");
            var notCalled = new OptionGroup("Available");
            var players = new Select(id: "players")
            {
                Name = "id",
                Children =
                {
                    notCalled,
                    existingWitnesses
                }
            };
            foreach(var player in DB.Players.AsQueryable().Where(x => !x.IsBuiltInAccount).OrderByDescending(x => x.Rating).ToList())
            {
                if(hearing.Witnesses.Any(x => x.Witness.Id == player.Id))
                {
                    existingWitnesses.Children.Add(new Option(player.Name, "")
                    {
                        ReadOnly = true,
                    });
                } else
                {
                    notCalled.Add(player.Name, player.Id.ToString());
                }
            }

            await ReplyFile("newwitness.html", 200, new Replacements(hearing)
                .Add("users", players));
        }

        [Method("GET")]
        [Path("/chess/cases/{n}/newruling")]
        [Regex("n", @"\d{1,4}")]
        public void ViewCreateRuling(int n)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if(hearing == null || hearing.Sealed || !hearing.isJudgeOnCase(SelfPlayer))
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Case Number", "Could not find a hearing by that case number");
                return;
            }
            await ReplyFile("newruling.html", 200, new Replacements(hearing));
        }

#endregion
        
#region API Endpoints

#region New Hearing
        [Method("POST"), Path("/chess/api/cases")]
        public void CreateHearing(string type, string[] respondents)
        {
            List<AppealsMember> _Respondents = new List<AppealsMember>();
            foreach(var text in respondents)
            {
                var split = text.Split(':');
                var id = int.Parse(split[0]);
                var inOfficial = split.Length > 1;
                var x = DB.Players.FirstOrDefault(x => x.Id == id);
                if(x == null)
                {
                    await RespondRaw($"Could not find user with id '{id}'", 404);
                    return;
                }
                if(x.IsBuiltInAccount)
                {
                    await RespondRaw($"'{x.Name} ({id})' is a built in account for internal usage.", 400);
                    return;
                }
                var memb = new AppealsMember(x, null);
                memb.Relation = Relation.Respondent;
                if (inOfficial)
                    memb.Relation |= Relation.OfficialCapacity;
                _Respondents.Add(memb);

            }
            var file = Context.Files.FirstOrDefault();
            if(file == null)
            {
                await RespondRaw("You did not upload an initial attachment; please return to previous page and retry.", 400);
                return;
            }
            var extension = file.FileName.Split('.')[^1];
            if(!isPermittedExtension(extension))
            {
                await RespondRaw($"File uploaded must be .txt, .pdf, or .md", 400);
                return;
            }
            var hearing = new AppealsHearing(new List<ChessPlayer>() { SelfPlayer }, _Respondents);
            hearing.Filed = DateTime.Now;
            hearing.IsArbiterCase = type == "arbiter";
            DB.Appeals.Add(hearing);
            DB.SaveChanges();
            string fName = "temp_writ." + extension;
            var attachment = new AppealsAttachment(fName, SelfPlayer.Id);
            var mf = new AppealsMotionFile()
            {
                Attachment = attachment,
            };
            var motion = new AppealsMotion()
            {
                Attachments = { mf },
                Filed = DateTime.Now,
                Hearing = hearing,
                MotionType = Motions.WritOfCertiorari,
                Movant = SelfPlayer,
            };
            mf.Motion = motion;
            if(hearing.IsArbiterCase)
            {
                motion.Holding = "Granted automatically";
                motion.HoldingDate = motion.Filed;
                hearing.Commenced = DateTime.Now;
            }
            hearing.Motions.Add(motion);
            DB.SaveChanges();
            attachment.FileName = $"{attachment.Id}.{extension}";
            var path = Path.Join(Program.BASE_PATH, "data", "coa", "attachments");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Join(path, attachment.FileName);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            await RespondRedirect($"/chess/cases/{hearing.Id}"), System.Net.HttpStatusCode.Redirect);
        }
#endregion

        [Method("POST")]
        [Path("/chess/api/cases/{n}/rulings")]
        [Regex("n", @"\d{1,4}")]
        public void CreateNewRuling(int n, string desc)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            var file = Context.Files.FirstOrDefault();
            if (file == null)
            {
                await RespondRaw("No file", 400);
                return;
            }
            var ext = file.FileName.Split('.')[^1];
            if (!isPermittedExtension(ext))
            {
                await RespondRaw("Extension must be .txt, .pdf or .md", 400);
                return;
            }
            var attachment = new AppealsAttachment(file.FileName, SelfPlayer.Id);
            var ruling = new AppealsRuling()
            {
                Holding = desc,
                Submitter = SelfPlayer,
                Attachment = attachment
            };
            hearing.Holding = desc;
            hearing.Ruling = ruling;
            hearing.Concluded = DateTime.Now;

            DB.SaveChanges();
            attachment.FileName = $"{attachment.Id}.{ext}";
            var path = Path.Join(Program.BASE_PATH, "data", "coa", "attachments");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Join(path, attachment.FileName);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            await RespondRedirect($"/chess/cases/{hearing.Id}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("POST")]
        [Path("/chess/api/cases/{n}/exhibits")]
        [Regex("n", @"\d{1,4}")]
        public void CreateNewExhibit(int n, string _ = null)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.getRelationToCase(SelfPlayer) == "Outside")
            {
                await RespondRaw("You are not related to this case.", 403);
                return;
            }
            var file = Context.Files.FirstOrDefault();
            if (file == null)
            {
                await RespondRaw("No file", 400);
                return;
            }
            var ext = file.FileName.Split('.')[^1];
            if (!isPermittedExtension(ext))
            {
                await RespondRaw("Extension is not permitted", 400);
                return;
            }
            var attachment = new AppealsAttachment(file.FileName, SelfPlayer.Id);
            var exhibit = new AppealsExhibit()
            {
                Attachment = attachment,
                Hearing = hearing,
            };
            hearing.Exhibits.Add(exhibit);
            DB.SaveChanges();

            attachment.FileName = $"{attachment.Id}.{ext}";
            var path = Path.Join(Program.BASE_PATH, "data", "coa", "attachments");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Join(path, attachment.FileName);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            await RespondRedirect($"/chess/cases/{hearing.Id}/exhibits/{exhibit.AttachmentId}"));
        }

        [Method("POST")]
        [Path("/chess/api/cases/{n}/motions/{mn}/files")]
        [Regex("n", @"\d{1,4}")]
        [Regex("mn", @"\d{1,2}")]
        public void AttachFileToMotion(int n, int mn, string notneeded = "") // string is to force body to be parsed, since it isn't until an argument is needed that isn't in query or regex.
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if(hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            var motion = hearing.Motions.FirstOrDefault(x => x.Id ==mn);
            if(motion == null)
            {
                await RespondRaw("Unknown motion", 404);
                return;
            }
            if(motion.Denied || motion.Granted)
            { // some outcome
                await RespondRaw("Motion has already been ruled on", 400);
                return;
            }
            var file = Context.Files.FirstOrDefault();
            if(file == null)
            {
                await RespondRaw("No file", 400);
                return;
            }
            var ext = file.FileName.Split('.')[^1];
            if(!isPermittedExtension(ext))
            {
                await RespondRaw("Extension must be .txt, .pdf or .md", 400);
                return;
            }
            string fName = $"{(motion.Attachments.Count + 1):00}_{file.FileName}";
            var attachment = new AppealsAttachment(fName, SelfPlayer.Id);
            var mf = new AppealsMotionFile()
            {
                Attachment = attachment,
                Motion = motion
            };
            motion.Attachments.Add(mf);
            DB.SaveChanges();

            attachment.FileName = $"{attachment.Id}.{ext}";
            var path = Path.Join(Program.BASE_PATH, "data", "coa", "attachments");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Join(path, attachment.FileName);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            await RespondRedirect($"/chess/cases/{hearing.Id}/motions/{motion.Id}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("PATCH")]
        [Path("/chess/api/cases/{n}/motions/{mn}/holding")]
        [Regex("n", @"\d{1,4}")]
        [Regex("mn", @"\d{1,2}")]
        public void SetMotionOutcome(int n, int mn)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            var motion = hearing.Motions.FirstOrDefault(x => x.Id ==mn);
            if (motion == null)
            {
                await RespondRaw("Unknown motion", 404);
                return;
            }
            motion.Holding = Uri.UnescapeDataString(Context.Body);
            motion.HoldingDate = DateTime.Now;
            if(motion.MotionType == Motions.WritOfCertiorari)
            {
                if(motion.Granted)
                {
                    hearing.Commenced = motion.HoldingDate;
                    hearing.Holding = null;
                    hearing.Concluded = null;
                } else if(motion.Denied)
                {
                    hearing.Holding = "Cert. denied, Court declined to hear petition; dismissed";
                    hearing.Concluded = motion.HoldingDate;
                }
            }
            await RespondRaw("");
        }

        [Method("POST")]
        [Path(@"/chess/api/cases/{n}/motions")]
        [Regex(".", @"\/chess\/api\/cases\/\d{1,4}\/motions(?!\/)")]
        public void CreateNewMotion(int n, string types)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if(hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.Holding != null)
            {
                await RespondRaw("Petition has concluded, motions cannot be added", 400);
                return;
            }

            var motionType = typeof(Motions).GetFields().FirstOrDefault(x => x.Name == types);
            if(motionType == null)
            {
                await RespondRaw("Unknown motion type", 400);
                return;
            }
            string name = (string)motionType.GetValue(null);
            if(name == Motions.WritOfCertiorari)
            {
                await RespondRaw("Motion for writ of cert. can only be made once - automatically when petition is filed.");
                return;
            }
            var motion = new AppealsMotion()
            {
                MotionType = name,
                Filed = DateTime.Now,
                Movant = SelfPlayer
            };
            hearing.Motions.Add(motion);
            DB.SaveChanges();
            await RespondRedirect($"/chess/cases/{n}/motions/{motion.Id}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("POST")]
        [Path("/chess/api/cases/{n}/witnesses")]
        [Regex(".", @"\/chess\/api\/cases\/\d{1,4}\/witnesses(?!\/)")]
        public void CreateNewWitness(int n, int id)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if (hearing == null)
            {
                await RespondRaw("Unknown hearing", 404);
                return;
            }
            if (hearing.Holding != null)
            {
                await RespondRaw("Petition has concluded, motions cannot be added", 400);
                return;
            }
            if(!hearing.CanCallWitness(SelfPlayer))
            {
                await RespondRaw("You cannot call witnesses for this petition.", System.Net.HttpStatusCode.Forbidden);
                return;
            }
            var player = DB.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                await RespondRaw("Unknown player", 404);
                return;
            }
            var existing = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == id);
            if(existing != null)
            {
                await RespondRedirect($"/chess/cases/{hearing.Id}/witnesses/{id}"), System.Net.HttpStatusCode.Conflict);
                return;
            }
            var witness = new AppealsWitness()
            {
                HearingId = hearing.Id,
                Witness = player
            };
            hearing.Witnesses.Add(witness);
            await RespondRedirect($"/chess/cases/{hearing.Id}/witnesses/{id}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("PUT")]
        [Path("/chess/api/appeal/{n}")]
        [Regex("n", @"\d{1,4}")]
        public void SubmitAppeal(int n)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == n);
            if(hearing == null)
            {
                await RespondRaw("Error: No hearing by that case number.", 404);
                return;
            }
            if(hearing.AppealOf.HasValue)
            {
                await RespondRaw("Error: Cases that are appeals cannot be appealed; judgement is final.");
                return;
            }
            if(!hearing.IsArbiterCase)
            {
                await RespondRaw("Error: Cases before the Court of Appeals cannot be appealed; judgement is final.");
                return;
            }
            if(hearing.Holding == null)
            {
                await RespondRaw("Error: A judgement must be delivered in the first instance before appeal. If Arbiter refuses to issue, file petition against Arbiter themselves.");
                return;
            }
            var relation = hearing.getRelationToCase(SelfPlayer);
            if(relation != "Claimant" && relation != "Respondent")
            {
                await RespondRaw("Error: only claimants or respondents may appeal.");
                return;
            }
            List<ChessPlayer> apellees;
            List<AppealsMember> apellants;
            if(relation == "Claimant")
            {
                apellees = new List<ChessPlayer>() { SelfPlayer };
                apellees.AddRange(hearing.Claimants.Where(x => x.MemberId != SelfPlayer.Id).Select(x => x.Member));
                apellants = hearing.Respondents.ToList();
            } else
            {
                apellees = new List<ChessPlayer>() { SelfPlayer };
                apellees.AddRange(hearing.Respondents.Where(x => x.MemberId != SelfPlayer.Id).Select(x => x.Member));
                apellants = hearing.Claimants.ToList();
            }
            var appeal = new AppealsHearing(apellees, apellants);
            appeal.AppealOf = hearing.Id;
            appeal.Filed = DateTime.Now;
            var mtn = new AppealsMotion()
            {
                Filed = DateTime.Now,
                MotionType = Motions.WritOfCertiorari,
                Movant = SelfPlayer,
                Attachments = new List<AppealsMotionFile>()
            };
            appeal.Motions.Add(mtn);
            DB.Appeals.Add(appeal);
            DB.SaveChanges();
            await RespondRedirect($"/chess/cases/{appeal.Id}"), System.Net.HttpStatusCode.Redirect);
        }

        string mimeFromExtension(string ext)
        {
            return ext switch
            {
                "txt" => "text/plain",
                "md" => "text/plain",
                "pdf" => "application/pdf",
                _ => ext,
            };
        }
        bool isPermittedExtension(string ext) => mimeFromExtension(ext) != ext;

        [Method("GET")]
        [Path("/chess/cases/{cn}/motions/{mi}/{ai}")]
        [Regex("cn", @"\d{1,4}")]
        [Regex("mi", @"\d{1,2}")]
        [Regex("ai", @"\d{1,2}")]
        public void GetFileRaw(int cn, int mi, int ai)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == cn);
            if (hearing == null || hearing.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a hearing at this URL");
                return;
            }
            var motion = hearing.Motions.FirstOrDefault(x => x.Id == mi);
            if (motion == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a motion at this URL");
                return;
            }
            var attachment = motion.Attachments.FirstOrDefault(x => x.AttachmentId == ai);
            if (attachment == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find an attachment at this URL");
                return;
            }
            attachment.Attachment ??= DB.AppealsAttachments.FirstOrDefault(x => x.Id == attachment.AttachmentId);
            var ext = attachment.Attachment.FileName.Split(".")[^1];
            StatusSent = 200;
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.ContentType = mimeFromExtension(ext);


            var path = Path.Join(Program.BASE_PATH, "data", "coa", "attachments");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Join(path, attachment.Attachment.FileName);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            fs.CopyTo(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
        }

        [Method("GET")]
        [Path("/chess/cases/{cn}/ruling")]
        [Regex("cn", @"\d{1,4}")]
        public void GetRulingRaw(int cn)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == cn);
            if (hearing == null || hearing.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a hearing at this URL");
                return;
            }
            if (hearing.Ruling == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a ruling at this URL");
                return;
            }
            if (hearing.Ruling.Attachment == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find an attachment at this URL");
                return;
            }
            var ext = hearing.Ruling.Attachment.FileName.Split(".")[^1];
            StatusSent = 200;
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.ContentType = mimeFromExtension(ext);
            var path = Path.Join(Program.BASE_PATH, "data", "coa", "attachments");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Join(path, hearing.Ruling.Attachment.FileName);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            fs.CopyTo(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
        }

        [Method("GET")]
        [Path("/chess/cases/{cn}/exhibit/{ex}")]
        [Regex("cn", @"\d{1,4}")]
        [Regex("ex", @"\d{1,2}")]
        public void GetExhibitRaw(int cn, int ex)
        {
            var hearing = DB.Appeals.FirstOrDefault(x => x.Id == cn);
            if (hearing == null || hearing.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a hearing at this URL");
                return;
            }
            var exhibit = hearing.Exhibits.FirstOrDefault(x => x.AttachmentId == ex);
            if(exhibit == null || hearing.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Exhibit is either non-existent or sealed.");
                return;
            }
            exhibit.Attachment ??= DB.AppealsAttachments.FirstOrDefault(x => x.Id == exhibit.AttachmentId);
            var ext = exhibit.Attachment.FileName.Split(".")[^1];
            StatusSent = 200;
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.ContentType = mimeFromExtension(ext);
            var path = Path.Join(Program.BASE_PATH, "data", "coa", "attachments");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Join(path, exhibit.Attachment.FileName);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            fs.CopyTo(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
        }

#endregion
    }
}
#endif