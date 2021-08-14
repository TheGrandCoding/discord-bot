using Discord;
using Discord.Commands;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class CommandHandlingService
    {
        private readonly CommandService _commands;
        private readonly SlashCommandService _slashCommands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;

        public CommandHandlingService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _slashCommands = services.GetRequiredService<SlashCommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            _slashCommands.CommandExecuted += SlashCommandExecutedAsync;
            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
            _discord.Ready += _discord_Ready;
        }

        private async Task _discord_Ready()
        {
#if DEBUG
            var guildIds = new List<ulong>() { 420240046428258304 };
#else
            var guildIds = Client.Guilds.Select(x => x.Id).ToList();
#endif
            try
            {
                await _slashCommands.RegisterCommandsAsync(Program.Client, guildIds, new CommandRegistrationOptions(OldCommandOptions.DELETE_UNUSED, ExistingCommandOptions.OVERWRITE));
            }
            catch (Discord.Net.HttpException http)
            {
                Program.LogError(http, "RegisterCommands");
                Program.LogError($"Request: {http.Request.ToString()}", "RegisterCommands");
                Program.LogError($"Response: {http.Error.ToString()}", "RegisterCommands");
                Program.Close(1);
            }
            Program.Client.InteractionCreated += executeInteraction;
        }

        public async Task InitializeAsync()
        {
            // Register modules that are public and inherit ModuleBase<T>.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        async Task executeInteraction(SocketInteraction x)
        {
            if (Program.ignoringCommands)
                return;
            var components = Program.Services.GetRequiredService<MessageComponentService>();
            try
            {
                IResult result;
                if (x.Type == InteractionType.ApplicationCommand)
                {
                    result = await _slashCommands.ExecuteAsync(x as SocketSlashCommand, Program.Services).ConfigureAwait(false);
                }
                else if (x.Type == InteractionType.MessageComponent)
                {
                    Program.LogDebug($"Executing message componenet {x.Id}", "Interactions");
                    result = await components.ExecuteAsync(x as SocketMessageComponent).ConfigureAwait(false);
                    Program.LogInfo($"Executed interaction {x.Id}: {result.IsSuccess} {result.Error} {result.ErrorReason}", "Interactions");
                }
                else
                {
                    Program.LogInfo($"Unknown interaction type: {x.Type} {(int)x.Type}", "Interactions");
                    result = MiscResult.FromError("Unknown interaction type");
                }
                if (!result.IsSuccess)
                {
                    if (result is ExecuteResult exec && exec.Exception != null)
                    {
                        Program.LogError(exec.Exception, "InteractionInvoke");
                        try
                        {
                            await x.RespondAsync(":x: Internal exception occured whilst handling this interaction: " + exec.Exception.Message,
                                ephemeral: true);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError($"{x.Id} {x.User?.Id ?? 0} {ex}", "InteractionCreated");
            }
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Ignore system messages, or messages from other bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            // This value holds the offset where the prefix ends
            var argPos = 0;
            if (!message.HasCharPrefix(Program.Prefix, ref argPos)) return;

            var context = new BotCommandContext(_discord, message);

            await _commands.ExecuteAsync(context, argPos, _services);
        }

        string getExecuted(CommandInfo cmd, string input)
        {
            string log = "";
            input = input.Substring(1);
            foreach(var x in cmd.Aliases)
            {
                if(input.StartsWith(x))
                {
                    log = x;
                    input = input.Replace(x, "");
                    input = input.TrimStart();
                    break;
                }
            }
            if (cmd.Attributes.Any(x => x is SensitiveAttribute))
                return log + " [redacted]";
            var split = input.Split(' ');
            for(int i = 0; i < cmd.Parameters.Count && i < split.Length; i++)
            {
                var arg = cmd.Parameters[i];
                if (arg.Attributes.Any(x => x is SensitiveAttribute))
                {
                    log += " [redacted]";
                } else
                {
                    var end = arg.IsRemainder ? new Index(1, true) : new Index(i + 1);
                    if(arg.IsOptional && (i + 1) >= split.Length)
                    {
                        log += $" <{arg.DefaultValue}>";
                    } else
                    {
                        var value = split[new Range(new Index(i), end)];
                        log += " " + string.Join(' ', value);
                    }
                }
                if (arg.IsRemainder)
                    break;
            }
            return log;
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!command.IsSpecified)
            {
                var builder = await Commands.Modules.Help.getBuilder(context, context.Message.Content.Substring(1)); 
                await context.Channel.SendMessageAsync($":question: Unknown command", embed: builder.Build(),
                    allowedMentions: AllowedMentions.None);
                return;
            }

            var thing = getExecuted(command.Value, context.Message.Content);
            Program.LogInfo($"{context.User.Username}#{context.User.Discriminator}: {Program.Prefix}{thing}", "Cmd");


            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess)
                return;

            // the command failed, let's notify the user that something happened.
            await context.Channel.SendMessageAsync($":warning: {result}",
                allowedMentions: AllowedMentions.None);
        }

        async Task sendEnsured(SocketSlashCommand context, string content = null, Embed[] embeds = null, bool ephemeral = true, AllowedMentions allowedMentions = null)
        {
            try
            {
                await context.RespondAsync(content, embeds, ephemeral: ephemeral, allowedMentions: allowedMentions ?? AllowedMentions.None);
            }
            catch
            {
                await context.FollowupAsync(content, embeds, ephemeral: ephemeral, allowedMentions: allowedMentions ?? AllowedMentions.None);
            }
        }

        public async Task SlashCommandExecutedAsync(Optional<SlashCommandInfo> command, SocketSlashCommand context, IResult result)
        {
            if (!command.IsSpecified)
            {
                await sendEnsured(context, ":question: Unknown slash command invoked. This shouldn't technically be possible.");
                return;
            }

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess)
                return;

            // the command failed, let's notify the user that something happened.
            await sendEnsured(context, $":warning: {result}",
                allowedMentions: AllowedMentions.None);
        }
    }

}
