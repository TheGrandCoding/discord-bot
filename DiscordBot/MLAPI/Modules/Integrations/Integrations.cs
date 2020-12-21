﻿using DiscordBot.MLAPI.Exceptions;
using Newtonsoft.Json.Linq;
using Sodium;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Integrations
{
    public class Integrations : APIBase
    {
        public Integrations(APIContext context, string path) : base(context, path)
        {
        }

        public InteractionResponse GetResponse(Interaction interaction)
        {
            if(interaction.Type == InteractionType.Ping)
                return new InteractionResponse(InteractionResponseType.Acknowledge);
            var signature = Context.Request.Headers["X-Signature-Ed25519"];
            var timestamp = Context.Request.Headers["X-Signature-Timestamp"];
            var message = timestamp + Context.Body;
            var publicKey = Program.Configuration["tokens:publickey"];
            if(!PublicKeyAuth.VerifyDetached(
                Encoding.UTF8.GetBytes(signature), 
                Encoding.UTF8.GetBytes(message),
                Encoding.UTF8.GetBytes(publicKey)))
            {
                throw new RedirectException(null, null);
            }
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
            } catch(RedirectException)
            {
                // Failed signature.
                RespondRaw("Signature failed.", 401);
            } 
            catch(Exception ex)
            {
                Program.LogMsg("Interactions", ex);
            } finally
            {
                var str = Program.Serialise(response);
                RespondRaw(str);
            }
        }
    }
}
