using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class RequireGithubSignatureValid : APIPrecondition
    {
        private readonly string _name;
        public RequireGithubSignatureValid(string secretName)
        {
            _name = "tokens:github:" + secretName.Replace("/", ":");
        }
        public override bool CanChildOverride(APIPrecondition child)
        {
            return true;
        }
        public override PreconditionResult Check(APIContext context)
        {
            var eventName = context.Request.Headers["X-GitHub-Event"];
            var signature = context.Request.Headers["X-Hub-Signature"];
            var delivery = context.Request.Headers["X-GitHub-Delivery"];
            return IsValidSignature(context.Body, eventName, signature)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("Failed signature verification");
        }

        private const string Sha1Prefix = "sha1=";
        private bool IsValidSignature(string payload, string eventName, string signatureWithPrefix)
        {
            if (string.IsNullOrWhiteSpace(payload))
                throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentNullException(nameof(eventName));
            if (string.IsNullOrWhiteSpace(signatureWithPrefix))
                throw new ArgumentNullException(nameof(signatureWithPrefix));

            if (signatureWithPrefix.StartsWith(Sha1Prefix, StringComparison.OrdinalIgnoreCase))
            {
                var signature = signatureWithPrefix.Substring(Sha1Prefix.Length);
                var secret = Encoding.ASCII.GetBytes(Program.Configuration[_name]);
                var payloadBytes = Encoding.ASCII.GetBytes(payload);

                using (var hmSha1 = new HMACSHA1(secret))
                {
                    var hash = hmSha1.ComputeHash(payloadBytes);
                    var hashString = ToHexString(hash);
                    if (hashString.Equals(signature))
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        public static string ToHexString(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.AppendFormat("{0:x2}", b);
            }

            return builder.ToString();
        }
    }
}
