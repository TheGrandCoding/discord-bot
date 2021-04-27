﻿using Discord;
using Discord.Commands.Builders;
using Discord.Rest;
using Discord.SlashCommands;
using Discord.WebSocket;
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
            await Interaction.AcknowledgeAsync();
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

        string getCmdRow(RestApplicationCommand cmd)
        {
            bool hasChildren = cmd.Options.Any(x => x.Type == ApplicationCommandOptionType.SubCommand || x.Type == ApplicationCommandOptionType.SubCommandGroup);
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
            bool hasChildren = cmd.Options.Any(x => x.Type == ApplicationCommandOptionType.SubCommand || x.Type == ApplicationCommandOptionType.SubCommandGroup);
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
            [SlashCommand("list", "Get all permission overwrites for the provided command")]
            public async Task ListCmdOverwrites([ParameterName("command-id")][Required]string strId)
            {
                if (!ulong.TryParse(strId, out var id))
                {
                    await Interaction.RespondAsync(":x: Command ID must be a valid ulong",
                        flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
                var cmdPerms = await Interaction.Guild.GetGuildCommandPermissionsAsync(id);
                if (cmdPerms == null)
                {
                    await Interaction.RespondAsync("Command has no permissions",
                        flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
                var perms = cmdPerms.Permissions;
                if (perms.Count == 0)
                {
                    await Interaction.RespondAsync("Command has no permissions.",
                        flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
                var sb = new StringBuilder();
                foreach (var thing in perms)
                {
                    if (thing.Type == ApplicationCommandPermissionType.User)
                        sb.Append($"User <@!{thing.Id}>");
                    else
                        sb.Append($"Role <&{thing.Id}>");
                    sb.Append(" " + emote(thing.Permission));
                    sb.Append("\r\n");
                }
                await Interaction.RespondAsync($"Command permissions:\r\n{sb}");
            }

            [SlashCommand("user", "Get or set the permission for a user for the command")]
            public async Task GetUserOverwrite(
                [ParameterName("command-id")]
                [Required]
                string strId,
                [Required] SocketGuildUser user, bool? value = null)
            {
                if(!ulong.TryParse(strId, out var id))
                {
                    await Interaction.RespondAsync(":x: Id must be a valid ulong.",
                        flags: InteractionResponseFlags.Ephemeral);
                    return;
                }
                await Interaction.AcknowledgeAsync();
                try
                {
                    RestApplicationCommand command = await getCommand(id);
                    if(command == null)
                    {
                        await Interaction.FollowupAsync(":x: Command does not exist");
                        return;
                    }
                    var existingPerms = await command.GetPermissionsAsync(Interaction.Guild);
                    var permBuilder = SlashCommandPermsBuilder.From(existingPerms);
                    var existingValue = permBuilder.Get(user);
                    if(value.HasValue)
                    {
                        if(existingValue != value.Value)
                        {
                            permBuilder.With(user, value.Value);
                            await command.ModifyPermissionsAsync(Interaction.Guild, x => { x.Permissions = permBuilder.Build(); });
                        }
                        await Interaction.FollowupAsync("Done!");
                    } else {
                        if (!existingValue.HasValue)
                            await Interaction.FollowupAsync("User has no explicit permission set.");
                        else
                            await Interaction.FollowupAsync("User permission for command: " + emote(existingValue.Value));
                    }
                }catch (Exception ex)
                {
                    Program.LogMsg(ex, "a");
                }
            }
        
            public async error_errrr SetDefaultOverwrite()
            {
                // TODO:
            }
        }
    }
}