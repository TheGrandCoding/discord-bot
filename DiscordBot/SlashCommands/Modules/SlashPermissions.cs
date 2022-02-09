using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [Group("commands", "List and modify slash commands in this guild")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class SlashPermissions : BotSlashBase
    {

        [SlashCommand("list", "Lists all commands in guild; optionally only those with the prefix")]
        public async Task ListCommands(string prefix = null, int page = 0)
        {
            try
            {
                await DeferAsync(true);
                var commands = await Program.Client.Rest.GetGuildApplicationCommands(Context.Guild.Id);
                var pages = new List<EmbedBuilder>();
                var embed = new EmbedBuilder();
                bool f = !string.IsNullOrWhiteSpace(prefix);
                embed.Title = "Slash Commands" + (f ? " /" + prefix : "");
                embed.Description = "";
                foreach(var cmd in commands)
                {
                    var row = getCmdRow(cmd) + "\r\n";
                    if(embed.Description.Length + row.Length > 2048)
                    {
                        embed.WithFooter($"Page {pages.Count}");
                        pages.Add(embed);
                        embed = new EmbedBuilder();
                        embed.Title = "Slash Commands" + (f ? " /" + prefix : "");
                        embed.Description = "";
                    }
                    embed.Description += row;
                }
                embed.WithFooter($"Page {pages.Count}");
                pages.Add(embed);
                if(page < 0 || page >= pages.Count)
                {
                    await FollowupAsync($":x: Index out of range: [0:{pages.Count - 1}]", embeds: null);
                    return;
                }
                await FollowupAsync(embeds: new[] { pages[page].Build() });
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "CmdList");
                throw ex;
            }
        }

        string getCmdRow(RestApplicationCommand cmd)
        {
            bool hasChildren = cmd.Options != null && cmd.Options.Any(x => x.Type == ApplicationCommandOptionType.SubCommand || x.Type == ApplicationCommandOptionType.SubCommandGroup);
            if (!hasChildren)
                return $"`/{cmd.Name}` {cmd.Id} - {cmd.Description}" + (cmd.IsDefaultPermission ? "" : " [x]");
            var sb = new StringBuilder();
            sb.Append($"- {cmd.Id} {cmd.Description}\r\n");
            foreach(var option in cmd.Options)
            {
                sb.Append(getCmdRow(cmd.Name, option) + "\r\n");
            }
            return sb.ToString();
        }
        string getCmdRow(string parent, RestApplicationCommandOption cmd)
        {
            bool hasChildren = cmd.Options != null && cmd.Options.Any(x => x.Type == ApplicationCommandOptionType.SubCommand || x.Type == ApplicationCommandOptionType.SubCommandGroup);
            if (!hasChildren)
                return $"`/{parent} {cmd.Name}` - {cmd.Description}";
            var sb = new StringBuilder();
            foreach (var option in cmd.Options)
            {
                sb.Append(getCmdRow(parent + " " + cmd.Name, option) + "\r\n");
            }
            return sb.ToString();
        }
    

        [Group("permissions", "Get and set permissions for slash commands in this guild")]
        public class SlashPermissions_Permissions : BotSlashBase
        {
            public CommandHandlingService CommandHandlingService { get; set; }
            InteractionService Service => CommandHandlingService.InteractionService;

            string emote(bool value) => value ? "✅" : "❌";
            
            async Task<IApplicationCommand> getCommand(string input)
            {
                if (ulong.TryParse(input, out var id))
                {
                    var guildCmd = await Context.Guild.GetApplicationCommandAsync(id);
                    if (guildCmd != null)
                        return guildCmd;

                    return await Program.Client.GetGlobalApplicationCommandAsync(id);
                }
                var guildCommands = await Context.Guild.GetApplicationCommandsAsync();
                var cmd = guildCommands.FirstOrDefault(x => x.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
                if(cmd != null) 
                    return cmd;
                var global = await Program.Client.GetGlobalApplicationCommandsAsync();
                return global.FirstOrDefault(x => x.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
            }

            async Task<GuildApplicationCommandPermission> getPerms(IApplicationCommand cmd)
            {
                return await cmd.GetCommandPermissionAsync(Context.Guild.Id);
            }
            
            [SlashCommand("list", "Get all permission overwrites for the provided command")]
            public async Task ListCmdOverwrites([Autocomplete(typeof(ApplicationCommandHandler))]string commandId)
            {
                try
                {
                    await DeferAsync(true);
                    var cmd = await getCommand(commandId);
                    if(cmd == null)
                    {
                        await FollowupAsync(":x: Command does not exist.", embeds: null);
                        return;
                    }
                    GuildApplicationCommandPermission cmdPerms = await getPerms(cmd);
                    if (cmdPerms == null || cmdPerms.Permissions == null || cmdPerms.Permissions.Count == 0)
                    {

                        await FollowupAsync("Default: " + emote(cmd.IsDefaultPermission) + "\r\n" +
                            "Command has no specific permissions set", embeds: null);
                        return;
                    }
                    var embed  = new EmbedBuilder();
                    embed .Title = $"{cmd.Name} Permissions";
                    var sb = new StringBuilder();
                    sb.Append("Default: " + emote(cmd.IsDefaultPermission) + "\r\n");
                    foreach (var thing in cmdPerms.Permissions)
                    {
                        if (thing.TargetType == ApplicationCommandPermissionTarget.User)
                            sb.Append($"User <@!{thing.TargetId}>");
                        else
                            sb.Append($"Role <@&{thing.TargetId}>");
                        sb.Append(" " + emote(thing.Permission));
                        sb.Append("\r\n");
                    }
                    embed.Description = sb.ToString();
                    await FollowupAsync(embeds: new[] { embed.Build() },
                        allowedMentions: AllowedMentions.None);
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "ListSlash");
                }
            }

            async Task manageThing(string strId, ulong thingId, ApplicationCommandPermissionTarget thingType, bool? value)
            {
                await DeferAsync(true);
                try
                {
                    var command = await getCommand(strId);
                    if (command == null)
                    {
                        await FollowupAsync(":x: Command does not exist", embeds: null);
                        return;
                    }

                    GuildApplicationCommandPermission existingPerms = await getPerms(command);
                    var permBuilder = SlashCommandPermsBuilder.From(existingPerms);
                    var existingValue = permBuilder.Get(thingId);
                    if (value.HasValue)
                    {
                        if (existingValue != value.Value)
                        {
                            permBuilder.With(thingId, thingType, value.Value);
                            await command.ModifyCommandPermissionAsync(Context.Guild.Id, permBuilder.Build());
                        }
                        await FollowupAsync("Done!", embeds: null);
                    }
                    else
                    {
                        if (!existingValue.HasValue)
                            await FollowupAsync($"{thingType} has no explicit permission set.", embeds: null);
                        else
                            await FollowupAsync($"{thingType} permission for command: " + emote(existingValue.Value), embeds: null);
                    }
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "a");
                }
            }

            [SlashCommand("user", "Get or set the permission for a user to use the command")]
            public async Task GetUserOverwrite(
                [Autocomplete(typeof(ApplicationCommandHandler))] string commandId,
                SocketGuildUser user, bool? value = null)
            {
                await manageThing(commandId, user.Id, ApplicationCommandPermissionTarget.User, value);
            }
        
            [SlashCommand("role", "Get or set the permission for a role to use the command")]
            public async Task RoleOverwrite(
                [Autocomplete(typeof(ApplicationCommandHandler))] string commandId,
                SocketRole role, bool? value = null)
            {
                await manageThing(commandId, role.Id, ApplicationCommandPermissionTarget.Role, value);
            }

            [SlashCommand("clear", "Removes the overwrite for the provided thing (user or role)")]
            public async Task ClearOverwrite(
                [Autocomplete(typeof(ApplicationCommandHandler))] string commandId, 
                string thingId)
            {
                await DeferAsync(true);
                if(!ulong.TryParse(thingId, out var id))
                {
                    await FollowupAsync(":x: `thingId` must be a valid ulong of the user or role.", embeds: null);
                    return;
                }
                var cmd = await getCommand(commandId);
                if (cmd == null)
                {
                    await RespondAsync(":x: Command does not exist",
                        ephemeral: true, embeds: null);
                    return;
                }
                var existingPerms = await getPerms(cmd);
                var permBuilder = SlashCommandPermsBuilder.From(existingPerms);
                permBuilder.Remove(id);
                if(cmd is RestGuildCommand gl)
                {
                    await gl.ModifyCommandPermissions(permBuilder.Build());
                    await FollowupAsync($"Done!");
                } else
                {
                    await FollowupAsync($":x: Failed! This command type is illegal");
                }
            }

            [SlashCommand("default", "Set whether this command is enabled for @everyone by default")]
            public async Task SetDefaultOverwrite(
                [Autocomplete(typeof(ApplicationCommandHandler))] string commandId, bool value)
            {
                await DeferAsync(true);
                var cmd = await getCommand(commandId);
                if (cmd == null)
                {
                    await FollowupAsync(":x: Command does not exist", embeds: null);
                    return;
                }
                await cmd.ModifyAsync(x => { x.IsDefaultPermission = value; });
                await FollowupAsync("Done!\r\nNote: due to a Discord bug, global commands may appear greyed out but will still be executable.");
            }
        }
    }

    public class ApplicationCommandHandler : AutocompleteHandler
    {
        public async override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            if (!(context.Guild is SocketGuild guild)) return AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, "Invalid context");

            var value = autocompleteInteraction.Data.Current.Value as string;
            bool empty = string.IsNullOrWhiteSpace(value);

            var commands = new List<SocketApplicationCommand>();
            var guildCmds = await guild.GetApplicationCommandsAsync();
            var globalCmds = await Program.Client.GetGlobalApplicationCommandsAsync();

            foreach(var guildCmd in guildCmds)
            {
                if (empty || guildCmd.Name.Contains(value, StringComparison.OrdinalIgnoreCase))
                    commands.Add(guildCmd);
            }
            foreach(var globalCmd in globalCmds)
            {
                if (empty || globalCmd.Name.Contains(value, StringComparison.OrdinalIgnoreCase))
                    commands.Add(globalCmd);
            }

            var results = commands.Select(x => new AutocompleteResult($"{(x.IsGlobalCommand ? "G" : "")}{(x.IsDefaultPermission ? "": "D")}/{x.Name} • {x.Description}", $"{x.Id}"));
            return AutocompletionResult.FromSuccess(results.Take(20));
        }
    }

    public class SlashCommandPermsBuilder
    {
        public List<ApplicationCommandPermission> _perms = new List<ApplicationCommandPermission>();
        public static SlashCommandPermsBuilder From(GuildApplicationCommandPermission existingPerms)
        {
            var x = new SlashCommandPermsBuilder();
            if(existingPerms != null)
                x._perms = existingPerms.Permissions.ToList();
            return x;
        }

        public void Remove(ulong id)
        {
            _perms.RemoveAll(x => x.TargetId == id);
        }

        public ApplicationCommandPermission[] Build()
        {
            return _perms.ToArray();
        }

        public SlashCommandPermsBuilder With(ulong thingId, ApplicationCommandPermissionTarget thingType, bool value)
        {
            Remove(thingId);
            _perms.Add(new ApplicationCommandPermission(thingId, thingType, value));
            return this;
        }

        public bool? Get(ulong thingId)
        {
            return _perms.FirstOrDefault(x => x.TargetId == thingId)?.Permission;
        }
    }
}
