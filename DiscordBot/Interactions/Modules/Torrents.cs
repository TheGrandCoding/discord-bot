using CodeHollow.FeedReader.Feeds;
using Discord;
using Discord.Rest;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static DiscordBot.Services.JackettService;

namespace DiscordBot.Interactions.Modules
{
    [Group("torrents", "Commands for torrents")]
    public class Torrents : BotSlashBase
    {
        public JackettService Jackett { get; set; }


        public static ConcurrentDictionary<ulong, TorrentSearchInfo> state = new ConcurrentDictionary<ulong, TorrentSearchInfo>();
        [SlashCommand("search", "Initialises a search for a torrent with the specified name")]
        public async Task Search(string text, 
            TorrentOrderBy orderBy,
            TorrentOrderDirection direction,
            bool isPrivate = false)
        {
            await DeferAsync(isPrivate);
            var builder = new ComponentBuilder();
            var values = Enum.GetValues(typeof(TorrentCategory));
            var slc = new SelectMenuBuilder()
                .WithCustomId($"torrents:select:{Context.User.Id}")
                .WithMinValues(1)
                .WithMaxValues(values.Length)
                .WithPlaceholder("Select torrent category");
            var ls = new List<SelectMenuOptionBuilder>();
            foreach(TorrentCategory x in values)
            {
                var opt = new SelectMenuOptionBuilder(Enum.GetName(typeof(TorrentCategory), x).Replace("_", "/"), $"{(int)x}");
                ls.Add(opt);
            }
            slc.WithOptions(ls);
            builder.WithSelectMenu(slc);
            var msg = await FollowupAsync("Please select one or more categories to search in", components: builder.Build(), embeds: null,
                ephemeral: isPrivate);
            var info = new TorrentSearchInfo()
            {
                Query = text,
                Ephemeral = isPrivate,
                Message = msg as RestFollowupMessage,
                OrderBy = orderBy
            };
            state[msg.Id] = info;
        }



        
    }

    public class TorrentSearchInfo
    {
        public string Site { get; set; } = "all";
        public string Query { get; set; }
        public TorrentCategory[] Categories { get; set; }
        public bool Ephemeral { get; set; }
        public RestFollowupMessage Message { get; set; }
        public int Page { get; set; } = 0;
        public TorrentOrderBy OrderBy { get; set; }
        public bool Ascending { get; set; }

        public TorrentInfo[] torrents { get; set; }

        public int MaxPages => (torrents?.Length ?? 0) / pageLength;
        public const int pageLength = 10;
    }
    public class TorrentInfo
    {
        public TorrentInfo(Rss20FeedItem item)
        {
            FeedItem = item;
            Torznabs = new Dictionary<string, string>();
            var np = item.Element.GetNamespaceOfPrefix("torznab");
            foreach (var el in item.Element.Elements(np + "attr"))
            {
                var name = el.Attribute("name").Value;
                var val = el.Attribute("value").Value;
                Torznabs[name] = val;
            }
        }
        public Rss20FeedItem FeedItem { get; }
        public Dictionary<string, string> Torznabs { get; set; }

        public string Title => FeedItem.Title;
        public string Url => FeedItem.Guid;
        public int Seeders => int.Parse(Torznabs.GetValueOrDefault("seeders", "0"));
        public int Peers => int.Parse(Torznabs.GetValueOrDefault("peers", "0"));

        public double Score
        {
            get
            {

                var seeds = Seeders;

                var diff = DateTime.Now - this.FeedItem.PublishingDate.Value;
                var age = diff.TotalSeconds;
                var oneOver = 1 / age;

                return seeds * oneOver;
            }
        }
    }

    public class TorrentComponents : BotComponentBase
    {
        public JackettService Jackett { get; set; }

