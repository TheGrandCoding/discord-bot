using Discord;
using Discord.WebSocket;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class BanAppeals : APIBase
    {
        public BanAppeals(APIContext context) : base(context, "appeals")
        {
            Service = Program.Services.GetRequiredService<BanAppealsService>();
        }

        public BanAppealsService Service { get; set; }

        List<SocketGuild> getAppealableGuilds()
        {
            var ls = new List<SocketGuild>();
            foreach (var guild in Program.Client.Guilds)
            {
                var appeal = Service.GetAppeal(guild, Context.User.Id);
                if (appeal != null || (guild.GetBanAsync(Context.User.Id).Result != null))
                {
                    ls.Add(guild);
                }
            }
            return ls;
        }

        string getGuildsSidebar()
        {
            string txt = "";
            foreach (var guild in getAppealableGuilds())
            {
                txt += $"<div class='guild-entry-item'>" +
                    $"<a href='/bans/{guild.Id}' alt='{guild.Name}'>" +
                    $"<img style='border-radius: 50%;' src='{guild.IconUrl}' alt='{guild.Name}' width='48' height='48'>" +
                    $"</a>" +
                    $"</div>";
            }
            return txt;
        }


        [Method("GET"), Path("/bans")]
        public void Base()
        {
            var table = new Table();
            table.Children.Add(new TableRow()
                .WithHeader("Guild")
                .WithHeader("Appeal"));
            foreach(var guild in getAppealableGuilds())
            {
                table.Children.Add(
                    new TableRow()
                    {
                        Children =
                        {
                            new TableData(guild.Name),
                            new TableData(null)
                            {
                                Children =
                                {
                                    new Anchor($"/bans/{guild.Id}", $"View or submit appeal")
                                }
                            }
                        }
                    }
                );
            }
            ReplyFile("base.html", 200, new Replacements()
                .Add("table", table));
        }

        [Method("GET"), Path("/bans/{guildId}")]
        [Regex("guildId", @"[0-9]{17,18}")]
        public void ViewAppeal(ulong guildId)
        {
            var guild = Program.Client.GetGuild(guildId);
            var appeal = Service.GetAppeal(guild, Context.User.Id);
            if(appeal == null)
            {
                var ban = guild.GetBanAsync(Context.User.Id).Result;
                if(ban == null)
                {
                    RespondRaw($"Unknown server", 404);
                    return;
                }
                appeal = Service.CreateAppeal(guild, Context.User).Result;
            }
            ReplyFile("appeal.html", 200, new Replacements()
                .Add("guild", guild)
                .Add("appeal", appeal)
                .Add("guilds", getGuildsSidebar())
                );
        }
    }
}
