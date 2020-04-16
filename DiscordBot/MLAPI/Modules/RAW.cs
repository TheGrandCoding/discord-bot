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
        public RAW(APIContext context) : base(context, "/")
        {
        }

        string display(bool value)
        {
            return value ? "list-item;" : "none;"; 
        }

        [Path("/"), Method("GET")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void BaseHTML()
        {
            if(Context.User == null)
            {
                ReplyFile("_base_nologin.html", 200);
            } else
            {
                ReplyFile("_base.html", 200, new Replacements());
            }
        }

        [Method("GET"), PathRegex(@"(\/|\\)_(\/|\\)((js|css|img)(\/|\\)[a-zA-Z0-9\/.-]*.\.(js|css|png|jpeg))")]
        [AllowNonAuthed(ConditionIfAuthed = true)]
        public void BackgroundWork()
        {
            if(Context.Endpoint == null || !(Context.Endpoint.Path is PathRegex pR))
            {
                RespondRaw("Failed internal settings", HttpStatusCode.InternalServerError);
                return;
            }
            var regex = new System.Text.RegularExpressions.Regex(pR.Path);
            var match = regex.Match(Context.HTTP.Request.Url.AbsolutePath);
            if(!match.Success)
            {
                RespondRaw("Failed to identify request resource.", HttpStatusCode.BadRequest);
                return;
            }
            var filePath = match.Groups[3].Value;
            var fileExtension = match.Groups[6].Value;
            var mimeType = getMimeType(fileExtension);
            var fullPath = Path.Combine(Program.BASE_PATH, "HTTP", "_", filePath);
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
            return "image/" + extension;
        }

        [Method("GET"), Path("/builtin")]
        [RequireUser(144462654201790464)]
        public void BuiltInAccounts()
        {
            var TABLE = "";
            bool change = false;
            foreach(var usrs in Program.Users.Where(x => x.BuiltIn))
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
    }
}
