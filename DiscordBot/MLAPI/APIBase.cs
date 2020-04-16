using DiscordBot.MLAPI.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.MLAPI
{
    public class APIBase
    {
        public APIBase(APIContext context, string path)
        {
            Context = context;
            BaseFolder = path;
        }
        private string BaseFolder { get; set; }
        public APIContext Context { get; set; }

        public List<APIPrecondition> BasePreconditions { get; protected set; } = new List<APIPrecondition>();

        public int StatusSent { get; set; } = 0;
        protected bool HasResponded => StatusSent != 0;

        public virtual void RespondRaw(string obj, int code = 200)
        {
            StatusSent = code;
            var bytes = System.Text.Encoding.UTF8.GetBytes(obj);
            Context.HTTP.Response.StatusCode = code;
            Context.HTTP.Response.Close(bytes, true);
        }

        public void RespondRaw(string obj, HttpStatusCode code)
            => RespondRaw(obj, (int)code); 

        protected string LoadFile(string path)
        {
            string proper = Path.Combine(Program.BASE_PATH, "HTTP", BaseFolder, path);
            return File.ReadAllText(proper, Encoding.UTF8);
        }

        const string matchRegex = "[<$]REPLACE id=['\"](\\S+)['\"]\\/[>$]";
        protected string ReplaceMatches(string input, Replacements replace)
        {
            replace.Add("logged", Context.User);
            var REGEX = new Regex(matchRegex);
            var match = REGEX.Match(input);
            while(match != null && match.Success && match.Captures.Count > 0 && match.Groups.Count > 1)
            {
                var key = match.Groups[1].Value;
                replace.TryGetValue(key, out var obj);
                var value = obj?.ToString() ?? "";
                input = input.Replace(match.Groups[0].Value, value);
                match = REGEX.Match(input);
            }
            return input;
        }

        protected void ReplyFile(string path, int code, Replacements replace = null)
        {
            var f = LoadFile(path);
            replace ??= new Replacements();
            var replaced = ReplaceMatches(f, replace);
            RespondRaw(replaced, code);
        }

        protected void ReplyFile(string path, HttpStatusCode code, Replacements replace = null)
            => ReplyFile(path, (int)code, replace);

        public virtual void BeforeExecute() { }
        public virtual void ResponseHalted(HaltExecutionException ex) { }
        public virtual void AfterExecute() { }
    }
}
