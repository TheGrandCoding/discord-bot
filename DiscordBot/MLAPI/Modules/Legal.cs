using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    [RequireApproval(false)]
    [RequireAuthentication(false)]
    [RequireVerifiedAccount(false)]
    public class Legal : APIBase
    {
        public Legal(APIContext context) : base(context, "legal") 
        {
        }

        [Method("GET"), Path("/terms")]
        public void Terms()
        {
            ReplyFile("terms.html", 200);
        }

        [Method("GET"), Path("/privacy")]
        public void Privacy()
        {
            ReplyFile("privacy.html", 200);
        }

        [Method("GET"), Path("/privacy/sar")]
        [RequireAuthentication(true)]
        public void SAR()
        {
            var accountData = Program.Serialise(Context.User, format: Newtonsoft.Json.Formatting.Indented);
            ReplyFile("sar.html", 200, new Replacements()
                .Add("account", accountData));
        }
    }
}
