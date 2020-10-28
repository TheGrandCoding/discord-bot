using Discord;
using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("rules")]
    [Name("Rules Module")]
    [RequireUserPermission(Discord.GuildPermission.ManageGuild)]
    [RequireContext(ContextType.Guild)]
    public class RulesModule : BotModule
    {
        public RulesService Service { get; set; }

        [Command("new")]
        [Summary("Creates a new rule")]
        public async Task Create()
        {
            if (!Service.Rules.TryGetValue(Context.Guild.Id, out var set))
            {
                set = new Classes.Rules.RuleSet();
                Service.Rules[Context.Guild.Id] = set;
                await ReplyAsync($"Please use `{Program.Prefix}rules channel [mention]` to set the channel rules are listed in");
                return;
            }
            await ReplyAsync("Please provide the short text for this rule (will be bolded)");
            var rep1 = await NextMessageAsync(timeout: TimeSpan.FromMinutes(5));
            if (rep1 == null || string.IsNullOrWhiteSpace(rep1.Content))
                return;
            await ReplyAsync("Please provide a longer description or further text for this rule");
            var rep2 = await NextMessageAsync(timeout: TimeSpan.FromMinutes(10));
            if (rep2 == null || string.IsNullOrWhiteSpace(rep2.Content))
                return;

            var field = new EmbedFieldBuilder()
            {
                Name = "#69",
                Value = $"**{rep1.Content}**: {rep2.Content}"
            };
            await ReplyAsync("Does this look correct? [y/n]", embed: new EmbedBuilder().AddField(field).Build());
            var confirm = await NextMessageAsync();
            if (confirm == null || string.IsNullOrWhiteSpace(confirm.Content) || confirm.Content.StartsWith('y') == false)
            {
                await ReplyAsync("Cancelling.");
                return;
            }
            set.CurrentRules.Add(new Classes.Rules.ServerRule()
            {
                Id = Interlocked.Increment(ref set.Counter),
                Short = rep1.Content,
                Long = rep2.Content
            });
            await Service.Update(set);
            Service.OnSave();
            await ReplyAsync("Done.");
        }
    
        [Command("remove")]
        [Alias("delete", "revoke", "repeal")]
        [Summary("Removes a rule")]
        public async Task Remove(int id)
        {
            if(!Service.Rules.TryGetValue(Context.Guild.Id, out var set))
            {
                await ReplyAsync("There are no rules for this guild.");
                return;
            }
            var removed = set.CurrentRules.RemoveAll(x => x.Id == id);
            if(removed == 0)
            {
                await ReplyAsync("No rules exist by that id.");
                return;
            }
            await Service.Update(set);
            Service.OnSave();
            await ReplyAsync("Removed.");
        }
    
        [Command("channel")]
        [Summary("Sets the rules to be displayed in the current channel")]
        public async Task SetChannel()
        {
            await SetChannel(Context.Channel as ITextChannel);
        }

        [Command("channel")]
        [Summary("Sets the rules to be displayed in the mentioned channel")]
        public async Task SetChannel(ITextChannel channel)
        {
            if(!Service.Rules.TryGetValue(channel.GuildId, out var set))
            {
                set = new Classes.Rules.RuleSet();
                Service.Rules[channel.GuildId] = set;
            }
            set.RuleChannel = channel;
            Service.OnSave();
            await ReplyAsync("Set channel to " + channel.Mention);
        }
    }
}
