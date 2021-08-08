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
    [CommandGroup("links", "Automatic thread creation for links")]
    [DefaultDisabled]
    public class Links : BotSlashBase
    {
        public LinksThreaderService Service { get; set; }
        [SlashCommand("set", "Adds or configures auto-thread for news links, and summary")]
        public async Task SetChannel([Required]SocketGuildChannel textchannel, 
            [Required]
            [Choice("Thread only, will create a thread on link", (int)ChannelFlags.Thread)]
            [Choice("Summarize only, will reply to message", (int)ChannelFlags.Summary)]
            [Choice("Thread *and* Summarize", (int)ChannelFlags.ThreadedSummary)]
            [Choice("None - this will remove config for this channel", (int)ChannelFlags.Disabled)]
            int flags)
        {
            if(flags == 0)
            {
                await RemoveChannel(textchannel);
                return;
            }
            if(!Service.Channels.TryGetValue(textchannel.Id, out var sv))
            {
                sv = new LinksThreaderService.ChannelConfiguration();
                Service.Channels[textchannel.Id] = sv;
            }
            sv.Flags = (ChannelFlags)flags;
            await Interaction.RespondAsync($"Done!", embeds: null, ephemeral: true);
            Service.OnSave();
        }

        [SlashCommand("remove")]
        public async Task RemoveChannel([Required]SocketGuildChannel textchannel)
        {
            Service.Channels.TryRemove(textchannel.Id, out _);
            await Interaction.RespondAsync($"Removed!", embeds: null, ephemeral: true);
            Service.OnSave();
        }

        [CommandGroup("blacklist", "Commands to control blacklisting on links")]
        public class LinksBlacklist : BotSlashBase
        {
            public LinksThreaderService Service { get; set; }

            [SlashCommand("add", "Adds a blacklist. If regex is true, applies to whole uri; else, strictly blacklist on domain")]
            public async Task Add([Required]SocketGuildChannel textchannel, [Required]string text, [Required]bool isregex)
            {
                if(!Service.Channels.TryGetValue(textchannel.Id, out var sv))
                {
                    await Interaction.RespondAsync($":x: Unknown channel!", ephemeral: true);
                    return;
                }
                sv.Blacklist.Add((isregex ? "/" : "") + text);
                await Interaction.RespondAsync("Done!", ephemeral: true);
            }

            [SlashCommand("Remove", "Removes a blacklist")]
            public async Task Remove([Required] SocketGuildChannel textchannel, [Required] string text)
            {
                if (!Service.Channels.TryGetValue(textchannel.Id, out var sv))
                {
                    await Interaction.RespondAsync($":x: Unknown channel!", ephemeral: true);
                    return;
                }
                if(sv.Blacklist.RemoveAll(x => x == text || x == $"/{text}") > 0)
                {
                    await Interaction.RespondAsync("Done!", ephemeral: true);
                } else
                {
                    await Interaction.RespondAsync(":x: No blacklists matched that.", ephemeral: true);
                }
            }

            [SlashCommand("view", "Views the blacklist for this channel")]
            public async Task Add([Required] SocketGuildChannel textchannel)
            {
                if (!Service.Channels.TryGetValue(textchannel.Id, out var sv))
                {
                    await Interaction.RespondAsync($":x: Unknown channel!", ephemeral: true);
                    return;
                }
                var embed = new EmbedBuilder();
                embed.Title = $"{textchannel.Name} blacklist";
                embed.Description = "Those marked with `*` are regex and will match the entire URI. The rest will strictly match on the host/domain\r\n";
                foreach(var blck in sv.Blacklist)
                {
                    if(blck.StartsWith("/"))
                    {
                        embed.Description += $"`{blck[1..]}`*" + "\r\n";
                    }
                    else
                    {
                        embed.Description += $"`{blck}`" + "\r\n";
                    }
                }
                await Interaction.RespondAsync(embeds: new[] { embed.Build() },
                    ephemeral: true);
            }

        }
    }
}
