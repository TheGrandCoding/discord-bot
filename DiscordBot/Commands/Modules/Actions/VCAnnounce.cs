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

namespace DiscordBot.Commands.Modules.Actions
{
    [Group("vc")]
    [Name("VC Announce")]
    public class VCAnnounce : BotBase
    {
        public VCAnnounceService Service { get; set; }
        [Command("join")]
        [Summary("If attachment, sets join sound to mp3 attachment. If no attachment, removes any join sound.")]
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

        [Command("release")]
        public async Task ClearLock()
        {
            Service.lck.Release(Service.waiting);
            Service.waiting = 0;
            await ReplyAsync("OK.");
        }
    }
}
