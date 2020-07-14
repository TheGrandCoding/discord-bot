using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    public class CleanSlateProtocol : Service
    {
        public override void OnDailyTick()
        {
            var guild = Program.Client.GetGuild(365230804734967840);
            var chnl = guild.GetTextChannel(516708276851834926);
            IMessage last = null;
            int total = 0;
            int deleted = 0;
            do
            {
                IEnumerable<IMessage> messages;
                if (last == null)
                    messages = chnl.GetMessagesAsync().FlattenAsync().Result;
                else
                    messages = chnl.GetMessagesAsync(last, Direction.Before).FlattenAsync().Result;
                foreach(var msg in messages)
                {
                    total++;
                    if(msg.Author.Id == 133622884122886144 && msg.Attachments.Count > 0)
                    {
                        deleted++;
#if DEBUG
                        Program.LogMsg($"{deleted:000}/{total:000} Would remove {msg.Id}, {msg.CreatedAt}", LogSeverity.Debug, "CleanSlate");
#else
                        msg.DeleteAsync(new RequestOptions()
                        {
                            AuditLogReason = "Clean slate protocol"
                        });
#endif
                    }
                    last = msg;
                }
            } while (last.CreatedAt.DayOfYear == DateTime.Now.DayOfYear);
            Program.LogMsg($"{deleted:000}/{total:000} removed", LogSeverity.Info, "CleanSlate");
        }
    }
}
