using Discord.Commands;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;

namespace DiscordBot.Classes
{
    public class BotDbUserOptions
    {
        [JsonConstructor]
        private BotDbUserOptions() { }
        public static BotDbUserOptions Default { get
            {
                return new BotDbUserOptions()
                {
                    PairedVoiceChannels = CreateChannelForVoice.WhenMuted
                };
            } }

        public CreateChannelForVoice PairedVoiceChannels { get; set; }
        public IsolationNotify WhenToNotifyIsolation { get; set; } = IsolationNotify.End;
    }

    [Flags]
    public enum IsolationNotify
    {
        Never = 0b00,
        Daily = 0b01,
        End   = 0b11
    }

    public enum CreateChannelForVoice
    {
        Never,
        WhenMuted,
        Always
    }
}
