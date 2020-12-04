using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

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
            var response = (HttpWebResponse)request.GetResponse();
            foreach(string hd in response.Headers.AllKeys)
            {
                var val = response.Headers[hd];
                if(hd == "Location")
                {
                    val = "/proxy" + val;
                }
                Context.HTTP.Response.AppendHeader(hd, val);
            }
            using var responseStream = response.GetResponseStream();
            if(response.ContentType.Contains("text") || response.ContentType.Contains("css"))
            {
                var stack = new StringStack("https://".Length);
                using var reader = new StreamReader(responseStream, Encoding.UTF8);
                using var writer = new StreamWriter(Context.HTTP.Response.OutputStream, Encoding.UTF8);
                string repUrl = $"{DiscordBot.MLAPI.Handler.LocalAPIUrl}/proxy";
                int urlLength = repUrl.Length;
                while(!reader.EndOfStream)
                {
                    var c = Convert.ToChar(reader.Read());
                    stack.Add(c);
                    var ss = stack.ToString();
                    if(ss == "https://")
                    {
                        writer.BaseStream.Seek(-("https://".Length), SeekOrigin.Current);
                        writer.Write(repUrl);
                        writer.BaseStream.Seek(urlLength, SeekOrigin.Current);
                    } else if (ss == "http://")
                    {
                        writer.BaseStream.Seek(-("http://".Length), SeekOrigin.Current);
                        writer.Write(repUrl);
                        writer.BaseStream.Seek(urlLength, SeekOrigin.Current);
                    }
                    writer.Write(c);
                }
            } else
            {
                responseStream.CopyTo(Context.HTTP.Response.OutputStream);
            }
            Context.HTTP.Response.StatusCode = (int)response.StatusCode;
            Context.HTTP.Response.StatusDescription = response.StatusDescription;
            Context.HTTP.Response.ContentType = response.ContentType;
            Context.HTTP.Response.ContentEncoding = Encoding.GetEncoding(response.ContentEncoding);
        }

        [Method("GET"), PathRegex(@"\/proxy\/(?<path>.+)")]
        public void ProxyGetWebsite(Uri path)
        {
            request(path);
        }

        [Method("POST"), PathRegex(@"\/proxy\/(?<path>.+)")]
        public void ProxyPostWebsite(Uri path)
        {
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

        public void Add(char c)
        {
            Chars.AddLast(c);
            if (Chars.Count > count)
                Chars.RemoveFirst();
        }

        public override string ToString()
        {
            return string.Join("", Chars);
        }
    }
}
