using Discord.Commands;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;

namespace DiscordBot.Classes
{
    public class BotUserOptions
    {
        [JsonConstructor]
        private BotUserOptions() { }
        public static BotUserOptions Default { get
            {
                return new BotUserOptions()
                {
                    PairedVoiceChannels = CreateChannelForVoice.Never
                };
            } }

        public CreateChannelForVoice PairedVoiceChannels { get; set; }
    }

    public enum CreateChannelForVoice
    {
        Never,
        WhenMuted,
        Always
    }
}
