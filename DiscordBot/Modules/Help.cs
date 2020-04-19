using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DiscordBot.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    [Name("Help Module")]
    public class Help : BotModule
    {
        public CommandService cmdService { get; set; }
        public IServiceProvider services { get; set; }
        private static List<string[]> brackets = new List<string[]>()
        {
            new string[] {"[", "]" },
            new string[] {"<", ">" },
        };
        [Command("help")]
        [Summary("Lists your available commands")]
        public async Task ListAll()
        {
            string content = $"";
            List<string> pages = new List<string>();
            foreach(var module in cmdService.Modules)
            {
                List<string> cmds = new List<string>();
                foreach(var cmd in module.Commands)
                {
                    string s = $"{Program.Prefix}**{cmd.Aliases.First()}**";
                    foreach(var arg in cmd.Parameters)
                    {
                        var br = arg.IsOptional ? brackets[0] : brackets[1];
                        s += $" {br[0]}{arg.Type.Name} {arg.Name}{br[1]}";
                    }
                    s += " - " + cmd.Summary;
                    var result = await cmd.CheckPreconditionsAsync(Context, services);
                    if (result.IsSuccess)
                        cmds.Add(s);
                }

                pages.Add($"**{module.Name ?? module.GetType().Name}**" +
                    $"\r\n" + string.Join("\r\n", cmds));
            }

            await PagedReplyAsync(new PaginatedMessage()
            {
                Pages = pages,
                Options = new PaginatedAppearanceOptions()
                {
                    DisplayInformationIcon = false,
                },
            });
        }
    
        static string getParamaterText(ParameterInfo param)
        {
            var s = $"`{param.Type.Name} {param.Name}";
            if (param.IsOptional)
                s += $" = {param.DefaultValue}";
            s += "`";
            if (!string.IsNullOrWhiteSpace(param.Summary))
                s += $" - {param.Summary}";
            string attrs = "";
            foreach(var c in param.Attributes)
            {
                attrs += $"{c.GetType().Name},";
            }
            if (!param.IsOptional)
                attrs += "Required,";
            if (!string.IsNullOrWhiteSpace(attrs))
            {
                s += $" [{attrs.Substring(0, attrs.Length - 1)}]";
            }
            return s;
        }

        static void addCommandField(CommandInfo cmd, EmbedBuilder builder)
        {
            string s = "";
            foreach (var arg in cmd.Parameters)
            {
                s += $"\n{getParamaterText(arg)}";
            }
            if (!string.IsNullOrWhiteSpace(cmd.Summary))
                s += "\n" + cmd.Summary;
            builder.AddField(Program.Prefix + cmd.Aliases.First(),
                string.IsNullOrWhiteSpace(s) ? "No arguments" : s, true);
        }

        public static async Task<EmbedBuilder> getBuilder(ICommandContext Context, string command)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Title = "Help - Commands";
            var cmdService = Program.Commands;
            var services = Program.Services;
            foreach (var cmd in await cmdService.GetExecutableCommandsAsync(Context, services))
            {
                if (cmd.Aliases.Contains(command))
                {
                    addCommandField(cmd, builder);
                }
            }
            builder.Color = Color.Green;
            if (builder.Fields.Count == 0)
            {
                builder.Description = $"No commands matched `{command}`, searching Modules instead";
                foreach (var mod in cmdService.Modules)
                {
                    if (mod.Name == command || mod.Aliases.Contains(command))
                    {
                        foreach (var cmd in await mod.GetExecutableCommandsAsync(Context, services))
                        {
                            addCommandField(cmd, builder);
                        }
                    }
                }
                if (builder.Fields.Count == 0)
                {
                    builder.AddField("No Commands", "Failed to find any commands you can execute in the current context");
                    builder.Color = Color.Red;
                }
                else
                {
                    builder.Color = Color.Blue;
                }
            }
            return builder;
        }

        [Command("help")]
        [Summary("View help on a specific command")]
        public async Task SeeSpecificHelp([Remainder]string command)
        {
            var builder = await getBuilder(Context, command);
            await ReplyAsync(embed: builder.Build());
        }
    }
}
