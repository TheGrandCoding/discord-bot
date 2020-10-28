using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Rules
{
    public class ServerRule
    {
        public int Id { get; set; }
        public string Short { get; set; }
        public string Long { get; set; }

    }

    public class RuleSet
    {
        public int Counter = 0;
        public List<ServerRule> CurrentRules { get; set; } = new List<ServerRule>();
        public List<ServerRule> ProposedRules { get; set; } = new List<ServerRule>();

        public List<IUserMessage> Messages { get; set; } = new List<IUserMessage>();
        public ITextChannel RuleChannel { get; set; }

        static char[] arr = new char[] { '.', ',', ':', ' ' };
        public static bool shouldAddSpace(string s)
        {
            return !arr.Any(x => s.StartsWith(x));
        }

        public List<EmbedBuilder> GetEmbeds()
        {
            var ls = new List<EmbedBuilder>();
            var builder = new EmbedBuilder();
            builder.Title = "Rules";
            foreach(var rule in CurrentRules)
            {
                var field = new EmbedFieldBuilder()
                {
                    Name = $"#{rule.Id}",
                    Value = $"**{rule.Short}**{(shouldAddSpace(rule.Long) ? " " : "")}{rule.Long}"
                };
                if(builder.Fields.Count >= 10)
                {
                    ls.Add(builder);
                    builder = new EmbedBuilder();
                }
                builder.AddField(field);
            }
            ls.Add(builder);
            return ls;
        }
    }
}
