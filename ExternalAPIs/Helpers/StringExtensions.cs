using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExternalAPIs.Helpers
{
    [DebuggerStepThrough]
    public static class StringExtensions
    {
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
        public static string ToDotCase(this string text)
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
                    sb.Append('.');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static List<string> ToSnakeCaseList<T>(this T value) where T : struct, Enum
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
        public static List<string> ToDotList<T>(this T value) where T : struct, Enum
        {
            var ls = new List<string>();
            foreach (var name in Enum.GetNames<T>())
            {
                if (name == "All") continue;
                T _flagV = (T)Enum.Parse(typeof(T), name);
                if (value.HasFlag(_flagV))
                {
                    ls.Add(name.ToDotCase());
                }
            }
            return ls;
        }

        public static string ToQueryString(this Dictionary<string, string> queryParams, string? endPoint = null)
        {
            bool start = true;
            var sb = new StringBuilder(endPoint);
            foreach ((var key, var value) in queryParams)
            {
                if (start)
                {
                    sb.Append('?');
                    start = false;
                }
                else
                {
                    sb.Append('&');
                }
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(value));
            }
            return sb.ToString();
        }
    }
}
