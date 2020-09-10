using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.HTMLHelpers.Objects
{
    public class HTMLPage : HTMLBase
    {
        public HTMLPage(string cls = null) : base("html", null, cls)
        {

        }
        protected override void WriteOpenTag(StringBuilder sb)
        {
            sb.Append($"<!DOCTYPE html>");
            base.WriteOpenTag(sb);
        }
    }
    public class PageHeader : HTMLBase
    {
        public PageHeader(bool addCommon = true) : base ("head", null, null)
        {
            if (addCommon)
                WithCommon();
        }
        public PageHeader WithStyle(string href)
        {
            if (!href.StartsWith("http"))
            {
                if (!href.StartsWith("/"))
                    href = "/" + href;
                href = "/_/css" + href;
            }
            Children.Add(new PageLink("stylesheet", "text/css", href));
            return this;
        }
        public PageHeader WithCommon() => WithStyle("common.css");
        public PageHeader WithMeta(Meta meta)
        {
            Children.Add(meta);
            return this;
        }
        public PageHeader WithOpenGraph(string title = null, string type = null, string description = null, string image = null, string url = null)
        {
            var things = new Dictionary<string, string>()
            {
                {"title", title }, {"type", type}, {"image", image}, {"url", url}, {"description", description}
            };
            foreach(var keypair in things)
            {
                if (!string.IsNullOrWhiteSpace(keypair.Value))
                {
                    Children.Add(Meta.Property($"og:{keypair.Key}", keypair.Value));
                }
            }
            return this;
        }
        public PageHeader WithTitle(string title)
        {
            Children.Add(new PageTitle(title));
            return this;
        }
    }
    public class PageBody : HTMLBase
    {
        public PageBody(string cls = null) : base("body", null, cls)
        {

        }
    }
    public class PageStyleSheet : HTMLBase
    {
        public PageStyleSheet(string text) : base("style", null, null)
        {
            RawText = text;
        }
    }
    public class PageLink : HTMLBase
    {
        public PageLink(string rel, string type, string href) : base ("link", null, null)
        {
            tagValues["rel"] = rel;
            tagValues["type"] = type;
            tagValues["href"] = href;
        }
    }

    public class Meta : HTMLBase
    {
        private Meta() : base("meta", null, null) { }

        public static Meta Charset(string charset) => KeyPair("charset", charset);

        public static Meta Property(string propertyName, string content)
        {
            var m = new Meta();
            m.tagValues["property"] = propertyName;
            m.tagValues["content"] = content;
            return m;
        }

        public static Meta KeyPair(string name, string content)
        {
            var m = new Meta();
            m.tagValues[name] = content;
            return m;
        }

        public static Meta HTTPHeader(string headerName, string value)
        {
            var m = new Meta();
            m.tagValues["http-equiv"] = headerName;
            m.tagValues["content"] = value;
            return m;
        }

    }

    public class PageTitle : HTMLBase
    {
        public PageTitle(string title) : base("title", null, null)
        {
            RawText = title;
        }
    }
}
