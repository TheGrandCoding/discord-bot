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
    public class RegisterWebhooksModule : BotModule
    {
        public SonarrWebhooksService SonarrService { get; set; }

        [Command("toggle sonarr")]
        public async Task ToggleSonarr()
        {
            var id = Context.Channel.Id;
            if(SonarrService.Channels.RemoveAll(x => x.Id == id) > 0)
            {
                await ReplyAsync("Removed.");
            } else
            {
                SonarrService.Channels.Add(Context.Channel as ITextChannel);
                await ReplyAsync("Added.");
            }
            SonarrService.OnSave();
        }

        [Command("list sonarr")]
        public async Task ListSonarr()
        {
            var s = "Channels:";
            foreach(var chnl in SonarrService.Channels.Where(x => x.GuildId == Context.Guild.Id))
            {
                s += $"\r\n {chnl.Id}, {MentionUtils.MentionChannel(chnl.Id)}";
            }
            await ReplyAsync(s);
        }
    }
}
