using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Components
{
    public class EchoModule : BotComponentBase
    {

        [ComponentInteraction("echo:*")]
        public async Task Echo(string id)
        {
            await Context.Interaction.UpdateAsync(x =>
            {
                x.Content = $"Clicked {Context.Interaction.Data.CustomId}";
            });
        }
    }
}
