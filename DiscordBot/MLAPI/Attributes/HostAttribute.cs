using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using WebSocketSharp;

namespace DiscordBot.MLAPI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)] // inherit handled manually
    public class HostAttribute : Attribute
    {
        public IEnumerable<string> Domain
        {
            get
            {
                if (_domains == null || _domains.Length == 0)
                    yield break;
                
                for(int i = 0; i < _domains.Length; i++)
                {
                    var _domain = _domains[i];
                    if (_domain.StartsWith("c:"))
                    {
                        _domain = _domain.Substring("c:".Length);
                        _domain = Program.Configuration[$"domains:{_domain}"];
                        _domains[i] = _domain;
                    }
                    if (_domain.StartsWith("a:"))
                    {
                        _domain = _domain.Substring("a:".Length);
                        _domain = _domain + "." + Handler.LocalAPIDomain;
                        _domains[i] = _domain;
                    }
                    yield return _domain;
                }
            }
        }
        private string[] _domains;
        public HostAttribute(params string[] domains)
        {
            _domains = domains;
        }

        public bool IsMatch(string host)
        {
            if (_domains == null) return true;
            return Domain.Any(x => x == host);
        }
    }
}
