using DiscordBot.MLAPI.Exceptions;
using Newtonsoft.Json.Linq;
using Sodium;
using System;
using System.Collections.Generic;
using System.Text;

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
            if(interaction.Type == InteractionType.Ping)
                return new InteractionResponse(InteractionResponseType.Acknowledge);
            var signature = Context.Request.Headers["X-Signature-Ed25519"];
            signature ??= Context.Request.Headers["X-Signature-Ed25519".ToLower()];
            var timestamp = Context.Request.Headers["X-Signature-Timestamp"];
            timestamp ??= Context.Request.Headers["X-Signature-Timestamp".ToLower()];
            Program.LogMsg($"Verifying {timestamp} with {signature}");
            var message = timestamp + Context.Body;
            var publicKey = Program.Configuration["tokens:publickey"];
            if(!PublicKeyAuth.VerifyDetached(
                Encoding.UTF8.GetBytes(signature), 
                Encoding.UTF8.GetBytes(message),
                Encoding.UTF8.GetBytes(publicKey)))
            {
                Program.LogMsg($"Failed verification.");
                throw new RedirectException(null, null);
            }
            Program.LogMsg($"Suceeded verification");
            return new InteractionResponse(InteractionResponseType.ChannelMessage, content: "Hey!");
        }

        [Method("POST"), Path("/interactions/discord")]
        public void Receive()
        {
            var ping = Program.Deserialise<Interaction>(Context.Body);
            InteractionResponse response = new InteractionResponse(InteractionResponseType.ChannelMessage, "Command failed to execute");
            try
            {
                response = GetResponse(ping);
                var str = Program.Serialise(response);
                Program.LogMsg($"Responding: {str}");
                RespondRaw(str);
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
