using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DiscordBot.MLAPI.Modules.Bot.Internal;

namespace DiscordBot.Interactions.Components
{
    public class InternalModule : BotComponentBase
    {
        [ComponentInteraction("internal:shutdown")]
        public async Task setShutdownState()
        {
            shutdownState = (ShutdownState)int.Parse(Context.Interaction.Data.Values.First());
            await Context.Interaction.UpdateAsync(r =>
            {
                r.Components = getShutdownComponents().Build();
                r.Content = $"Now in state ${shutdownState}";
            });
        }

        [ComponentInteraction("internal:app:*:*")]
        public async Task approval(string uId, string v)
        {
            var result = bool.Parse(v);
            var user = Program.GetUserOrDefault(ulong.Parse(uId));
            if (!MLAPI.Handler.holding.TryGetValue(user.Id, out var info))
                return;
            if (result)
            {
                info.s.Approved = true;
                user.ApprovedIPs.Add(info.ip);
            }
            else
            {
                user.Sessions.Remove(info.s);
            }
            Program.Save();
            await Context.Interaction.UpdateAsync(m =>
            {
                m.Embeds = new[] { MLAPI.Handler.getBuilder(info.s, result).Build() };
                m.Content = "This login has been " + (result ? "approved\r\nThe IP address has been whitelisted, and now redacted." : "rejected");
            });
        }
    }
}
