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
                        .WithMeta(Meta.Charset("UTF-8"))
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
                if(today == date)
                {
                    var diff = DateTime.UtcNow - file.LastWriteTimeUtc;
                    int remainders = (int)diff.TotalMinutes % 60;
                    if(remainders >= 58)
                    {
                        var errPage = getPage(new Paragraph("Speed test is in progress, cannot load data; please check back in 5 minutes"));
                        RespondRaw(errPage, System.Net.HttpStatusCode.Conflict);
                        return;
                    }
                }
                var lines = File.ReadAllLines(file.FullName).Skip(1).ToList();
                var STATS = new DayStats(date);
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
                    STATS.Add(dl, ul, pg);
                }
                if (i++ < mustSkip)
                    continue;
                WEEKLY.Children.Add(STATS.Row);
            }
            var page = getPage(new Paragraph("Average speeds across days."),
                            WEEKLY,
                            new Paragraph("Recorded speeds today"),
                            DAILY);
            RespondRaw(page.ToString(), 200);
        }

        [Method("GET"), Path("/speed")]
        public void MLBase() => Base();
    
    
    
        class DayStats
        {
            public string Date { get; set; }
            public string Download => strGet(DlValues);
            public string Upload => strGet(UlValues);
            public string Ping => strGet(PnValues);

            (double average, double percUnc) get(List<double> values)
            {
                var avg = values.Average();
                var range = values.Max() - values.Min();
                var absUnc = range / 2;
                return (avg, absUnc / avg);
            }

            string strGet(List<double> values)
            {
                (double average, double percUnc) = get(values);
                return $"{average:#0.00} ± {(percUnc * 100):#0}%";
            }

            public TableRow Row { get
                {
                    return new TableRow(id: Date)
                    {
                        Children =
                        {
                            new TableData(Date),
                            new TableData(Download),
                            new TableData(Upload),
                            new TableData(Ping)
                        }
                    };
                } }

            List<double> DlValues { get; set; } = new List<double>();
            List<double> UlValues { get; set; } = new List<double>();
            List<double> PnValues { get; set; } = new List<double>();

            public void Add(double dl, double ul, double pn)
            {
                DlValues.Add(dl);
                UlValues.Add(ul);
                PnValues.Add(pn);
            }

            public DayStats(string date)
            {
                Date = date;
            }
        }
    }
}
