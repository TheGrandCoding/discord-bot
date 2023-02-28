using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class CleanSlateProtocol : Service
    {
        public override void OnLoaded(IServiceProvider services) => CleanTheSlate().Wait();
        public override void OnDailyTick() => CleanTheSlate().Wait();

        public async Task CleanTheSlate()
        {
#if DEBUG
            if (Program.DailyValidateFailed())
                return;
#endif
            var guild = Program.Client.GetGuild(365230804734967840);
            var chnl = guild.GetTextChannel(516708276851834926);
            IMessage last = null;
            int total = 0;
            int deleted = 0;
            var blk = new List<ulong>();
            do
            {
                IEnumerable<IMessage> messages;
                if (last == null)
                    messages = await chnl.GetMessagesAsync().FlattenAsync();
                else
                    messages = await chnl.GetMessagesAsync(last, Direction.Before).FlattenAsync();
                foreach(var msg in messages)
                {
                    total++;
                    if(msg.Author.Id == 133622884122886144 && msg.Attachments.Count > 0)
                    {
                        deleted++;
                        Debug($"{deleted:000}/{total:000} Would remove {msg.Id}, {msg.CreatedAt}", "CleanSlate");
                        blk.Add(msg.Id);
                    }
                    last = msg;
                }
            } while (last.CreatedAt.DayOfYear == DateTime.Now.DayOfYear || last.CreatedAt.DayOfYear == (DateTime.Now.DayOfYear-1));
#if !DEBUG
            while(blk.Count > 0)
            {
                await chnl.DeleteMessagesAsync(blk.Take(100), new RequestOptions()
                {
                    AuditLogReason = "Clean slate protocol"
                });
                blk = blk.Skip(100).ToList();
            }
#endif
            Info($"{deleted:000}/{total:000} removed", "CleanSlate");
        }
    }
}
