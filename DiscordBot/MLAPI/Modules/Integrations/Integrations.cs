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

        public InteractionResponse GetResponse(Interaction interaction)
        {
            var signature = Context.Request.Headers["X-Signature-Ed25519"];
            signature ??= Context.Request.Headers["X-Signature-Ed25519".ToLower()];
            var timestamp = Context.Request.Headers["X-Signature-Timestamp"];
            timestamp ??= Context.Request.Headers["X-Signature-Timestamp".ToLower()];
            Program.LogMsg($"Verifying {timestamp} with {signature}");
            var message = timestamp + Context.Body;
            var publicKey = Program.Configuration["tokens:publickey"];
            if(!PublicKeyAuth.VerifyDetached(
                Sodium.Utilities.HexToBinary(signature), 
                Encoding.UTF8.GetBytes(message),
                Sodium.Utilities.HexToBinary(publicKey)))
            {
                Program.LogMsg($"Failed verification.");
                throw new RedirectException(null, null);
            }
            Program.LogMsg($"Suceeded verification");
            if (interaction.Type == InteractionType.Ping)
                return new InteractionResponse(InteractionResponseType.Pong);
            return new InteractionResponse(InteractionResponseType.Acknowledge);
        }

        void executeCommand(Interaction interaction)
        {
            var context = new InteractionCommandContext(interaction);
            context.Channel = (IMessageChannel)Program.Client.GetChannel(interaction.ChannelId);
            context.Guild = Program.Client.GetGuild(interaction.GuildId);
            context.User = Program.Client.GetUser(interaction.Member.User.Id);
            context.BotUser = Program.GetUser(context.User);

            var moduleTypes = Assembly.GetAssembly(typeof(InteractionBase)).GetTypes()
                .Where(x => x == typeof(InteractionBase));
            MethodInfo method = null;
            foreach(var module in moduleTypes)
            {
                var commands = module.GetMethods()
                    .Where(x => x.ReturnType == typeof(Task));
                foreach(var cmd in commands)
                {
                    var id = cmd.GetCustomAttribute<IdAttribute>();
                    if(id != null && id.Id == interaction.Data.Id)
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
            foreach(var option in interaction.Data.Options)
            {
                args.Add(option.Value);
            }
            Program.LogMsg($"Invoking cmd with {args.Count} args");
            var expected = method.GetParameters().Count();
            while (args.Count < expected)
                args.Add(null);
            method.Invoke(obj, args.ToArray());
        }

        [Method("POST"), Path("/interactions/discord")]
        public void Receive()
        {
            var ping = Program.Deserialise<Interaction>(Context.Body);
            try
            {
                var response = GetResponse(ping);
                var str = Program.Serialise(response);
                Program.LogMsg($"Responding: {str}");
                RespondRaw(str);
                if(response.Type == InteractionResponseType.Acknowledge)
                {
                    executeCommand(ping);
                }
            } catch(RedirectException)
            {
                // Failed signature.
                RespondRaw("Signature failed.", 401);
            } 
            catch(Exception ex)
            {
                Program.LogMsg("Interactions", ex);
            }
        }
    }
}
