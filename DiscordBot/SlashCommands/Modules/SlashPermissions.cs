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
                await Interaction.AcknowledgeAsync(flags: InteractionResponseFlags.Ephemeral);
                var commands = await Interaction.Guild.GetGuildCommandsAsync();
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
                    await Interaction.FollowupAsync($":x: Index out of range: [0:{pages.Count - 1}]");
                    return;
                }
                await Interaction.FollowupAsync(embed: pages[page].Build());
            }
            catch (Exception ex)
            {
                Program.LogMsg("CmdList", ex);
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
            async Task<RestApplicationCommand> getCommand(ulong id)
            {
                return (RestApplicationCommand)(await Interaction.Guild.GetGuildCommandAsync(id))
                    ?? await Program.Client.GetGlobalCommandAsync(id);
            }
            
            async Task<RestApplicationCommand> getCommand(string input)
            {
                if (ulong.TryParse(input, out var id))
                    return await getCommand(id);
                var srv = Program.Services.GetRequiredService<SlashCommandService>();
                var cmds = await Interaction.Guild.GetGuildCommandsAsync();
                var cmd = cmds.FirstOrDefault(x => x.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
                if (cmd != null)
                    return cmd;
                var global = await Program.Client.GetGlobalCommandsAsync();
                return global.FirstOrDefault(x => x.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
            }
            
            [SlashCommand("list", "Get all permission overwrites for the provided command")]
            public async Task ListCmdOverwrites([ParameterName("command")][Required]string strId)
            {
                try
                {
                    await Interaction.AcknowledgeAsync(flags: InteractionResponseFlags.Ephemeral);
                    var cmd = await getCommand(strId);
                    if(cmd == null)
                    {
                        await Interaction.FollowupAsync(":x: Command does not exist.");
                        return;
                    }
                    var cmdPerms = await cmd.GetPermissionsAsync(Interaction.Guild);
                    if (cmdPerms == null || cmdPerms.Permissions == null || cmdPerms.Permissions.Count == 0)
                    {

                        await Interaction.FollowupAsync("Default: " + emote(cmd.DefaultPermission) + "\r\n" +
                            "Command has no specific permissions set");
                        return;
                    }
                    var embed  = new EmbedBuilder();
                    embed .Title = $"{cmd.Name} Permissions";
                    var sb = new StringBuilder();
                    sb.Append("Default: " + emote(cmd.DefaultPermission) + "\r\n");
                    foreach (var thing in cmdPerms.Permissions)
                    {
                        if (thing.Type == ApplicationCommandPermissionType.User)
                            sb.Append($"User <@!{thing.Id}>");
                        else
                            sb.Append($"Role <@&{thing.Id}>");
                        sb.Append(" " + emote(thing.Permission));
                        sb.Append("\r\n");
                    }
                    embed.Description = sb.ToString();
                    await Interaction.FollowupAsync(embed: embed.Build(),
                        allowedMentions: AllowedMentions.None);
                }
                catch (Exception ex)
                {
                    Program.LogMsg(ex, "ListSlash");
                }
            }

            async Task manageThing(string strId, ulong thingId, ApplicationCommandPermissionType thingType, bool? value)
            {
                await Interaction.AcknowledgeAsync(flags: InteractionResponseFlags.Ephemeral);
                try
                {
                    RestApplicationCommand command = await getCommand(strId);
                    if (command == null)
                    {
                        await Interaction.FollowupAsync(":x: Command does not exist");
                        return;
                    }
                    var existingPerms = await command.GetPermissionsAsync(Interaction.Guild);
                    var permBuilder = SlashCommandPermsBuilder.From(existingPerms);
                    var existingValue = permBuilder.Get(thingId);
                    if (value.HasValue)
                    {
                        if (existingValue != value.Value)
                        {
                            permBuilder.With(thingId, thingType, value.Value);
                            await command.ModifyPermissionsAsync(Interaction.Guild, x => { x.Permissions = permBuilder.Build(); });
                        }
                        await Interaction.FollowupAsync("Done!");
                    }
                    else
                    {
                        if (!existingValue.HasValue)
                            await Interaction.FollowupAsync($"{thingType} has no explicit permission set.");
                        else
                            await Interaction.FollowupAsync($"{thingType} permission for command: " + emote(existingValue.Value));
                    }
                }
                catch (Exception ex)
                {
                    Program.LogMsg(ex, "a");
                }
            }

            [SlashCommand("user", "Get or set the permission for a user to use the command")]
            public async Task GetUserOverwrite(
                [ParameterName("command")]
                [Required]
                string strId,
                [Required] SocketGuildUser user, bool? value = null)
            {
                await manageThing(strId, user.Id, ApplicationCommandPermissionType.User, value);
            }
        
            [SlashCommand("role", "Get or set the permission for a role to use the command")]
            public async Task RoleOverwrite(
                [ParameterName("command")]
                [Required]
                string strId,
                [Required] SocketRole role, bool? value = null)
            {
                await manageThing(strId, role.Id, ApplicationCommandPermissionType.Role, value);
            }

            [SlashCommand("clear", "Removes the overwrite for the provided thing (user or role)")]
            public async Task ClearOverwrite(
                [ParameterName("command")]
                [Required]
                string strId, [Required] string thingId)
            {
                await Interaction.AcknowledgeAsync(flags: InteractionResponseFlags.Ephemeral);
                if(!ulong.TryParse(thingId, out var id))
                {
                    await Interaction.FollowupAsync(":x: `thingId` must be a valid ulong of the user or role.");
                    return;
                }
                var cmd = await getCommand(strId);
                if (cmd == null)
                {
                    await Interaction.RespondAsync(":x: Command does not exist",
                        flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
                var existingPerms = await cmd.GetPermissionsAsync(Interaction.Guild);
                var permBuilder = SlashCommandPermsBuilder.From(existingPerms);
                permBuilder.Remove(id);
                await cmd.ModifyPermissionsAsync(Interaction.Guild, x => x.Permissions = permBuilder.Build());
                await Interaction.FollowupAsync("Done!");
            }

            [SlashCommand("default", "Set whether this command is enabled for @everyone by default")]
            public async Task SetDefaultOverwrite(
                [ParameterName("command")]
                [Required]
                string strId, [Required] bool value)
            {
                await Interaction.AcknowledgeAsync(flags: InteractionResponseFlags.Ephemeral);
                var cmd = await getCommand(strId);
                if (cmd == null)
                {
                    await Interaction.FollowupAsync(":x: Command does not exist");
                    return;
                }
                if(cmd is RestGlobalCommand glb)
                {
                    await glb.ModifyPermissionsAsync(Interaction.Guild, x =>
                    {
                        x.Permissions = new SlashCommandPermsBuilder()
                            .With(Interaction.Guild.Id, ApplicationCommandPermissionType.Role, value) // @everyone role
                            .Build();
                    });
                    await Interaction.FollowupAsync("Done!\r\nNote: due to a Discord bug, the command will appear greyed out but will still be executable.");
                } else if(cmd is RestGuildCommand gld)
                {
                    await gld.ModifyAsync(x => x.DefaultPermission = value);
                    await Interaction.FollowupAsync("Done!");
                }
            }
        }
    }
}
