﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FacebookAPI.Helpers
{
    public static class StringExtensions
    {
        [DebuggerStepThrough]
        public static string ToSnakeCase(this string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (text.Length < 2)
            {
                return text;
            }
            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(text[0]));
            for (int i = 1; i < text.Length; ++i)
            {
                char c = text[i];
                if (char.IsUpper(c))
                {
                    sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        [DebuggerStepThrough]
        public static List<string> ToFlagList<T>(this T value) where T : struct, Enum
        {
            var ls = new List<string>();
            foreach (var name in Enum.GetNames<T>())
            {
                if (name == "All") continue;
                T _flagV = (T)Enum.Parse(typeof(T), name);
                if (value.HasFlag(_flagV))
                {
                    ls.Add(name.ToSnakeCase());
                }
            }
            return ls;
        }
    }
}