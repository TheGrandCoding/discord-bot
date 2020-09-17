using DiscordBot.Classes.Chess;
using DiscordBot.Classes.Chess.COA;
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
            CoAInstance = Program.Services.GetRequiredService<CoAService>();
            Sidebar = SidebarType.Local;
            InjectObjects.Add(new PageLink("stylesheet", "text/css", "/_/css/chessCOA.css"));
        }
        public ChessService ChesssInstance { get; set; }
        public CoAService CoAInstance { get; set; }

        public override void AfterExecute()
        {
            if(Context.Method != "GET" && (StatusSent > 100 && StatusSent < 400))
            {
                CoAInstance.OnSave();
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
            Func<AppealHearing, string> getAnchor = x => new Anchor($"/chess/cases/{x.CaseNumber}", x.Title) + "<br/>" +
                (x.IsArbiterCase ? "Arbiter" : "Court of Appeals");
            foreach(var hearing in CoAService.Hearings)
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
                        .WithCell(new Anchor($"/chess/cases/{hearing.CaseNumber}/motions/00", hearing.Motions.FirstOrDefault()?.MotionType ?? "none")));
                }
            }
            Sidebar = SidebarType.None;
            ReplyFile("base.html", 200, new Replacements()
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
            RespondRaw($"<a href='/oauth2/misc?state={thing}'>Open in incognite.</a>");
        }

        void coaLogin(object sender, object[] args)
        {
            Login.SetLoginSession(Context, this.ChesssInstance.BuiltInCoAUser);
            RespondRaw(LoadRedirectFile("/chess"), System.Net.HttpStatusCode.Redirect);
        }

        #region View Hearing

        string getHearingOutcome(AppealHearing h)
        {
            string outcom = "";
            if(h.Opinion != null)
            {
                string who = h.IsArbiterCase ? "Arbiter" : "Court";
                outcom = $"<p>The {who}'s ruling on this case:</p><iframe class='coaDoc' src='/chess/cases/{h.CaseNumber}/ruling'></iframe>";
            } else if(h.isClerkOnCase(SelfPlayer))
            {
                outcom = new Paragraph(new Anchor($"/chess/cases/{h.CaseNumber}/newruling", "Submit new ruling"));
            }
            return outcom;
        }
        string getHearingActions(AppealHearing h)
        {
            if (h.Holding == null || !h.IsArbiterCase)
                return "";
            var appeals = CoAService.Hearings.Any(x => x.AppealOf == h.CaseNumber);
            if (appeals)
                return "";
            var rel = h.getRelationToCase(SelfPlayer);
            if (rel == "Claimant" || rel == "Respondent")
                return new Paragraph(new Input("button", "Appeal this Hearing")
                {
                    OnClick = $"appealHearing({h.CaseNumber});"
                });
            return "";

        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})(?!\d*\/)")]
        public void ViewHearingInfo(int n)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
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

            int index = 0;
            foreach(var motion in hearing.Motions)
            {
                motions.Children.Add(new TableRow()
                    .WithCell(new Anchor($"/chess/cases/{hearing.CaseNumber}/motions/{index++}", motion.Filed.ToString("dd/MM/yyyy, hh:mm:ss")))
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

            index = 0;
            foreach(var exhibit in hearing.Exhibits)
            {
                exhibits.Children.Add(new TableRow()
                    .WithCell(new Anchor($"/chess/cases/{hearing.CaseNumber}/exhibit/{index++}", $"#{index - 1}"))
                    .WithCell(hearing.getRelationToCase(exhibit.UploadedBy) + " " + exhibit.UploadedBy.Name)
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
                    .WithCell(new Anchor($"/chess/cases/{hearing.CaseNumber}/witness/{witness.Witness.Id}", witness.Witness.Name))
                    .WithCell(witness.ConcludedOn.HasValue ? witness.ConcludedOn.Value.ToString("dd/MM/yyyy") : "Remains ongoing")
                );
            }

            if(hearing.Holding == null)
            {
                motions.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableHeader(new Anchor($"/chess/cases/{hearing.CaseNumber}/newmotion", "File a new motion"))
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
                                new TableHeader(new Anchor($"/chess/cases/{hearing.CaseNumber}/newexhibit", "Submit a new exhibit"))
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
                                new TableHeader(new Anchor($"/chess/cases/{hearing.CaseNumber}/newwitness", "Call a new witness"))
                                {
                                    ColSpan = "2"
                                }
                            }
                        });
                    }
                }
            }

            ReplyFile("hearing.html", 200, new Replacements(hearing)
                .Add("actions", getHearingActions(hearing))
                .Add("outcomes", getHearingOutcome(hearing))
                .Add("motions", motions)
                .Add("exhibits", exhibits)
                .Add("notice", (hearing.AppealOf.HasValue && hearing.Motions.First().Attachments.Count == 0) ? "<p class='warn'>You must upload a document to the Motion below which explains why the Court should agree to hear your appeal</p>" : "")
                .Add("witnesses", witnesses)
            );
        }
        #endregion

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})\/motions\/(?<mn>\d{1,2})(?!\/)")]
        public void ViewMotionInfo(int n, int mn)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if(hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            var motion = hearing.Motions.ElementAtOrDefault(mn);
            if(motion == null)
            {
                RespondRaw("Unknown motion", 404);
                return;
            }

            var attachments = new Div();
            int attachmentIndex = 0;
            foreach(var attch in motion.Attachments)
            {
                string relation = hearing.getRelationToCase(attch.UploadedBy);
                var div = new Div(id: attch.FileName, cls: "file file-" + relation.ToLower())
                {
                    Children =
                    {
                        new Paragraph($"Filed by {relation} {attch.UploadedBy.Name}")
                    }
                };
                if(hearing.isClerkOnCase(SelfPlayer))
                {
                    div.Children.Add(new Input("button", attch.Sealed ? "Unseal" : "Seal")
                    {
                        OnClick = $"toggleSeal({attachmentIndex});"
                    });
                }
                div.Children.Add(new RawObject($"<iframe src='/chess/cases/{n}/motions/{mn}/{attachmentIndex++}'></iframe>"));
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
            ReplyFile("motion.html", 200, new Replacements(hearing)
                .Add("files", attachments)
                .Add("motion", motion)
                .Add("mholding", holding)
                .Add("chief", cj)
                .IfElse("canadd", motion.Granted || motion.Denied, "display: none;", "display: block")
                .Add("cjDoUrl", $"/chess/api/cases/{n}/motions/{mn}/holding")
                .Add("newpath", $"cases/{n}/motions/{mn}/files"));
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})\/exhibits\/(?<ex>\d{1,2})(?!\/)")]
        public void ViewExhibitInfo(int n, int ex)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            var exhibit = hearing.Exhibits.ElementAtOrDefault(ex);
            if(exhibit == null)
            {
                RespondRaw("Unknown exhibit", 404);
                return;
            }
            ReplyFile("exhibit.html", 200, new Replacements(hearing)
                .Add("exhibit", exhibit)
                .Add("index", ex)
                .Add("iframe", $"<iframe src='/chess/cases/{n}/exhibit/{ex}'></iframe>"));
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})\/witnesses\/(?<id>\d{1,3})(?!\/)")]
        public void ViewWitnessInfo(int n, int id)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if(hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.Sealed)
            {
                RespondRaw("Hearing has been Ordered sealed by the Court of Appeals.", System.Net.HttpStatusCode.UnavailableForLegalReasons);
                return;
            }
            var witness = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == id);
            if(witness == null)
            {
                RespondRaw("Unknown witness", 404);
                return;
            }
            ReplyFile("witness.html", 200, new Replacements(hearing)
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
            int i = 0;
            foreach(var p in ChessService.Players.Where(x => !x.IsBuiltInAccount).OrderByDescending(x => x.Rating))
            {
                if(type == "arbiter")
                {
                    if (p.Permission != ChessPerm.Moderator)
                        continue;
                }
                multiSelect.Children.Add(new Div(cls: "playerListElement " + ((i % 2 == 0) ? "even" : "odd"))
                {
                    Children =
                    {
                        new Input("checkbox", id: $"cb-{p.Id}")
                        {
                            OnClick = "toggle(this);"
                        },
                        new Label(p.Name, $"lbl-{p.Id}")
                    }
                });
                i++;
            }
            ReplyFile($"new_{type}.html", 200, new Replacements()
                .Add("players", multiSelect));
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})\/newmotion")]
        public void ViewCreateMotion(int n)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.Holding != null)
            {
                RespondRaw("Petition has been concluded, no motions may be added.", 400);
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
            ReplyFile("newmotion.html", 200, new Replacements(hearing)
                .Add("types", list));
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})\/newexhibit")]
        public void ViewCreateExhibit(int n)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if (hearing.Holding != null)
            {
                RespondRaw("Petition has been concluded, no exhibits may be added.", 400);
                return;
            }
            if(hearing.getRelationToCase(SelfPlayer) == "Outside")
            {
                RespondRaw("You are unrelated to this case.", 403);
                return;
            }
            ReplyFile("newexhibit.html", 200, new Replacements(hearing));
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})\/newwitness")]
        public void ViewCreateWitness(int n)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if (hearing.Holding != null)
            {
                RespondRaw("Petition has been concluded, no further witnesses may be called.", 400);
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
            foreach(var player in ChessService.Players.Where(x => !x.IsBuiltInAccount).OrderByDescending(x => x.Rating))
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

            ReplyFile("newwitness.html", 200, new Replacements(hearing)
                .Add("users", players));
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<n>\d{1,4})\/newruling")]
        public void ViewCreateRuling(int n)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if(hearing == null || hearing.Sealed || !hearing.isJudgeOnCase(SelfPlayer))
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Case Number", "Could not find a hearing by that case number");
                return;
            }
            ReplyFile("newruling.html", 200, new Replacements(hearing));
        }

        #endregion
        #region API Endpoints

        #region New Hearing
        [Method("POST"), Path("/chess/api/cases")]
        public void CreateHearing(string type, int[] respondents)
        {
            List<ChessPlayer> _Respondents = new List<ChessPlayer>();
            foreach(var id in respondents)
            {
                var x = ChesssInstance.GetPlayer(id);
                if(x == null)
                {
                    RespondRaw($"Could not find user with id '{id}'", 404);
                    return;
                }
                if(x.IsBuiltInAccount)
                {
                    RespondRaw($"'{x.Name} ({id})' is a built in account for internal usage.", 400);
                    return;
                }
                _Respondents.Add(x);

            }
            var file = Context.Files.FirstOrDefault();
            if(file == null)
            {
                RespondRaw("You did not upload an initial attachment; please return to previous page and retry.", 400);
                return;
            }
            var extension = file.FileName.Split('.')[^1];
            if(!isPermittedExtension(extension))
            {
                RespondRaw($"File uploaded must be .txt, .pdf, or .md", 400);
                return;
            }
            var hearing = new AppealHearing(new List<ChessPlayer>() { SelfPlayer }, _Respondents);
            hearing.CaseNumber = CoAService.Hearings.Count + 1;
            hearing.Filed = DateTime.Now;
            hearing.IsArbiterCase = type == "arbiter";

            string fName = "00_writ_cert." + extension;
            var attachment = new CoAttachment(fName, SelfPlayer);
            var motion = new CoAMotion()
            {
                Attachments = { attachment },
                Filed = DateTime.Now,
                Hearing = hearing,
                MotionType = Motions.WritOfCertiorari,
                Movant = SelfPlayer,
            };
            if(hearing.IsArbiterCase)
            {
                motion.Holding = "Granted automatically";
                motion.HoldingDate = motion.Filed;
                hearing.Commenced = DateTime.Now;
            }
            hearing.Motions.Add(motion);
            hearing.SetIds();
            if (!Directory.Exists(motion.DataPath))
                Directory.CreateDirectory(motion.DataPath);
            using var fs = new FileStream(attachment.DataPath, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            CoAService.Hearings.Add(hearing);
            RespondRaw(LoadRedirectFile($"/chess/cases/{hearing.CaseNumber}"), System.Net.HttpStatusCode.Redirect);
        }
        #endregion

        [Method("POST"), PathRegex(@"\/chess\/api\/cases\/(?<n>\d{1,4})\/rulings")]
        public void CreateNewRuling(int n, string desc)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            var file = Context.Files.FirstOrDefault();
            if (file == null)
            {
                RespondRaw("No file", 400);
                return;
            }
            var ext = file.FileName.Split('.')[^1];
            if (!isPermittedExtension(ext))
            {
                RespondRaw("Extension must be .txt, .pdf or .md", 400);
                return;
            }
            var attachment = new CoAttachment(file.FileName, SelfPlayer);
            var ruling = new CoAOpinion()
            {
                Short = desc,
                Submitter = SelfPlayer,
                Attachment = attachment
            };
            hearing.Holding = desc;
            hearing.Opinion = ruling;
            hearing.Concluded = DateTime.Now;
            ruling.SetIds(hearing);

            if (!Directory.Exists(ruling.DataPath))
                Directory.CreateDirectory(ruling.DataPath);
            using var fs = new FileStream(attachment.DataPath, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            RespondRaw(LoadRedirectFile($"/chess/cases/{hearing.CaseNumber}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("POST"), PathRegex(@"\/chess\/api\/cases\/(?<n>\d{1,4})\/exhibits")]
        public void CreateNewExhibit(int n, string _ = null)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.getRelationToCase(SelfPlayer) == "Outside")
            {
                RespondRaw("You are not related to this case.", 403);
                return;
            }
            var file = Context.Files.FirstOrDefault();
            if (file == null)
            {
                RespondRaw("No file", 400);
                return;
            }
            var ext = file.FileName.Split('.')[^1];
            if (!isPermittedExtension(ext))
            {
                RespondRaw("Extension is not permitted", 400);
                return;
            }
            var attachment = new CoAttachment(file.FileName, SelfPlayer);
            string folder = Path.Combine(hearing.DataPath, "exhibits");
            attachment.SetIds(folder, hearing.Exhibits.Count);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            using var fs = new FileStream(attachment.DataPath, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            hearing.Exhibits.Add(attachment);
            RespondRaw(LoadRedirectFile($"/chess/cases/{hearing.CaseNumber}/exhibits/{hearing.Exhibits.Count - 1}"));
        }

        [Method("POST"), PathRegex(@"\/chess\/api\/cases\/(?<n>\d{1,4})\/motions\/(?<mn>\d{1,2})\/files")]
        public void AttachFileToMotion(int n, int mn, string notneeded = "") // string is to force body to be parsed, since it isn't until an argument is needed that isn't in query or regex.
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if(hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            var motion = hearing.Motions.ElementAtOrDefault(mn);
            if(motion == null)
            {
                RespondRaw("Unknown motion", 404);
                return;
            }
            if(motion.Denied || motion.Granted)
            { // some outcome
                RespondRaw("Motion has already been ruled on", 400);
                return;
            }
            var file = Context.Files.FirstOrDefault();
            if(file == null)
            {
                RespondRaw("No file", 400);
                return;
            }
            var ext = file.FileName.Split('.')[^1];
            if(!isPermittedExtension(ext))
            {
                RespondRaw("Extension must be .txt, .pdf or .md", 400);
                return;
            }
            string fName = $"{(motion.Attachments.Count + 1):00}_{file.FileName}";
            var attachment = new CoAttachment(fName, SelfPlayer);
            motion.Attachments.Add(attachment);
            motion.SetIds(hearing);

            if (!Directory.Exists(motion.DataPath))
                Directory.CreateDirectory(motion.DataPath);
            using var fs = new FileStream(attachment.DataPath, FileMode.Create, FileAccess.Write);
            file.Data.CopyTo(fs);
            RespondRaw(LoadRedirectFile($"/chess/cases/{hearing.CaseNumber}/motions/{hearing.Motions.IndexOf(motion)}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("PATCH"), PathRegex(@"\/chess\/api\/cases\/(?<n>\d{1,4})\/motions\/(?<mn>\d{1,2})\/holding")]
        public void SetMotionOutcome(int n, int mn)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            var motion = hearing.Motions.ElementAtOrDefault(mn);
            if (motion == null)
            {
                RespondRaw("Unknown motion", 404);
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
            RespondRaw("");
        }

        [Method("POST"), PathRegex(@"\/chess\/api\/cases\/(?<n>\d{1,4})\/motions(?!\/)")]
        public void CreateNewMotion(int n, string types)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if(hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if(hearing.Holding != null)
            {
                RespondRaw("Petition has concluded, motions cannot be added", 400);
                return;
            }

            var motionType = typeof(Motions).GetFields().FirstOrDefault(x => x.Name == types);
            if(motionType == null)
            {
                RespondRaw("Unknown motion type", 400);
                return;
            }
            string name = (string)motionType.GetValue(null);
            if(name == Motions.WritOfCertiorari)
            {
                RespondRaw("Motion for writ of cert. can only be made once - automatically when petition is filed.");
                return;
            }
            var motion = new CoAMotion()
            {
                MotionType = name,
                Filed = DateTime.Now,
                Movant = SelfPlayer
            };
            motion.SetIds(hearing);
            hearing.Motions.Add(motion);
            ChesssInstance.OnSave();
            RespondRaw(LoadRedirectFile($"/chess/cases/{n}/motions/{hearing.Motions.Count - 1}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("POST"), PathRegex(@"\/chess\/api\/cases\/(?<n>\d{1,4})\/witnesses(?!\/)")]
        public void CreateNewWitness(int n, int id)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if (hearing == null)
            {
                RespondRaw("Unknown hearing", 404);
                return;
            }
            if (hearing.Holding != null)
            {
                RespondRaw("Petition has concluded, motions cannot be added", 400);
                return;
            }
            if(!hearing.CanCallWitness(SelfPlayer))
            {
                RespondRaw("You cannot call witnesses for this petition.", System.Net.HttpStatusCode.Forbidden);
                return;
            }
            var player = ChessService.Players.FirstOrDefault(x => x.Id == id);
            if(player == null)
            {
                RespondRaw("Unknown player", 404);
                return;
            }
            var existing = hearing.Witnesses.FirstOrDefault(x => x.Witness.Id == id);
            if(existing != null)
            {
                RespondRaw(LoadRedirectFile($"/chess/cases/{hearing.CaseNumber}/witnesses/{id}"), System.Net.HttpStatusCode.Conflict);
                return;
            }
            var witness = new CoAWitness(player);
            hearing.Witnesses.Add(witness);
            witness.SetIds(hearing);
            RespondRaw(LoadRedirectFile($"/chess/cases/{hearing.CaseNumber}/witnesses/{id}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("PUT"), PathRegex(@"\/chess\/api\/appeal\/(?<n>\d{1,4})")]
        public void SubmitAppeal(int n)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == n);
            if(hearing == null)
            {
                RespondRaw("Error: No hearing by that case number.", 404);
                return;
            }
            if(hearing.AppealOf.HasValue)
            {
                RespondRaw("Error: Cases that are appeals cannot be appealed; judgement is final.");
                return;
            }
            if(!hearing.IsArbiterCase)
            {
                RespondRaw("Error: Cases before the Court of Appeals cannot be appealed; judgement is final.");
                return;
            }
            if(hearing.Holding == null)
            {
                RespondRaw("Error: A judgement must be delivered in the first instance before appeal. If Arbiter refuses to issue, file petition against Arbiter themselves.");
                return;
            }
            var relation = hearing.getRelationToCase(SelfPlayer);
            if(relation != "Claimant" && relation != "Respondent")
            {
                RespondRaw("Error: only claimants or respondents may appeal.");
                return;
            }
            List<ChessPlayer> apellees;
            List<ChessPlayer> apellants;
            if(relation == "Claimant")
            {
                apellees = new List<ChessPlayer>() { SelfPlayer };
                apellees.AddRange(hearing.Claimants.Where(x => x != SelfPlayer));
                apellants = hearing.Respondents;
            } else
            {
                apellees = new List<ChessPlayer>() { SelfPlayer };
                apellees.AddRange(hearing.Respondents.Where(x => x != SelfPlayer));
                apellants = hearing.Claimants;
            }
            var appeal = new AppealHearing(apellees, apellants);
            appeal.AppealOf = hearing.CaseNumber;
            appeal.Filed = DateTime.Now;
            var mtn = new CoAMotion()
            {
                Filed = DateTime.Now,
                MotionType = Motions.WritOfCertiorari,
                Movant = SelfPlayer,
                Attachments = new List<CoAttachment>()
            };
            appeal.Motions.Add(mtn);
            appeal.CaseNumber = CoAService.Hearings.Count + 1;
            CoAService.Hearings.Add(appeal);
            appeal.SetIds();
            RespondRaw(LoadRedirectFile($"/chess/cases/{appeal.CaseNumber}"), System.Net.HttpStatusCode.Redirect);
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

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<cn>\d{1,4})\/motions\/(?<mi>\d{1,2})\/(?<ai>\d{1,2})")]
        public void GetFileRaw(int cn, int mi, int ai)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == cn);
            if (hearing == null || hearing.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a hearing at this URL");
                return;
            }
            var motion = hearing.Motions.ElementAtOrDefault(mi);
            if (motion == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a motion at this URL");
                return;
            }
            var attachment = motion.Attachments.ElementAtOrDefault(ai);
            if (attachment == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find an attachment at this URL");
                return;
            }
            var ext = attachment.FileName.Split(".")[^1];
            StatusSent = 200;
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.ContentType = mimeFromExtension(ext);
            using var fs = new FileStream(attachment.DataPath, FileMode.Open, FileAccess.Read);
            fs.CopyTo(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<cn>\d{1,4})\/ruling")]
        public void GetRulingRaw(int cn)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == cn);
            if (hearing == null || hearing.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a hearing at this URL");
                return;
            }
            if (hearing.Opinion == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a ruling at this URL");
                return;
            }
            if (hearing.Opinion.Attachment == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find an attachment at this URL");
                return;
            }
            var ext = hearing.Opinion.Attachment.FileName.Split(".")[^1];
            StatusSent = 200;
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.ContentType = mimeFromExtension(ext);
            using var fs = new FileStream(hearing.Opinion.Attachment.DataPath, FileMode.Open, FileAccess.Read);
            fs.CopyTo(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
        }

        [Method("GET"), PathRegex(@"\/chess\/cases\/(?<cn>\d{1,4})\/exhibit\/(?<ex>\d{1,2})")]
        public void GetExhibitRaw(int cn, int ex)
        {
            var hearing = CoAService.Hearings.FirstOrDefault(x => x.CaseNumber == cn);
            if (hearing == null || hearing.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Could not find a hearing at this URL");
                return;
            }
            var exhibit = hearing.Exhibits.ElementAtOrDefault(ex);
            if(exhibit == null || exhibit.Sealed)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "", "Exhibit is either non-existent or sealed.");
                return;
            }
            var ext = exhibit.FileName.Split(".")[^1];
            StatusSent = 200;
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.ContentType = mimeFromExtension(ext);
            using var fs = new FileStream(exhibit.DataPath, FileMode.Open, FileAccess.Read);
            fs.CopyTo(Context.HTTP.Response.OutputStream);
            Context.HTTP.Response.Close();
        }

        #endregion
    }
}
