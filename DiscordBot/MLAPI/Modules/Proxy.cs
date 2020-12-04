using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace DiscordBot.MLAPI.Modules
{
    [RequireAuthentication(false, false)]
    [RequireApproval(false)]
    public class Proxy : APIBase
    {
        public Proxy(APIContext context) : base(context, "proxy")
        {
        }

        void request(Uri path)
        {
            Program.LogMsg($"{Context.Method} {path}");
            var request = (HttpWebRequest)WebRequest.CreateHttp(path);
            request.Method = Context.Method;
            request.CookieContainer = new CookieContainer();
            foreach(Cookie ck in Context.Request.Cookies)
            {
                Program.LogMsg($"Add-Cookie: {ck}");
                request.CookieContainer.Add(ck);
            }
            request.Headers = new WebHeaderCollection();
            foreach(var name in Context.Request.Headers.AllKeys)
            {
                Program.LogMsg($"Add-Header: {name}={Context.Request.Headers[name]}");
                request.Headers.Add(name, Context.Request.Headers[name]);
            }
            var response = (HttpWebResponse)request.GetResponse();
            Program.LogMsg($"Response: {response.StatusCode}");
            foreach(string hd in response.Headers.AllKeys)
            {
                var val = response.Headers[hd];
                if(hd == "Location")
                {
                    val = "/proxy" + val;
                }
                Program.LogMsg($"Response-Header: {hd}={val}");
                Context.HTTP.Response.AppendHeader(hd, val);
            }


            using var responseStream = response.GetResponseStream();
            Program.LogMsg($"Content-Type: {response.ContentType}");
            if(response.ContentType.Contains("text") || response.ContentType.Contains("css"))
            {
                Program.LogMsg("Doing string checks");
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
                Program.LogMsg("Copying entire thing");
                responseStream.CopyTo(Context.HTTP.Response.OutputStream);
            }
            Context.HTTP.Response.StatusCode = (int)response.StatusCode;
            Context.HTTP.Response.StatusDescription = response.StatusDescription;
            Context.HTTP.Response.ContentType = response.ContentType;
            Context.HTTP.Response.ContentEncoding = Encoding.GetEncoding(response.ContentEncoding);
            Context.HTTP.Response.Close();
            StatusSent = (int)response.StatusCode;
            Program.LogMsg($"Done with {StatusSent}");
        }

        [Method("GET"), PathRegex(@"\/proxy\/.+")]
        public void ProxyGetWebsite()
        {
            var str = Context.HTTP.Request.Url.PathAndQuery.Substring("proxy/".Length + 1);
            var indexOfMMM = str.IndexOf(':');
            str = str.Insert(indexOfMMM, "/");
            var path = new Uri(str);
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
