using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;

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
        }

        public async Task InitializeAsync()
        {
            // Register modules that are public and inherit ModuleBase<T>.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
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
                await context.Channel.SendMessageAsync($":question: Unknown command", embed: builder.Build());
                return;
            }

            var thing = getExecuted(command.Value, context.Message.Content);
            Program.LogMsg($"{context.User.Username}#{context.User.Discriminator}: {Program.Prefix}{thing}", LogSeverity.Info, "Cmd");


            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess)
                return;

            // the command failed, let's notify the user that something happened.
            await context.Channel.SendMessageAsync($":warning: {result}");
        }
    }
}
