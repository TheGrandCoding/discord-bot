using DiscordBot.Classes.Rules;
using DiscordBot.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class RulesService : SavedService
    {
        public Dictionary<ulong, RuleSet> Rules { get; set; }

        public override string GenerateSave()
        {
            return Program.Serialise(Rules);
        }
        public override void OnReady()
        {
            var sv = ReadSave();
            Rules = Program.Deserialise<Dictionary<ulong, RuleSet>>(sv);
        }
        public override void OnDailyTick()
        {
            bool dirty = false;
            foreach(var keypair in Rules)
            {
                var set = keypair.Value;
                dirty = Update(set).Result || dirty;
            }
            if (dirty)
                OnSave();
        }

        public async Task<bool> Update(RuleSet set)
        {
            bool dirty = false;
            var embeds = set.GetEmbeds();
            int i = 0;
            foreach (var embed in embeds)
            {
                var message = set.Messages.ElementAtOrDefault(i++);
                if (message == null)
                {
                    message = await set.RuleChannel.SendMessageAsync(embed: embed.Build());
                    set.Messages.Add(message);
                    dirty = true;
                    continue;
                }
                message.ModifyAsync(x => x.Embed = embed.Build()).Wait();
            }
            var excess = set.Messages.Skip(i);
            foreach (var thing in excess)
            {
                thing.DeleteAndTrackAsync("Rules updated and this message is unneeded").Wait();
                set.Messages.Remove(thing);
                dirty = true;
            }
            return dirty;
        }
    }
}
