using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.Classes.Legislation;
using DiscordBot.Classes.Legislation.Amending;
using DiscordBot.Commands;
using DiscordBot.MLAPI;
using DiscordBot.Services;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.Legislation
{
    [Group("amend")]
    [Name("Legislation Amending")]
    public class Amending : BotModule
    {
        public Act Act;
        public AmendmentGroup Group;

        public LegislationService Service { get; set; }

        struct SaveInfo
        {
            public Act act { get; set; }
            public AmendmentGroup group { get; set; }
        }
        static Dictionary<ulong, SaveInfo> saves = new Dictionary<ulong, SaveInfo>();
        protected override void BeforeExecute(CommandInfo command)
        {
            if (!saves.ContainsKey(Context.User.Id))
                saves[Context.User.Id] = new SaveInfo();
            var info = saves[Context.User.Id];
            Act = info.act;
            Group = info.group;
        }

        protected override void AfterExecute(CommandInfo command)
        {
            var info = new SaveInfo();
            info.act = Act;
            info.group = Group;
            saves[Context.User.Id] = info;
            if (Act != null)
            {
                Service.Laws[Act.PathName] = Act;
                Service.SaveAct(Act);
                Context.Channel.SendMessageAsync(embed:
                    new EmbedBuilder()
                    .WithTitle($"{Act.ShortRef} #{Group.Id}")
                    .WithDescription($"Your amendment should be visible on the MLAPI website, linked above.")
                    .WithUrl(Act.URL)
                    .WithFooter(Act.PathName)
                    .Build());
            }
        }

        async Task<string> escape(Task<string> thing) => escape(await thing);

        string escape(string thing)
        {
            return thing
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        async Task<string> GetResponse(string prompt)
        {
            await ReplyAsync(prompt);
            var msg = await NextMessageAsync(timeout: TimeSpan.FromMinutes(15));
            if (msg == null || string.IsNullOrWhiteSpace(msg.Content))
                throw new Exception($"No response recieved");
            return msg.Content;
        }

        [Command("approve")]
        [Summary("Approves an amendment to take place")]
        [Discord.Commands.RequireOwner]
        public async Task<RuntimeResult> ApproveAmmendment(string name, int id)
        {
            if (!Service.Laws.TryGetValue(name, out var act))
                return new BotResult("Could not find a bill by that name");
            if (!act.AmendmentReferences.TryGetValue(id, out var group))
                return new BotResult($"There no amendment group matches that id");
            if (!group.Draft)
                return new BotResult("That amendment has already been approved");
            var properName = name.Split('-')[1]; // "amend-[name]-id"
            if (!Service.Laws.TryGetValue(properName, out var original))
                return new BotResult("Could not find original bill that this amends");
            group.Draft = false;
            group.Date = DateTime.Now;
            int oldId = group.Id;
            group.Id = original.AmendmentReferences.Count + 1; // may have changed since last.
            original.AmendmentReferences[group.Id] = group;
            foreach (var actAmendment in act.Amendments.Where(x => x.GroupId == oldId))
            {
                actAmendment.GroupId = group.Id;
                actAmendment.AmendsAct = original;
                original.Amendments.Add(actAmendment);
            }
            foreach (var section in act.Children)
            {
                var oSection = original.Children.FirstOrDefault(x => x.Number == section.Number && x.Group == section.Group);
                foreach (var sectionAmendment in section.Amendments.Where(x => x.GroupId == oldId))
                {
                    sectionAmendment.GroupId = group.Id;
                    sectionAmendment.AmendsAct = original;
                    oSection.Amendments.Add(sectionAmendment);
                }
                foreach (var sectionAmendment in section.TextAmendments.Where(x => x.GroupId == oldId))
                {
                    sectionAmendment.GroupId = group.Id;
                    sectionAmendment.AmendsAct = original;
                    oSection.TextAmendments.Add(sectionAmendment);
                }

                foreach (var paragraph in section.Children)
                {
                    var oParagraph = oSection.Children.FirstOrDefault(x => x.Number == paragraph.Number);
                    foreach (var paragraphAmendment in paragraph.Amendments.Where(x => x.GroupId == oldId))
                    {
                        paragraphAmendment.GroupId = group.Id;
                        paragraphAmendment.AmendsAct = original;
                        oParagraph.Amendments.Add(paragraphAmendment);
                    }
                    foreach (var paragraphAmendment in paragraph.TextAmendments.Where(x => x.GroupId == oldId))
                    {
                        paragraphAmendment.GroupId = group.Id;
                        paragraphAmendment.AmendsAct = original;
                        oParagraph.TextAmendments.Add(paragraphAmendment);
                    }

                    foreach (var clause in paragraph.Children)
                    {
                        var oClause = oParagraph.Children.FirstOrDefault(x => x.Number == clause.Number);
                        foreach (var clauseAmendment in clause.TextAmendments.Where(x => x.GroupId == oldId))
                        {
                            clauseAmendment.GroupId = group.Id;
                            clauseAmendment.AmendsAct = original;
                            oClause.TextAmendments.Add(clauseAmendment);
                        }
                    }
                }
            }
            Service.SaveAct(original);
            Service.RemoveAct(act);
            Act = null;
            Group = null;
            await ReplyAsync("Amendment applied, should be visible at: " + original.URL);
            return new BotResult();
        }

        [Command("continue")]
        [Summary("Continues an amendment")]
        public async Task<RuntimeResult> ContinueAmendment(string name)
        {
            if (Group != null)
                return new BotResult("You are already amending something");
            name = $"amend-{name}-{Context.User.Id}";
            if (!Service.Laws.TryGetValue(name, out var act))
                return new BotResult("Could not find a bill by that name");
            var highestId = act.AmendmentReferences.Keys.OrderByDescending(x => x).FirstOrDefault();
            if (!act.AmendmentReferences.TryGetValue(highestId, out var group))
                return new BotResult($"Could not find the the latest amendment for this bill");
            if (!group.Draft)
                return new BotResult("This amendment has been finished");
            if (group.Author.Id != Context.User.Id)
                return new BotResult("You did not author this amendment");
            Act = act;
            Group = group;
            await ReplyAsync($"Continued amendment {group.Id} for {Act.ShortRef}");
            return new BotResult();
        }

        [Command("start")]
        [Summary("Begins the amendment process for the given bill")]
        public async Task<RuntimeResult> StartAmending(string name)
        {
            if (name.Contains("-"))
                return new BotResult($"Name contains illegal charactor: `-`");
            if (Group != null)
                return new BotResult($"You are already amending a bill, you must abandon or complete that Group first.");
            if (!Service.Laws.TryGetValue(name, out var act))
                return new BotResult("Could not find a bill by that name");
            var content = JsonConvert.SerializeObject(act, new BotUserConverter());
            Act = JsonConvert.DeserializeObject<Act>(content, new BotUserConverter()); // easiest way to clone everything
            Act.PathName = $"amend-{name}-{Context.User.Id}";
            Group = new AmendmentGroup()
            {
                Author = Context.BotUser,
                Contributors = new BotUser[] { },
                Draft = true,
                Id = Act.AmendmentReferences.Count + 1,
                Date = DateTime.Now
            };
            Act.AmendmentReferences[Group.Id] = Group;
            await ReplyAsync("Amendment started");
            return new BotResult();
        }

         
        #region Text Replacements
        #region Common

        string getWords(TextAmenderBuilder builder)
        {
            string wordStr = "";
            var wordSplit = builder.AllWords;
            for (int i = 0; i < wordSplit.Count; i++)
            {
                string value = wordSplit[i];
                value = value
                    .Replace("\u005c", @"\\")
                    .Replace("\r\n", @"\\r\\n")
                    .Replace("*", @"\*");
                wordStr += $"{i:00}: {value}\r\n";
            }
            return wordStr;
        }

        async Task<TextAmendment> craftTextInsertion(dynamic clauseOrParagraph)
        {
            await ReplyAsync("All text amendments are based upon inserting, removing or replacing text at a certain index.\r\n" +
                "You must specific the START index to which the inserted text will preceed.");
            var amendment = new TextAmendment()
            {
                Type = AmendType.Insert,
                Start = 0,
                New = ""
            };
            var amendments = new List<TextAmendment>();
            dynamic thing = clauseOrParagraph.TextAmendments;
            if (thing is List<TextAmendment>)
                amendments.AddRange(thing);
            string _TEXT;
            try
            {
                _TEXT = clauseOrParagraph.Text;
            }
            catch
            {
                _TEXT = clauseOrParagraph.Header;
            }
            do
            {
                var builder = new TextAmenderBuilder(_TEXT, new AmendmentBuilder(0, false), amendments, true);
                string wordStr = getWords(builder);
                if (!string.IsNullOrWhiteSpace(amendment.New))
                {
                    await ReplyAsync("Does this look right to you?\r\n>>> " + builder.RawText);
                    var resp = await GetResponse("If the above is correct, entry `y`, otherwise type `n`");
                    if (resp.StartsWith("y"))
                        break;
                    amendments.Remove(amendment);
                }
                await ReplyAsync($"Text words:\r\n>>> {wordStr}");
                var index = await GetResponse($"Please provide the index of the word that this text will be inserted BEFORE");
                if (!int.TryParse(index, out var number))
                    throw new ArgumentException("Input provided was not an integer!");
                if (number < 0 || number > builder.AllWords.Count)
                    throw new ArgumentException($"Index was out of range! (0-{builder.AllWords.Count})");
                amendment.Start = number;
                string text = await escape(GetResponse("Please provide the new text to insert"));
                if (text.Length > 2)
                {
                    if (text.First() == '"' && text.Last() == '"')
                    {
                        text = text.Substring(1, text.Length - 2); // allow for user to preserve whitespace by quoting
                    } else if (text.StartsWith(@"\"""))
                    {
                        text = text.Substring(2, text.Length - 3);
                    }
                }
                amendment.New = text;
                amendments.Add(amendment);
            } while (true);
            return amendment;
        }
        async Task<TextAmendment> craftTextReplacement(dynamic clauseOrParagraph)
        {
            await ReplyAsync("All text amendments are based upon inserting, removing or replacing text at a certain index.\r\n" +
                "You must specific the START index to which the inserted text will preceed.");
            var amendment = new TextAmendment()
            {
                Type = AmendType.Replace,
                Start = 0,
                Length = 0,
                New = ""
            };
            var amendments = new List<TextAmendment>();
            dynamic thing = clauseOrParagraph.TextAmendments;
            if(thing is List<TextAmendment>) 
                amendments.AddRange(thing);
            string _TEXT;
            try
            {
                _TEXT = clauseOrParagraph.Text;
            } catch
            {
                _TEXT = clauseOrParagraph.Header;
            }
            do
            {
                var builder = new TextAmenderBuilder(_TEXT, new AmendmentBuilder(0, false), amendments, true);
                string wordStr = getWords(builder);
                if (amendment.Length > 0 && !string.IsNullOrWhiteSpace(amendment.New))
                {
                    await ReplyAsync("Does this look right to you?\r\n>>> " + builder.RawText);
                    var resp = await GetResponse("If the above is correct, entry `y`, otherwise type `n`");
                    if (resp.StartsWith("y"))
                        break;
                    amendments.Remove(amendment);
                }
                await ReplyAsync($"Text words:\r\n>>> {wordStr}");
                var index = await GetResponse($"Please provide the index of the first word that this amendment will replace");
                if (!int.TryParse(index, out var number))
                    throw new ArgumentException("Input provided was not an integer!");
                if (number < 0 || number > builder.AllWords.Count)
                    throw new ArgumentException($"Index was out of range! (0-{builder.AllWords.Count})");
                amendment.Start = number;
                string text = await escape(GetResponse("Please provide the new text to insert"));
                if (text.Length > 2)
                {
                    if (text.First() == '"' && text.Last() == '"')
                    {
                        text = text.Substring(1, text.Length - 2); // allow for user to preserve whitespace by quoting
                    }
                    else if (text.StartsWith(@"\"""))
                    {
                        text = text.Substring(2, text.Length - 3);
                    }
                }
                amendment.New = text;
                var lengthStr = await GetResponse("Please provide how many words, including that index, should be replaced with the provided string");
                if (!int.TryParse(lengthStr, out var length))
                    throw new ArgumentException("Input provided was not an integer!");
                if (length <= 0)
                    throw new ArgumentException("Input was out of range! (>=1)");
                amendment.Length = length;

                amendments.Add(amendment);
            } while (true);
            return amendment;
        }
        async Task<TextAmendment> craftTextRemoval(dynamic clauseOrParagraph)
        {
            await ReplyAsync("All text amendments are based upon inserting, removing or replacing text at a certain index.\r\n" +
                "You must specific the START index to which the inserted text will preceed.\r\n" +
                "For removals, you must specifiy how many words to remove after that index");
            var amendment = new TextAmendment()
            {
                Type = AmendType.Repeal,
                Start = 0,
                Length = 0
            };
            var amendments = new List<TextAmendment>();
            dynamic thing = clauseOrParagraph.TextAmendments;
            if (thing is List<TextAmendment>)
                amendments.AddRange(thing);
            string _TEXT;
            try
            {
                _TEXT = clauseOrParagraph.Text;
            }
            catch
            {
                _TEXT = clauseOrParagraph.Header;
            }
            do
            {
                var builder = new TextAmenderBuilder(_TEXT, new AmendmentBuilder(0, false), amendments, true);
                string wordStr = getWords(builder);
                if (amendment.Length > 0)
                {
                    await ReplyAsync("Does this look right to you?\r\n>>> " + builder.RawText);
                    var resp = await GetResponse("If the above is correct, entry `y`, otherwise type `n`");
                    if (resp.StartsWith("y"))
                        break;
                    amendments.Remove(amendment);
                }
                await ReplyAsync($"Text words:\r\n>>> {wordStr}");
                var index = await GetResponse($"Please provide the index of the first word that this amendment will remove");
                if (!int.TryParse(index, out var number))
                    throw new ArgumentException("Input provided was not an integer!");
                if (number < 0 || number > builder.AllWords.Count)
                    throw new ArgumentException($"Index was out of range! (0-{builder.AllWords.Count})");
                amendment.Start = number;
                var lengthStr = await GetResponse("Please provide how many words, including that index, should be removed after it");
                if (!int.TryParse(lengthStr, out var length))
                    throw new ArgumentException("Input provided was not an integer!");
                if (length <= 0)
                    throw new ArgumentException("Input was out of range! (>=1)");
                amendment.Length = length;
                amendments.Add(amendment);
            } while (true);
            return amendment;
        }

        async Task<RuntimeResult> insertThing(dynamic clauseOrParagraph)
        {
            TextAmendment amendment;
            try
            {
                amendment = await craftTextInsertion(clauseOrParagraph);
            }
            catch (ArgumentException ex)
            {
                return new BotResult(ex.Message);
            }
            amendment.AmendsAct = Act;
            amendment.GroupId = Group.Id;
            try
            {
                clauseOrParagraph.TextAmendments.Add(amendment);
            }
            catch
            {
                clauseOrParagraph.Amendments.Add(amendment);
            }
            await ReplyAsync("Added!");
            return new BotResult();
        }
        async Task<RuntimeResult> removeThing(dynamic clauseOrParagraph)
        {
            TextAmendment amendment;
            try
            {
                amendment = await craftTextRemoval(clauseOrParagraph);
            }
            catch (ArgumentException ex)
            {
                return new BotResult(ex.Message);
            }
            amendment.AmendsAct = Act;
            amendment.GroupId = Group.Id;
            try
            {
                clauseOrParagraph.TextAmendments.Add(amendment);
            }
            catch
            {
                clauseOrParagraph.Amendments.Add(amendment);
            }
            await ReplyAsync("Added!");
            return new BotResult();
        }
        async Task<RuntimeResult> replaceThing(dynamic clauseOrParagraph)
        {
            TextAmendment amendment;
            try
            {
                amendment = await craftTextReplacement(clauseOrParagraph);
            }
            catch (ArgumentException ex)
            {
                return new BotResult(ex.Message);
            }
            amendment.AmendsAct = Act;
            amendment.GroupId = Group.Id;
            try
            {
                clauseOrParagraph.TextAmendments.Add(amendment);
            }
            catch
            {
                clauseOrParagraph.Amendments.Add(amendment);
            }
            await ReplyAsync("Added!");
            return new BotResult();
        }
        #endregion

        #region Clause
        [Command("insert text")]
        [Summary("Amends the text of the provided clause")]
        public async Task<RuntimeResult> InsertClauseText(string section, string paragraph, string clause)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            var Clause = Paragraph.Children.FirstOrDefault(x => x.Number == clause);

            return await insertThing(Clause);
        }

        [Command("repeal text")]
        [Summary("Amends the text of the provided clause")]
        public async Task<RuntimeResult> RemoveClauseText(string section, string paragraph, string clause)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            var Clause = Paragraph.Children.FirstOrDefault(x => x.Number == clause);
            return await removeThing(Clause);
        }

        [Command("replace text")]
        [Summary("Replaces the text of the provided clause")]
        public async Task<RuntimeResult> ReplaceClauseText(string section, string paragraph, string clause)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            var Clause = Paragraph.Children.FirstOrDefault(x => x.Number == clause);
            return await replaceThing(Clause);
        }

        #endregion
        #region Paragraph
        [Command("insert text")]
        [Summary("Amends the text of the provided paragraph")]
        public async Task<RuntimeResult> InsertParagraphText(string section, string paragraph)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);

            return await insertThing(Paragraph);
        }

        [Command("repeal text")]
        [Summary("Amends the text of the provided paragraph")]
        public async Task<RuntimeResult> RemoveParagraphText(string section, string paragraph)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            return await removeThing(Paragraph);
        }

        [Command("replace text")]
        [Summary("Replaces the text of the provided paragraph")]
        public async Task<RuntimeResult> ReplaceParagraphText(string section, string paragraph)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            return await replaceThing(Paragraph);
        }
        #endregion

        #region Section
        // Change Section's Header.
        [Command("insert text")]
        [Summary("Amends the header of the provided section")]
        public async Task<RuntimeResult> InsertSectionHeader(string section)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            return await insertThing(Section);
        }

        [Command("repeal text")]
        [Summary("Amends the header of the provided section")]
        public async Task<RuntimeResult> RemoveSectionHeader(string section)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            return await removeThing(Section);
        }

        [Command("replace text")]
        [Summary("Replaces the header of the provided section")]
        public async Task<RuntimeResult> ReplaceSectionHeader(string section)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            return await replaceThing(Section);
        }
        #endregion

        #endregion

        #region Thing Repealings
        [Command("repeal section")]
        [Summary("Repeals the given section")]
        public async Task<RuntimeResult> RepealSection(string number)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == number);
            if (Section == null)
                return new BotResult($"That section does not exist");
            var applies = Act.Amendments.FirstOrDefault(x => x.Target == Section.Number && x.Type == AmendType.Repeal);
            if (applies != null)
                return new BotResult($"This section is already repealed: {applies.GetDescription()}");
            var amend = new ActAmendment()
            {
                Type = AmendType.Repeal,
                Target = number,
                GroupId = Group.Id,
                AmendsAct = Act
            };
            Act.Amendments.Add(amend);
            await ReplyAsync("Repealed");
            return new BotResult();
        }

        [Command("repeal paragraph")]
        [Summary("Repeals the given paragraph")]
        public async Task<RuntimeResult> RepealParagraph(string section, string number)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            if (Section == null)
                return new BotResult($"That section does not exist");
            var sectionApplies = Act.Amendments.FirstOrDefault(x => x.Target == Section.Number && x.Type == AmendType.Repeal);
            if (sectionApplies != null)
                return new BotResult($"This section is already repealed: {sectionApplies.GetDescription()}");
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == number);
            if (Paragraph == null)
                return new BotResult($"That paragraph does not exist");
            var paragraphApplies = Section.Amendments.FirstOrDefault(x => x.Target == number && x.Type == AmendType.Repeal);
            if (paragraphApplies != null)
                return new BotResult("That paragraph is already repealed: " + paragraphApplies.GetDescription());
            var amend = new SectionAmendment()
            {
                Type = AmendType.Repeal,
                Target = number,
                GroupId = Group.Id,
                AmendsAct = Act
            };
            Section.Amendments.Add(amend);
            await ReplyAsync("Repealed");
            return new BotResult();
        }

        [Command("repeal clause")]
        [Summary("Repeals the given clause")]
        public async Task<RuntimeResult> RepealClause(string section, string paragraph, string number)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            if (Section == null)
                return new BotResult($"That section does not exist");
            var sectionApplies = Act.Amendments.FirstOrDefault(x => x.Target == Section.Number && x.Type == AmendType.Repeal);
            if (sectionApplies != null)
                return new BotResult($"This section is already repealed: {sectionApplies.GetDescription()}");
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            if (Paragraph == null)
                return new BotResult($"That paragraph does not exist");
            var paragraphApplies = Section.Amendments.FirstOrDefault(x => x.Target == paragraph && x.Type == AmendType.Repeal);
            if (paragraphApplies != null)
                return new BotResult("That paragraph is already repealed: " + paragraphApplies.GetDescription());
            var Clause = Paragraph.Children.FirstOrDefault(x => x.Number == number);
            if (Clause == null)
                return new BotResult("That clause does not exist");
            var clauseApplies = Paragraph.Amendments.FirstOrDefault(x => x.Target == number && x.Type == AmendType.Repeal);
            if (clauseApplies != null)
                return new BotResult("That clause is already repealed: " + paragraphApplies.GetDescription());
            var amend = new ParagraphAmendment()
            {
                Type = AmendType.Repeal,
                Target = number,
                GroupId = Group.Id,
                AmendsAct = Act
            };
            Paragraph.Amendments.Add(amend);
            await ReplyAsync("Repealed");
            return new BotResult();
        }
        #endregion
    
    
    }
}
