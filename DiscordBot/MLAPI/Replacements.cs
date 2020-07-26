using DiscordBot.Classes.Chess.COA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class Replacements
    {
        /// <summary>
        /// Creates an empty Replacements object
        /// </summary>
        public Replacements()
        {
        }

        public Replacements(CoAHearing h)
        {
            // People can technically join petitions, or multiple people be named
            // There's one primary respondent, but others can be manually added to the title.
            // Thus, the title is the most up-to-date.
            Add("hearing", h);
            Add("claimant", string.Join("; ", h.Claimants.Select(x => x.Name)));
            Add("respondent", string.Join("; ", h.Respondents.Select(x => x.Name)));
            Add("casen", new DiscordBot.Classes.HTMLHelpers.Objects.Anchor($"/chess/coa/cases/{h.CaseNumber}", h.CaseNumber.ToString("0000")));
            Add("filed", h.Filed.ToString("dd/MM/yyyy"));
            var writ = h.Motions.FirstOrDefault(x => x.MotionType == Motions.WritOfCertiorari);
            if (writ != null && writ.Denied)
            {
                Add("commenced", "Never: Court refused to hear petition");
                Add("closed", writ.HoldingDate.Value.ToString("dd/MM/yyyy"));
            } else
            {
                Add("commenced", h.Commenced.HasValue ? h.Commenced.Value.ToString("dd/MM/yyyy") : "Not yet commenced");
                Add("closed", h.Concluded.HasValue ? h.Concluded.Value.ToString("dd/MM/yyyy") : "Not yet concluded");
            }
            Add("holding", h.Holding);
        }


        public Dictionary<string, object> objs = new Dictionary<string, object>();
        public Replacements Add(string name, object obj)
        {
            objs[name] = obj;
            return this;
        }

        public Replacements AddIf(bool thing, string name, string obj)
        {
            if(thing)
            {
                objs[name] = obj;
            }
            return this;
        }

        public Replacements IfElse(string name, bool thing, string obj1, string obj2)
        {
            objs[name] = thing ? obj1 : obj2;
            return this;
        }

        bool TryGetFieldOrProperty(object obj, string name, out object value)
        {
            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var split = name.Split('.');
            foreach(var p in properties)
            {
                if(p.Name.ToLower() == name)
                {
                    value = p.GetValue(obj);
                    return true;
                }
                if(p.Name.ToLower() == split[0])
                    return TryGetFieldOrProperty(p.GetValue(obj), string.Join(".", split[1..]), out value);
            }
            value = null;
            return false;
        }

        public bool TryGetValue(string key, out object obj)
        {
            obj = null;
            if (objs.TryGetValue(key, out obj))
                return true;
            var wanted = key.Split('.');
            if(objs.TryGetValue(wanted[0], out var cls))
            {
                return TryGetFieldOrProperty(cls, string.Join(".", wanted[1..]), out obj);
            }
            return false;
        }
    }
}
