using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("links", "Automatic thread creation for links")]
    [DefaultPermission(false)]
    public class Links : BotSlashBase
    {
        public LinksThreaderService Service { get; set; }
        public MessageComponentService CompService { get; set; }


        public static ComponentBuilder getBuilder(ulong channelId)
        {
            var srv = Program.Services.GetRequiredService<LinksThreaderService>();
            ChannelFlags existing = ChannelFlags.Disabled;
            if (srv.Channels.TryGetValue(channelId, out var sv))
                existing = sv.Flags;
            return new ComponentBuilder()
                .WithSelectMenu(new SelectMenuBuilder()
                    .WithCustomId($"links:set:{channelId}")
                    .AddOption("None", "0", "If only this is set, remove the channel config", isDefault: existing == ChannelFlags.Disabled)
                    .AddOption("Summarize", $"{(int)ChannelFlags.Summary}", "Summarize the link; responds in thread or as reply", isDefault: existing.HasFlag(ChannelFlags.Summary))
                    .AddOption("Thread", $"{(int)ChannelFlags.Thread}", "Creates a thread for the link, name as link title", isDefault: existing.HasFlag(ChannelFlags.Thread))
                    .AddOption("Delete", $"{(int)ChannelFlags.Delete}", "Deletes any messages without a valid link", isDefault: existing.HasFlag(ChannelFlags.Delete))
                    .WithMinValues(1).WithMaxValues(3)
                );
        }

        [SlashCommand("set", "Adds or configures auto-thread, summary and deletion for news links")]
        public async Task SetChannel(SocketGuildChannel otherchannel = null)
        {
            var channel = otherchannel ?? (Context.Channel as SocketGuildChannel);
            await RespondAsync($"Please configure options for {MentionUtils.MentionChannel(channel.Id)} accordingly.", 
                components: getBuilder(channel.Id).Build(), ephemeral: true);
        }

        [SlashCommand("remove", "Removes channel from linking")]
        public async Task RemoveChannel(SocketGuildChannel textchannel)
        {
            Service.Channels.TryRemove(textchannel.Id, out _);
            await RespondAsync($"Removed!", embeds: null, ephemeral: true);
            Service.OnSave();
        }

        [Group("blacklist", "Commands to control blacklisting on links")]
        public class LinksBlacklist : BotSlashBase
        {
            public LinksThreaderService Service { get; set; }

            [SlashCommand("add", "Adds a blacklist. If regex is true, applies to whole uri; else, strictly blacklist on domain")]
            public async Task Add(SocketGuildChannel textchannel, string text, bool isRegex)
            {
                if(!Service.Channels.TryGetValue(textchannel.Id, out var sv))
                {
                    await RespondAsync($":x: Unknown channel!", ephemeral: true);
                    return;
                }
                sv.Blacklist.Add((isRegex ? "/" : "") + text);
                await RespondAsync("Done!", ephemeral: true);
            }

            [SlashCommand("remove", "Removes a blacklist")]
            public async Task Remove(SocketGuildChannel textchannel, string text)
            {
                if (!Service.Channels.TryGetValue(textchannel.Id, out var sv))
                {
                    await RespondAsync($":x: Unknown channel!", ephemeral: true);
                    return;
                }
                if(sv.Blacklist.RemoveAll(x => x == text || x == $"/{text}") > 0)
                {
                    await RespondAsync("Done!", ephemeral: true);
                } else
                {
                    await RespondAsync(":x: No blacklists matched that.", ephemeral: true);
                }
            }

            [SlashCommand("view", "Views the blacklist for this channel")]
            public async Task Add(SocketGuildChannel textchannel)
            {
                if (!Service.Channels.TryGetValue(textchannel.Id, out var sv))
                {
                    await RespondAsync($":x: Unknown channel!", ephemeral: true);
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
                await RespondAsync(embeds: new[] { embed.Build() },
                    ephemeral: true);
            }

        }
    }
}
