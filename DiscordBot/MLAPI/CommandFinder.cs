﻿using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

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
            var match = new Dictionary<APIEndpoint, Match>();
            foreach(var method in methodCmds)
            {
                if(method.IsMatch(Request.Url.AbsolutePath, out var mtch))
                    match[method] = mtch;
            }
            if (match.Count == 0)
            {
                Errors.Add(new ParseResult(null).WithError("No endpoint matched the given path"));
                return false;
            }
            else
            {
                List<ParseResult> all = new List<ParseResult>();
                foreach (var cmd in match)
                {
                    all.Add(kowalski_analysis(cmd.Key, cmd.Value));
                }
                context.Endpoint = null;
                var success = all.Where(x => x.Valid).ToList();
                if (success.Count == 1)
                {
                    BestFind = success[0];
                    return true;
                }
                else if (success.Count > 1)
                {
                    Errors.Add(new ParseResult(null).WithError($"Conflict: Multiple endpoints are valid: " +
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

        ParseResult kowalski_analysis(APIEndpoint cmd, Match rgxMatch)
        {
            context.Endpoint = cmd; // temporary for PathRegex
            int weight = 0;
            var cnt = System.Activator.CreateInstance(cmd.Module.Type, context);
            var commandBase = (APIBase)cnt;
            var preconditions = new List<APIPrecondition>();
            var final = new ParseResult(cmd);
            final.CommandBase = commandBase;

            var ORS = new Dictionary<string, List<PreconditionResult>>();
            var ANDS = new Dictionary<string, List<PreconditionResult>>();
            var building = new List<APIPrecondition>();
            building.AddRange(cmd.Preconditions);
            Type parent = commandBase.GetType();
            do
            {
                var attrs = parent.GetCustomAttributes<APIPrecondition>();
                building.AddRange(attrs);
                parent = parent.BaseType;
            } while (parent != null);
            building.Reverse();
            foreach(var nextThing in building)
            {
                var previousThing = preconditions.FirstOrDefault(x => x.TypeId == nextThing.TypeId);
                if(previousThing != null)
                {
                    if (!previousThing.CanChildOverride(nextThing))
                    {
                        continue;
                    }
                    preconditions.Remove(previousThing);
                }
                preconditions.Add(nextThing);
            }
            foreach (var pred in preconditions)
            {
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

            bool requireExcessQueryMatch = true;
            foreach (var pred in preconditions)
            {
                if (pred is RequireNoExcessQuery rqp)
                    requireExcessQueryMatch = rqp.Required;
                PreconditionResult result = null;
                try
                {
                    result = pred.Check(context);
                }
                catch (HaltExecutionException e)
                {
                    final.WithException(e);
                    result = PreconditionResult.FromError(e.ToString());
                }
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
                    return final.WithError($"{string.Join(",", ls.Where(x => !x.IsSuccess).Select(x => x.ErrorReason))}");
            }

            weight += 5;

            foreach (var and in ANDS.Keys)
            {
                var ls = ANDS[and];
                var anyFailure = ls.FirstOrDefault(x => x.IsSuccess == false);
                if (anyFailure != null)
                    return final.WithError($"{string.Join(",", ls.Where(x => !x.IsSuccess).Select(x => x.ErrorReason))}");
            }

            weight += 5;

            var serverName = (RequireServerName)preconditions.Last(x => x is RequireServerName);
            if (serverName.Domain == context.Host)
                weight += 5;

            var args = new List<object>();
            var paramaters = cmd.Function.GetParameters();
            foreach (var param in paramaters)
            {
                string value = null;
                if(cmd.Regexs.TryGetValue(param.Name, out var pattern))
                {
                    var match = rgxMatch.Groups[param.Name];
                    value = match.Value;
                } else
                {
                    value = context.GetQuery(param.Name);
                }
                if (value == null && param.IsOptional == false)
                    return final.WithError($"No argument specified for required item {param.Name}");
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
                    var typeResult = Program.AttemptParseInput(value, param.ParameterType);
                    if (typeResult.IsSuccess)
                    {
                        args.Add(typeResult.BestMatch);
                    }
                    else
                    {
                        return final.WithError($"Could not parse value for {param.Name} as {param.ParameterType.Name}: {typeResult.ErrorReason}");
                    }
                }
            }
            weight += args.Count;
            foreach (var key in context.GetAllKeys())
            {
                var para = paramaters.FirstOrDefault(x => x.Name == key);
                if (para == null && requireExcessQueryMatch)
                    return final.WithError($"Unknown argument specified: {key}");
            }
            weight += 50;
            final.Arguments = args;
            return final;
        }
    }

    public class ParseResult
    {
        public int Weight { get; set; } = 0;
        public bool Valid => ErrorReason == null;
        public APIEndpoint Command { get; set; }
        public APIBase CommandBase { get; set; }
        public string ErrorReason { get; set; }

        public List<HaltExecutionException> Exceptions { get; set; }


        public bool RequiresAuthentication { get; set; }
        public List<object> Arguments { get; set; }


        public ParseResult(APIEndpoint cmd)
        {
            Exceptions = new List<HaltExecutionException>();
            Command = cmd;
        }

        public ParseResult WithException(HaltExecutionException e)
        {
            Exceptions.Add(e);
            return this;
        }
        public ParseResult WithError(string e)
        {
            ErrorReason = e;
            return this;
        }
    }

    public class CommandFindResult
    {
        public APIEndpoint Command { get; set; }
        public bool RequiresAuthentication { get; set; }
    }
}
