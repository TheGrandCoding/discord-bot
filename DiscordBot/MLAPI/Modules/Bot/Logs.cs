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

namespace DiscordBot.MLAPI.Modules.Bot
{
    public class Logs : APIBase
    {
        public Logs(APIContext c) : base(c, "/") 
        {
            hasPerms = PermChecker.HasPerm(Context, Perms.Bot.Developer.SeeAPILogs);
        }

        public bool hasPerms;

        [RequirePermNode(Perms.Bot.Developer.SeeLatestLog)]
        [Method("GET"), Path("/bot/logs/today")]
        public void GetTodaysLog()
        {

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
    
        [Method("GET"), PathRegex(@"\/bot\/logs\/api\/(?<file>20(19|20|21)-[0-9]{1,2}-[0-9]{1,2})", "/bot/logs/api/yyyy-MM-dd")]
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

        [Method("GET"), PathRegex(@"/bot/logs/api/(?!.*\/.)(?<id>[a-zA-Z0-9-]+)", "/bot/logs/api/guid")]
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
    }
}
