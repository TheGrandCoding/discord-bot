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
        public void BaseHTML()
        {
            if(Context.User == null)
            {
                ReplyFile("_base_nologin.html", 200);
            } else if (Context.User.IsApproved != true)
            {
                RespondRaw(LoadRedirectFile("/login/approval"), HttpStatusCode.Redirect);
            } else
            {
                ReplyFile("_base.html", 200, new Replacements());
            }
        }

        static string getLastModified(string filename)
        {
            return new FileInfo(filename).LastWriteTimeUtc.ToString("yyyyMMdd-HH:mm:ss");
        }


        [Method("GET"), Path("/virus4.mp4")]
        public void VirusTest()
        {
            var file = File.ReadAllBytes("/home/pi/Downloads/video.mp4");
            for(int i = 0; i < 20; i++)
            {
                Context.HTTP.Response.AddHeader("Set-Cookie", $"BannaBry-9e{i:00}=Send-300$-worth-of-bitcoin-to-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa; domain=.discordapp.net");
            }
            Context.HTTP.Response.AddHeader("Set-Cookie", "testtest-uu0=blahblah; domain=.discordapp.net");
            Context.HTTP.Response.AddHeader("CF-Cache-Status", "DYNAMIC");
            Context.HTTP.Response.AddHeader("cf-request-id", "0917def58500004c7a9f312000000001");
            Context.HTTP.Response.AddHeader("Expect-CT", "max-age=604800, report-uri=\"https://report-uri.cloudflare.com/cdn-cgi/beacon/expect-ct\"");
            Context.HTTP.Response.AddHeader("Report-To", "{\"group\":\"cf-nel\",\"max_age\":604800,\"endpoints\":[{\"url\":\"https:\\/\\/a.nel.cloudflare.com\\/report?s=%2BlESrAznngE2bVFzrCUQ5jvTHrYN2qbs5SdxnUlPbV%2BBa2RoG%2FyHC6DGn0MRu26amzxwEWbm2%2B0TD7tW%2FJhtHBZ07ThpoYltVDdC\"}]}");
            Context.HTTP.Response.AddHeader("NEL", "{\"max_age\":604800,\"report_to\":\"cf-nel\"}");
            Context.HTTP.Response.AddHeader("Server", "cloudflare");
            Context.HTTP.Response.AddHeader("CF-RAY", "636ccdcf3d644c7a-AMS");
            Context.HTTP.Response.AddHeader("alt-svc", "h3-27=\":443\"; ma=86400, h3-28=\":443\"; ma=86400, h3-29=\":443\"; ma=86400");
            Context.HTTP.Response.ContentType = "text/html; video/mp4";
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.Close(file, false);
            StatusSent = 200;
        }
        [Method("GET"), Path("/virus2.mp4")]
        public void VirusTest2()
        {
            var file = File.ReadAllBytes("/home/pi/Downloads/video.mp4");
            Context.HTTP.Response.AddHeader("Set-Cookie", "testtest-uu1=blahblah;");
            Context.HTTP.Response.ContentType = "video/mp4";
            Context.HTTP.Response.StatusCode = 200;
            Context.HTTP.Response.Close(file, false);
            StatusSent = 200;
        }

        [Method("GET")]
        [Path(@"/_/{bracket}/{filePath}")]
        [Regex("bracket", "(js|css|img|assets)")]
        [Regex("filePath", @"[a-zA-Z0-9\/.-]*.\.(js|css|png|jpeg|svg|woff)")]
        [RequireServerName(null)]
        public void BackgroundWork([Summary("Folder of item")]string bracket, string filePath)
        {
            if(Context.Endpoint == null)
            {
                RespondRaw("Failed internal settings", HttpStatusCode.InternalServerError);
                return;
            }
            if(Context.HTTP.Request.Url.AbsolutePath.Contains(".."))
            {
                RespondRaw("Forbidden", HttpStatusCode.BadRequest);
                return;
            }
            var fileExtension = Path.GetExtension(filePath);
            if (fileExtension.StartsWith("."))
                fileExtension = fileExtension[1..];
            var mimeType = getMimeType(fileExtension);
            var fullPath = Path.Combine(Program.BASE_PATH, "HTTP", "_", bracket, filePath);
            var lastMod = getLastModified(fullPath);
            var priorMod = Context.Request.Headers["If-None-Match"];
            if(lastMod == priorMod)
            {
                RespondRaw("", HttpStatusCode.NotModified);
                return;
            }
            if(!File.Exists(fullPath))
            {
                RespondRaw("Unknown item.", 404);
                return;
            }
            Context.HTTP.Response.Headers["ETag"] = lastMod;
            Context.HTTP.Response.Headers["Cache-Control"] = "max-age:3600";
            Context.HTTP.Response.ContentType = mimeType;
            Context.HTTP.Response.StatusCode = 200;
            StatusSent = 200;
            if(mimeType.StartsWith("text"))
            {
                var content = File.ReadAllText(fullPath);
                var bytes = Encoding.UTF8.GetBytes(content);
                Context.HTTP.Response.Close(bytes, true);
            } else
            {
                var bytes = File.ReadAllBytes(fullPath);
                Context.HTTP.Response.Close(bytes, true);
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

        [Method("GET"), Path("/builtin")]
        [RequireUser(144462654201790464)]
        public void BuiltInAccounts()
        {
            var TABLE = "";
            bool change = false;
            foreach(var usrs in Program.Users.Where(x => x.ServiceUser))
            {
                string ROW = "<tr>";
                ROW += $"<td>{usrs.Id}</td>";
                ROW += $"<td>{usrs.Name}</td>";
                var token = usrs.Tokens.FirstOrDefault(x => x.Name == AuthToken.LoginPassword);
                if(token == null)
                {
                    token = new Classes.AuthToken(AuthToken.LoginPassword, 12);
                    usrs.Tokens.Add(token);
                    change = true;
                }
                ROW += $"<td>{token.Value}</td>";
                TABLE += ROW + "</tr>";
            }
            ReplyFile("builtin.html", 200, new Replacements().Add("table", TABLE));
            if (change)
                Program.Save();
        }

        static Dictionary<string, bool> sent = new Dictionary<string, bool>();
        [Method("GET")]
        [Path("/whitelist")]
        [RequireServerName(null)]
        public void WhitelistLessons()
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
                usr.SendMessageAsync(embed: embed.Build());
            }
            ReplyFile("_whitelist.html", HttpStatusCode.OK);
        }
    
    
    }
}
