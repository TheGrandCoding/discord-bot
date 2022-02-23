using Discord.Interactions;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Components
{
    public class LinksModule : BotComponentBase
    {
        public LinksThreaderService Service { get; set; }
        [ComponentInteraction("links:set:*")]
        public async Task SetChannelComponent(string _channelId)
        {
            if (!ulong.TryParse(_channelId, out var channelId)) return;
            await Context.Interaction.DeferAsync();


            int value = 0;
            foreach (var str in Context.Interaction.Data.Values)
                value |= int.Parse(str);
            if (value == 0)
            {
                Service.Channels.TryRemove(channelId, out _);
                await Context.Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Content = "Removed!";
                });
                return;
            }
            if (!Service.Channels.TryGetValue(channelId, out var sv))
            {
                sv = new LinksThreaderService.ChannelConfiguration();
                Service.Channels[channelId] = sv;
            }
            sv.Flags = (ChannelFlags)value;

            await Context.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = $"Set!";
                x.Components = Modules.Links.getBuilder(channelId).Build();
            });

            Service.OnSave();
        }
    }
}
