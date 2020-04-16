using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    public class WebUser
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public ushort Discriminator { get; set; }
    }
}
