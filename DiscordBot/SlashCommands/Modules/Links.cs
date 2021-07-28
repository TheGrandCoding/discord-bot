using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("links", "Automatic thread creation for links")]
    [DefaultDisabled]
    public class Links : BotSlashBase
    {
        public LinksThreaderService Service { get; set; }
        [SlashCommand("set", "Adds or configures auto-thread for news links, and summary")]
        public async Task SetChannel([Required]SocketGuildChannel textchannel, bool summarize)
        {
            Service.Channels[textchannel.Id] = summarize ? ChannelConfig.Summary : ChannelConfig.Thread;
            await Interaction.RespondAsync($"Done!", embeds: null, ephemeral: true);
            Service.OnSave();
        }

        [SlashCommand("remove")]
        public async Task RemoveChannel([Required]SocketGuildChannel textchannel)
        {
            Service.Channels.TryRemove(textchannel.Id, out _);
            await Interaction.RespondAsync($"Done!", embeds: null, ephemeral: true);
            Service.OnSave();
        }
    }
}
