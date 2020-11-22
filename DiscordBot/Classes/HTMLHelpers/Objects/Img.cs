using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Img : DOMBase
    {
        public Img(string id = null, string cls = null) : base("img", id, cls)
        {
        }

        public string Alt { get => get(nameof(Alt)); set => set(nameof(Alt), value); }
        public string Src { get => get(nameof(Src)); set => set(nameof(Src), value); }
    }

    public class Svg : DOMBase
    {
        public Svg(string id = null, string cls = null) : base("svg", id, cls)
        {
        }
    }

    public class ForeignObject : DOMBase
    {
        public ForeignObject(string id = null, string cls = null) : base("foreignObject", id, cls)
        {
        }
    }
}
