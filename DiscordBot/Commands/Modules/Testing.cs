using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Commands;
using DiscordBot.Commands.Attributes;
using DiscordBot.Services;
using DiscordBot.Services.BuiltIn;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Modules
{
    [Summary("Testing Commands")]
    public class Testing : BotBase
    {
        [Command("confirm")]
        public async Task Confirm()
        {
            var result = await ConfirmAsync("Are you sure?");
            if (result == null)
                await ReplyAsync("You didn't reply");
            else
                await ReplyAsync($"You are {(result.Value ? "sure" : "not sure")}");
        }

        [Command("thread")]
        public async Task Thread()
        {
            var chnl = Context.Channel as SocketTextChannel;
            var thread = await chnl.CreateThreadAsync("Test Thread",
                autoArchiveDuration: ThreadArchiveDuration.OneHour, 
                message: Context.Message);
            await thread.SendMessageAsync($"A new thread has been opened!");
        }

        [Command("emote")]
        public async Task Emote(IEmote e)
        {
            await ReplyAsync(e.ToString());
        }

        [Command("delete")]
        public async Task Delete()
        {
            await Context.Message.DeleteAndTrackAsync("test");
        }

        public static void response(object sender, ReactionEventArgs e)
        {
            e.Message.ModifyAsync(x =>
            {
                x.Content = $"Reacted! Sent in response to {e.State}";
            });
        }

        [Command("count_mlapi")]
        [RequireContext(ContextType.Guild)]
        public async Task CountThings()
        {
            var byName = new Dictionary<string, List<MLAPI_Entry>>();
            var byDate = new Dictionary<string, List<MLAPI_Entry>>();
            int removed = 0;

            ulong? lastMessageId = null;
            ITextChannel chnl = Context.Channel as ITextChannel;
            while(true)
            {
                IEnumerable<IMessage> msgs;
                if(lastMessageId == null)
                {
                    msgs = await chnl.GetMessagesAsync().FlattenAsync();
                } else
                {
                    msgs = await chnl.GetMessagesAsync(lastMessageId.Value, Direction.Before).FlattenAsync();
                }

                foreach(IMessage msg in msgs)
                {
                    if (msg.Id == lastMessageId) continue;
                    if (lastMessageId.HasValue == false || msg.Id < lastMessageId)
                        lastMessageId = msg.Id;
                    if (!(msg.Author is IWebhookUser webh)) continue;

                    if (msg.Embeds.Count == 0) continue;
                    var embed = msg.Embeds.First();
                    if (embed.Title == "Reply" || embed.Title == "Reported Comment" || embed.Title.StartsWith("Inbox:") || embed.Title == "Error With Image") continue;

                    if (embed.Title == "Removed Comment")
                    {
                        removed++;
                        continue;
                    }

                    string type = embed.Description;
                    if (type.Contains('\n'))
                        type = type.Split('\n')[0];
                    int colonIndex = type.LastIndexOf(':');
                    if(colonIndex <= 0)
                        continue;

                    var entry = new MLAPI_Entry("yeet", embed.Footer.Value.Text, embed.Title, type.Substring(0, colonIndex), msg.CreatedAt);
                    if (entry.reason == "IgnorePost")
                        continue;
                    byName.AddInner(entry.reason, entry);
                    byDate.AddInner($"{entry.date:yyyy-MM-dd}", entry);
                }
                if (msgs.Count() < 100)
                    break;
            }

            int total = 0;
            foreach(var date in byDate)
            {
                File.AppendAllText(Path.Combine(Program.BASE_PATH, "by_dates.csv"), $"\"{date.Key}\",{date.Value.Count}\n");
                total += date.Value.Count;
            }
            foreach(var type in byName)
            {
                File.AppendAllText(Path.Combine(Program.BASE_PATH, "by_names.csv"), $"\"{type.Key}\",{type.Value.Count}\n");
            }
            File.WriteAllText(Path.Combine(Program.BASE_PATH, "dates.json"), JsonConvert.SerializeObject(byDate));
            await ReplyAsync($"Done! Counted {total} total, with {removed} removals.");
        }

        [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
        class MLAPI_Entry
        {
            public string postId;
            public string authorName;
            public string title;
            public string reason;
            public DateTimeOffset date;

            public MLAPI_Entry(string id, string name, string ttl, string rsn, DateTimeOffset datetime)
            {
                postId = id;
                authorName = name;
                title = ttl;
                reason = rsn;
                date = datetime;
            }

            private string GetDebuggerDisplay()
            {
                return $"{postId} /u/{authorName}: {title} {reason} @ {date:yyyy-MM-dd}";
            }
        }
    }
}
