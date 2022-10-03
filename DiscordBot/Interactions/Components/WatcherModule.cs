using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services.arr;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Components
{
    public class WatcherModule : BotComponentBase
    {
        public WatcherService Service { get; set; }
        public SocketUserMessage Message { get
            {
                return Context.Interaction.Message;
            } }

        public string Url { get
            {
                var e = Message.Embeds.First();
                return e.Url;
            }
        }


        [ComponentInteraction("watch:update")]
        public async Task Update()
        {
            var modal = new ModalBuilder("Update", $"watch:modify:{Context.Interaction.Message.Id}");
            modal.AddTextInput("Duration", "duration", placeholder: "mm:ss", required: true);
            await RespondWithModalAsync(modal.Build());
        }

        [ComponentInteraction("watch:complete")]
        public async Task Delete()
        {
            await RespondAsync($"Marking that as complete...", ephemeral: true);
            var auth = await WatcherService.JellyfinAuth.Parse(Url, Context.User.Id, Service);
            await Service.MarkWatched(Service.GetItemIdFromUrl(Url), auth);

            await Message.DeleteAsync();
            await ModifyOriginalResponseAsync(x => x.Content = "Done!");
        }

        [ComponentInteraction("watch:next")]
        public async Task Next()
        {
            await RespondAsync($"Marking episode as complete..", ephemeral: true);
            var episodeId = Service.GetItemIdFromUrl(Url);

            var auth = await WatcherService.JellyfinAuth.Parse(Url, Context.User.Id, Service);
            await Service.MarkWatched(episodeId, auth);
            await ModifyOriginalResponseAsync(x => x.Content = "Completed. Updating message to next episode..");


            var em = Message.Embeds.First();
            var parentId = em.Footer.Value.Text;

            WatcherService.JellyfinItem nextUp = null;
            if(em.Color == Color.Red)
            { // playlist
                nextUp = await Service.GetItemInfo(parentId, auth);
            } else
            { // episode in series
                nextUp = (await Service.GetNextUp(auth, parentId)).FirstOrDefault();
            }


            if (nextUp == null)
            {
                var e = em.ToEmbedBuilder();
                await Message.ModifyAsync(x =>
                {
                    x.Embeds = new[] { e
                        .WithDescription("Finished.\r\nNo next up episode.")
                        .Build() };
                });
                return;
            }

            FileAttachment? fa = null;
            if(nextUp is WatcherService.JellyfinPlaylist pl)
            {
                fa = await pl.ToPlaylistFile(Service, auth, true);
            }

            var nextBuilder = await nextUp.ToEmbed(Service, auth);
            nextBuilder.Description = (nextBuilder.Description ?? "") + $"**Next Up**";
            await Message.ModifyAsync(x =>
            {
                x.Embeds = new[] { nextBuilder.Build() };
                x.Components = Service.GetComponents(true).Build();
                x.Attachments = fa == null ? null : new List<FileAttachment>() { fa.Value };
            });
            await ModifyOriginalResponseAsync(x => x.Content = "Done!");
        }
    }

    public class WatcherModalModule : InteractionModuleBase<SocketInteractionContext<SocketModal>>
    {

        [ModalInteraction("watch:modify:*")]
        public async Task ModalModify(string currentId, ModifyModal modal)
        {
            await RespondAsync("Done!", ephemeral: true);
            var message = (IUserMessage)await Context.Channel.GetMessageAsync(ulong.Parse(currentId));
            var embed = message.Embeds.First().ToEmbedBuilder();
            var desc = embed.Description ?? "";
            var splt = desc.Split('\n');
            if (splt.Length <= 1)
                embed.Description = modal.Duration;
            else
                embed.Description = splt[0] + "\n" + modal.Duration;

            if(modal.Duration.Contains(':'))
            {
                var ts = TimeSpan.ParseExact(modal.Duration, new[] { @"mm\:ss", @"h\:mm\:ss" }, System.Globalization.CultureInfo.InvariantCulture);
                var ticks = (ulong)(ts.TotalMilliseconds * 10_000);
                var srv = Program.Services.GetRequiredService<WatcherService>();
                var itemId = srv.GetItemIdFromUrl(embed.Url);
                var auth = await WatcherService.JellyfinAuth.Parse(embed.Url, Context.User.Id, srv);
                string playSessionId = null;
                if (auth.AuthKey == srv.JellyfinApiKey)
                { // need to get an API key with a session thing
                    var sessions = await srv.GetFirstCapableSession(auth);
                    playSessionId = sessions.Id;
                }
                
                await srv.SetWatchedTime(itemId, ticks, playSessionId, auth);
            }

            await message.ModifyAsync(x => x.Embeds = new[] { embed.Build() });
        }

        public class ModifyModal : IModal
        {
            public string Title => "Update";

            [InputLabel("Duration")]
            [ModalTextInput("duration")]
            public string Duration { get; set; }

        }
    }
}
