using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Bot
{
    public class Users : AuthedAPIBase
    {
        public Users(APIContext context) : base(context, "bot")
        {
        }

        [Method("GET"), Path("/bot/approve")]
        [RequirePermNode(Perms.Bot.ApproveUser)]
        public void ApproveList()
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
                        .WithHeader("Delete")
                }
            };
            foreach(var bUser in Context.BotDB.Users.Where(x => x.Approved.HasValue == false))
            {
                var namebld = new StringBuilder();
                namebld.Append(bUser.Name);
                if(bUser.Connections.Discord != null)
                {
                    namebld.Append($"#{bUser.Connections.Discord.Discriminator}");
                }

                table.Children.Add(new TableRow()
                {
                    Children =
                    {
                        new TableData(namebld.ToString()),
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
                        },
                        new TableData("")
                        {
                            Children =
                            {
                                new Input("button", "Delete")
                                {
                                    OnClick = $"rmusr('{bUser.Id}')"
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
        [RequirePermNode(Perms.Bot.ApproveUser)]
        public void Set(uint id, bool approved)
        {
            var bUser = Context.BotDB.GetUserAsync(id).Result;
            if(bUser == null)
            {
                RespondRaw("No user.", 404);
                return;
            }
            bUser.Approved = approved;
            RespondRaw("OK.");
        }

        [Method("DELETE"), Path("/api/bot/user")]
        [RequirePermNode(Perms.Bot.All)]
        public void Delete(uint id)
        {
            var bUser = Context.BotDB.GetUserAsync(id).Result;
            if (bUser == null)
            {
                RespondRaw("No user.", 404);
                return;
            }
            if(bUser.Approved.GetValueOrDefault(false))
            {
                RespondRaw("Cannot delete accounts that have been approved.", 400);
                return;
            }
            Context.BotDB.DeleteUserAsync(bUser).Wait();
            RespondRaw("OK");
        }
    }
}
