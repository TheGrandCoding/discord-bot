using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Voting
{
    public class VoteUser : VoteBase
    {
        public VoteUser(APIContext c) : base(c) { }

        string toBase64(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }
        string fromBase64(string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }


        [Method("GET"), Path("/vote/nouser")]
        public void NoUser()
        {
            string content = toBase64(Context.Id.ToString()) + "." + toBase64(Context.User?.VerifiedEmail ?? "noemail");
            var email = Program.Configuration["vote:adminEmail"];
            email += "?subject=" + Uri.EscapeDataString("Awards Voting Issue");
            email += "&body=" + Uri.EscapeDataString($"Please do not edit this:\r\n" +
                $"{content}\r\n" +
                $"-----------------------------------------------\r\n" +
                $"Please provide any additional information, such as your tutor group:\r\n");
            ReplyFile("nouser.html", 200, new Replacements()
                .Add("email", email)
                .Add("username", EmailName));
        }
    }
}
