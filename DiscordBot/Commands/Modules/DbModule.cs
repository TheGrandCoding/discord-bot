using Discord;
using Discord.Commands;
using DiscordBot.Classes.DbContexts;
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
    public class DbModule : BotBase
    {
        static long cast(ulong s) 
        {
            unchecked
            {
                return (long)s;
            }
        }
        
        string formatCSV(IEnumerable<MsgModel> messages, LogContext db)
        {
            var sb = new StringBuilder();
            sb.Append($"Id,AuthorId,Timestamp,Content\r\n");
            foreach (var msg in messages)
            {
                sb.Append($"\"{msg.Message}\",");
                sb.Append($"\"{msg.Author}\",");
                sb.Append($"\"{SnowflakeUtils.FromSnowflake(msg.Message):HH:mm:ss.fff}\",");
                if (msg.ContentId.HasValue)
                {
                    var content = db.Contents.FirstOrDefault(x => x.Id == msg.ContentId.Value);
                    sb.Append($"\"{content.Content.Replace("\n", "\\n")}\"");
                }
                else
                {
                    sb.Append("\"<no content available>\"");
                }
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        string formatSimple(IEnumerable<MsgModel> models, LogContext db)
        {
            var sb = new StringBuilder();
            foreach (var msg in models) 
            {
                var content = msg.ContentId.HasValue ? db.Contents.FirstOrDefault(x => x.Id == msg.ContentId.Value).Content : "<no content available>";
                content = (content ?? "<null>").Replace("\n", "\\n");
                sb.Append($"{SnowflakeUtils.FromSnowflake(msg.Message):HH:mm} {msg.Author}: {content}\r\n");
            }
            return sb.ToString();
        }


        [Command("export")]
        public async Task Export(ulong channel, string format = "simple", ulong after = ulong.MinValue, ulong before = ulong.MaxValue)
        {
            using var db = Services.GetRequiredService<LogContext>();
            var chnlId = cast(channel);
            var messages = db.Messages.AsQueryable()
                .Where(x => x.ChannelId == chnlId)
                .ToList()
                .Where(x => x.Message >= after)
                .Where(x => x.Message <= before);
            string text;
            if(format == "simple")
            {
                text = formatSimple(messages, db);
            } else if (format == "csv")
            {
                text = formatCSV(messages, db);
            } else
            {
                await ReplyAsync("Error: 'format' must be simple or csv");
                return;
            }
            var path = Path.Combine(Path.GetTempPath(), $"{channel}.txt");
            File.WriteAllText(path, text);
            await Context.Channel.SendFileAsync(path);
        }
    }
}
