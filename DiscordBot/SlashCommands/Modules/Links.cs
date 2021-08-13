﻿using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("links", "Automatic thread creation for links")]
    [DefaultDisabled]
    public class Links : BotSlashBase
    {
        public LinksThreaderService Service { get; set; }
        public MessageComponentService CompService { get; set; }
        [SlashCommand("set", "Adds or configures auto-thread, summary and deletion for news links")]
        public async Task SetChannel(SocketGuildChannel otherchannel = null)
        {
            var channel = otherchannel ?? (Interaction.Channel as SocketGuildChannel);
            var regId = Interaction.User.Id.ToString() + "setChannel";
            var components = new ComponentBuilder();
            ChannelFlags existing = ChannelFlags.Disabled;
            if (Service.Channels.TryGetValue(channel.Id, out var sv))
                existing = sv.Flags;
            components.WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId(regId)
                .AddOption("None", "0", "If only this is set, remove the channel config", @default: existing == ChannelFlags.Disabled)
                .AddOption("Summarize", $"{(int)ChannelFlags.Summary}", "Attempts to summarize the link; responds in thread or as reply", @default: existing.HasFlag(ChannelFlags.Summary))
                .AddOption("Thread", $"{(int)ChannelFlags.Thread}", "Creates a thread for the link, name as link title", @default: existing.HasFlag(ChannelFlags.Thread))
                .AddOption("Delete", $"{(int)ChannelFlags.Delete}", "Deletes any messages without a valid link", @default: existing.HasFlag(ChannelFlags.Delete))
                );

            await Interaction.RespondAsync(component: components.Build());
            var msg = await Interaction.GetOriginalResponseAsync();
            CompService.Register(regId, msg, async e =>
            {
                CompService.Unregister(regId);
                await Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = $"Handled.";
                });
                var value = int.Parse(e.Interaction.Data.Values.First());
                if(value == 0)
                {
                    await RemoveChannel(channel);
                    return;
                }
                if(!Service.Channels.TryGetValue(channel.Id, out var sv))
                {
                    sv = new LinksThreaderService.ChannelConfiguration();
                    Service.Channels[channel.Id] = sv;
                }
                sv.Flags = (ChannelFlags)value;
            Service.OnSave();
            }, doSave: false);
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
