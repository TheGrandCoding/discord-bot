using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace DiscordBot.Services.Rules
{
    public class MaliciousLinkService : Service
    {
        string[] _hashes;
        static string data_dir = Path.Combine(Program.BASE_PATH, "data", "cache", "links");
        static string suffix_file = Path.Combine(data_dir, "suffixes.txt");
        static string hashes_file = Path.Combine(data_dir, "hashes.json");

        public const string DiscordBlacklist = "https://cdn.discordapp.com/bad-domains/hashes.json";


        private PublicSuffixList suffixList;

        public override void OnReady()
        {
            if (!Directory.Exists(data_dir))
                Directory.CreateDirectory(data_dir);

            if(!File.Exists(suffix_file) || (DateTime.Now - File.GetLastWriteTime(suffix_file)).TotalDays > 1)
            {
                using(var wc = new WebClient())
                {
                    wc.DownloadFile("https://publicsuffix.org/list/public_suffix_list.dat", suffix_file);
                }
            }
            suffixList = new PublicSuffixList(File.ReadAllLines(suffix_file));

            if(!File.Exists(hashes_file) || (DateTime.Now - File.GetLastWriteTime(hashes_file)).TotalDays > 1)
            {
                using (var wc = new WebClient())
                {
                    wc.DownloadFile(DiscordBlacklist, hashes_file);
                }
            }

            _hashes = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(hashes_file));
        }

        public bool IsUrlProhibited(Uri uri)
            => !uri.IsLoopback && IsDomainProhibited(uri.Host);
        public bool IsDomainProhibited(string domain)
        {
            if (domain == "localhost")
                return false;

            var mainDomain = suffixList.GetDomainPart(domain);
            var hash = Hash.GetSHA256(mainDomain).ToLower();
            return _hashes.Contains(hash);
        }


        class PublicSuffixList
        {
            public PublicSuffixList(string data)
                : this(data.Split(new[] { '\r', '\n' }))
            {

            }

            public PublicSuffixList(IEnumerable<string> lines)
            {
                var rules = new List<SuffixRule>();
                foreach(var ln in lines.Select(x => x.Trim()))
                {
                    if (string.IsNullOrWhiteSpace(ln))
                        continue;
                    if (ln.StartsWith("//"))
                        continue;
                    rules.Add(new SuffixRule(ln));
                }
                Rules = rules.ToArray();
            }
        
            public SuffixRule[] Rules { get; set; }


            List<SuffixRule> getMatchingRules(string domain)
            {
                var groups = domain.Split(".").Reverse().ToArray();
                var matching = new List<SuffixRule>();
                foreach(var rule in Rules)
                {
                    if (rule.Matches(domain, groups))
                        matching.Add(rule);
                }
                return matching;
            }
            public string GetDomainPart(string domain)
            {
                var matching = getMatchingRules(domain);
                
                var ordered = matching.OrderByDescending(x => x.Type == SuffixRuleType.Exception ? 1 : 0)
                    .ThenByDescending(x => x.Labels)
                    .ThenByDescending(x => x.Domain);

                var priority = ordered.FirstOrDefault();
                if (priority == null)
                    return domain;
                return priority.GetMatching(domain);
            }
        }

        [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
        class SuffixRule
        {
            public string Domain { get; set; }
            public SuffixRuleType Type { get; set; }
            public int Labels { get; set; }

            public SuffixRule(string line)
            {
                Labels = line.Split(".").Length;
                Domain = line.ToLower();
                if(line.StartsWith("!"))
                {
                    Type = SuffixRuleType.Exception;
                    Domain = Domain[1..];
                    Labels -= 1;
                } else if (line.Contains("*"))
                {
                    Type = SuffixRuleType.Wildcard;
                } else
                {
                    Type = SuffixRuleType.Normal;
                }

            }
        
            public bool Matches(string domain, string[] domainParts)
            {
                int i = -1;
                foreach(var part in Domain.Split(".").Reverse())
                {
                    i++;
                    if (part == "*")
                        continue;
                    if (i >= domainParts.Length)
                        return false;
                    if (domainParts[i] != part)
                        return false;
                }
                return true;
            }

            public string GetMatching(string otherDomain)
            {
                var parts = otherDomain.Split(".").Reverse().ToList();
                return string.Join(".", parts.Take(Domain.Split(".").Count() + 1).Reverse());
            }

            public override string ToString()
            {
                return (Type == SuffixRuleType.Exception ? "!" : "") + Domain;
            }
            private string GetDebuggerDisplay()
            {
                return ToString();
            }
        }

        enum SuffixRuleType
        {
            Normal,
            Wildcard,
            Exception
        }

    }
}
