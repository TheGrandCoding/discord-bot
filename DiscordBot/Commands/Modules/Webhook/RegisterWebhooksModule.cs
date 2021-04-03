using Discord;
using Discord.Commands;
using DiscordBot.Services.Sonarr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Webhook
{
    [RequireContext(ContextType.Guild)]
    [Name("Webhooks")]
    [RequireUserPermission(Discord.ChannelPermission.ManageMessages)]
    [Group("webhooks"), Alias("webhook")]
    public class RegisterWebhooksModule : BotBase
    {
        public SonarrWebhooksService SonarrService { get; set; }

        [Command("toggle sonarr")]
        public async Task ToggleSonarr()
        {
            var id = Context.Channel.Id;
            if(SonarrService.Channels.RemoveAll(x => x.Channel.Id == id) > 0)
            {
                await ReplyAsync("Removed.");
            } else
            {
                var sv = new SaveChannel()
                {
                    Channel = Context.Channel as ITextChannel,
                    ShowsPrivate = false
                };
                SonarrService.Channels.Add(sv);
                await ReplyAsync("Added.");
            }
            SonarrService.OnSave();
        }

        [Command("list sonarr")]
        public async Task ListSonarr()
        {
            var s = "Channels:";
            foreach(var chnl in SonarrService.Channels.Where(x => x.Channel.GuildId == Context.Guild.Id))
            {
                s += $"\r\n {chnl.Channel.Id}, {MentionUtils.MentionChannel(chnl.Channel.Id)}";
                if (chnl.ShowsPrivate)
                    s += " (private)";
            }
            await ReplyAsync(s);
        }

        [Command("private sonarr")]
        [RequireOwner]
        public async Task PrivateSonarr()
        {
            var chnl = SonarrService.Channels.FirstOrDefault(x => x.Channel.Id == Context.Channel.Id);
            if(chnl == null)
            {
                await ReplyAsync("No webhook for current channel!");
            } else
            {
                chnl.ShowsPrivate = !chnl.ShowsPrivate;
                await ReplyAsync("Channel showing private shows now: " + chnl.ShowsPrivate.ToString());
                SonarrService.OnSave();
            }
        }

        [Command("tags sonarr")]
        [RequireOwner]
        public async Task TagsSonarr(List<string> tags)
        {
            var chnl = SonarrService.Channels.FirstOrDefault(x => x.Channel.Id == Context.Channel.Id);
            if (chnl == null)
            {
                await ReplyAsync("No webhook for current channel!");
            }
            else
            {
                chnl.TagRequired = tags;
                await ReplyAsync("Channel requires any of these tags to be present: " + string.Join(',', tags));
                SonarrService.OnSave();
            }
        }
    }
}
