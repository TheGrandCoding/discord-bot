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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Legislation
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
                    .WithTitle($"{Act.ShortTitle} #{Group.Id}")
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
            foreach (var section in act.Children)
            {
                var oSection = original.Children.FirstOrDefault(x => x.Number == section.Number && x.Group == section.Group);
                foreach (var sectionAmendment in section.TextAmendments.Where(x => x.GroupId == oldId))
                {
                    sectionAmendment.GroupId = group.Id;
                    sectionAmendment.AmendsAct = original;
                    oSection.TextAmendments.Add(sectionAmendment);
                }

                foreach (var paragraph in section.Children)
                {
                    var oParagraph = oSection.Children.FirstOrDefault(x => x.Number == paragraph.Number);
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
            await ReplyAsync($"Continued amendment {group.Id} for {Act.ShortTitle}");
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
                    amendment.New = null;
                    continue; // reset.
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
                Type = AmendType.Substitute,
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
                if (amendment.Length > 0 && !string.IsNullOrWhiteSpace(amendment.New))
                {
                    await ReplyAsync("Does this look right to you?\r\n>>> " + builder.RawText);
                    var resp = await GetResponse("If the above is correct, entry `y`, otherwise type `n`");
                    if (resp.StartsWith("y"))
                        break;
                    amendments.Remove(amendment);
                    amendment.Length = 0;
                    amendment.New = null;
                    continue; // reset.
                }
                string wordStr = getWords(builder);
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
                if (amendment.Length > 0)
                {
                    await ReplyAsync("Does this look right to you?\r\n>>> " + builder.RawText);
                    var resp = await GetResponse("If the above is correct, entry `y`, otherwise type `n`");
                    if (resp.StartsWith("y"))
                        break;
                    amendments.Remove(amendment);
                    amendment.Length = 0;
                    continue; // reset.
                }
                string wordStr = getWords(builder);
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
            Section.RepealedById = Group.Id;
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
            if (Section.RepealedById.HasValue)
                return new BotResult($"This section is already repealed: {Section.RepealedBy.GetDescription()}");
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == number);
            if (Paragraph == null)
                return new BotResult($"That paragraph does not exist");
            if (Paragraph.RepealedById.HasValue)
                return new BotResult("That paragraph is already repealed: " + Paragraph.RepealedBy.GetDescription());
            Paragraph.RepealedById = Group.Id;
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
            if (Section.RepealedById.HasValue)
                return new BotResult($"This section is already repealed: {Section.RepealedBy.GetDescription()}");
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            if (Paragraph == null)
                return new BotResult($"That paragraph does not exist");
            if (Paragraph.RepealedById.HasValue)
                return new BotResult("That paragraph is already repealed: " + Paragraph.RepealedBy.GetDescription());
            var Clause = Paragraph.Children.FirstOrDefault(x => x.Number == number);
            if (Clause == null)
                return new BotResult("That clause does not exist");
            if (Clause.RepealedById != null)
                return new BotResult("That clause is already repealed: " + Clause.RepealedBy.GetDescription());
            Clause.RepealedById = Group.Id;
            await ReplyAsync("Repealed");
            return new BotResult();
        }
        #endregion

        #region Thing Insertion
        string getNextLetter(string letter)
        {
            int code = Convert.ToInt32(letter[^1]); // gets last char
            code = code + 1; // moves to next letter in alphabet.
            if (code >= 91)
                return letter + "A"; // append entire new letter
            return letter[0..^1] + Convert.ToChar(code); // cycle upwards.
        }

        string getNextLetter<TChild>(LawThing<TChild> parent, string letter) where TChild: LawThing
        {
            while (parent.Children.Any(x => x.Number == letter))
                letter = getNextLetter(letter);
            return letter;
        }

        #region Clause

        [Command("insert section")]
        [Summary("Inserts a section at the specified location")]
        public async Task<RuntimeResult> InsertSection(string section)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            if (Section != null)
            {
                var nextLetter = getNextLetter(Act, section);
                await ReplyAsync($"Section already exists by that number, reply if to use **{nextLetter}** instead; wait to cancel");
                var next = await NextMessageAsync();
                if(next == null || string.IsNullOrWhiteSpace(next.Content))
                    return new BotResult("Cancelled due to no response on conflict.");
                section = nextLetter;
            }
            var noticeMsg = await ReplyAsync("Please send the header of the section; react if group.");
            await noticeMsg.AddReactionAsync(Emotes.THUMBS_UP);
            var textMsg = await NextMessageAsync(timeout: TimeSpan.FromMinutes(5));
            if (textMsg == null || string.IsNullOrWhiteSpace(textMsg.Content))
                return new BotResult("Cancelled.");
            Section = new Section(textMsg.Content);
            Section.Number = section;
            int count = await noticeMsg.GetReactionUsersAsync(Emotes.THUMBS_UP, 5).CountAsync();
            Section.Group = count > 1;
            Section.InsertedById = Group.Id;
            Act.Children.Add(Section);
            Act.Sort();
            await ReplyAsync("Inserted.");
            return new BotResult();
        }

        [Command("insert paragraph")]
        [Summary("Inserts a paragraph to the specified location")]
        public async Task<RuntimeResult> InsertParagraph(string section, string paragraph)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            if (Section == null)
                return new BotResult($"Section does not exist; use `{Program.Prefix}amend insert section {section}` to insert it first.");
            if (Section.RepealedById.HasValue)
                return new BotResult("That section has been repealed");
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            if(Paragraph != null)
            {
                var nextLetter = getNextLetter(Section, paragraph);
                await ReplyAsync($"Paragraph already exists by that number, reply if to use **{nextLetter}** instead; wait to cancel");
                var next = await NextMessageAsync();
                if (next == null || string.IsNullOrWhiteSpace(next.Content))
                    return new BotResult("Cancelled due to no response on conflict.");
                paragraph = nextLetter;
            }
            await ReplyAsync("Please provide any text for the paragraph; reply with `-` for null");
            var textMsg = await NextMessageAsync(timeout: TimeSpan.FromMinutes(15));
            if (textMsg == null || string.IsNullOrWhiteSpace(textMsg.Content))
                return new BotResult("Cancelled");
            var text = textMsg.Content == "-" ? null : textMsg.Content;
            Paragraph = new TextualLawThing(text)
            {
                InsertedById = Group.Id,
                Number = paragraph
            };
            Section.Children.Add((TextualLawThing)Paragraph);
            await ReplyAsync("Inserted");
            return new BotResult();
        }

        [Command("insert clause")]
        [Summary("Inserts a clause at the specified location")]
        public async Task<RuntimeResult> InsertClause(string section, string paragraph, string number)
        {
            var Section = Act.Children.FirstOrDefault(x => x.Number == section);
            if (Section == null)
                return new BotResult($"That section does not exist");
            if (Section.RepealedById.HasValue)
                return new BotResult($"This section is repealed: {Section.RepealedBy.GetDescription()}");
            var Paragraph = Section.Children.FirstOrDefault(x => x.Number == paragraph);
            if (Paragraph == null)
                return new BotResult($"That paragraph does not exist");
            if (Paragraph.RepealedById.HasValue)
                return new BotResult("That paragraph is repealed: " + Paragraph.RepealedBy.GetDescription());
            var Clause = Paragraph.Children.FirstOrDefault(x => x.Number == number);
            if (Clause != null)
            {
                var suggest = getNextLetter(Paragraph, number);
                await ReplyAsync($"That clause already exists. Would you like to insert a new one with number **{suggest}** instead? Reply if yes, wait to cancel");
                var next = await NextMessageAsync();
                if (next == null || string.IsNullOrWhiteSpace(next.Content))
                    return new BotResult("Cancelled");
                number = suggest;
                Clause = null;
            }
            await ReplyAsync("Please provide any text for the clause");
            var textMsg = await NextMessageAsync(timeout: TimeSpan.FromMinutes(15));
            if (textMsg == null || string.IsNullOrWhiteSpace(textMsg.Content))
                return new BotResult("Cancelled");
            Clause = new TextualLawThing(textMsg.Content)
            {
                Number = number,
                InsertedById = Group.Id,
            };
            Paragraph.Children.Add(Clause);
            await ReplyAsync("Inserted, please amend the text of the clause.");
            return new BotResult();
        }

        #endregion

        #endregion
    }
}
