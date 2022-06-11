using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services.arr;
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
            await Service.MarkWatched(Service.GetApiKeyFromUrl(Url), Service.GetItemIdFromUrl(Url));

            await Message.DeleteAsync();
            await ModifyOriginalResponseAsync(x => x.Content = "Done!");
        }

        [ComponentInteraction("watch:next")]
        public async Task Next()
        {
            await RespondAsync($"Marking episode as complete..", ephemeral: true);
            var apiKey = Service.GetApiKeyFromUrl(Url);
            var episodeId = Service.GetItemIdFromUrl(Url);
            
            await Service.MarkWatched(apiKey, episodeId);
            await ModifyOriginalResponseAsync(x => x.Content = "Completed. Updating message to next episode..");


            var seriesId = Message.Embeds.First().Footer.Value.Text;

            var nextUp = (await Service.GetNextUp(apiKey, seriesId)).First();
            var ep = nextUp as WatcherService.JellyfinEpisodeItem;
            var nextBuilder = nextUp.ToEmbed(Service, apiKey);
            nextBuilder.Description = (nextBuilder.Description ?? "") + $"**Next Up**";
            await Message.ModifyAsync(x =>
            {
                x.Embeds = new[] { nextBuilder.Build() };
                x.Components = Service.GetComponents(true).Build();
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
            embed.Description = modal.Duration;
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
