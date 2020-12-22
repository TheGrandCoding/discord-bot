using Discord;
using DiscordBot.MLAPI.Exceptions;
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
    [RequireApproval(false)]
    [RequireAuthentication(false, false)]
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
            var args = new List<object>();
            var options = interaction.Data.Options ?? new ApplicationCommandInteractionDataOption[0];
            foreach(var option in options)
            {
                args.Add(option.Value);
            }
            Program.LogMsg($"Invoking cmd with {args.Count} args");
            var expected = method.GetParameters().Count();
            while (args.Count < expected)
                args.Add(null);
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

        [Method("POST"), Path("/interactions/discord")]
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
                    executeCommand(ping);
                }
            }
            catch(Exception ex)
            {
                Program.LogMsg("Interactions", ex);
            }
        }
    }
}
