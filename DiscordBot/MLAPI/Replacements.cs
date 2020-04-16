﻿using DiscordBot.Classes.Chess.CoA;
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

        public Replacements(CoAHearing hearing)
        {
            if (hearing.Justices.Length == 1)
                Add("type", "Solo");
            else if (hearing.Justices.Length == 3)
                Add("type", "Panel");
            else
                Add("type", "Enbanc");


            string justices = string.Join(", ", hearing.Justices.Select(x => x.Name));
            if (hearing.IsRequested)
                justices = $"<label color='red'>Case awaiting certification/approval from {MLAPI.Modules.CoA.JudgesToCertify} justices</label>";
            Add("justices", justices);

            Add("outcome1", "");
            Add("outcome2", "");
            if(hearing.HasFinished)
            {
                Add("outcome1", "Outcome");
                Add("outcome2", $"<strong>{hearing.Verdict}</strong>");
            }
            Add("hearing", hearing);
            Add("plaintiff", hearing.Plaintiff.Name);
            Add("defendant", hearing.Defendant.Name);
            Add("opened", hearing.Opened.ToString("ddd, dd MMMM yyyy"));
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
