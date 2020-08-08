using DiscordBot.Classes.Chess.COA;
using DiscordBot.Classes.HTMLHelpers.Objects;
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

        Div getCoAContainer(AppealHearing h)
        {
            var main = new Div(cls: "container");
            main.Children.Add(new Div(cls: "column")
            {
                Children =
                {
                    new Paragraph($"Filed {h.Filed:MMMM dd, yyyy}")
                }
            });
            if(h.Commenced.HasValue)
            {
                main.Children.Add(new Div(cls: "column")
                {
                    Children =
                    {
                        new Paragraph($"Commenced {h.Commenced:MMMM dd, yyyy}")
                    }
                });
            }
            if(h.Concluded.HasValue)
            {
                main.Children.Add(new Div(cls: "column")
                {
                    Children =
                    {
                        new Paragraph($"Decided {h.Concluded:MMMM dd, yyyy}")
                    }
                });
            }
            foreach (var c in main.Children)
                c.Class = $"column column-{main.Children.Count}";
            return main;
        }

        public Replacements(AppealHearing h)
        {
            // People can technically join petitions, or multiple people be named
            // There's one primary respondent, but others can be manually added to the title.
            // Thus, the title is the most up-to-date.
            Add("hearing", h);
            Add("venue", h.IsArbiterCase ? "THE ARBITER" : "COURT OF APPEALS");
            Add("claimant", string.Join("; ", h.Claimants.Select(x => x.Name)).ToUpper());
            Add("respondent", string.Join("; ", h.Respondents.Select(x => x.Name)).ToUpper());
            Add("casen", new DiscordBot.Classes.HTMLHelpers.Objects.Anchor($"/chess/cases/{h.CaseNumber}", h.CaseNumber.ToString("0000")));
            IfElse("cType", h.AppealOf.HasValue, "APPELLEE", "CLAIMANT");
            IfElse("rType", h.AppealOf.HasValue, "APPELLANT", "RESPONDENT");
            IfElse("hType", h.AppealOf.HasValue, "On matter from Appeal", (h.IsArbiterCase ? "On matter for Arbiter to Review" : "On matter for Court to Review"));
            Add("container", getCoAContainer(h));
            Add("holding", h.Holding == null ? "" : $"<p><em>{h.Holding}</em></p><hr/>");
            Add("appealInfo", h.AppealOf.HasValue ? $"<p>Pursuant to an appeal of <a href='/chess/cases/{h.AppealOf.Value}'>No. {h.AppealOf.Value:0000}</a><hr/>" : "");
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
