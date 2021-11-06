using DiscordBot.Classes;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Permissions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DiscordBot.MLAPI.Modules.Bot
{
    public class Logs : AuthedAPIBase
    {
        public Logs(APIContext c) : base(c, "bot") 
        {
            hasPerms = PermChecker.HasPerm(Context, Perms.Bot.Developer.SeeAPILogs);
        }

        public bool hasPerms;

        [RequirePermNode(Perms.Bot.Developer.SeeLatestLog)]
        [Method("GET"), Path("/bot/logs")]
        public void GetTodaysLog()
        {
            ReplyFile("logs.html", HttpStatusCode.OK);
        }

        static string apiLogFolder => Path.Combine(Program.BASE_PATH, "APILogs");

        [RequireOwner]
        [Method("GET"), Path("/bot/logs/api")]
        public void ApiLogsBase()
        {
            var dir = new DirectoryInfo(apiLogFolder);
            var files = dir.GetFiles("*.txt");
            var page = new HTMLPage();
            page.Children.Add(new PageHeader());
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                    {
                        Children =
                        {
                            new TableHeader("Date")
                        }
                    }
                }
            };
            foreach(var x in files)
            {
                var row = new TableRow()
                {
                    Children =
                    {
                        new TableData(null)
                        {
                            Children =
                            {
                                new Anchor($"/bot/logs/api/{x.Name.Replace(x.Extension , "")}", x.Name)
                            }
                        }
                    }
                };
                table.Children.Add(row);
            }
            page.Children.Add(new PageBody() { Children = { table } });
            RespondRaw(page, 200);
        }

        List<APILogEntry> getLog(string file)
        {
            using var stream = new StreamReader(Path.Combine(Program.BASE_PATH, "APILogs", file));
            var logs = new List<APILogEntry>();
            do
            {
                logs.Add(new APILogEntry(stream));
            } while (!stream.EndOfStream);
            return logs;
        }

        TableData methodData(HttpMethod method)
        {
            return new TableData(null)
            {
                Children =
                {
                    new Span(cls: "label method-" + method.Method)
                    {
                        RawText = method.Method
                    }
                }
            };
        }

        string getType(int result)
        {
            if (result == 0)
                return "nocomplete";
            if (result >= 100 && result < 200)
                return "blank";
            if (result >= 200 && result < 300)
                return "success";
            if (result >= 300 && result < 400)
                return "redirect";
            if (result >= 400 && result < 500)
                return "clienterr";
            if (result >= 500 && result < 600)
                return "servererr";
            return $"unknown code-{result}";
        }

        TableData resultData(HttpStatusCode result)
        {
            return new TableData(null)
            {
                Children =
                {
                    new Span(cls: "label " + getType((int)result))
                    {
                        RawText = $"{((int)result == 0 ? "[N/A]" : result.ToString())}"
                    }
                }
            };
        }

        Table getTable(string file)
        {
            var logs = getLog(file);
            var table = new Table()
            {
                Children =
                {
                    new TableRow()
                    {
                        Children =
                        {
                            new TableHeader("Id"),
                            new TableHeader("Method"),
                            new TableHeader("Path"),
                            new TableHeader("User"),
                            new TableHeader("Result")
                        }
                    }
                }
            };
            foreach(var log in logs)
            {
                if(log.UserId != Context.User.Id)
                {
                    if (!hasPerms)
                        continue;
                }
                table.Children.Add(new TableRow(id: log.Id.ToString())
                {
                    Children = {
                        new TableData(null)
                        {
                            Children =
                            {
                                new Anchor($"/bot/logs/api/{log.Id}", log.Id.ToString())
                            }
                        },
                        methodData(log.Method),
                        new TableData(log.Path),
                        new TableData(log.User),
                        resultData(log.Result)
                    }
                });
            }
            return table;
        }
    
        [Method("GET")]
        [Path(@"/bot/logs/api/{file}")]
        [Regex("file", "20(19|20|21)-[0-9]{1,2}-[0-9]{1,2}")]
        public void ApiLog(string file)
        {
            if (file.Contains('/') || file.Contains('.'))
                throw new HaltExecutionException("Invalid file");
            var now = file == DateTime.Now.ToString("yyyy-MM-dd");
            file = file + ".txt";
            Table table;
            if(now)
            {
                lock (Handler.logLock)
                    table = getTable(file);
            } else
            {
                table = getTable(file);
            }
            var page = new HTMLPage()
            {
                Children =
                {
                    new PageHeader(),
                    new PageBody()
                    {
                        Children =
                        {
                            table
                        }
                    }
                }
            };
            RespondRaw(page, 200);
        }

        APILogEntry getLogEntry(Guid id)
        {
            var dir = new DirectoryInfo(apiLogFolder);
            foreach(var file in dir.GetFiles("*.txt").Reverse()) // start recent.
            {
                var logs = getLog(file.Name);
                var any = logs.FirstOrDefault(x => x.Id == id);
                if (any != null)
                    return any;
            }
            return null;
        }

        [Method("GET")]
        [Path(@"/bot/logs/api/{id}")]
        [Regex(".", @"/bot/logs/api/(?!.*\/.)(?<id>[a-zA-Z0-9-]+)")]
        public void ApiLogSpecific(Guid id)
        {
            var logEntry = getLogEntry(id);
            if(logEntry == null || (!hasPerms && logEntry.UserId != Context.User.Id))
            {
                RespondRaw("Not found.", 404);
                return;
            }
            bool dontlogContent = logEntry.Path.StartsWith("/login")
                || logEntry.Path == "/bot/build"; // since the JSON code there is... big.
            var page = new HTMLPage()
            {
                Children =
                {
                    new PageHeader()
                        .WithStyle("logs.css"),
                    new PageBody()
                    {
                        Children =
                        {
                            new Pre(logEntry.ToString(!dontlogContent), cls: "code")
                        }
                    }
                }
            };
            RespondRaw(page, 200);
        }

        class HTTPData
        {
            public List<KeyValuePair<string, string>> Headers = new List<KeyValuePair<string, string>>();
            public string Content { get; set; }

            protected void continueParse(StreamReader reader)
            {
                string line = reader.ReadLine();
                while(!string.IsNullOrWhiteSpace(line))
                {
                    var spl = line.Split(":");
                    Headers.Add(new KeyValuePair<string, string>(spl[0], spl[1].Substring(1)));
                    line = reader.ReadLine();
                }
                var c = new StringBuilder();
                line = reader.ReadLine();
                while(!string.IsNullOrWhiteSpace(line) && line.StartsWith("========") == false)
                {
                    c.Append(line + "\n");
                    line = reader.ReadLine();
                }
            }
        }
        class RequestData : HTTPData
        {
            public string Method { get; set; }
            public string PathAndQuery { get; set; }

            public string URL { get
                {
                    var host = Headers.FirstOrDefault(x => x.Key == "Host");
                    return (host.Value ?? "") + PathAndQuery;
                } }

            public static RequestData Parse(StreamReader reader)
            {
                var data = new RequestData();
                var split = reader.ReadLine().Split(" ");
                data.Method = split[0];
                data.PathAndQuery = split[1];
                data.continueParse(reader);
                return data;
            }
        }
        class ResponseData : HTTPData
        {
            public int Code { get; set; }

            public static ResponseData Parse(StreamReader reader)
            {
                var data = new ResponseData();
                var split = reader.ReadLine().Split(" ");
                data.Code = int.Parse(split[1]);
                data.continueParse(reader);
                return data;
            }
        }
        class HttpPair
        {
            public RequestData Request { get; set; }
            public ResponseData Response { get; set; }

            public static HttpPair Parse(StreamReader file)
            {
                var http = new HttpPair();
                http.Request = RequestData.Parse(file);
                var s = file.ReadLine();
                http.Response = ResponseData.Parse(file);
                return http;
            }
            public static HttpPair Parse(string path)
            {
                using var sr = new StreamReader(path);
                return Parse(sr);
            }
        }


        [RequireOwner]
        [Method("GET"), Path("/bot/logs/http")]
        public void HttpLogsBase()
        {
            var folder = BotHttpClient.LogFolder;
            var files = Directory.EnumerateFiles(folder, "*.txt");
            var table = new Table();
            table.WithHeaderColumn("Order");
            table.WithHeaderColumn("URL");
            table.WithHeaderColumn("Status Code");
            foreach(var file in files)
            {
                var pair = HttpPair.Parse(file);
                string order = Path.GetFileNameWithoutExtension(file);
                var anchor = new Anchor($"/bog/logs/http/{order}", order);
                table.WithRow(anchor, pair.Request.URL, pair.Response.Code.ToString());
            }
            var page = new HTMLPage();
            page.Children.Add(new PageHeader());
            page.Children.Add(new PageBody() { Children = { table } });
            RespondRaw(page);
        }

        [Method("GET")]
        [Path(@"/bot/logs/http/{order}")]
        [Regex("order", "[0-9]+")]
        public void HttpLogs(string order)
        {
            var path = Path.Combine(BotHttpClient.LogFolder, order + ".txt");

            var data = File.ReadAllText(path).Replace("<", "&lt;").Replace(">", "&gt;");

            var page = new HTMLPage();
            page.Children.Add(new PageHeader());
            var pre = new Pre(null)
            {
                Children = { new Code(data) }
            };
            page.Children.Add(new PageBody() { Children = { pre } });
            RespondRaw(page, 200);
            
        }
    }
}
