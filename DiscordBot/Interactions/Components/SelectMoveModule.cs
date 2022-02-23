using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Interactions;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions
{
    public class SelectMoveModule : InteractionModuleBase<SocketInteractionContext<SocketMessageComponent>>
    {
        [ComponentInteraction("slcmv:*")]
        public async Task Handle(string usrId)
        {
            var srv = Program.Services.GetRequiredService<SelectMoveService>();
            if (!srv.UserCycles.TryGetValue(ulong.Parse(usrId), out var sv))
                return;
            var index = int.Parse(Context.Interaction.Data.Values.First());
            sv.Index = index;
            await Context.Interaction.UpdateAsync(x =>
            {
                x.Content = "Updated!";
                x.Components = sv.GetBuilder().Build();
            });
        }
    }
}
