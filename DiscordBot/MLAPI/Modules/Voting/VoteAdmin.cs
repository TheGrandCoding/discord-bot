using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.Voting;
using DiscordBot.MLAPI.Exceptions;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Voting
{
    public class VoteAdmin : VoteBase
    {
        public VoteAdmin(APIContext context) : base(context) { }

        void throwIfNoPerms(UserFlags flags, Election election)
        {
            bool hasPerms = (Account.Flags & (flags)) != 0;
            if(!hasPerms && election != null)
            {
                var tb = Service.Database.GetCollection<Election>("elections");
                var first = tb.Find(x => x.Id == election.Id).FirstOrDefault();
                if(first.LocalFlags.TryGetValue(Account.UserName, out var f))
                {
                    hasPerms = (f & (flags)) == 0;
                }
            }
            if(!hasPerms)
            {
                ReplyFile("nopermission.html", System.Net.HttpStatusCode.Forbidden);
                throw new HaltExecutionException("Forbidden");
            }
        }

        #region HTML Endpoints

        [Method("GET"), Path("/vote/admin")]
        public void AdminBase()
        {
            var ul = new UnorderedList();
            bool isGlobal = (Account.Flags & (UserFlags.Helper | UserFlags.Sysop)) != 0;
            if(isGlobal) 
            {
                ul.AddItem(new Anchor("/vote/admin/users", "View, add or change user information"));
                ul.AddItem(new Anchor("/vote/admin/bulk", "Add users from a CSV format"));
                if(Account.Flags.HasFlag(UserFlags.Sysop))
                {
                    ul.AddItem(new Anchor("/vote/admin/new", "Add new vote"));
                }
            }
            var tb = Service.Database.GetCollection<Election>("elections");
            var all = tb.Find(FilterDefinition<Election>.Empty).ToList();
            foreach(var election in all)
            {
                bool hasPerms = false;
                try
                {
                    throwIfNoPerms(UserFlags.Helper | UserFlags.Sysop, election);
                    hasPerms = true;
                }
                catch (HaltExecutionException) { }
                if(hasPerms)
                {
                    ul.AddItem(new Anchor($"/vote/admin/{election.Id}", $"Administrate {election.Title}"));
                }
            }
            ReplyFile("admin/base.html", 200,
                new Replacements()
                .Add("list", ul));
        }

        [Method("GET"), Path("/vote/admin/new")]
        public void CreateNewElection()
        {
            throwIfNoPerms(UserFlags.Sysop, null);
            ReplyFile("admin/new.html", 200);
        }

        [Method("GET"), PathRegex(@"\/vote\/admin\/(?<id>[a-z0-9]{24})")]
        public void ViewCategories(string id)
        {
            if(!ObjectId.TryParse(id, out var eId))
            {
                HTTPError(System.Net.HttpStatusCode.BadRequest, "URL malformed", "Could not parse ID from URL");
                return;
            }
            var catTable = new Table()
            {
                Children =
                {
                    new TableRow()
                    {
                        Children =
                        {
                            new TableHeader("Id"),
                            new TableHeader("Prompt")
                        }
                    }
                }
            };
            Func<Category, TableData> getData = category =>
            {
                var parent = new TableData("");
                parent.Children.Add(new Label(category.Prompt, cls: "cat-prompt"));
                parent.Children.Add(new Input("button", value:"Edit", cls: "cat-inp")
                {
                    OnClick = "editCategory(this);"
                });
                return parent;
            };

            var election = Service.Elections.Find(x => x.Id == eId).FirstOrDefault();
            if(election == null)
            {
                HTTPError(System.Net.HttpStatusCode.NotFound, "Election", "Could not find election by that Id");
                return;
            }

            foreach (var c in election.Categories)
            {
                catTable.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableData(c.Key.ToString()),
                        getData(c.Value)
                    }
                });
            }

            var userTable = new Table()
            {
                Children =
                {
                    new TableRow()
                    {
                        Children =
                        {
                            new TableHeader("User"),
                            new TableHeader("Local Flags"),
                            new TableHeader("Global Flags")
                        }
                    }
                }
            };

            foreach(var keypair in election.LocalFlags)
            {
                var user = Service.Users.Find(x => x.UserName == keypair.Key).FirstOrDefault();
                var row = new TableRow(id: keypair.Key);
                if(user == null)
                {
                    row.Children.Add(new TableData($"[Unknown:{keypair.Key}]"));
                } else
                {
                    row.Children.Add(new TableData(user.FullName));
                }
                row.Children.Add(
                    new TableData($"<strong>{(int)keypair.Value}:</strong> {keypair.Value}"));
                row.Children.Add(
                    new TableData(
                        user == null
                        ? $"N/A"
                        : $"<strong>{(int)user.Flags}:</strong> {user.Flags}"
                        ));
                userTable.Children.Add(row);
            }

            ReplyFile("admin/info.html", 200, new Replacements()
                .Add(nameof(catTable), catTable)
                .Add(nameof(userTable), userTable)
                .Add(nameof(election), election));
        }
        
        [Method("GET"), Path("/vote/admin/users")]
        public void ViewUsers()
        { // TODOish: maybe add a limit, then use JS to request batches more?
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                    {
                        Children =
                        {
                            new TableHeader("UserName"),
                            new TableHeader("First Name"),
                            new TableHeader("Last Name"),
                            new TableHeader("Tutor"),
                            new TableHeader("Type"),
                            new TableHeader("Edit")
                        }
                    }
                }
            };
            var db = Service.Database.GetCollection<User>("users");
            foreach(var user in db.AsQueryable())
            {
                var row = new TableRow(id:user.UserName);
                row.Children.Add(new TableData(user.UserName));
                row.Children.Add(new TableData(user.FirstName));
                row.Children.Add(new TableData(user.LastName));
                row.Children.Add(new TableData(user.Tutor));
                row.Children.Add(new TableData($"{user.Flags}")
                    .WithTag("value", $"{(int)user.Flags}"));
                row.Children.Add(new TableData(null)
                {
                    Children =
                    {
                        new Input("button", "Edit")
                        {
                            OnClick = "editUser(this);"
                        }
                    }
                });
                table.Children.Add(row);
            }

            string inputs = "";
            foreach(UserFlags value in Enum.GetValues(typeof(UserFlags)))
            {
                if(value != UserFlags.None)
                {
                    bool canModify = true;
                    if (value == UserFlags.Sysop)
                        canModify = false;
                    else if (value == UserFlags.Helper && Account.Flags.HasFlag(UserFlags.Sysop) == false)
                        canModify = false;
                    inputs += $"<span><input type='checkbox' {(canModify ? "" : "disabled")} onclick='toggleFlag(this);' class='flag' id='flag-{value}' value='{(int)value}'><label> {value}</label></span><br/>";
                }
            }

            ReplyFile("admin/users.html", 200, new Replacements()
                .Add("table", table)
                .Add("flagboxes", inputs));
        }

        #endregion

        #region API Endpoints

        [Method("POST"), Path("/vote/api/admin/new")]
        public void NewElection(string title, string sysop, string helpers)
        {
            var election = new Election()
            {
                Title = title,
                CanVote = UserFlags.Student,
                CanBeVoted = UserFlags.Student,
                Categories = new Dictionary<string, Category>(),
                Id = ObjectId.GenerateNewId(),
                LocalFlags = new Dictionary<string, UserFlags>(),
            };
            if(!string.IsNullOrWhiteSpace(sysop))
            {
                var usr = Service.Users.Find(Builders<User>.Filter.Eq(x => x.UserName, sysop)).FirstOrDefault();
                if(usr != null)
                {
                    election.LocalFlags[usr.UserName] = UserFlags.Sysop;
                }
            }
            if(!string.IsNullOrWhiteSpace(helpers))
            {
                var split = helpers.Split(',', '.', ' ', '-');
                foreach(var text in split)
                {
                    var usr = Service.Users.Find(Builders<User>.Filter.Eq(x => x.UserName, text)).FirstOrDefault();
                    if (usr != null)
                    {
                        election.LocalFlags[usr.UserName] = UserFlags.Helper;
                    }
                }
            }
            Service.Elections.InsertOne(election);
            RespondRaw(LoadRedirectFile($"/vote/admin/{election.Id}"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("POST"), PathRegex(@"\/vote\/api\/admin\/(?<id>[a-z0-9]{24})\/categories")]
        public void AddOrAmendCategory(string id, int number, string prompt)
        {
            if(!ObjectId.TryParse(id, out var eId))
            {
                RespondRaw(LoadRedirectFile("/vote/admin"), 302);
                return;
            }
            var election = Service.Elections.Find(x => x.Id == eId).FirstOrDefault();
            if(election == null)
            {
                RespondRaw(LoadRedirectFile("/vote/admin"), 302);
                return;
            }
            if(number == -1)
            {
                // inserting new category.
                var num = election.Categories.Count;
                var newCat = new Category()
                {
                    Number = num + 1,
                    Prompt = prompt,
                    Votes = new Dictionary<string, List<string>>()
                };
                election.Categories.Add(newCat.Number.ToString(), newCat);
            } else
            {
                // amending existing category
                if (election.Categories.TryGetValue(number.ToString(), out var c))
                    c.Prompt = prompt;
            }
            var filter = Builders<Election>.Filter.Eq(x => x.Id, eId);
            Service.Elections.ReplaceOne(filter, election);
            RespondRaw(LoadRedirectFile($"/vote/admin/{eId}/categories"), System.Net.HttpStatusCode.Redirect);
        }

        [Method("POST"), Path("/vote/api/admin/users")]
        public void AddOrUpdate(string id, string username, string first, string last, string tutor, int flags)
        {
            var userFlags = (UserFlags)flags;
            var table = Service.Database.GetCollection<User>("users");
            var filter = Builders<User>.Filter.Eq(x => x.UserName, id);
            var existing = table.Find(filter).FirstOrDefault();
            if(userFlags.HasFlag(UserFlags.Sysop) || (existing?.Flags.HasFlag(UserFlags.Sysop) ?? false))
            {
                RespondRaw("Forbidden: Cannot modify flag Sysop", System.Net.HttpStatusCode.Forbidden);
                return;
            }
            if((userFlags.HasFlag(UserFlags.Staff) || (existing?.Flags.HasFlag(UserFlags.Staff) ?? false)) && !Account.Flags.HasFlag(UserFlags.Sysop))
            {
                RespondRaw("Forbidden: You cannot modify flag Staff", System.Net.HttpStatusCode.Forbidden);
                return;
            }
            var usr = new User()
            {
                UserName = username,
                FirstName = first,
                LastName = last,
                Tutor = tutor,
                Flags = userFlags
            };
            table.ReplaceOne(filter, usr);
            RespondRaw(LoadRedirectFile("/vote/admin/users#" + usr.UserName), System.Net.HttpStatusCode.Redirect);
        }

        #endregion
    }
}
