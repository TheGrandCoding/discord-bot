using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Classes.Attributes;
using EduLinkDLL;
using EduLinkDLL.Classes;
using EduLinkDLL.Exceptions;
using Html2Markdown;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Services.EduLink
{
    [RequireService(typeof(ReactionService), typeof(ELTimetableService))]
    public class ELHomeworkService : SavedService
    {
        private static ELHomeworkService instance;
        public ELHomeworkService()
        {
            instance = this;
        }
        private EduLinkService EduLink { get; set; }
        private ReactionService Reaction { get; set; }

        public List<int> SeenHomeworks { get; set; }
        public Dictionary<ulong, HomeworkPreferences> Info { get; set; }

        public SocketGuild Guild { get; private set; }
        public SocketCategoryChannel Category { get; private set; }

        Dictionary<string, ITextChannel> _chnlCache = new Dictionary<string, ITextChannel>();
        Dictionary<int, DiscordHomework> _hwkCache = new Dictionary<int, DiscordHomework>();

        class save
        {

            public List<int> seen { get; set; }
            public Dictionary<ulong, HomeworkPreferences> info { get; set; }
        }

        public override string GenerateSave()
        {
            var s = new save()
            {
                seen = SeenHomeworks.Take(50).ToList(),
                info = Info
            };
            return Program.Serialise(s);
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
            var s = Program.Deserialise<save>(ReadSave());
            SeenHomeworks = s.seen ?? new List<int>();
            Info = s.info ?? new Dictionary<ulong, HomeworkPreferences>();
        }

        public override void OnLoaded()
        {
#if DEBUG
            OnDailyTick();
#endif
        }

        public override void OnDailyTick()
        {
            printHomeworks().Wait();
        }

        async Task<List<DiscordHomework>> getAllHomeworksDue()
        {
            Dictionary<int, DiscordHomework> homeworks = new Dictionary<int, DiscordHomework>();
            foreach(var keypair in EduLink.Clients)
            {
                var client = keypair.Value;
                var grouped = await client.HomeworkAsync();
                foreach(var hwk in grouped.Current)
                {
                    if (hwk.Subject == "-1" || hwk.Subject == "Other")
                        continue;
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

        bool isNew(DiscordHomework x)
        {
            if (SeenHomeworks.Contains(x.Homework.Id))
                return false;
            SeenHomeworks.Add(x.Homework.Id);
            return true;
        }

        async Task<List<DiscordHomework>> getHwksToPrint()
        {
            var ls = new List<DiscordHomework>();
            foreach(var x in await getAllHomeworksDue())
            {
                if(x.Homework.DueText == "tomorrow" || x.Homework.DueText == "today" || isNew(x))
                {
                    ls.Add(x);
                    await x.Homework.GetDetails(); // gets description
                }
            }
            return ls;
        }
        async Task printHomeworks()
        {
            try
            {
                var printing = await getHwksToPrint();
                foreach(var hwk in printing)
                {
                    _hwkCache[hwk.Homework.Id] = hwk;
                    var subject = reduceSubjectAliases(hwk.Homework.Subject);
                    var chnl = getSubjectChannel(subject);
                    var embed = hwk.ToEmbed(EduLink);
                    hwk.LatestMessage = await chnl.SendMessageAsync(embed: embed.Build());
                    await hwk.LatestMessage.AddReactionAsync(Emotes.WHITE_CHECK_MARK);
                    Reaction.Register(hwk.LatestMessage, EventAction.Added | EventAction.Removed, handleReaction, hwk.Homework.Id.ToString());
                }
            }
            catch (Exception ex)
            {
                Program.LogMsg("EduHwk", ex);
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
                    x.Embed = homework.ToEmbed(EduLink).Build();
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

        public List<BotUser> GetStudents(EduLinkService service)
        {
            if (Homework.Subject == "-1") // "Other"? impossible to tell.
                return new List<BotUser>();
            var classes = new List<string>();
            foreach(var usr in Homeworks.Keys)
            {
                try
                {
                    var id = service.Clients.FirstOrDefault(x => x.Value.CurrentUser.UserName == usr.CurrentUser.UserName).Key;
                    var bUser = Program.GetUserOrDefault(id);
                    var keypair = bUser.Classes.FirstOrDefault(x => x.Value == Homework.Subject);
                    if (keypair.Key != null && !classes.Contains(keypair.Key))
                        classes.Add(keypair.Key);
                } catch (Exception ex)
                {
                    Program.LogMsg($"{usr.CurrentUser.UserName}-ELH", ex);
                }
            }
            var students = new List<BotUser>();
            foreach(var user in Program.Users)
            {
                foreach(var sub in classes)
                {
                    if (user.Classes.ContainsKey(sub))
                    {
                        students.Add(user);
                        break;
                    }
                }
            }
            return students;
        }

        public Homework Homework => Homeworks.Values.FirstOrDefault();

        public IUserMessage LatestMessage { get; set; }

        public EmbedBuilder ToEmbed(EduLinkService s)
        {
            var builder = new EmbedBuilder();
            builder.Title = Program.Limit(Homework.Activity, 256);
            var md = new Converter().Convert(Homework.Description);
            builder.Description = Program.Limit(md, 2048);
            builder.WithTimestamp(Homework.DueDate);
            builder.Author = new EmbedAuthorBuilder()
                .WithName(Homework.SetBy);
            string dueFor = "";
            var students = GetStudents(s);
            foreach(var student in students)
            {
                var client = s.Clients.GetValueOrDefault(student.Id);
                string done = client == null ? "" : Homeworks[client].Status == "Submitted" ? "✅" : "";
                dueFor += $"- {student.Name} {done}\n";
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
