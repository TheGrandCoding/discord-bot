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
        [ComponentInteraction("selectmoveservice:*")]
        public async Task Handle(string msgId, string[] arr)
        {
            var srv = Program.GlobalServices.GetRequiredService<SelectMoveService>();
            var id = ulong.Parse(msgId);
            var sv = srv.UserCycles.Values.FirstOrDefault(x => x.Message.Id == id);
            if (sv == null)
                return;
            var index = int.Parse(arr.First());
            sv.Index = index;
            await Context.Interaction.UpdateAsync(x =>
            {
                x.Content = "Updated!";
                x.Components = sv.GetBuilder().Build();
            });
        }
    }
}
