using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("vc", "Commands for voice channels")]
    public class VoiceCommands : BotSlashBase
    {
        public VCTextService TextService { get; set; }

        [SlashCommand("pair", "Threads will be made in the context channel.")]
        public async Task PairWith([Required]SocketGuildChannel voice)
        {
            if(!(voice is SocketVoiceChannel vc)) {
                await Interaction.RespondAsync(":x: You must select a voice channel, " +
                    "which will be paired with the channel you execute this command in",
                    ephemeral: true);
                return;
            }
            TextService.PairedChannels[vc] = Interaction.Channel as ITextChannel;
            await Interaction.RespondAsync($"Done!",
                ephemeral: true);
        }
    }
}
