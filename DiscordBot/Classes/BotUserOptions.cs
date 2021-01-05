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
