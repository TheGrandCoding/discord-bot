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
            } }


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
