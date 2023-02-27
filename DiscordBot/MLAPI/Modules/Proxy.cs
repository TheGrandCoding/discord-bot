using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using DiscordBot.Utils;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace DiscordBot.MLAPI.Modules
{
    public class Proxy : APIBase
    {
        public Proxy(APIContext context) : base(context, "proxy")
        {
        }

        void request(Uri path)
        {
            var request = (HttpWebRequest)WebRequest.CreateHttp(path);
            request.Method = Context.Method;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.CookieContainer = new CookieContainer();
            foreach(Cookie ck in Context.Request.Cookies)
            {
                request.CookieContainer.Add(ck);
            }
            request.Headers = new WebHeaderCollection();
            foreach(var name in Context.Request.Headers.AllKeys)
            {
                request.Headers.Add(name, Context.Request.Headers[name]);
            }
            request.Headers["Host"] = path.Host;
            using var response = (HttpWebResponse)request.GetResponseWithoutException();

            using var responseStream = response.GetResponseStream();
            var RESPONSEARRAY = new List<byte>();
            Context.HTTP.Response.ContentType = response.ContentType;
            if (response.ContentEncoding != null)
                Context.HTTP.Response.ContentEncoding = Encoding.GetEncoding(response.ContentEncoding);
            if (response.ContentType.Contains("text") || response.ContentType.Contains("css"))
            {
                //var stack = new StringStack("https://".Length);
                var buffer = new StringStack("xhttps://".Length);
                using var reader = new StreamReader(responseStream, Encoding.UTF8);
                string repUrl = $"{DiscordBot.MLAPI.Handler.LocalAPIUrl}/proxy/";
                int urlLength = repUrl.Length;
                while(!reader.EndOfStream)
                {
                    var c = Convert.ToChar(reader.Read());
                    //stack.Add(c);
                    var ss = buffer.ToString();
                    if(ss.StartsWith("https://"))
                    {
                        RESPONSEARRAY.AddRange(Encoding.UTF8.GetBytes(repUrl));
                    } else if (ss.StartsWith("http://"))
                    {
                        RESPONSEARRAY.AddRange(Encoding.UTF8.GetBytes(repUrl));
                    }
                    if (buffer.Count > "https://".Length)
                    {
                        RESPONSEARRAY.AddRange(Encoding.UTF8.GetBytes(buffer.Peek().ToString()));
                    }
                    buffer.Add(c);
                }
                while (buffer.Count > 0)
                    RESPONSEARRAY.AddRange(Encoding.UTF8.GetBytes(buffer.Pop().ToString()));
            }
            else if(response.ContentLength > 0)
            {
                var buffer = new byte[1024];
                int read;
                do
                {
                    read = responseStream.Read(buffer, 0, buffer.Length);
                    RESPONSEARRAY.AddRange(buffer.Take(read));
                    if (read == 0)
                        break;
                } while (read > 0);
            }
            Context.HTTP.Response.StatusCode = (int)response.StatusCode;
            Context.HTTP.Response.ContentLength64 = RESPONSEARRAY.LongCount();
            Context.HTTP.Response.StatusDescription = response.StatusDescription;
            StatusSent = (int)response.StatusCode;
            foreach (string hd in response.Headers.AllKeys)
            {
                var val = response.Headers[hd];
                if (hd == "Location")
                {
                    val = "/proxy/" + val;
                }
                if (hd == "Content-Length")
                    continue;
                Context.HTTP.Response.AppendHeader(hd, val);
            }
            int retries = 0;
        start:
            try
            {
                Context.HTTP.Response.Close(RESPONSEARRAY.ToArray(), false);
            }
            catch(InvalidOperationException ex)
            {
                Program.LogWarning("Failed to close stream " + ex.ToString(), "Proxy");
                retries++;
                if(retries < 20)
                {
                    Thread.Sleep(50);
                    goto start;
                }
            }
        }

        [Method("GET")]
        [Path("/proxy/{url}")]
        [Regex("url", ".+")]
        [RequireNoExcessQuery(false)]
        public async Task ProxyGetWebsite(string url)
        {
            var str = Context.HTTP.Request.Url.PathAndQuery.Substring("proxy/".Length + 1);
            if(!(str.StartsWith("https://") || str.StartsWith("http://")))
            {
                var indexOfMMM = str.IndexOf(':');
                str = str.Insert(indexOfMMM + 1, "/");
            }
            var path = new Uri(str);
            request(path);
        }

        [Method("POST")]
        [Path("/proxy/{url}")]
        [Regex("url", ".+")]
        [RequireNoExcessQuery(false)]
        public async Task ProxyPostWebsite(string url)
        {
            var str = Context.HTTP.Request.Url.PathAndQuery.Substring("proxy/".Length + 1);
            if (!(str.StartsWith("https://") || str.StartsWith("http://")))
            {
                var indexOfMMM = str.IndexOf(':');
                str = str.Insert(indexOfMMM + 1, "/");
            }
            var path = new Uri(str);
            request(path);
        }

    }

    public class StringStack
    {
        public LinkedList<char> Chars;
        int count;

        public StringStack(int number)
        {
            Chars = new LinkedList<char>();
            count = number;
        }

        public int Count => Chars.Count;

        public void Add(char c)
        {
            Chars.AddLast(c);
            if (Chars.Count > count)
                Chars.RemoveFirst();
        }

        public char Peek()
        {
            return Chars.First.Value;
        }
        public char Pop()
        {
            var c = Peek();
            Chars.RemoveFirst();
            return c;
        }

        public override string ToString()
        {
            return string.Join("", Chars);
        }
    }
}
