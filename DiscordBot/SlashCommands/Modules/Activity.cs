﻿using Discord;
using Discord.SlashCommands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.SlashCommands.Modules
{
    [CommandGroup("activity")]
    public class Activity : BotSlashBase
    {
        public const ulong PokerNight = 755827207812677713;
        public const ulong BetrayalIO = 773336526917861400;
        public const ulong YoutubeTogether = 755600276941176913;
        public const ulong FishingtonIO = 814288819477020702;

        public async Task<IInviteMetadata> createActivity(ulong id, SocketVoiceChannel vc)
        {
            return await vc.CreateInviteAsync(applicationId: id);
        }

        [SlashCommand("start", "Sends an invite for the selected activity")]
        public async Task ChoiceActivity(
            [Choice("Poker Night", 0)]
            [Choice("Betrayal.io", 1)]
            [Choice("Youtube Together", 2)]
            [Choice("Fishington.io", 3)]
            [Required]
            int activity)
        {

            var vc = Interaction.Member.VoiceChannel;
            if(vc == null)
            {
                await Interaction.RespondAsync(":x: You must be in a voice channel to run this command",
                    flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            if(activity < 0 || activity > 3)
            {
                await Interaction.RespondAsync(":x: Invalid choice",
                    flags: InteractionResponseFlags.Ephemeral);
                return;
            }
            await Interaction.AcknowledgeAsync();

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
                default:
                    name = null;
                    invite = null;
                    break;
            }
            await Interaction.FollowupAsync($"Click on the link below to join **{name}**:\r\nhttps://discord.gg/{invite.Code}");
        }

        [SlashCommand("id", "Sends an invite to begin an application of the provided ID")]
        public async Task AppId(
            [ParameterName("application-id")]
            [Required] 
            string strid, 
            [Required]
            [ParameterName("voice-channel")]
            SocketGuildChannel chnl)
        {
            if(!(chnl is SocketVoiceChannel vc))
            {
                await Interaction.RespondAsync(":x: Channel must be a voice channel",
                    flags: Discord.InteractionResponseFlags.Ephemeral);
                return;
            }

            if(!ulong.TryParse(strid, out var id))
            {
                await Interaction.RespondAsync(":x: Input was not a valid ulong. Must be a number",
                    flags: Discord.InteractionResponseFlags.Ephemeral);
                return;
            }
            await Interaction.AcknowledgeAsync(Discord.InteractionResponseFlags.Ephemeral);
            try
            {
                var invite = await createActivity(id, vc);
                await Interaction.FollowupAsync("Click to join below:\r\nhttps://discord.gg/" + invite.Code);
            } catch(Exception ex)
            {
                Program.LogMsg(ex, "ActivityId");
                await Interaction.FollowupAsync(":x: Exception: " + ex.Message);

            }
        }

    }
}