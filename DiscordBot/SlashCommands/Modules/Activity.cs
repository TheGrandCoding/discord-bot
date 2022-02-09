using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [Group("activity", "Commands for creating VC activities")]
    public class Activity : BotSlashBase
    {
        public const ulong PokerNight = 755827207812677713;
        public const ulong BetrayalIO = 773336526917861400;
        public const ulong YoutubeTogether = 755600276941176913;
        public const ulong FishingtonIO = 814288819477020702;
        public const ulong ChessInThePark = 832012774040141894;

        public async Task<IInviteMetadata> createActivity(ulong id, SocketVoiceChannel vc)
        {
            return await vc.CreateInviteToApplicationAsync(applicationId: id, Time.S.Day);
        }

        [SlashCommand("start", "Sends an invite for the selected activity")]
        public async Task ChoiceActivity(
            [Choice("Poker Night", 0)]
            [Choice("Betrayal.io", 1)]
            [Choice("Youtube Together", 2)]
            [Choice("Fishington.io", 3)]
            [Choice("Chess in the Park", 4)]
            int activity)
        {
            var vc = (Context.Interaction.User as SocketGuildUser)?.VoiceChannel ?? null;
            if(vc == null)
            {
                await RespondAsync(":x: You must be in a voice channel to run this command",
                    ephemeral: true, embeds: null);
                return;
            }
            if(activity < 0 || activity > 4)
            {
                await RespondAsync(":x: Invalid choice",
                    ephemeral: true, embeds: null);
                return;
            }
            await DeferAsync();

            IInviteMetadata invite;
            string name;
            switch (activity)
            {
                case 0:
                    name = "Poker Night";
                    invite = await createActivity(PokerNight, vc);
                    break;
                case 1:
                    name = "Betrayal.io";
                    invite = await createActivity(BetrayalIO, vc);
                    break;
                case 2:
                    name = "Youtube Together";
                    invite = await createActivity(YoutubeTogether, vc);
                    break;
                case 3:
                    name = "Fishington.io";
                    invite = await createActivity(FishingtonIO, vc);
                    break;
                case 4:
                    name = "Chess in the Park";
                    invite = await createActivity(ChessInThePark, vc);
                    break;
                default:
                    name = null;
                    invite = null;
                    break;
            }
            await FollowupAsync($"Click on the link below to join **{name}**:\r\nhttps://discord.gg/{invite.Code}", embeds: null);
        }

        [SlashCommand("id", "Sends an invite to begin an application of the provided ID")]
        public async Task AppId(
            string applicationId, 
            SocketVoiceChannel voiceChannel)
        {
            if(!(voiceChannel is SocketVoiceChannel vc))
            {
                await RespondAsync(":x: Channel must be a voice channel",
                    ephemeral: true, embeds: null);
                return;
            }

            if(!ulong.TryParse(applicationId, out var id))
            {
                await RespondAsync(":x: Input was not a valid ulong. Must be a number",
                    ephemeral: true, embeds: null);
                return;
            }
            await Context.Interaction.DeferAsync(true);
            try
            {
                var invite = await createActivity(id, vc);
                await FollowupAsync("Click to join below:\r\nhttps://discord.gg/" + invite.Code, embeds: null);
            } catch(Exception ex)
            {
                Program.LogError(ex, "ActivityId");
                await FollowupAsync(":x: Exception: " + ex.Message, embeds: null);

            }
        }

    }
}
