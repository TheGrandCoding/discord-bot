using Discord;
using Discord.Commands;
using DiscordBot.Services.Radarr;
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
                if (chnl.TagRequired.Count > 0)
                    s += " [" + string.Join(',', chnl.TagRequired) + "]";
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
        public async Task TagsSonarr(string tags)
        {
            string[] ls;
            if (tags == "none" || tags == "[]")
                ls = new string[0];
            else
                ls = tags.Split(',');
            var chnl = SonarrService.Channels.FirstOrDefault(x => x.Channel.Id == Context.Channel.Id);
            if (chnl == null)
            {
                await ReplyAsync("No webhook for current channel!");
            }
            else
            {
                chnl.TagRequired = ls.ToList();
                await ReplyAsync("Channel requires any of these tags to be present: " + string.Join(',', tags));
                SonarrService.OnSave();
            }
        }


        public RadarrWebhookService RadarrService { get; set; }

        [Command("toggle radarr")]
        public async Task ToggleRadarr()
        {
            var id = Context.Channel.Id;
            if (RadarrService.Channels.RemoveAll(x => x.Channel.Id == id) > 0)
            {
                await ReplyAsync("Removed.");
            }
            else
            {
                var sv = new SaveChannel()
                {
                    Channel = Context.Channel as ITextChannel,
                    ShowsPrivate = false
                };
                RadarrService.Channels.Add(sv);
                await ReplyAsync("Added.");
            }
            RadarrService.OnSave();
        }

        [Command("list radarr")]
        public async Task ListRadarr()
        {
            var s = "Channels:";
            foreach (var chnl in RadarrService.Channels.Where(x => x.Channel.GuildId == Context.Guild.Id))
            {
                s += $"\r\n {chnl.Channel.Id}, {MentionUtils.MentionChannel(chnl.Channel.Id)}";
                if (chnl.ShowsPrivate)
                    s += " (private)";
                if (chnl.TagRequired.Count > 0)
                    s += " [" + string.Join(',', chnl.TagRequired) + "]";
            }
            await ReplyAsync(s);
        }

        [Command("private radarr")]
        [RequireOwner]
        public async Task PrivateRadarr()
        {
            var chnl = RadarrService.Channels.FirstOrDefault(x => x.Channel.Id == Context.Channel.Id);
            if (chnl == null)
            {
                await ReplyAsync("No webhook for current channel!");
            }
            else
            {
                chnl.ShowsPrivate = !chnl.ShowsPrivate;
                await ReplyAsync("Channel showing private shows now: " + chnl.ShowsPrivate.ToString());
                RadarrService.OnSave();
            }
        }

        [Command("tags radarr")]
        [RequireOwner]
        public async Task TagsRadarr(string tags)
        {
            string[] ls;
            if (tags == "none" || tags == "[]")
                ls = new string[0];
            else
                ls = tags.Split(',');
            var chnl = RadarrService.Channels.FirstOrDefault(x => x.Channel.Id == Context.Channel.Id);
            if (chnl == null)
            {
                await ReplyAsync("No webhook for current channel!");
            }
            else
            {
                chnl.TagRequired = ls.ToList();
                await ReplyAsync("Channel requires any of these tags to be present: " + string.Join(',', tags));
                RadarrService.OnSave();
            }
        }

    }
}
