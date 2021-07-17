using CodeHollow.FeedReader.Feeds;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.SlashCommands;
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
            var msg = await Interaction.FollowupAsync("Please select one or more categories to search in", component: builder.Build(), embeds: null,
                ephemeral: isPrivate);
            var info = new TorrentSearchInfo()
            {
                Query = text,
                Ephemeral = isPrivate,
                Message = msg
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
            await e.Interaction.RespondAsync(type: InteractionResponseType.DeferredUpdateMessage, 
                embeds: null, ephemeral: info.Ephemeral);
            Components.Unregister(e.Message);
            Components.Unregister(e.ComponentId);
            var values = e.Interaction.Data.Values;
            info.Categories = values.Select(x => (TorrentCategory)int.Parse(x)).ToArray();
            await Task.Run(async () =>
            {
                try
                {
                    await handle(info, e.Interaction).ConfigureAwait(false);
                } catch(Exception ex)
                {
                    Program.LogError(ex, "Torrents");
                    try
                    {
                        await e.Interaction.FollowupAsync($"An internal error occured whilst performing that task: {ex.Message}", embeds: null);
                    }
                    catch { }
                }
            });
        }

        async Task<EmbedBuilder> getBuilder(TorrentSearchInfo info, TorrentInfo[] items)
        {
            var start = info.Page * pageLength;
            var end = Math.Min(start + pageLength, items.Length);
            var relevant = items[new Range(start, end)];

            var builder = new EmbedBuilder();
            builder.Title = $"Results for '{info.Query}'";
            builder.WithFooter($"{info.Page}/{items.Length / pageLength}");

            foreach (var x in relevant)
            {
                var title = $"[{x.Seeders}/{x.Peers}] {x.Title}";
                title = Program.Clamp(title, EmbedBuilder.MaxTitleLength);
                builder.AddField(title,
                    $"[Link]({x.Url}) | {TimestampTag.FromDateTime(x.FeedItem.PublishingDate.Value, TimestampTagStyles.Relative)}");
            }

            return builder;
        }
        ComponentBuilder getComponents(TorrentSearchInfo info, string idPrefix, int max)
        {
            var components = new ComponentBuilder();
            components.WithButton("Previous", idPrefix + ":prev", disabled: info.Page == 0);
            components.WithButton("Next", idPrefix + ":next", disabled: info.Page == max);
            return components;
        }

        const int pageLength = 10;
        async Task handle(TorrentSearchInfo info, SocketMessageComponent interaction)
        {
            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = $"Searching `{info.Query}` in {info.Site} trackers...";
                x.Components = new ComponentBuilder().Build();
            });
            var items = await Jackett.SearchAsync(info.Site, info.Query, info.Categories);
            var torrents = items.Select(x => new TorrentInfo(x.SpecificItem as Rss20FeedItem))
                                .OrderByDescending(x => x.Score).ToArray();
            var builder = await getBuilder(info, torrents);
            var max = int.Parse(builder.Footer.Text.Split('/')[1]);
            var idPrefix = Interaction.User.Id.ToString() + "." + AuthToken.Generate(12);
            var components = getComponents(info, idPrefix, max);

            Func<CallbackEventArgs, Task> movePage = async (CallbackEventArgs e) =>
            {
                if (e.ComponentId.EndsWith("prev"))
                    info.Page = Math.Max(0, info.Page - 1);
                else
                    info.Page = Math.Min(max, info.Page + 1);
                await e.Interaction.AcknowledgeAsync(InteractionResponseFlags.Ephemeral);
                var builder = await getBuilder(info, torrents);
                await e.Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Embed = builder.Build();
                    x.Components = getComponents(info, idPrefix, max).Build();
                });
            };

            foreach (var suffix in new[] { ":prev", ":next" })
                Components.Register(idPrefix + suffix, interaction.Message, async e => await movePage(e), doSave: false);

            await interaction.ModifyOriginalResponseAsync(x =>
            {
                x.Content = "";
                x.Embed = builder.Build();
                x.Components = components.Build();
            });

        }



        class TorrentSearchInfo
        {
            public string Site { get; set; } = "all";
            public string Query { get; set; }
            public TorrentCategory[] Categories { get; set; }
            public bool Ephemeral { get; set; }
            public RestFollowupMessage Message { get; set; }
            public int Page { get; set; } = 0;
        }

        class TorrentInfo
        {
            public TorrentInfo(Rss20FeedItem item)
            {
                FeedItem = item;
                Torznabs = new Dictionary<string, string>();
                var np = item.Element.GetNamespaceOfPrefix("torznab");
                foreach(var el in item.Element.Elements(np + "attr"))
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

            public double Score { get
                {

                    var seeds = Seeders;
                    
                    var diff = DateTime.Now - this.FeedItem.PublishingDate.Value;
                    var age = diff.TotalSeconds;
                    var oneOver = 1 / age;

                    return seeds * oneOver;
                } }
        }
    }

}
