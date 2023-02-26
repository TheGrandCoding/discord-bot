using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Bot
{
    public class Users : AuthedAPIBase
    {
        public Users(APIContext context) : base(context, "bot")
        {
        }

        [Method("GET"), Path("/bot/approve")]
        [RequirePermNode(Perms.Bot.ApproveUser)]
        public async Task ApproveList()
        {
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                        .WithHeader("Name")
                        .WithHeader("Id")
                        .WithHeader("Approve")
                        .WithHeader("Deny")
                }
            };
            foreach(var bUser in Program.Users.Where(x => x.IsApproved.HasValue == false))
            {
                table.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableData($"{bUser.Name}#{(bUser.OverrideDiscriminator ?? bUser.DiscriminatorValue)}"),
                        new TableData($"{bUser.Id}"),
                        new TableData("")
                        {
                            Children =
                            {
                                new Input("button", "Approve")
                                {
                                    OnClick = $"approve('{bUser.Id}')"
                                }
                            }
                        },
                        new TableData("")
                        {
                            Children =
                            {
                                new Input("button", "Deny")
                                {
                                    OnClick = $"deny('{bUser.Id}')"
                                }
                            }
                        }
                    }
                });
            }
            ReplyFile("approve.html", 200, new Replacements()
                .Add("table", table));
        }
   
        [Method("POST"), Path("/bot/approve")]
        public async Task Set(ulong id, bool approved)
        {
            var bUser = Program.GetUserOrDefault(id);
            if(bUser == null)
            {
                RespondRaw("No user.", 404);
                return;
            }
            bUser.IsApproved = approved;
            Program.Save();
            RespondRaw("OK.");
        }
    }
}
