using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Interactions.Modules
{
    [Group("hours", "Commands to register hours stuff")]
    [DontAutoRegister]
    public class HoursModule : BotSlashBase
    {
        public HoursService Service { get; set; }

        [SlashCommand("register", "Register some things")]
        public async Task RegisterSetting(
            [Summary("id", "A unique identifer for this work period")]string id,
            [Summary("startDate", "A yyyy-MM-dd start date")]string startDateStr,
            [Summary("endDate", "A yyyy-MM-dd end date")]string endDateStr,
            [Summary("startTime", "A hh:mm start time")]string startTime,
            [Summary("endTime", "A hh:mm end time")] string endTime,
            [Summary("regular", "The regular hourly rate")]double regular,
            [Summary("overtime", "The overtime rate, if different")] double? overtime = null
            )
        {
            Func<string, Task> _error = async (x) =>
            {
                await RespondAsync($":x: {x}", ephemeral: true);
            };
            if (!DateTime.TryParse(startDateStr, out var startdate))
            {
                await _error("Start date was not in the required format.");
                return;
            }
            if (!DateTime.TryParse(endDateStr, out var enddate))
            {
                await _error("End date was not in the required format.");
                return;
            }
            using var db = Service.DB();
            await db.AddSetting(id, Context.User.Id, startdate, enddate, startTime, endTime, regular, overtime ?? regular);
            await db.SaveChangesAsync();
            await RespondAsync("Done!", ephemeral: true);
        }
    }

    public class HoursComponents : BotComponentBase
    {
        public HoursService Service { get; set; }

        [ComponentInteraction("hours:edit")]
        public async Task EditModal()
        {
            await RespondWithModalAsync<editModal>($"hours:modal:edit:{Context.Interaction.Message.Id}");
        }

        [ComponentInteraction("hours:noover")]
        public async Task NoOvertime()
        {
            var embed = Context.Interaction.Message.Embeds.First();
            var builder = embed.ToEmbedBuilder();
            var overtime = builder.Fields.First(x => x.Name == "Overtime");
            overtime.Value = "0.0";
            await Context.Interaction.UpdateAsync(x => x.Embeds = new[] { builder.Build() });
        }

        [ComponentInteraction("hours:1hr")]
        public async Task OneHourOvertime()
        {
            var embed = Context.Interaction.Message.Embeds.First();
            var builder = embed.ToEmbedBuilder();
            var overtime = builder.Fields.First(x => x.Name == "Overtime");
            overtime.Value = "1.0";
            await Context.Interaction.UpdateAsync(x => x.Embeds = new[] { builder.Build() });
        }

        [ComponentInteraction("hours:confirm")]
        public async Task Confirm()
        {
            var embed = Context.Interaction.Message.Embeds.First();
            var regular = double.Parse(embed.Fields.First(x => x.Name == "Regular").Value as string);
            var overtime = double.Parse(embed.Fields.First(x => x.Name == "Overtime").Value as string);

            using var db = Service.DB();
            await db.AddEntry(embed.Footer.Value.Text, Context.User.Id, regular, overtime, Context.Interaction.Message.CreatedAt.UtcDateTime.Date);
            await db.SaveChangesAsync();
            await RespondAsync($"Added!", ephemeral: true);
            await Context.Interaction.Message.DeleteAsync();
        }
    }

    public class HoursModals : Discord.Interactions.InteractionModuleBase<SocketInteractionContext<SocketModal>>
    {
        public HoursService Service { get; set; }

        [ModalInteraction("hours:modal:edit:*")]
        public async Task EditModal(ulong msgId, editModal modal)
        {
            var msg = await Context.Channel.GetMessageAsync(msgId);
            if(msg == null)
            {
                await RespondAsync(":x: Could not fetch message.", ephemeral: true);
                return;
            }
            var embed = msg.Embeds.First();
            var builder = embed.ToEmbedBuilder();
            var overtime = builder.Fields.First(x => x.Name == "Overtime");
            overtime.Value = modal.OvertimeHours;
            var reg = builder.Fields.First(x => x.Name == "Regular");
            reg.Value = modal.RegularHours;
            await Context.Interaction.UpdateAsync(x => x.Embeds = new[] { builder.Build() });
        }

    }

    public class editModal : IModal
    {
        public string Title => "Edit";

        [ModalTextInput("regular", placeholder: "8.0")]
        public double RegularHours { get; set; }

        [ModalTextInput("overtime", placeholder: "1.0")]
        public double OvertimeHours { get; set; }

    }
}
