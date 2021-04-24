using Discord;
using DiscordBot.MLAPI.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Sodium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.Integrations
{
    public class Integrations : APIBase
    {
        public Integrations(APIContext context) : base(context, "interactions")
        {
        }


        public bool isValidSignature(Interaction interaction)
        {
            var signature = Context.Request.Headers["X-Signature-Ed25519"];
            signature ??= Context.Request.Headers["X-Signature-Ed25519".ToLower()];
            var timestamp = Context.Request.Headers["X-Signature-Timestamp"];
            timestamp ??= Context.Request.Headers["X-Signature-Timestamp".ToLower()];
            Program.LogMsg($"Verifying {timestamp} with {signature}");
            var message = timestamp + Context.Body;
            var publicKey = Program.Configuration["tokens:publickey"];
#if !DEBUG
            if(!PublicKeyAuth.VerifyDetached(
                Sodium.Utilities.HexToBinary(signature), 
                Encoding.UTF8.GetBytes(message),
                Sodium.Utilities.HexToBinary(publicKey)))
            {
                Program.LogMsg($"Failed verification.");
                return false;
            }
#endif
            Program.LogMsg($"Suceeded verification");
            return true;
        }

        void executeCommand(Interaction interaction)
        {
            var context = new InteractionCommandContext(interaction);
            context.Channel = (IMessageChannel)Program.Client.GetChannel(interaction.ChannelId);
            context.Guild = Program.Client.GetGuild(interaction.GuildId);
            context.User = Program.Client.GetUser(interaction.Member.User.Id);
            context.BotUser = Program.GetUser(context.User);

            var type = typeof(InteractionBase);
            var moduleTypes = Assembly.GetAssembly(type).GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(type));
            MethodInfo method = null;
            foreach(var module in moduleTypes)
            {
                var commands = module.GetMethods()
                    .Where(x => x.ReturnType == typeof(Task));
                foreach(var cmd in commands)
                {
                    var ids = cmd.GetCustomAttributes<IdAttribute>();
                    if(ids != null && ids.Any(id => id.Id == interaction.Data.Id))
                    {
                        Program.LogMsg($"Found cmd: {cmd.Name}");
                        method = cmd;
                        break;
                    }
                }
                if (method != null)
                    break;
            }
            if (method == null)
                return;
            var obj = Activator.CreateInstance(method.DeclaringType, new object[1] { context });
            var options = interaction.Data.Options ?? new ApplicationCommandInteractionDataOption[0];
            var args = new List<object>();
            var paramaters = method.GetParameters();
            foreach (var param in paramaters)
            {
                var option = options.FirstOrDefault(x => x.Name == param.Name);
                object value = option?.Value ?? null;
                Program.LogMsg($"For {param.Name}: {value.GetType().Name} {value}", LogSeverity.Verbose);
                if (value == null && param.IsOptional == false)
                    throw new InvalidOperationException($"No argument specified for required item {param.Name}");
                if (value == null)
                {
                    Program.LogMsg($"For {param.Name}: Adding default value", LogSeverity.Verbose);
                    args.Add(param.DefaultValue);
                    continue;
                }
                if(param.ParameterType == value.GetType())
                {
                    Program.LogMsg($"For {param.Name}: Types match", LogSeverity.Verbose);
                    args.Add(value);
                } else
                {
                    var typeResult = Program.AttemptParseInput($"{value}", param.ParameterType);
                    if (typeResult.IsSuccess)
                    {
                        args.Add(typeResult.BestMatch);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Could not parse value for {param.Name} as {param.ParameterType.Name}: {typeResult.ErrorReason}");
                    }
                }
            }
            Program.LogMsg($"Invoking cmd with {args.Count} args");
            try
            {
                method.Invoke(obj, args.ToArray());
            }
            catch (TargetInvocationException outer)
            {
                Exception ex = outer.InnerException;
                try
                {
                    RespondRaw(Program.Serialise(
                        new InteractionResponse(InteractionResponseType.ChannelMessage, "Error: " + ex.Message)));
                }
                catch { }
                Program.LogMsg(ex, "CmdInteraction");
            }
            catch (Exception ex)
            {
                try
                {
                    RespondRaw(Program.Serialise(
                        new InteractionResponse(InteractionResponseType.ChannelMessage, "Error: " + ex.Message)));
                }
                catch { }
                Program.LogMsg(ex, "ExCmdInt");
            }
        }

        //[Method("POST"), Path("/interactions/discord")]
        // Now receiving over gateway.
        public void Receive()
        {
            var ping = Program.Deserialise<Interaction>(Context.Body);
            try
            {
                if(!isValidSignature(ping))
                {
                    RespondRaw("Signature failed.", 401);
                    return;
                }
                if(ping.Type == InteractionType.Ping)
                {
                    var pong = new InteractionResponse(InteractionResponseType.Pong);
                    RespondRaw(Program.Serialise(pong));
                    return;
                }
                if(ping.Type == InteractionType.ApplicationCommand)
                {
                    try
                    {
                        executeCommand(ping);
                    }
                    catch (Exception ex)
                    {
                        RespondRaw(Program.Serialise(
                        new InteractionResponse(InteractionResponseType.ChannelMessage, "Error: " + ex.Message)));
                        Program.LogMsg("EXecute", ex);
                    }
                }
            }
            catch(Exception ex)
            {
                Program.LogMsg("Interactions", ex);
            }
        }
    }
}
