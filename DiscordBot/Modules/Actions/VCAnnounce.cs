using Discord;
using Discord.Commands;
using DiscordBot.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.Actions
{
    [Group("vc")]
    public class VCAnnounce : BotModule
    {
        public VCAnnounceService Service { get; set; }
        [Command("join")]
        public async Task<RuntimeResult> SetJoin()
        {
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if(attachment == null)
            {
                var f = Service.getMediaType(Context.User, "join");
                File.Delete(f);
                await ReplyAsync("Removed.");
            } else
            {
                if (!attachment.Filename.EndsWith(".mp3"))
                    return new BotResult("File must be an mp3.");
                var f = Service.getMediaType(Context.User, "join");
                Program.downloadFile(new Uri(attachment.Url), f);
                await ReplyAsync("Added.");
            }
            return new BotResult();
        }
    }
}
