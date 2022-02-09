using Discord;
using Discord.Commands;
using Discord.Interactions;
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
        public const uint CommandVersion = 2;
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;

        public InteractionService InteractionService { get; private set; }

        public CommandHandlingService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
            _discord.Ready += _discord_Ready;
        }

        async Task registerCommands()
        {
            try
            {
                bool clearGuild = false;
#if DEBUG
                var guild = Program.Client.GetGuild(420240046428258304);
                var botMod = InteractionService.GetModuleInfo<DiscordBot.SlashCommands.Modules.BotDevCmds>();

                await InteractionService.AddModulesToGuildAsync(guild, true, InteractionService.Modules.ToArray());
#else
                await InteractionService.RegisterCommandsGloballyAsync();
                clearGuild = true;
                await InteractionService.AddModulesToGuildAsync(guild, clearGuild, botMod);
#endif


                Program.CommandVersions = CommandVersion;
            }
            catch (Discord.Net.HttpException http)
            {
                Program.LogError(http, "RegisterCommands");
                Program.LogError($"Request: {http.Request.ToString()}", "RegisterCommands");
                foreach (var err in http.Errors)
                {
                    foreach (var jsErr in err.Errors)
                    {
                        Program.LogError($"{err.Path} {jsErr.Code}: {jsErr.Message}", "RegisterCommands");
                    }
                }
                Program.Close(1);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "RegisterCommands");
                Program.Close(1);
            }
        }

        private async Task _discord_Ready()
        {
            if(CommandVersion != Program.CommandVersions)
                await registerCommands();
            Program.Client.InteractionCreated += executeInteraction;
        }

        public async Task InitializeAsync(InteractionService slash)
        {
            InteractionService = slash;
            InteractionService.SlashCommandExecuted += SlashCommandExecutedAsync;
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
                if (x.Type == InteractionType.ApplicationCommand)
                {
                    var ctx = new SocketInteractionContext(_discord, x);
                    var specialResult = await InteractionService.ExecuteCommandAsync(ctx, Program.Services).ConfigureAwait(false);
                    if(!specialResult.IsSuccess)
                    {
                        if(specialResult is Discord.Interactions.ExecuteResult sExe && sExe.Exception != null)
                        {
                            Program.LogError(sExe.Exception, "SlashCommand");
                            try
                            {
                                await x.RespondAsync(":x: Internal exception occured whilst handling this interaction: " + sExe.Exception.Message,
                                    ephemeral: true);
                            }
                            catch { }
                        }
                    }
                    return;
                }
                if(x.Type == InteractionType.ApplicationCommandAutocomplete)
                {
                    var ctx = new SocketInteractionContext<SocketAutocompleteInteraction>(_discord, x as SocketAutocompleteInteraction);
                    var specialResult = await InteractionService.ExecuteCommandAsync(ctx, _services).ConfigureAwait(false);
                    if (!specialResult.IsSuccess)
                    {
                        if (specialResult is Discord.Interactions.ExecuteResult sExe && sExe.Exception != null)
                        {
                            Program.LogError(sExe.Exception, "SlashCommand");
                            try
                            {
                                await x.RespondAsync(":x: Internal exception occured whilst handling this interaction: " + sExe.Exception.Message,
                                    ephemeral: true);
                            }
                            catch { }
                        }
                    }
                    return;

                } 

                Discord.Commands.IResult result;
                if (x.Type == InteractionType.MessageComponent)
                {
                    Program.LogDebug($"Executing message component {x.Id}", "Interactions");
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
                    if (result is Discord.Commands.ExecuteResult exec && exec.Exception != null)
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

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, Discord.Commands.IResult result)
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

        async Task sendEnsured(IInteractionContext context, string content = null, Embed[] embeds = null, bool ephemeral = true, AllowedMentions allowedMentions = null)
        {
            try
            {
                await context.Interaction.RespondAsync(content, embeds, ephemeral: ephemeral, allowedMentions: allowedMentions ?? AllowedMentions.None);
            }
            catch
            {
                await context.Interaction.FollowupAsync(content, embeds, ephemeral: ephemeral, allowedMentions: allowedMentions ?? AllowedMentions.None);
            }
        }

        public async Task SlashCommandExecutedAsync(SlashCommandInfo command, IInteractionContext context, Discord.Interactions.IResult result)
        {
            if (command == null)
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
