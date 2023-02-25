﻿using DiscordBot.Classes.HTMLHelpers.Objects;
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


        string getName(DirectoryInfo info)
        {
            try
            {
                return File.ReadAllText(Path.Combine(info.FullName, "name.txt")).Trim();
            } catch
            {
                return info.Name;
            }
        }
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
        public void ViewMailsFolder()
        {
            var table = new Table()
                .WithHeaderColumn("To")
                .WithHeaderColumn("From")
                .WithHeaderColumn("Date")
                .WithHeaderColumn("Pages");
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
                    foreach(var date in sender.EnumerateDirectories())
                    {
                        var dateName = getName(date);
                        var pageFiles = date.EnumerateFiles();
                        var tr = new TableRow()
                            .WithCell(recName)
                            .WithCell(sendName)
                            .WithCell(dateName)
                            .WithCell(string.Join(", ", pageFiles.Select(x => x.Name)))
                            .WithTag("data-link", $"/ocr/view/{recipient.Name}/{sender.Name}/{date.Name}")
                            .WithTag("onclick", "gotorow(event)");
                        table.Children.Add(tr);
                    }
                }
            }
            ReplyFile("folder.html", 200,
                new Replacements().Add("table", table));
        }

        [Method("GET"), Path("/ocr/upload")]
        public void Upload()
        {
            ReplyFile("upload.html", 200);
        }

        [Method("POST"), Path("/ocr/upload")]
        [RequireNoExcessQuery(false)]
        public void DoUpload(string recipient, string sender, DateTime date)
        {
            var dir = Path.Combine(BaseDir.FullName, Program.GetSafePath(recipient), Program.GetSafePath(sender), $"{date:yyyy-MM-dd}");
            if(!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
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
            RespondRedirect($"/ocr/view/{dir.Replace(BaseDir.FullName, "")}");
        }

        [Method("GET"), Path("/ocr/view/{rec}/{send}/{date}")]
        [Regex("rec", RegexAttribute.Alpha)]
        [Regex("send", RegexAttribute.Alpha)]
        [Regex("date", RegexAttribute.Date)]
        public void ViewMail(string rec, string send, string date)
        {
            var path = Path.Combine(BaseDir.FullName, rec, send, date);
            var data = new List<string>();
            foreach(var file in Directory.EnumerateFiles(path))
            {
                var info = new FileInfo(file);
                if (info.Name.EndsWith(".txt")) continue;
                var ocr = info.Name.Replace(info.Extension, ".txt");

                data.Add("<div class='file'>");
                data.Add($"<img title=\"{info.Name}\" src=\"/ocr/raw/{rec}/{send}/{date}/{info.Name}\"></img>");
                data.Add($"<iframe src=\"/ocr/raw/{rec}/{send}/{date}/{ocr}\"></iframe>");
                data.Add("</div>");
            }
            ReplyFile("mail.html", 200, new Replacements().Add("pages", string.Join("\n", data)));
        }

        [Method("GET"), Path("/ocr/raw/{rec}/{send}/{date}/{file}")]
        [Regex("rec", RegexAttribute.Alpha)]
        [Regex("send", RegexAttribute.Alpha)]
        [Regex("date", RegexAttribute.Date)]
        [Regex("file", RegexAttribute.Filename)]
        public void FetchRaw(string rec, string send, string date, string file)
        {
            var path = Path.Combine(BaseDir.FullName, rec, send, date, Program.GetSafePath(file));
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    ReplyStream(fs, 200);
                }
            } catch(FileNotFoundException)
            {
                if(file.EndsWith(".txt"))
                {
                    RespondRaw("[No text recognition found]", 400);
                } else
                {
                    RespondRaw("", 404);
                }
            }
        }
    }
}
