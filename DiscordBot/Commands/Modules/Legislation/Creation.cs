using Discord;
using Discord.Commands;
using DiscordBot.Classes.Legislation;
using DiscordBot.Commands;
using DiscordBot.MLAPI;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules.Legislation
{
    [Group("draft")]
    [Name("Drafting Legislation Commands")]
    public class Creation : BotBase
    {
        struct SaveInfo
        {
            public Act act { get; set; }
            public Section section { get; set; }
            public TextualLawThing paragraph { get; set; }
            public TextualLawThing clause { get; set; }
        }
        static Dictionary<ulong, SaveInfo> saves = new Dictionary<ulong, SaveInfo>();
        public Act Act;
        public Section Section;
        public TextualLawThing Paragraph;
        public TextualLawThing Clause;

        Embed embedAct()
        {
            return new EmbedBuilder()
                .WithTitle(Act.LongTitle)
                .WithUrl($"{Handler.LocalAPIUrl}/laws/{Act.PathName}")
                .WithDescription($"{Act.ShortTitle}, {Act.Children.Count} sections:\r\n" +
                    string.Join(", ", Act.Children.Select(x => x.Number)))
                .Build();
        }
        Embed embedSection()
        {
            return new EmbedBuilder()
                .WithTitle($"{Section.Header}")
                .WithUrl($"{Handler.LocalAPIUrl}/laws/{Act.PathName}#section-{Section.Number}")
                .WithDescription($"{(Section.Group ? "*Section group*" : $"{Section.Children.Count} paragraphs")}")
                .Build();
        }
        Embed embedParagraph()
        {
            return new EmbedBuilder()
                .WithTitle($"({Paragraph.Number})")
                .WithUrl($"{Handler.LocalAPIUrl}/laws/{Act.PathName}#section-{Section.Number}-{Paragraph.Number}")
                .WithDescription($"{Paragraph.Children.Count} clauses, text:\r\n>>> {Paragraph.Text}")
                .Build();
        }
        Embed embedClause()
        {
            return new EmbedBuilder()
                .WithTitle($"({Clause.Number})")
                .WithUrl($"{Handler.LocalAPIUrl}/laws/{Act.PathName}#section-{Section.Number}-{Paragraph.Number}-{Clause.Number}")
                .WithDescription($">>> {Clause.Text}")
                .Build();
        }

        public string ActCommands => $"Use `{Program.Prefix}draft create section` to create new section\r\n" +
                $"Use `{Program.Prefix}draft create section [number]`, to replace that preexisting section, or insert at, with a new section,";

        public string SectionCommands =>
                $"Use `{Program.Prefix}draft create paragraph` to create a new child paragraph to this section\r\n" +
                $"Use `{Program.Prefix}draft create paragraph [number]` to insert and overwrite a new paragraph at that paragraph\r\n" +
                $"Use `{Program.Prefix}draft edit section [sectionNumber]` to switch to editing a different section";

        public string ParagraphCommands =>
            $"Use `{Program.Prefix}draft create clause` to create a new child clause to this paragraph\r\n" +
            $"Use `{Program.Prefix}draft create clause [number]` to insert and overwrite a new paragraph at that paragraph\r\n" +
            $"Use `{Program.Prefix}draft edit paragraph [paragraphNumber]` to switch to and edit that paragraph";

        public string ClauseCommands =>
            $"Use `{Program.Prefix}draft edit clause [clauseNumber]` to edit the text of that clause;\r\n" +
            $"Use also any of the previous commands:\r\n{ActCommands}\r\n{SectionCommands}\r\n{ParagraphCommands}";



        public LegislationService Service { get; set; }

        protected override void BeforeExecute(CommandInfo command)
        {
            base.BeforeExecute(command);
            if (!saves.ContainsKey(Context.User.Id))
                saves[Context.User.Id] = new SaveInfo();
            var info = saves[Context.User.Id];
            Act = info.act;
            Section = info.section;
            Paragraph = info.paragraph;
            Clause = info.clause;
        }

        protected override void AfterExecute(CommandInfo command)
        {
            var info = new SaveInfo();
            info.act = Act;
            info.section = Section;
            info.paragraph = Paragraph;
            info.clause = Clause;
            saves[Context.User.Id] = info;
            if(Act != null)
            {
                Service.Laws[Act.PathName] = Act;
            }
        }

        async Task<string> GetResponse(string prompt)
        {
            await ReplyAsync(prompt);
            var result = await NextMessageAsync(timeout: TimeSpan.FromMinutes(15));
            if (!result.IsSuccess)
                throw new Exception($"No response recieved");
            return result.Value.Content;
        }


        [Command("create bill"), Alias("law")]
        [Summary("Creates a new draft bill")]
        public async Task<RuntimeResult> CreateNew()
        {
            var path = await GetResponse($"Please provide the short path name, which appears in the URL and in save file. Only alphanumeric");
            if (path.Contains("-"))
                return new BotResult("Illegal character `-` in path name");
            path = $"draft-{path}-{Context.User.Id}";
            if (Service.Laws.TryGetValue(path, out var act))
                return new BotResult($"You are already editing an act by that identifier");
            var shortTitle = await GetResponse("Please provide the short title for the law");
            var longTitle = await GetResponse("Please provide the long title for the law");
            Act = new Act(longTitle)
            {
                LongTitle = longTitle,
                ShortTitle = shortTitle,
                Draft = true,
                PathName = path,
            };
            await ReplyAsync($"Act creation initialised.\r\n" + ActCommands, embed: embedAct());
            return new BotResult();
        }

        [Command("create section")]
        [Summary("Creates or sets a new draft section to a preexisting bill")]
        public async Task<RuntimeResult> CreateNewSection(string number = null)
        {
            if (Act == null)
                return new BotResult("You must be creating a draft bill first.");
            var header = await GetResponse($"Please provide the Header value for this section. To set a group section, the first charactor of your message must be `=`");
            number = number?.ToUpper() ?? null; 
            bool group = false;
            if(header[0] == '=')
            {
                group = true;
                header = header.Substring(1);
            }
            if(number != null)
            {
                var count = Act.Children.RemoveAll(x => x.Number.ToUpper() == number);
                await ReplyAsync($"Removed {count} previous section(s) that had the same number, they will be replaced by this new one.");
            }
            Section = new Section(header) 
            { 
                Number = number ?? $"{Act.Children.Count + 1}",
                Group = group 
            };
            Act.Children.Add(Section);
            if(Section.Group)
            {
                Section = null;
                await ReplyAsync($"Section group has been created;\r\n" + ActCommands);
            } else
            {
                await ReplyAsync($"Section initiaisation set.\r\n" + SectionCommands, embed: embedSection());
            }
            return new BotResult();
        }

        [Command("create paragraph")]
        [Summary("Create or insert and overwrite at the number")]
        public async Task<RuntimeResult> CreateNewParagraph(string number = null)
        {
            if (Section == null)
                return new BotResult($"No section is being edited");
            if (Section.Group)
                return new BotResult($"Section groups cannot contain paragraphs");
            var text = await GetResponse("Please provide the text for this paragraph, or reply `-` for no text.");
            if (text == "-")
                text = null;
            number = number?.ToUpper() ?? null; 
            if(number != null)
            {
                var count = Section.Children.RemoveAll(x => x.Number.ToUpper() == number);
                await ReplyAsync($"Removed {count} previous paragraph(s) that had the same number, they will be replaced by this new one.");
            }
            Paragraph = new TextualLawThing(text)
            {
                Number = number ?? $"{Section.Children.Count + 1}",
            };
            Section.Children.Add(Paragraph);
            await ReplyAsync($"New paragraph has been inserted\r\n" + ParagraphCommands, embed: embedParagraph());
            return new BotResult();
        }
    
        [Command("create clause")]
        [Summary("Creates or inserts and overwrites at the number")]
        public async Task<RuntimeResult> CreateNewClause(string number = null)
        {
            if (Section == null)
                return new BotResult("No section is being edited");
            if (Section.Group)
                return new BotResult("Section groups cannot contain paragraphs nor clauses");
            if (Paragraph == null)
                return new BotResult("No paragraph is being edited");
            var text = await GetResponse("Please provide the text for this new clause");
            number = number?.ToLower() ?? null;
            if(number != null)
            {
                var count = Paragraph.Children.RemoveAll(x => x.Number == number);
                await ReplyAsync($"Removed {count} previous clause(s)");
            }
            Clause = new TextualLawThing(text)
            {
                Number = number ?? $"{Convert.ToChar(Paragraph.Children.Count + 1 + 97)}",
            };
            Paragraph.Children.Add(Clause);
            await ReplyAsync(ClauseCommands, embed: embedClause());
            return new BotResult();
        }

        [Command("edit section")]
        [Summary("Switches to and edits the provided section")]
        public async Task<RuntimeResult> EditSection(string number)
        {
            if (Act == null)
                return new BotResult("You are not editing a law");
            number = number.ToUpper();
            var section = Act.Children.FirstOrDefault(x => x.Number == number);
            if (section == null)
                return new BotResult($"There is no section by that number, only by `{string.Join("`, `", Act.Children.Select(x => x.Number))}`\r\n" +
                    $"Or perhaps you meant `{Program.Prefix}draft create section {number}`?");
            Section = section;
            await ReplyAsync($"You are now editing {Section.Number}\r\n{SectionCommands}", embed: embedSection());
            var shouldChangeHeader = await GetResponse("Do you wish to change the header or group status for this section?\r\n" +
                "If so, please reply with `y`, otherwise reply with `n`");
            if (shouldChangeHeader.StartsWith("y") == false)
                return new BotResult();
            var header = await GetResponse($"Please provide the new header value, remember if your message starts `=` it'll be a group section\r\n" +
                $"Existing value: `{(Section.Group ? "=" : "")}{Section.Header}`");
            Section.Group = header[0] == '=';
            if (Section.Group)
                header = header.Substring(1);
            Section.Header = header;
            await ReplyAsync($"Section header updated", embed: embedSection());
            return new BotResult();
        }

        [Command("edit paragraph")]
        [Summary("Switch to and edit the numbered paragraph")]
        public async Task<RuntimeResult> EditParagraph(string number)
        {
            if (Act == null)
                return new BotResult("You are not editing a law");
            if (Section == null || Section.Group)
                return new BotResult("You are not editing a section");
            number = number.ToUpper();
            var paragraph = Section.Children.FirstOrDefault(x => x.Number == number);
            if (paragraph == null)
                return new BotResult($"There is no section by that number, only by `{string.Join("`, `", Section.Children.Select(x => x.Number))}`\r\n" +
                    $"Or perhaps you meant `{Program.Prefix}draft create paragraph {number}`?");
            Paragraph = paragraph;
            await ReplyAsync("You are now editing this paragraph", embed: embedParagraph());
            var shouldEditText = await GetResponse("Do you want to edit this paragraph's text?\r\n" +
                "If so, reply with `y`, otherwise with `n`");
            if (!shouldEditText.StartsWith("y"))
                return new BotResult();
            Paragraph.Text = await GetResponse("Please provide the new text for this paragraph.\r\n" +
                $"It's current text is as follows:\r\n>>> {Paragraph.Text}");
            await ReplyAsync("Updated text", embed: embedParagraph());
            return new BotResult();
        }
    }
}
