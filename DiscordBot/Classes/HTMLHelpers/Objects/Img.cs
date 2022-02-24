using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class Img : DOMBase
    {
        public Img(string src = null, string id = null, string cls = null) : base("img", id, cls)
        {
            Src = src;
        }

        public string Alt { get => get(nameof(Alt)); set => set(nameof(Alt), value); }
        public string Src { get => get(nameof(Src)); set => set(nameof(Src), value); }
    }

    public class Svg : DOMBase
    {
        public Svg(string viewbox, string width, string height, string id = null, string cls = null) 
            : base("svg", id, cls)
        {
            Viewbox = viewbox;
            Height = height;
            Width = width;
        }
        public string Height { get => get(nameof(Height)); set => set(nameof(Height), value); }
        public string Width { get => get(nameof(Width)); set => set(nameof(Width), value); }
        public string Viewbox { get => get(nameof(Viewbox)); set => set(nameof(Viewbox), value); }
    }

    public class ForeignObject : DOMBase
    {
        public ForeignObject(string id = null, string cls = null) : base("foreignObject", id, cls)
        {
        }
    }
}
