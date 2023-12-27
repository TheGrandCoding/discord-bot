using Discord;
using Discord.Commands;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.RESTAPI.Functions.HTML
{
    public class RAW : APIBase
    {
        public RAW(APIContext context) : base(context, "")
        {
        }

        string display(bool value)
        {
            return value ? "list-item;" : "none;"; 
        }

        [Path("/"), Method("GET")]
        public async Task BaseHTML()
        {
            if(Context.User == null)
            {
                await ReplyFile("_base_nologin.html", 200);
            } else if (Context.User.Approved != true)
            {
                await RespondRedirect("/login/approval");
            } else
            {
                await ReplyFile("_base.html", 200, new Replacements());
            }
        }

        [Method("GET"), Path("/gh-catch")]
        public async Task GHCatch()
        {
            await ReplyFile("gh-catch.html", 200);
        }

        static string getLastModified(string filename)
        {
            return new FileInfo(filename).LastWriteTimeUtc.ToString("yyyyMMdd-HH:mm:ss");
        }

        [Method("GET")]
        [Path(@"/_/{bracket}/{filePath}")]
        [Regex("bracket", "(js|css|img|assets)")]
        [Regex("filePath", @"[a-zA-Z0-9\/._-]*.\.(js|css|png|jpeg|svg|woff|mp3)")]
        [RequireNoExcessQuery(false)]
        [HostAttribute(null)]
        public async Task BackgroundWork([Summary("Folder of item")]string bracket, string filePath)
        {
            if(Context.Endpoint == null)
            {
                await RespondRaw("Failed internal settings", HttpStatusCode.InternalServerError);
                return;
            }
            if(Context.HTTP.Request.Url.AbsolutePath.Contains(".."))
            {
                await RespondRaw("Forbidden", HttpStatusCode.BadRequest);
                return;
            }
            var fileExtension = Path.GetExtension(filePath);
            if (fileExtension.StartsWith("."))
                fileExtension = fileExtension[1..];
            var mimeType = getMimeType(fileExtension);
            var fullPath = Path.Combine(Program.BASE_PATH, "HTTP", "_", bracket, Program.GetSafePath(filePath));
            var lastMod = getLastModified(fullPath);
            var priorMod = Context.Request.Headers["If-None-Match"];
            if (lastMod == priorMod)
            {
                await RespondRaw("", HttpStatusCode.NotModified);
                return;
            }
            if (!File.Exists(fullPath))
            {
                await RespondRaw("Unknown item.", 404);
                return;
            }
            Context.HTTP.Response.Headers["ETag"] = lastMod;
            Context.HTTP.Response.Headers["Cache-Control"] = "max-age:3600";
            Context.HTTP.Response.ContentType = mimeType;

            using (var fs = File.OpenRead(fullPath))
            {
                await ReplyStream(fs, 200);
            }
        }

        string getMimeType(string extension)
        {
            if (extension == "js")
                return "text/javascript";
            if (extension == "css")
                return "text/css";
            if (extension == "svg")
                return "image/svg+xml";
            if (extension == "woff")
                return "application/font-woff";
            return "image/" + extension;
        }

        static Dictionary<string, bool> sent = new Dictionary<string, bool>();
        [Method("GET")]
        [Path("/whitelist")]
        public async Task WhitelistLessons()
        {
            if (!sent.ContainsKey(Context.IP))
            {
                sent[Context.IP] = true;
                var usr = Program.AppInfo.Owner;
                var embed = new EmbedBuilder();
                embed.Title = "Lesson Whitelist";
                embed.Description = "Attempt to access lesson path by an unwhitelisted user.";
                if(Context.User != null)
                    embed.AddField("Authed User", Context.User.Name);
                foreach(string header in Context.Request.Headers.Keys)
                {
                    var value = Context.Request.Headers[header];
                    embed.AddField(header, Program.Clamp(value, 256));
                    if (embed.Fields.Count >= 25)
                        break;
                }
                await usr.SendMessageAsync(embed: embed.Build());
            }
            await ReplyFile("_whitelist.html", HttpStatusCode.OK);
        }
    
        [Method("GET"), Path("/dfa")]
        public async Task DFA()
        {
            await ReplyFile("dfa.html", 200);
        }
    
    }
}
