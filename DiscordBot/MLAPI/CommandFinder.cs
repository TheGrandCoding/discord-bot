using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace DiscordBot.MLAPI
{
    public class CommandFinder
    {
        public APIContext context;
        public ParseResult BestFind { get; private set; }
        public bool Success => BestFind != null;
        public List<ParseResult> Errors { get; private set; } = new List<ParseResult>();
        public HttpListenerRequest Request { get; }
        List<APIEndpoint> methodCmds;
        public CommandFinder(APIContext con, List<APIEndpoint> cmds)
        {
            context = con;
            Request = con.Request;
            methodCmds = cmds;
        }
        public bool Search()
        {
            var match = methodCmds.Where(x => x.Path.IsMatch(Request.Url.AbsolutePath)).ToList();
            if (match.Count == 0)
            {
                Errors.Add(new ParseResult("No endpoint matched the given path"));
                return false;
            }
            else
            {
                List<ParseResult> all = new List<ParseResult>();
                foreach (var cmd in match)
                {
                    all.Add(kowalski_analysis(cmd));
                }
                var success = all.Where(x => x.Valid).ToList();
                if (success.Count == 1)
                {
                    BestFind = success[0];
                    return true;
                }
                else if (success.Count > 1)
                {
                    Errors.Add(new ParseResult($"Conflict: Multiple endpoints are valid: " +
                    string.Join(", ", success.Select(x => x.Command.fullInfo()))));
                    return false;
                }
                // Multiple errors: we must decide whether to display all of them or just one or some.
                var errors = all.Where(x => !x.Valid).ToList();
                var orderWeight = errors.OrderByDescending(x => x.Weight);
                // Find which one is the best option
                var highest = orderWeight.First();
                // But what if multiple have the same weight and also failed?
                var othersAlsoHighest = errors.Where(x => x.Weight == highest.Weight);
                Errors.AddRange(othersAlsoHighest);
                return false;
            }
        }

        ParseResult kowalski_analysis(APIEndpoint cmd)
        {
            int weight = 0;
            var cnt = System.Activator.CreateInstance(cmd.Module, context);
            var commandBase = (APIBase)cnt;
            var preconditions = new List<APIPrecondition>();
            preconditions.AddRange(cmd.Preconditions);
            preconditions.AddRange(commandBase.BasePreconditions);

            bool requiresAuth = true;
            var ORS = new Dictionary<string, List<PreconditionResult>>();
            var ANDS = new Dictionary<string, List<PreconditionResult>>();
            foreach (var pred in preconditions)
            {
                if (pred.Overriden)
                    continue;
                if (pred is AllowNonAuthed allow)
                {
                    requiresAuth = false;
                }
                else
                {
                    var others = preconditions.Where(x => x != pred && x.GetType() == pred.GetType());
                    var first = others.FirstOrDefault();
                    if (first != null)
                    {
                        if (pred.CanChildOverride(first))
                            first.Overriden = true;
                    }
                }
                if (string.IsNullOrWhiteSpace(pred.OR))
                {
                    ANDS.TryAdd(pred.AND, new List<PreconditionResult>());
                }
                else
                {
                    ORS.TryAdd(pred.OR, new List<PreconditionResult>());
                    if (!string.IsNullOrWhiteSpace(pred.AND))
                        ANDS.TryAdd(pred.AND, new List<PreconditionResult>());
                }
            }

            if(requiresAuth && context.User == null)
            {
                return new ParseResult(cmd, "Requires authentication (login)")
                {
                    RequiresAuthentication = true,
                };
            }

            foreach (var pred in preconditions)
            {
                if (pred.Overriden)
                    continue;
                var result = pred.Check(context);
                if (ORS.TryGetValue(pred.OR, out var orls))
                    orls.Add(result);
                if((string.IsNullOrWhiteSpace(pred.AND) && string.IsNullOrWhiteSpace(pred.OR))
                    || !string.IsNullOrWhiteSpace(pred.AND))
                    if (ANDS.TryGetValue(pred.AND, out var andls))
                        andls.Add(result);
            }

            foreach (var or in ORS.Keys)
            {
                var ls = ORS[or];
                var anySuccess = ls.FirstOrDefault(x => x.IsSuccess);
                if (anySuccess == null)
                    return new ParseResult(cmd, $"{string.Join(",", ls.Where(x => !x.IsSuccess).Select(x => x.ErrorReason))}");
            }

            weight += 5;

            foreach (var and in ANDS.Keys)
            {
                var ls = ANDS[and];
                var anyFailure = ls.FirstOrDefault(x => x.IsSuccess == false);
                if (anyFailure != null)
                    return new ParseResult(cmd, $"{string.Join(",", ls.Where(x => !x.IsSuccess).Select(x => x.ErrorReason))}");
            }

            weight += 5;

            var args = new List<object>();
            var paramaters = cmd.Function.GetParameters();
            foreach (var param in paramaters)
            {
                var value = context.GetQuery(param.Name);
                if (value == null && param.IsOptional == false)
                    return new ParseResult(cmd, $"No argument specified for required item {param.Name}");
                if (value == null)
                {
                    args.Add(param.DefaultValue);
                    continue;
                }
                if (param.ParameterType == typeof(string))
                {
                    args.Add(Uri.UnescapeDataString(value));
                }
                else
                {
                    var typeResult = attemptParse(value, param.ParameterType);
                    if (typeResult.IsSuccess)
                    {
                        args.Add(typeResult.BestMatch);
                    }
                    else
                    {
                        return new ParseResult(cmd, $"Could not parse value for {param.Name} as {param.ParameterType.Name}: {typeResult.ErrorReason}");
                    }
                }
            }
            weight += args.Count;
            foreach (var key in context.GetAllKeys())
            {
                var para = paramaters.FirstOrDefault(x => x.Name == key);
                if (para == null)
                    return new ParseResult(cmd, $"Unknown argument specified: {key}");
            }
            weight += 50;
            var final = new ParseResult(cmd, null);
            final.RequiresAuthentication = requiresAuth;
            final.CommandBase = commandBase;
            final.Arguments = args;
            return final;
        }

        Discord.Commands.TypeReaderResult attemptParse(string input, Type desired)
        {
            var thing = Program.Commands.GetType().GetField("_defaultTypeReaders", BindingFlags.NonPublic | BindingFlags.Instance);
            var defaultTypeReaders = thing.GetValue(Program.Commands) as IDictionary<Type, Discord.Commands.TypeReader>;

            Dictionary<Type, Discord.Commands.TypeReader> combined = new Dictionary<Type, Discord.Commands.TypeReader>();
            foreach (var keypair in defaultTypeReaders)
                combined.Add(keypair.Key, keypair.Value);
            foreach (var keypair in Program.Commands.TypeReaders)
                combined[keypair.Key] = keypair?.FirstOrDefault();

            var reader = combined[desired];
            if (reader == null)
            {
                return Discord.Commands.TypeReaderResult.FromError(
                    Discord.Commands.CommandError.Exception, $"Endpoint expects parser for {desired.Name}, but unavailable - my error; not yours");
            }
            var result = reader.ReadAsync(null, input, Program.Services).Result;
            if (result.IsSuccess)
            {
                return Discord.Commands.TypeReaderResult.FromSuccess(result.BestMatch);
            }
            else
            {
                return Discord.Commands.TypeReaderResult.FromError(
                    Discord.Commands.CommandError.ParseFailed, result.ErrorReason);
            }
        }
    }

    public class ParseResult
    {
        public int Weight { get; set; } = 0;
        public bool Valid => ErrorReason == null;
        public APIEndpoint Command { get; set; }
        public APIBase CommandBase { get; set; }
        public string ErrorReason { get; set; }


        public bool RequiresAuthentication { get; set; }
        public List<object> Arguments { get; set; }


        public ParseResult(APIEndpoint cmd, string r)
        {
            Command = cmd;
            ErrorReason = r;
        }
        public ParseResult(string r) : this(null, r) { }
    }

    public class CommandFindResult
    {
        public APIEndpoint Command { get; set; }
        public bool RequiresAuthentication { get; set; }
    }
}
