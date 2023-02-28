using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services.Rules;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Components
{
    public class PenaltyModule : BotComponentBase
    {
        [ComponentInteraction("penalty:*:*")]
        public async Task handleMuteChange(string penId, string value)
        {

            await Context.Interaction.DeferAsync(true);
            var This = Services.GetRequiredService<PenaltyService>();
            var penaltyId = int.Parse(penId);
            var actionType = int.Parse(value);
            var penalty = This.FindPenalty(x =>
            {
                return x.Id == penaltyId;
            }) as MutePenalty;
            if (penalty == null)
            {
                await Context.Interaction.UpdateAsync(x =>
                {
                    x.Content = "*Mute has been removed*";
                    x.Components = new ComponentBuilder().Build();
                });
                await Context.Interaction.FollowupAsync("This mute has already been removed and cannot be modified further",
                    ephemeral: true, embeds: null);
                return;
            }
            switch (actionType)
            {
                case 0:
                    penalty.Duration = new TimeSpan(1, 0, 0);
                    await Context.Interaction.FollowupAsync($"{Context.User.Mention} has set duration of mute for {penalty.Target.Mention} to one hour", embeds: null);
                    break;
                case 1:
                    penalty.Duration = penalty.Duration.GetValueOrDefault(TimeSpan.FromHours(0)).Add(TimeSpan.FromHours(1));
                    await Context.Interaction.FollowupAsync($"{Context.User.Mention} has increased duration of mute for {penalty.Target.Mention}" +
                        $" to {Program.FormatTimeSpan(penalty.Duration.Value)}", embeds: null);
                    break;
                case 2:
                    if (!penalty.Duration.HasValue || penalty.Duration.Value.TotalHours < 1)
                    {
                        await Context.Interaction.FollowupAsync("Duration is already lower than one hour, cannot reduce by one.",
                            ephemeral: true, embeds: null);
                        return;
                    }
                    penalty.Duration = penalty.Duration.GetValueOrDefault(TimeSpan.FromHours(0)).Add(TimeSpan.FromHours(-1));
                    await Context.Interaction.FollowupAsync($"{Context.User.Mention} has decreased duration of mute for {penalty.Target.Mention}" +
                        $" to {Program.FormatTimeSpan(penalty.Duration.Value)}", embeds: null);
                    break;
                case 3:
                    This.RemovePenalty(penaltyId);
                    await Context.Interaction.FollowupAsync($"{Context.User.Mention} removed the mute of {penalty.Target.Mention}", embeds: null);
                    await Context.Interaction.UpdateAsync(x =>
                    {
                        x.Content = "*This mute has been removed*";
                        x.Components = new ComponentBuilder().Build();
                    });
                    return;
                default:
                    await Context.Interaction.FollowupAsync($"Unknown action: {actionType}.", embeds: null);
                    return;

            }
            await Context.Interaction.UpdateAsync(x => x.Embeds = new[] { This.getRoleMuteBuilder(penalty.Target as SocketGuildUser, penalty).Build() });
            This.OnSave();
        }
    }
}
