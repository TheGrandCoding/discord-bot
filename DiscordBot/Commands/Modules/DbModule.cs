using Discord;
using Discord.Commands;
using DiscordBot.Services;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Group("db")]
    [RequireOwner]
    public class DbModule : BotModule
    {
        static long cast(ulong s) 
        {
            unchecked
            {
                return (long)s;
            }
        }
        [Command("export")]
        public async Task Export(ulong channel, ulong after = ulong.MinValue, ulong before = ulong.MaxValue)
        {
            using var db = Program.Services.GetRequiredService<LogContext>();
            var chnlId = cast(channel);
            var messages = db.Messages.AsQueryable()
                .Where(x => x.ChannelId == chnlId)
                .ToList()
                .Where(x => x.Message >= after)
                .Where(x => x.Message <= before);
            var sb = new StringBuilder();
            sb.Append($"Id,AuthorId,Timestamp,Content\r\n");
            foreach (var msg in messages)
            {
                sb.Append($"\"{msg.Message}\",");
                sb.Append($"\"{msg.Author}\",");
                sb.Append($"\"{SnowflakeUtils.FromSnowflake(msg.Message):HH:mm:ss.fff}\",");
                if(msg.ContentId.HasValue)
                {
                    var content = db.Contents.FirstOrDefault(x => x.Id == msg.ContentId.Value);
                    sb.Append($"\"{content.Content.Replace("\n", "\\n")}\"");
                } else
                {
                    sb.Append("\"<no content available>\"");
                }
                sb.Append("\r\n");
            }
            var path = Path.Combine(Path.GetTempPath(), $"{channel}.csv");
            File.WriteAllText(path, sb.ToString());
            await Context.Channel.SendFileAsync(path);
        }
    }
}
