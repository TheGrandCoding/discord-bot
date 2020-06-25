using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Classes.Voting;
using DiscordBot.MLAPI.Exceptions;
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

        public override void BeforeExecute()
        {
            base.BeforeExecute();
            var adminFlags = UserFlags.Helper | UserFlags.Sysop;
            var result = adminFlags & Account.Flags;
            if(result == 0)
            {
                ReplyFile("nopermission.html", System.Net.HttpStatusCode.Forbidden);
                throw new HaltExecutionException("Forbidden");
            }
        }

        #region HTML Endpoints

        [Method("GET"), Path("/vote/admin")]
        public void AdminBase()
        {
            ReplyFile("admin/base.html", 200);
        }

        [Method("GET"), Path("/vote/admin/categories")]
        public void ViewCategories()
        {
            var table = new Table()
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
            var categories = Service.Database.GetCollection<Category>("categories");
            foreach (var c in categories.AsQueryable())
            {
                table.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableData(c.Number.ToString()),
                        getData(c)
                    }
                });
            }
            ReplyFile("admin/categories.html", 200, new Replacements()
                .Add("table", table));
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

        [Method("POST"), Path("/vote/api/admin/categories")]
        public void AddOrAmendCategory(int id, string prompt)
        {
            var table = Service.Database.GetCollection<Category>("categories");
            if(id == -1)
            {
                // inserting new category.
                var num = (int)table.CountDocuments(FilterDefinition<Category>.Empty);
                var newCat = new Category()
                {
                    Number = num + 1,
                    Prompt = prompt,
                    Votes = new Dictionary<string, List<string>>()
                };
                table.InsertOne(newCat);
            } else
            {
                // amending existing category
                var filter = Builders<Category>.Filter.Eq(x => x.Number, id);
                var setter = Builders<Category>.Update.Set(x => x.Prompt, prompt);
                table.UpdateOne(filter, setter);
            }
            RespondRaw(LoadRedirectFile("/vote/admin/categories"), System.Net.HttpStatusCode.Redirect);
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
