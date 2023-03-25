using DiscordBot.Classes.HTMLHelpers.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{
    [RequirePermNode(Perms.Bot.Developer.ViewOCRMail)]
    public class OCRMail : AuthedAPIBase
    {
        public OCRMail(APIContext ctx) : base(ctx, "ocr-mail")
        {
        }

        DirectoryInfo BaseDir { get
            {
                return new DirectoryInfo(Program.Configuration["urls:ocrdir"]);
            } }


        string getData(DirectoryInfo info, string fName, string dflt)
        {
            try
            {
                return File.ReadAllText(Path.Combine(info.FullName, fName)).Trim();
            } catch
            {
                return dflt;
            }
        }
        string getName(DirectoryInfo info) => getData(info, "name.txt", info.Name);
        string getSubject(DirectoryInfo info) => getData(info, "subject.txt", "[None set]");
        int getNextPageNum(string info)
        {
            int x = 0;
            foreach(var file in Directory.EnumerateFiles(info))
            {
                var s = Path.GetFileName(file).Split('_');
                if(s.Length == 2)
                {
                    var pagen = int.Parse(s[0]);
                    if (pagen > x)
                        x = pagen;
                }
            }
            return x + 1;
        }

        [Method("GET"), Path("/ocr/view")]
        public async Task ViewMailsFolder()
        {
            var table = new Table()
                .WithHeaderColumn("To")
                .WithHeaderColumn("From")
                .WithHeaderColumn("Date")
                .WithHeaderColumn("Subject");
            table.Children.Add(new TableRow()
            {
                Children =
                {
                    new TableData(null)
                    { 
                        Children =
                        {
                            new Anchor("/ocr/upload", "Add new")
                        },
                        ColSpan = "4"
                    }
                }
            });
            foreach(var recipient in BaseDir.EnumerateDirectories())
            {
                if (recipient.Name.StartsWith('.')) continue;
                var recName = getName(recipient);
                foreach(var sender in recipient.EnumerateDirectories())
                {
                    var sendName = getName(sender);
                    foreach (var date in sender.EnumerateDirectories())
                    {
                        var dateName = getName(date);
                        var tr = new TableRow()
                            .WithCell(recName)
                            .WithCell(sendName)
                            .WithCell(dateName)
                            .WithCell(getSubject(date))
                            .WithTag("data-link", $"/ocr/view/{recipient.Name}/{sender.Name}/{date.Name}")
                            .WithTag("onclick", "gotorow(event)");
                        table.Children.Add(tr);
                    }
                }
            }
            table.OrderChildrenDescending(x =>
            {
                if (x is not TableRow tr)
                    return DateTime.MaxValue;
                if (tr.Children[0].Tag == "th")
                    return DateTime.MaxValue;
                if (tr.Children.Count < 3)
                    return DateTime.MaxValue.AddSeconds(-2);
                var date = tr.Children[2].RawText;
                return DateTime.ParseExact(date, "yyyy-MM-dd", System.Threading.Thread.CurrentThread.CurrentCulture);
            });
            await ReplyFile("folder.html", 200,
                new Replacements().Add("table", table));
        }

        [Method("GET"), Path("/ocr/upload")]
        public async Task Upload()
        {
            var recipients = new List<Option>();
            var senders = new List<Option>();
            foreach (var recipient in BaseDir.EnumerateDirectories())
            {
                if (recipient.Name.StartsWith('.')) continue;
                var recName = getName(recipient);
                recipients.Add(new Option(recName, recipient.Name));
                foreach (var sender in recipient.EnumerateDirectories())
                {
                    var sendName = getName(sender);
                    senders.Add(new Option(sendName, sender.Name));
                }
            }
            await ReplyFile("upload.html", 200, new Replacements()
                .Add("recipients", string.Join("\n", recipients))
                .Add("senders", string.Join("\n", senders)));
        }

        string ensureDirectory(string parentPath, string dirName)
        {
            var stripped = dirName.Trim().Replace(" ", "");
            var fullPath = Path.Combine(parentPath, Program.GetSafePath(stripped));
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                if (stripped != dirName)
                    File.WriteAllText(Path.Combine(fullPath, "name.txt"), dirName);
            }
            return fullPath;
        }

        [Method("POST"), Path("/ocr/upload")]
        [RequireNoExcessQuery(false)]
        public async Task DoUpload(string recipient, string sender, string date)
        {
            var recDir = ensureDirectory(BaseDir.FullName, recipient);
            var sendDir = ensureDirectory(recDir, sender);
            var dir = ensureDirectory(sendDir, date);
            int next = getNextPageNum(dir);
            int count = 1;
            foreach(var file in Context.Files)
            {
                var ext = file.FileName.Substring(file.FileName.LastIndexOf('.'));
                using (var fs = File.OpenWrite(Path.Combine(dir, $"{next:00}_{count++}{ext}")))
                {
                    file.Data.CopyTo(fs);
                }
            }
            await RespondRedirect($"/ocr/view/{dir.Replace(BaseDir.FullName, "")}");
        }

        [Method("GET"), Path("/ocr/view/{rec}/{send}/{date}")]
        [Regex("rec", RegexAttribute.Alpha)]
        [Regex("send", RegexAttribute.Alpha)]
        [Regex("date", RegexAttribute.Date + "[a-z]?")] // add optional suffix in case multiple letters on same day
        public async Task ViewMail(string rec, string send, string date)
        {
            var path = Path.Combine(BaseDir.FullName,
                Program.GetSafePath(rec),
                Program.GetSafePath(send),
                Program.GetSafePath(date));
            var data = new List<string>();
            foreach(var file in Directory.EnumerateFiles(path).OrderBy(x => x))
            {
                var info = new FileInfo(file);
                if (info.Name.EndsWith(".txt")) continue;
                var ocr = info.Name.Replace(info.Extension, ".txt");

                data.Add("<div class='file'>");
                data.Add($"<img title=\"{info.Name}\" onclick=\"loadImg(event)\" data-src=\"/ocr/raw/{rec}/{send}/{date}/{info.Name}\"></img>");
                data.Add($"<iframe src=\"/ocr/raw/{rec}/{send}/{date}/{ocr}\"></iframe>");
                data.Add("</div>");
            }
            await ReplyFile("mail.html", 200, new Replacements().Add("pages", string.Join("\n", data)));
        }

        [Method("GET"), Path("/ocr/raw/{rec}/{send}/{date}/{file}")]
        [Regex("rec", RegexAttribute.Alpha)]
        [Regex("send", RegexAttribute.Alpha)]
        [Regex("date", RegexAttribute.Date + "[a-z]?")]
        [Regex("file", RegexAttribute.Filename)]
        public async Task FetchMailRaw(string rec, string send, string date, string file)
        {
            var path = Path.Combine(BaseDir.FullName, 
                Program.GetSafePath(rec), 
                Program.GetSafePath(send), 
                Program.GetSafePath(date), 
                Program.GetSafePath(file));
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    await ReplyStream(fs, 200);
                }
            } catch(FileNotFoundException)
            {
                if(file.EndsWith(".txt"))
                {
                    await RespondRaw("[No text recognition found]", 400);
                } else
                {
                    await RespondRaw("", 404);
                }
            }
        }
    }
}
