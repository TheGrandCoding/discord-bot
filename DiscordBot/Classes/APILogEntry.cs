using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.MLAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DiscordBot.Classes
{
    public class APILogEntry
    {
        public Guid Id { get; set; }
        public HttpMethod Method { get; set; }
        public DateTime Date { get; set; }
        public string Path { get; set; }
        public string IP { get; set; }
        public ulong? UserId { get
            {
                if (User == null)
                    return null;
                var split = User.Split('/');
                if (ulong.TryParse(split[0], out var id))
                    return id;
                return null;
            } }
        public string User { get; set; }
        public string[] Headers { get; set; }
        
        public string Body { get; set; }
        
        public HttpStatusCode Result { get; set; }
        public string[] ResultMore { get; set; }

        public string GetResultString()
        {
            if ((int)Result == 0)
                return "[No completion]";
            return $"{Result}";
        }

        static string[] streamableTypes = new string[]
        {
            "text/plain", "application/json"
        };

        const string seperator = "================================================";
        const string body = "<><><><><>";

        public APILogEntry(APIContext context)
        {
            Id = context.Id;
            Method = new HttpMethod(context.Method);
            Date = DateTime.Now;
            Path = context.Request.Url.PathAndQuery;
            IP = context.Request.Headers["X-Forwarded-For"];
            IP ??= context.Request.RemoteEndPoint.Address.ToString();
            if (context.User != null)
                User = $"{context.User.Id}/{context.User.Name}";
            var headers = new List<string>();
            foreach(var x in context.Request.Headers.AllKeys)
            {
                headers.Add($"{x}: {context.Request.Headers[x]}");
            }
            Headers = headers.ToArray();
            if(context.Request.HasEntityBody && context.Request.ContentType != null && streamableTypes.Any(x => context.Request.ContentType.StartsWith(x)))
            {
                Body = context.Body;
            }
        }
        public void End(HttpStatusCode code, params string[] more)
        {
            Result = code;
            ResultMore = more;
        }

        public APILogEntry(StreamReader reader)
        {
            string line;
            do
            {
                line = reader.ReadLine();
            } while (string.IsNullOrWhiteSpace(line) || line.StartsWith(seperator));
            var LINES = new Dictionary<string, List<string>>();
            string _body = "";
            bool addingBody = false;
            List<string> ls;
            do
            {
                if (line.StartsWith("h:"))
                {
                    var header = line.Substring("h:".Length);
                    var pair = header.Split(':');
                    if (LINES.TryGetValue("h:" + pair[0], out ls))
                        ls.Add(pair[1]);
                    else
                        LINES["h:" + pair[0]] = new List<string>() { pair[1] };
                    line = reader.ReadLine();
                    continue;
                }
                var split = line.Split(':');
                if((!addingBody && line == body) || line == ">>>>>>")
                    addingBody = true;
                if ((addingBody && line == body) || line == "<<<<<<")
                    addingBody = false;
                if(addingBody)
                {
                    _body += line;
                    line = reader.ReadLine();
                    continue;
                }
                if(split.Length > 1)
                {
                    var key = split[0];
                    var value = string.Join(":", split.Skip(1)).Substring(1);
                    if (LINES.TryGetValue(key, out ls))
                        ls.Add(value);
                    else
                        LINES[key] = new List<string>() { value };
                }
                line = reader.ReadLine();
            } while (!(reader.EndOfStream || string.IsNullOrWhiteSpace(line) || line == seperator));
            if (_body != "")
                Body = _body;
            Id = Guid.Parse(LINES["Id"].Last());
            Method = new HttpMethod(LINES["Method"].Last());
            if (LINES.TryGetValue("Date", out ls))
                Date = DateTime.Parse(ls.Last());
            Path = LINES["Path"].Last();
            IP = LINES["IP"].Last();
            if (LINES.TryGetValue("User", out ls))
                User = ls.Last();
            var headers = new List<string>();
            foreach(var keypair in LINES)
            {
                if(keypair.Key.StartsWith("h:"))
                {
                    string name = keypair.Key.Substring(2);
                    foreach (var value in keypair.Value)
                        headers.Add($"{name}: {value}");
                }
            }
            Headers = headers.ToArray();
            if (LINES.TryGetValue("Result", out ls))
            {
                foreach(var x in ls)
                {
                    if(int.TryParse(x, out var code))
                    {
                        Result = (HttpStatusCode)code;
                    } else if (x.Contains("Redirect"))
                    {
                        Result = HttpStatusCode.TemporaryRedirect;
                    }
                }
            }
            var more = new List<string>();
            if(LINES.TryGetValue("More", out ls))
            {
                foreach (var x in ls)
                    more.Add(x);
            }
            ResultMore = more.ToArray();
        }

        public string DebuggerDisplay {  get
            {
                return $"{Method} {Path} {Result}";
            } }

        public override string ToString() => ToString(true);

        public string ToString(bool includeBody = true)
        {
            string basic = @$"{seperator}
Id: {Id}
Method: {Method}
Date: {Date}
Path: {Path}
IP: {IP}
User: {(User ?? "null")}
";
            foreach (var h in Headers)
                basic += $"h:{h}\r\n";
            if(Body != null)
            {
                basic += $"{body}\r\n{(includeBody ? Body : "[redacted]")}\r\n{body}\r\n";
            }
            if ((int)Result != 0)
                basic += $"Result: {(int)Result}\r\n";
            if(ResultMore != null)
            {
                foreach (var x in ResultMore)
                    basic += $"More: {x}\r\n";
            }
            return basic;
        }
    }
}
