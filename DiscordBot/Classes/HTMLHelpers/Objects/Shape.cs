using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public abstract class ShapeBase : DOMBase
    {
        public ShapeBase(string x, string y, string width, string height, string tag, string id, string cls) : base(tag, id, cls)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string X { get => get("x"); set => set("x", value); }
        public string Y { get => get("y"); set => set("y", value); }
        public string Width { get => get("width"); set => set("width", value); }
        public string Height { get => get("height"); set => set("height", value); }
        public string Fill { get => get("fill"); set => set("fill", value); }
        public string Mask { get => get("mask"); set => set("mask", value); }
    }
    public class Rect : ShapeBase
    {
        public Rect(string x, string y, string width, string height, string id = null, string cls = null) : base(x, y, width, height, "rect", id, cls)
        {
        }
    }
}
