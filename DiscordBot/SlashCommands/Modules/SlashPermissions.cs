using Discord;
using Discord.Commands.Builders;
using Discord.Rest;
using Discord.SlashCommands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("commands", "List and modify slash commands in this guild")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class SlashPermissions : BotSlashBase
    {
        [SlashCommand("list", "Lists all commands in guild; optionally only those with the prefix")]
        public async Task ListCommands(string prefix = null, int page = 0)
        {
            try
            {
                await Interaction.DeferAsync(true);
                var commands = await Program.Client.Rest.GetGuildApplicationCommands(Interaction.Guild.Id);
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
                    await Interaction.FollowupAsync($":x: Index out of range: [0:{pages.Count - 1}]", embeds: null);
                    return;
                }
                await Interaction.FollowupAsync(embeds: new[] { pages[page].Build() });
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
                return $"`/{cmd.Name}` {cmd.Id} - {cmd.Description}" + (cmd.DefaultPermission ? "" : " [x]");
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
    

        [CommandGroup("permissions", "Get and set permissions for slash commands in this guild")]
        public class SlashPermissions_Permissions : BotSlashBase
        {

            string emote(bool value) => value ? "✅" : "❌";
            async Task<SocketApplicationCommand> getCommand(ulong id)
            {
                return (SocketApplicationCommand)(await Interaction.Guild.GetApplicationCommandAsync(id))
                    ?? await Program.Client.GetGlobalApplicationCommandAsync(id);
            }
            
            async Task<SocketApplicationCommand> getCommand(string input)
            {
                if (ulong.TryParse(input, out var id))
                    return await getCommand(id);
                var srv = Program.Services.GetRequiredService<SlashCommandService>();
                var cmds = await Interaction.Guild.GetApplicationCommandsAsync();
                var cmd = cmds.FirstOrDefault(x => x.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
                if (cmd != null)
                    return cmd;
                var global = await Program.Client.GetGlobalApplicationCommandsAsync();
                return global.FirstOrDefault(x => x.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
            }
            
            [SlashCommand("list", "Get all permission overwrites for the provided command")]
            public async Task ListCmdOverwrites([ParameterName("command")][Required]string strId)
            {
                try
                {
                    await Interaction.DeferAsync(true);
                    var cmd = await getCommand(strId);
                    if(cmd == null)
                    {
                        await Interaction.FollowupAsync(":x: Command does not exist.", embeds: null);
                        return;
                    }
                    GuildApplicationCommandPermission cmdPerms = await cmd.GetCommandPermission(Interaction.Guild.Id);
                    if (cmdPerms == null || cmdPerms.Permissions == null || cmdPerms.Permissions.Count == 0)
                    {

                        await Interaction.FollowupAsync("Default: " + emote(cmd.DefaultPermission) + "\r\n" +
                            "Command has no specific permissions set", embeds: null);
                        return;
                    }
                    var embed  = new EmbedBuilder();
                    embed .Title = $"{cmd.Name} Permissions";
                    var sb = new StringBuilder();
                    sb.Append("Default: " + emote(cmd.DefaultPermission) + "\r\n");
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
                    await Interaction.FollowupAsync(embeds: new[] { embed.Build() },
                        allowedMentions: AllowedMentions.None);
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "ListSlash");
                }
            }

            async Task manageThing(string strId, ulong thingId, ApplicationCommandPermissionTarget thingType, bool? value)
            {
                await Interaction.DeferAsync(true);
                try
                {
                    SocketApplicationCommand command = await getCommand(strId);
                    if (command == null)
                    {
                        await Interaction.FollowupAsync(":x: Command does not exist", embeds: null);
                        return;
                    }

                    GuildApplicationCommandPermission existingPerms = await command.GetCommandPermission(Interaction.Guild.Id);
                    var permBuilder = SlashCommandPermsBuilder.From(existingPerms);
                    var existingValue = permBuilder.Get(thingId);
                    if (value.HasValue)
                    {
                        if (existingValue != value.Value)
                        {
                            permBuilder.With(thingId, thingType, value.Value);
                            await command.ModifyCommandPermissions(permBuilder.Build().ToArray(), Interaction.Guild.Id);
                        }
                        await Interaction.FollowupAsync("Done!", embeds: null);
                    }
                    else
                    {
                        if (!existingValue.HasValue)
                            await Interaction.FollowupAsync($"{thingType} has no explicit permission set.", embeds: null);
                        else
                            await Interaction.FollowupAsync($"{thingType} permission for command: " + emote(existingValue.Value), embeds: null);
                    }
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "a");
                }
            }

            [SlashCommand("user", "Get or set the permission for a user to use the command")]
            public async Task GetUserOverwrite(
                [ParameterName("command")]
                [Required]
                string strId,
                [Required] SocketGuildUser user, bool? value = null)
            {
                await manageThing(strId, user.Id, ApplicationCommandPermissionTarget.User, value);
            }
        
            [SlashCommand("role", "Get or set the permission for a role to use the command")]
            public async Task RoleOverwrite(
                [ParameterName("command")]
                [Required]
                string strId,
                [Required] SocketRole role, bool? value = null)
            {
                await manageThing(strId, role.Id, ApplicationCommandPermissionTarget.Role, value);
            }

            [SlashCommand("clear", "Removes the overwrite for the provided thing (user or role)")]
            public async Task ClearOverwrite(
                [ParameterName("command")]
                [Required]
                string strId, 
                [ParameterName("id")]
                [Required] 
                string thingId)
            {
                await Interaction.DeferAsync(true);
                if(!ulong.TryParse(thingId, out var id))
                {
                    await Interaction.FollowupAsync(":x: `thingId` must be a valid ulong of the user or role.", embeds: null);
                    return;
                }
                var cmd = await getCommand(strId);
                if (cmd == null)
                {
                    await Interaction.RespondAsync(":x: Command does not exist",
                        ephemeral: true, embeds: null);
                    return;
                }
                var existingPerms = await cmd.GetCommandPermission(Interaction.Guild.Id);
                var permBuilder = SlashCommandPermsBuilder.From(existingPerms);
                permBuilder.Remove(id);
                await cmd.ModifyCommandPermissions(permBuilder.Build().ToArray(), Interaction.Guild.Id);
                await Interaction.FollowupAsync("Done!", embeds: null);
            }

            [SlashCommand("default", "Set whether this command is enabled for @everyone by default")]
            public async Task SetDefaultOverwrite(
                [ParameterName("command")]
                [Required]
                string strId, [Required] bool value)
            {
                await Interaction.DeferAsync(true);
                var cmd = await getCommand(strId);
                if (cmd == null)
                {
                    await Interaction.FollowupAsync(":x: Command does not exist", embeds: null);
                    return;
                }
                if(cmd.IsGlobalCommand)
                {
                    await cmd.ModifyCommandPermissions(
                        new SlashCommandPermsBuilder()
                            .With(Interaction.Guild.Id, ApplicationCommandPermissionTarget.Role, value) // @everyone role
                            .Build().ToArray(), Interaction.Guild.Id);
                    await Interaction.FollowupAsync("Done!\r\nNote: due to a Discord bug, the command will appear greyed out but will still be executable.", embeds: null);
                } else
                {
                    await cmd.ModifyAsync<ApplicationCommandProperties>(x => x.DefaultPermission = value);
                    await Interaction.FollowupAsync("Done!", embeds: null);
                }
            }
        }
    }
}