        [ComponentInteraction("torrents:select:*")]
        public async Task categorySelected(string uId)
        {
            ulong userId = ulong.Parse(uId);
            if (!Torrents.state.TryGetValue(Context.Interaction.Message.Id, out var info))
            {
                await Context.Interaction.RespondAsync(":x: Unable to find state information, please run the command and try again",
                    ephemeral: true, embeds: null);
                return;
            }
            await Context.Interaction.DeferAsync(info.Ephemeral);
            var values = Context.Interaction.Data.Values;
            info.Categories = values.Select(x => (TorrentCategory)int.Parse(x)).ToArray();
            await Task.Run(async () =>
            {
                try
                {
                    await handle(Context.Interaction.Message.Id, info, Context.Interaction).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Program.LogError(ex, "Torrents");
                    try
                    {
                        await Context.Interaction.FollowupAsync($"An internal error occured whilst performing that task: {ex.Message}", embeds: null);
                    }
                    catch { }
                }
            });
        }

        [ComponentInteraction("torrents:move:*:*")]
        public async Task torrentMovePage(string id, string dir)
        {
            var state = ulong.Parse(id);
            if (!Torrents.state.TryGetValue(state, out var info))
                return;
            if (dir == "prev")
                info.Page = Math.Max(0, info.Page - 1);
            else
                info.Page = Math.Min(info.MaxPages, info.Page + 1);
            await Context.Interaction.DeferAsync(true);
            var builder = await getBuilder(info);
            await Context.Interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Embeds = new[] { builder.Build() };
                x.Components = getComponents(state, info).Build();
            });
        }


        async Task handle(ulong id, TorrentSearchInfo info, SocketMessageComponent interaction)
        {
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = $"Searching `{info.Query}` in {info.Site} trackers...";
                x.Components = new ComponentBuilder().Build();
            });
            var items = await Jackett.SearchAsync(info.Site, info.Query, info.Categories);
            var torrents = getOrderedInfos(info, items.Select(x => new TorrentInfo(x.SpecificItem as Rss20FeedItem)))
                .ToArray();

            var builder = await getBuilder(info);
            var components = getComponents(id, info);

            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = "";
                x.Embeds = new[] { builder.Build() };
                x.Components = components.Build();
            });

        }

        static async Task<EmbedBuilder> getBuilder(TorrentSearchInfo info)
        {
            var start = info.Page * TorrentSearchInfo.pageLength;
            var end = Math.Min(start + TorrentSearchInfo.pageLength, info.torrents.Length);
            var relevant = info.torrents[new Range(start, end)];

            var builder = new EmbedBuilder();
            builder.Title = $"Results for '{info.Query}'";
            builder.WithFooter($"{info.Page}/{info.MaxPages}");

            foreach (var x in relevant)
            {
                var title = $"[{x.Seeders}/{x.Peers}] {x.Title}";
                title = Program.Clamp(title, EmbedBuilder.MaxTitleLength);
                builder.AddField(title,
                    $"[Link]({x.Url}) | {TimestampTag.FromDateTime(x.FeedItem.PublishingDate.Value, TimestampTagStyles.Relative)}");
            }

            return builder;
        }
        static ComponentBuilder getComponents(ulong id, TorrentSearchInfo info)
        {
            var components = new ComponentBuilder();
            components.WithButton("Previous", $"torrents:move:{id}:prev", disabled: info.Page == 0);
            components.WithButton("Next", $"torrents:move:{id}:next", disabled: info.Page == info.MaxPages);
            return components;
        }

        IOrderedEnumerable<TorrentInfo> getOrderedInfos(TorrentSearchInfo info, IEnumerable<TorrentInfo> torrents)
        {
            var ord = info.OrderBy;
            var asc = info.Ascending;
            switch (ord)
            {
                case TorrentOrderBy.Time:
                    Func<TorrentInfo, DateTime> f = (TorrentInfo x) => x.FeedItem.PublishingDate.GetValueOrDefault(DateTime.MinValue);
                    return asc
                        ? torrents.OrderBy(f)
                        : torrents.OrderByDescending(f);
                case TorrentOrderBy.Seeds:
                    return asc
                        ? torrents.OrderBy(x => x.Seeders)
                        : torrents.OrderByDescending(x => x.Seeders);
                case TorrentOrderBy.Leechers:
                    return asc
                        ? torrents.OrderBy(x => x.Peers)
                        : torrents.OrderByDescending(x => x.Peers);
                default:
                    return asc
                        ? torrents.OrderBy(x => x.Score)
                        : torrents.OrderByDescending(x => x.Score);
            }
        }

    }

}
