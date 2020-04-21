using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes
{
    /// <summary>
    /// Displays a box on the doc page
    /// </summary>
    public class DocBoxAttribute : Attribute
    {
        public string Type { get; set; }
        public string Content { get; set; }
        public DocBoxAttribute(string type, string content)
        {
            Type = type;
            Content = content;
        }
        public string HTML()
        {
            return $"<div class='msgBox {Type}'><p>{Content}</p></div>";
        }
    }
}
