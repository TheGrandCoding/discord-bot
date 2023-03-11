using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using WebSocketSharp;

namespace DiscordBot.MLAPI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)] // inherit handled manually
    public class HostAttribute : Attribute
    {
        public string Domain
        {
            get
            {
                if (_domain == null)
                    return _domain;
                if (_domain.StartsWith("c:"))
                {
                    _domain = _domain.Substring("c:".Length);
                    _domain = Program.Configuration[$"domains:{_domain}"];
                }
                if(_domain.StartsWith("a:"))
                {
                    _domain = _domain.Substring("a:".Length);
                    _domain = _domain + "." + Handler.LocalAPIDomain;
                }
                return _domain;
            }
        }
        private string _domain;
        public HostAttribute(string domainName, bool failIfdebug = false)
        {
            _domain = domainName;
        }

        public bool IsMatch(string host)
        {
            if (Domain == null) return true;
            return Domain == host;
        }
    }
}
