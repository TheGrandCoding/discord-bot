using DiscordBot.Classes.HTMLHelpers;
using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DiscordBot.MLAPI.Modules
{
    public class SpeedTestViewer : APIBase
    {
        public SpeedTestViewer(APIContext c) : base(c, "speed") { }

        TableRow getHeader(string when)
        {
            return new TableRow()
            {
                Children =
                {
                    new TableHeader(when),
                    new TableHeader("Download (Mbps)"),
                    new TableHeader("Upload (Mbps)"),
                    new TableHeader("Ping")
                }
            };
        }

        HTMLPage getPage(params HTMLBase[] objects)
        {
            var page = new HTMLPage()
            {
                Children =
                {
                    new PageHeader()
                        .WithStyle("/_/css/common.css"),
                }
            };
            var body = new PageBody();
            foreach (var x in objects)
                body.Children.Add(x);
            page.Children.Add(body);
            return page;
        }

        [Method("GET"), Path("/")]
        [RequireServerName("c:speedtest")]
        [RequireAuthentication(false)]
        public void Base()
        {
            var WEEKLY = new Table();
            WEEKLY.Children.Add(getHeader("Date"));
            var DAILY = new Table();
            DAILY.Children.Add(getHeader("Time"));

            var folderPath = Path.Combine(Program.BASE_PATH, "internet");
            var directory = new DirectoryInfo(folderPath);
            var files = directory.GetFiles("*.csv");
            int mustSkip = files.Length - 7;
            int i = 0;
            string today = DateTime.Now.ToString("yy-MM-dd");
            foreach (var file in files)
            {
                string date = file.Name.Replace(file.Extension, "");
                double download = 0;
                double upload = 0;
                double ping = 0;
                if(today == date)
                {
                    var diff = DateTime.UtcNow - file.LastWriteTimeUtc;
                    int remainders = (int)diff.TotalMinutes % 30;
                    if(remainders >= 28)
                    {
                        var errPage = getPage(new Paragraph("Speed test is in progress, cannot load data; please check back in 5 minutes"));
                        RespondRaw(errPage, System.Net.HttpStatusCode.Conflict);
                        return;
                    }
                }
                var lines = File.ReadAllLines(file.FullName).Skip(1).ToList();
                foreach(var line in lines)
                {
                    var array = line.Split(','); 
                    double dl = double.Parse(array[1]) / 1024;
                    double ul = double.Parse(array[2]) / 1024;
                    double pg = double.Parse(array[3]);
                    if(date == today)
                    {
                        var row = new TableRow();
                        row.Children.Add(new TableHeader(DateTime.Parse(array[0]).ToShortTimeString()));
                        row.Children.Add(new TableData(dl.ToString("00.00")));
                        row.Children.Add(new TableData(ul.ToString("00.00")));
                        row.Children.Add(new TableData(pg.ToString("00")));
                        DAILY.Children.Add(row);
                    }
                    download += dl;
                    upload += ul;
                    ping += pg;
                }
                if (i++ < mustSkip)
                    continue;
                var dlAverage = download / lines.Count;
                var ulAverage = upload / lines.Count;
                var pgAverage = ping / lines.Count;
                var weeklyRow = new TableRow()
                {
                    Children =
                    {
                        new TableHeader(date),
                        new TableData(dlAverage.ToString("00.00")),
                        new TableData(ulAverage.ToString("0.00")),
                        new TableData(pgAverage.ToString("00.0"))
                    }
                };
                WEEKLY.Children.Add(weeklyRow);
            }
            var page = getPage(new Paragraph("Average speeds across days."),
                            WEEKLY,
                            new Paragraph("Recorded speeds today"),
                            DAILY);
            RespondRaw(page.ToString(), 200);
        }

        [Method("GET"), Path("/speed")]
        public void MLBase() => Base();
    }
}
