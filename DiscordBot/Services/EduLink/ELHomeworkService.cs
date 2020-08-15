using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using EduLinkDLL;
using EduLinkDLL.Classes;
using EduLinkDLL.Exceptions;
using Html2Markdown;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.EduLink
{
    [RequireService(typeof(EduLinkService), typeof(ReactionService))]
    public class ELHomeworkService : SavedService
    {
        private static ELHomeworkService instance;
        public ELHomeworkService()
        {
            instance = this;
        }
        private EduLinkService EduLink { get; set; }
        private ReactionService Reaction { get; set; }

        public Dictionary<ulong, HomeworkPreferences> Info { get; set; }

        public SocketGuild Guild { get; private set; }
        public SocketCategoryChannel Category { get; private set; }

        Dictionary<string, ITextChannel> _chnlCache = new Dictionary<string, ITextChannel>();
        Dictionary<int, DiscordHomework> _hwkCache = new Dictionary<int, DiscordHomework>();

        public override string GenerateSave()
        {
            return Program.Serialise(Info);
        }

        string reduceSubjectAliases(string s)
        {
            return s switch
            {
                "Computer S" => "Computing",
                _ => s
            };
        }

        ITextChannel getSubjectChannel(string subject)
        {
            var name = subject.ToLower().Replace(" ", "-").Replace(".", "");
            if (_chnlCache.TryGetValue(name, out var c))
                return c;
            c = Guild.TextChannels.FirstOrDefault(x => x.CategoryId == Category.Id && x.Name == name);
            if(c != null)
            {
                _chnlCache[name] = c;
                return c;
            }
            c = Guild.CreateTextChannelAsync(name, x =>
            {
                x.CategoryId = Category.Id;
                x.Topic = $"Homework for {subject}";
            }).Result;
            _chnlCache[name] = c;
            return c;
        }

        public override void OnReady()
        {
            EduLink = Program.Services.GetRequiredService<EduLinkService>();
            Reaction = Program.Services.GetRequiredService<ReactionService>();
            Guild = Program.Client.GetGuild(ulong.Parse(Program.Configuration["guilds:edulink"]));
            Category = Guild.CategoryChannels.FirstOrDefault(x => x.Name == "homework");
            Info = Program.Deserialise<Dictionary<ulong, HomeworkPreferences>>(ReadSave());
        }

        public override void OnDailyTick()
        {
            printHomeworks().Wait();
        }

        async Task<List<DiscordHomework>> getAllHomeworksDue()
        {
            Dictionary<int, DiscordHomework> homeworks = new Dictionary<int, DiscordHomework>();
            foreach(var client in EduLink.Clients.Values)
            {
                var grouped = await client.HomeworkAsync();
                foreach(var hwk in grouped.Current)
                {
                    if (homeworks.TryGetValue(hwk.Id, out var dsh))
                    {
                        dsh.Homeworks[client] = hwk;
                    }
                    else
                    {
                        homeworks[hwk.Id] = new DiscordHomework()
                        {
                            Homeworks = new Dictionary<EduLinkClient, Homework>()
                            {
                                {client, hwk }
                            }
                        };
                    }
                }
            }
            return homeworks.Values.ToList();
        }
        async Task<List<DiscordHomework>> getHwksToPrint()
        {
            var ls = new List<DiscordHomework>();
            foreach(var x in await getAllHomeworksDue())
            {
                if(x.Homework.DueText == "tomorrow" || x.Homework.DueText == "today")
                {
                    ls.Add(x);
                    await x.Homework.GetDetails(); // gets description
                }
            }
            return ls;
        }
        async Task printHomeworks()
        {
            var printing = await getHwksToPrint();
            foreach(var hwk in printing)
            {
                _hwkCache[hwk.Homework.Id] = hwk;
                var subject = reduceSubjectAliases(hwk.Homework.Subject);
                var chnl = getSubjectChannel(subject);
                var embed = hwk.ToEmbed();
                hwk.LatestMessage = await chnl.SendMessageAsync(embed: embed.Build());
                await hwk.LatestMessage.AddReactionAsync(Emotes.WHITE_CHECK_MARK);
                Reaction.Register(hwk.LatestMessage, EventAction.Added | EventAction.Removed, handleReaction, hwk.Homework.Id.ToString());
            }
        }

        async Task handleAsync(ReactionEventArgs args)
        {
            if (!Emotes.WHITE_CHECK_MARK.Equals(args.Emote))
                return;
            if (!EduLink.Clients.TryGetValue(args.User.Id, out var client))
                return;
            if (!int.TryParse(args.State, out var hwkId))
                return;
            if (!_hwkCache.TryGetValue(hwkId, out var homework))
                return;
            if (!homework.Homeworks.TryGetValue(client, out var hwk))
                return;
            bool completed = args.Action == EventAction.Added ? true : false;
            try
            {
                await hwk.SetStatus(completed);
                await args.User.SendMessageAsync($"Homework has been marked {(completed ? "completed" : "not yet completed")}");
                await homework.LatestMessage.ModifyAsync(x =>
                {
                    x.Embed = homework.ToEmbed().Build();
                });
            }
            catch (EduLinkException ex)
            {
                await args.User.SendMessageAsync($":x: Failed to set status: {ex.Message}");
            }
        }

        public static void handleReaction(object sender, ReactionEventArgs args) 
        {
            instance.handleAsync(args).Wait();
        }
    }

    public class DiscordHomework
    {
        public Dictionary<EduLinkClient, Homework> Homeworks { get; set; } = new Dictionary<EduLinkClient, Homework>();

        public Homework Homework => Homeworks.Values.FirstOrDefault();

        public IUserMessage LatestMessage { get; set; }

        public EmbedBuilder ToEmbed()
        {
            var builder = new EmbedBuilder();
            builder.Title = $"#{Homework.Id} {Program.Limit(Homework.Activity, 256)}";
            var md = new Converter().Convert(Homework.Description);
            builder.Description = Program.Limit(md, 2048);
            builder.WithTimestamp(Homework.DueDate);
            builder.Author = new EmbedAuthorBuilder()
                .WithName(Homework.SetBy);
            string dueFor = "";
            foreach(var client in Homeworks.Keys)
            {
                string done = Homeworks[client].Status == "Submitted" ? "✅" : "";
                dueFor += $"- {client.CurrentUser.Forename} {done}\n";
            }
            if (!string.IsNullOrWhiteSpace(dueFor))
                builder.AddField("Students", dueFor, true);
            return builder;
        }
    }

    public class HomeworkPreferences
    {
        public bool Mention { get; set; }
    }


}
