using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Image : DOMBase
    {
        public Image(string id = null, string cls = null) : base("img", id, cls)
        {
        }

        public string Alt { get => get(nameof(Alt)); set => set(nameof(Alt), value); }
        public string Src { get => get(nameof(Src)); set => set(nameof(Src), value); }
    }
}
