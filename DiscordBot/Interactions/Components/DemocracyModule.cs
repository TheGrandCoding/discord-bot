using Discord;
using Discord.Interactions;
using DiscordBot.Commands.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Components
{
    public class DemocracyModule : BotComponentBase
    {
        public DemocracyService Service { get; set; }

        [ComponentInteraction("democracy:*")]
        public async Task handleButtonClick(string type)
        {
            await Context.Interaction.DeferAsync();
            if (!Service.TryGetValue(Context.Interaction.Message.Id, out var item))
            {
                await Context.Interaction.FollowupAsync("No proposal exists on the message. Weird.",
                    ephemeral: true, embeds: null);
                return;
            }
            var user = Context.User as IGuildUser;

            /*if (item.HasVoted(user))
            {
                await e.Interaction.FollowupAsync("You have already voted in this proposal",
                    ephemeral: true, embeds: null);
                return;
            }*/
            if (!item.CanVote(user))
            {
                await Context.Interaction.FollowupAsync($"You are unable to vote in this proposal",
                    ephemeral: true, embeds: null);
                return;
            }

            var previous = item.RemoveVotes(user); // and removes abstained
            var value = bool.Parse(type);
            if (value)
                item.Ayes.Add(user);
            else
                item.Noes.Add(user);

            var votestr = value ? "aye" : "no";
            if (previous.HasValue)
            {
                if (previous.Value == value)
                    await Context.Interaction.FollowupAsync($"You have already voted *{votestr}*; your vote remains recorded",
                        ephemeral: true);
                else
                    await item.DiscussionThread.SendMessageAsync($"{user.Mention} has changed their vote from {(previous.Value ? "aye" : "no")} to **{votestr}**");

            }
            else
            {
                await item.DiscussionThread.SendMessageAsync($"{user.Mention} has voted **{votestr}**");
            }

            await item.Update();
            var remove = await Service.ShouldRemove(item);
            if (remove)
            {
                Service.Unregister(item);
                await item.DiscussionThread.SendMessageAsync($"The question \"{item.getQuestion()}?\" has been answered; {item.StatusMessage.Content}");
                await item.DiscussionThread.ModifyAsync(x =>
                {
                    x.Archived = true;
                    x.Locked = true;
                }, new RequestOptions() { AuditLogReason = $"Vote has ended" });
            }
            Service.OnSave();
        }
    }
}
