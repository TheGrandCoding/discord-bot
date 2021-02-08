using DiscordBot.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class TimeTracker : APIBase
    {
        public TimeTracker(APIContext context) : base(context, "tracker")
        {

        }
        [Method("GET"), Path("/tracker")]
        public void Base()
        {
            var existing = Context.User.Tokens.FirstOrDefault(x => x.Name == AuthToken.TimeToken);
            if(existing == null)
            {
                existing = new AuthToken(AuthToken.TimeToken, 12, "html.tracker", "html.tracker.*");
                Context.User.Tokens.Add(existing);
            }
            RespondRaw(existing.Value);
        }
    }
}
