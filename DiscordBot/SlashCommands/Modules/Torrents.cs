using Discord;
using Discord.Commands;
using Discord.SlashCommands;
using DiscordBot.Classes;
using DiscordBot.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DiscordBot.Services.JackettService;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("torrents")]
    public class Torrents : BotSlashBase
    {
        public MessageComponentService Components { get; set; }
        public JackettService Jackett { get; set; }


        static ConcurrentDictionary<string, TorrentSearchInfo> state = new ConcurrentDictionary<string, TorrentSearchInfo>();
        [SlashCommand("search", "Initialises a search for a torrent with the specified name")]
        public async Task Search([Required]string text, [ParameterName("private")]bool isPrivate = false)
        {
            await Interaction.AcknowledgeAsync(isPrivate ? InteractionResponseFlags.Ephemeral : InteractionResponseFlags.None);
            var builder = new ComponentBuilder();
            var values = Enum.GetValues(typeof(TorrentCategory));
            var slc = new SelectMenuBuilder()
                .WithLabel("Torrent Category(s)")
                .WithCustomId($"{Interaction.User.Id}.{AuthToken.Generate(12)}")
                .WithMinValues(1)
                .WithMaxValues(values.Length);
            var ls = new List<SelectMenuOptionBuilder>();
            foreach(TorrentCategory x in values)
            {
                var opt = new SelectMenuOptionBuilder(Enum.GetName(typeof(TorrentCategory), x).Replace("_", "/"), $"{(int)x}");
                ls.Add(opt);
            }
            slc.WithOptions(ls);
            builder.WithSelectMenu(slc);
            var msg = await Interaction.FollowupAsync("Please select one or more categories to search in", component: builder.Build(), embeds: null,
                ephemeral: isPrivate);
            var info = new TorrentSearchInfo()
            {
                Query = text,
                Ephemeral = isPrivate
            };
            state[slc.CustomId] = info;
            Components.Register(slc.CustomId, msg, categorySelected, doSave: false);
        }

        async Task categorySelected(CallbackEventArgs e)
        {
            if(!state.TryGetValue(e.ComponentId, out var info))
            {
                await e.Interaction.RespondAsync(":x: Unable to find state information, please run the command and try again",
                    ephemeral: true, embeds: null);
                return;
            }
            await e.Interaction.AcknowledgeAsync(info.Ephemeral ? InteractionResponseFlags.Ephemeral : InteractionResponseFlags.None);
            Components.Unregister(e.Message);
            var msg = await e.Interaction.FollowupAsync($"Fetching feed...", embeds: null,
                ephemeral: info.Ephemeral);
            var values = e.Interaction.Data.Values;
            info.Categories = values.Select(x => (TorrentCategory)int.Parse(x)).ToArray();
            info.Message = msg;
            await Task.Run(async () =>
            {
                try
                {
                    await search(info).ConfigureAwait(false);
                } catch(Exception ex)
                {
                    Program.LogError(ex, "Torrents");
                    try
                    {
                        await msg.ModifyAsync(x => x.Content = "An internal error occured: " + ex.Message);
                    }
                    catch { }
                }
            });
    }

        const int pageLength = 25;
        async Task search(TorrentSearchInfo info)
        {
            var items = await Jackett.SearchAsync(info.Site, info.Query, info.Categories);

            var start = info.Page * pageLength;
            var end = start + pageLength;
            var relevant = items[new Range(start, end)];

            var builder = new EmbedBuilder();
            builder.Title = $"Results for '{info.Query}'";
            builder.WithFooter($"{info.Site}|{string.Join(",", info.Categories.Cast<int>())}");

            foreach(var x in relevant)
            {
                builder.AddField(x.Title, x.Id);
            }

            await info.Message.ModifyAsync(x =>
            {
                x.Content = $"{info.Page}/{items.Length / pageLength}";
                x.Embed = builder.Build();
            });

        }

        class TorrentSearchInfo
        {
            public string Site { get; set; } = "all";
            public string Query { get; set; }
            public TorrentCategory[] Categories { get; set; }
            public bool Ephemeral { get; set; }
            public IUserMessage Message { get; set; }
            public int Page { get; set; } = 0;
        }
    }

}
