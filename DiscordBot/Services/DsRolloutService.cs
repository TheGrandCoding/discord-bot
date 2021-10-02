using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class DsRolloutService : SavedService
    {
        public ConcurrentDictionary<string, Experiment> Experiments { get; set; } = new ConcurrentDictionary<string, Experiment>();

        public ConcurrentDictionary<ulong, GuildSave> Guilds { get; set; } = new ConcurrentDictionary<ulong, GuildSave>();


        public override string GenerateSave()
        {
            var sv = new serviceSave()
            {
                experiments = Experiments.Values.ToList(),
                guilds = new Dictionary<ulong, GuildSave>(Guilds)
            };
            return Program.Serialise(sv);
        }

        public async Task<List<Experiment>> GetCurrentExperiments()
        {
            var http = Program.Services.GetRequiredService<HttpClient>();
            var response = await http.GetAsync("https://rollouts.advaith.workers.dev");

            if (!response.IsSuccessStatusCode)
                return null;

            var text = await response.Content.ReadAsStringAsync();
            var arr = JArray.Parse(text);

            return arr.Select(x => Experiment.Create(x as JObject)).ToList();
        }

        public override void OnDailyTick()
        {
            updateTask().Wait();
        }

        public async Task sendMessageFor(Experiment experiment, EmbedBuilder builder)
        {
            foreach((var guildId, var guildSave) in Guilds)
            {
                var guild = Program.Client.GetGuild(guildId);
                IUserMessage message;
                if(!guildSave.Messages.TryGetValue(experiment.Id, out message))
                {
                    message = await guildSave.Channel.SendMessageAsync(embed: experiment.ToEmbed().Build());
                    guildSave.Messages[experiment.Id] = message;
                }

                IThreadChannel thread = guild.GetThreadChannel(message.Id);
                // TODO: the thread might have been archived in the time
                //       however it's ID should be the same as the message
                //       so if we can get the archived threads, we can find it.
                if (thread == null)
                {
                    thread = await guildSave.Channel.CreateThreadAsync(experiment.Title, message: message);
                }
                if(builder != null)
                    await thread.SendMessageAsync(embed: builder.Build());
            }
        }

        async Task updateTask()
        {
            if (Guilds.Count == 0)
                return;
            var currentExperiments = await GetCurrentExperiments();
            var existingExperiments = Experiments.Values.ToList();

            bool changes = false;
            foreach(var updatedExp in currentExperiments)
            {
                var existing = existingExperiments.FirstOrDefault(x => x.Id == updatedExp.Id);
                EmbedBuilder builder;
                if(existing == null)
                { // this is a brand new experiment
                    // send messages, etc
                    changes = true;
                    builder = updatedExp.ToEmbed();
                    builder.Color = Color.Green;
                    await sendMessageFor(updatedExp, builder);
                    continue;
                }

                // compare them, see if equal
                builder = new EmbedBuilder();

                // look at the treatments to see if they're changed
                var treatmentIds = new List<int>() { -1 };
                treatmentIds.AddRange(existing.Treatments.Keys);
                treatmentIds.AddRange(updatedExp.Treatments.Keys);
                treatmentIds = treatmentIds.Distinct().ToList();

                Func<int, string> percF = (int i) => $"{((i / 10000) * 100):00.0}%";

                foreach(var id in treatmentIds)
                {
                    var existingPop = existing.Rollout.Populations.FirstOrDefault(x => x.Bucket == id);
                    var newPop = updatedExp.Rollout.Populations.FirstOrDefault(x => x.Bucket == id);

                    if(newPop == null && existingPop == null)
                    { // ?
                        continue;
                    } else if (newPop == null)
                    { // treatment removed
                        builder.AddField($"Removed {id}",
                            $"{existing.Treatments[id]}\n{existingPop.Count}", true);
                    } else if(existingPop == null)
                    { // treatment added
                        builder.AddField($"Added {id}",
                            $"{updatedExp.Treatments[id]}\n{newPop.Count}");
                    } else
                    { // treatment maybe modified?
                        if(newPop.Count != existingPop.Count)
                        {
                            builder.AddField($"Modified % {id}",
                                $"{updatedExp.Treatments[id]}\n" +
                                $"{existingPop.Count} -> **{newPop.Count}** - {percF(newPop.Count)}");
                        }
                    }

                }

                // TODO: maybe look at filters to see if they've changed

                if(builder.Fields.Count > 0)
                {
                    builder.Title = $"Changes";

                    changes = true;
                    await sendMessageFor(updatedExp, builder);
                }
            }

            if(changes)
            {
                Experiments.Clear();
                foreach (var item in currentExperiments)
                    Experiments[item.Id] = item;
                OnSave();
            }
        }

        public static bool getInt(string name, out int i)
        {
            var mtch = Regex.Match(name, "\\d+");
            if (mtch != null && mtch.Success)
            {
                i = int.Parse(mtch.Value);
                return true;
            }
            i = 0;
            return false;
        }

        public override void OnReady()
        {
            var sv = Program.Deserialise<serviceSave>(ReadSave("{}"));
            foreach(var exp in (sv.experiments ?? new List<Experiment>()))
                Experiments[exp.Id] = exp;
            Guilds = new ConcurrentDictionary<ulong, GuildSave>(sv.guilds ?? new Dictionary<ulong, GuildSave>());
        }

        public class serviceSave
        {
            public List<Experiment> experiments { get; set; }

            public Dictionary<ulong, GuildSave> guilds { get; set; }
        }

        public class GuildSave
        {
            public Dictionary<string, IUserMessage> Messages { get; set; }
            public ITextChannel Channel { get; set; }
        }
    }


    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class Experiment
    {
        [JsonConstructor]
        private Experiment() { }
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("treatments")]
        public Dictionary<int, string> Treatments { get; set; }
        [JsonProperty("buckets")]
        public List<int> Buckets { get; set; }
        [JsonProperty("hash")]
        public ulong Hash { get; set; }

        [JsonIgnore]
        private string DebuggerDisplay
        {
            get
            {
                return $"{Id} {Type} {Title} {Hash}";
            }
        }

        [JsonProperty("rollout")]
        public Rollout Rollout { get; set; }

        public static Experiment Create(JObject obj)
        {
            var experiment = new Experiment();
            var data = obj["data"];
            experiment.Id = data["id"].ToObject<string>();
            experiment.Type = data["type"].ToObject<string>();
            experiment.Title = data["title"].ToObject<string>();
            var description = data["description"].ToObject<string[]>();
            experiment.Treatments = new Dictionary<int, string>();
            foreach(var d in description)
            {
                if (DsRolloutService.getInt(d, out var id))
                    experiment.Treatments[id] = d;
                else
                    experiment.Treatments[-1] = d;
            }
            experiment.Buckets = data["buckets"].ToObject<List<int>>();
            experiment.Hash = data["hash"].ToObject<ulong>();

            var rollouts = obj["rollout"] as JArray;
            experiment.Rollout = Rollout.Create(rollouts);

            return experiment;
        }
    
        public EmbedBuilder ToEmbed()
        {
            var builder = new EmbedBuilder();
            builder.WithTitle(Title);

            var treatmentCounts = new Dictionary<int, int>();

            var rl = this.Rollout;
            if (rl.Filters.Count > 0)
            {
                builder.AddField("Filters", string.Join("\n", rl.Filters.Select(x => x.ToString())), true);
            }
            
            foreach(var pop in rl.Populations)
            {
                treatmentCounts[pop.Bucket] = pop.Count;
            }
            treatmentCounts[-1] = rl.ControlCount;

            var sb = new StringBuilder();
            foreach((var id, var treatment) in Treatments)
            {
                if (treatmentCounts.TryGetValue(id, out var count))
                {
                    var perc = count / (double)Rollout.TotalCount;
                    string pstr = $"{(perc * 100):00.0}% ";
                    if(pstr[0] == '0')
                        pstr = " " + pstr[1..];
                    sb.Append(pstr);
                } else
                {
                    sb.Append("  --  ");
                }
                
                sb.Append(treatment);
                sb.Append("\n");
            }

            builder.WithDescription("```\n" + sb.ToString() + "```");

            builder.WithFooter(Id);

            return builder;
        }

        public string GetTreatment(SocketGuild guild)
            => GetTreatment(guild.Id, guild.MemberCount, guild.Features.ToArray());

        public string GetTreatment(ulong serverId)
            => GetTreatment(serverId, null, null);
    
        public string GetTreatment(ulong serverId, int? memberCount, string[] features)
        {
            var id = Rollout.GetTreatmentId(Id, serverId, memberCount, features);
            if (!id.HasValue)
                return null;
            if (id == -1)
                return "Control";
            return Treatments.GetValueOrDefault(id.Value, $"Treatment {id}");
        }
    }

    public class Rollout
    {
        [JsonConstructor]
        private Rollout() { }

        [JsonProperty("hash")]
        public ulong Hash { get; set; }
        [JsonProperty("u")]
        public int Unknown { get; set; }

        [JsonProperty("populations")]
        public List<Population> Populations { get; set; }

        [JsonProperty("filters", ItemTypeNameHandling = TypeNameHandling.All)]
        public List<Filter> Filters { get; set; }

        [JsonIgnore] // don't really need to know this
        public List<BucketOverride> Overrides { get; set; }

        [JsonIgnore]
        public int AlternativeCount { get
            {
                int c = 0;
                foreach (var thing in Populations)
                    if (thing.Bucket != -1)
                        c += thing.Count;
                return c;
            } }

        [JsonIgnore]
        public int ControlCount => TotalCount - AlternativeCount;

        [JsonIgnore]
        public const int TotalCount = 10_000; // constant it seems
            //=> (Populations ?? new List<Population>()).Sum(x => x.Count);


        public int? GetTreatmentId(string expId, ulong serverId, int? memberCount, string[] features)
        {
            foreach(var f in Filters)
            {
                if(f is IDFilter id)
                {
                    if (serverId < id.Start || serverId > id.End)
                        return null; // doesn't meet filter
                } else if(f is MemberCountFilter mf && memberCount.HasValue)
                {
                    if (memberCount.Value < mf.Start || memberCount.Value > mf.End)
                        return null;
                } else if (f is FeatureFilter ff && features != null)
                {
                    foreach (var k in ff.Features)
                        if (!features.Contains(k))
                            return null;
                }
            }

            var bytes = Encoding.ASCII.GetBytes($"{expId}:{serverId}");
            using var algo = Murmur.MurmurHash.Create32();
            var hash = algo.ComputeHash(bytes);


            var intHash = BitConverter.ToUInt32(hash);
            var value = intHash % 10_000;
            foreach(var pop in Populations)
            {
                if (pop.InPopulation(value))
                    return pop.Bucket;
            }
            return -1; // default
        }

        public static Rollout Create(JArray array)
        {
            var rl = new Rollout();
            rl.Hash = array[0].ToObject<ulong>();
            // [1] is null
            rl.Unknown = array[2].ToObject<int>();

            var bigArray = array[3][0];

            var populations = bigArray[0];
            rl.Populations = populations.Select(x => Population.Create(x))
                                        .ToList();

            var filters = bigArray[1];

            rl.Filters = filters.Select(x => Filter.Create(x))
                .ToList();

            var overrides = array[4];

            rl.Overrides = overrides.Select(x => BucketOverride.Create(x))
                .ToList();

            return rl;
        }
    }

    public struct PopulationRange
    {
        [JsonProperty("s")]
        public int Start { get; set; }
        [JsonProperty("e")]
        public int End { get; set; }

        [JsonIgnore]
        public int Count => End - Start;

        public override string ToString()
            => $"{Start}-{End}";
    }

    public class Population
    {
        [JsonProperty("b")]
        public int Bucket { get; set; }

        [JsonProperty("r")]
        public List<PopulationRange> Ranges { get; set; } = new List<PopulationRange>();

        [JsonIgnore]
        public int Count => Ranges.Sum(x => x.Count);

        public bool InPopulation(uint value)
        {
            foreach(var rng in Ranges)
            {
                if (value >= rng.Start && value < rng.End)
                    return true;
            }
            return false;
        }

        public static Population Create(JToken x)
        {
            var pop = new Population();
            pop.Bucket = x[0].ToObject<int>();
            foreach(var group in x[1])
            {
                var rng = new PopulationRange()
                {
                    Start = group["s"].ToObject<int>(),
                    End = group["e"].ToObject<int>()
                };
                pop.Ranges.Add(rng);
            }
            return pop;
        }
        public override string ToString() 
            => $"{Bucket} {string.Join(", ", Ranges.Select(x => x.ToString()))}";
    }

    public enum FilterType : ulong
    {
        Feature         = 1604612045,
        ID              = 2404720969,
        MemberCount     = 2918402255
    }

    public abstract class Filter
    {
        [JsonProperty("t")]
        public virtual FilterType Type { get; }

        public static Filter Create(JToken x)
        {
            var type = x[0].ToObject<FilterType>();
            if (type == FilterType.Feature)
                return FeatureFilter.Create(x);
            else if (type == FilterType.ID)
                return IDFilter.Create(x);
            else if (type == FilterType.MemberCount)
                return MemberCountFilter.Create(x);
            throw new ArgumentException($"Unknown filter type: {type}");
        }

        public override string ToString()
            => $"{Type}";
    }

    public class FeatureFilter : Filter
    {
        public override FilterType Type => FilterType.Feature;
        [JsonProperty("f")]
        public string[] Features { get; set; }

        public new static Filter Create(JToken x)
        {
            var f = new FeatureFilter();
            f.Features = x[1][0][1].ToObject<string[]>();
            return f;
        }

        public override string ToString()
            => "Server has feature" + (Features.Length > 0 ? "s" : "") + " "
                + string.Join(" ", Features);
    }

    public class IDFilter : Filter
    {
        public override FilterType Type => FilterType.ID;
        [JsonProperty("s")]
        public ulong Start { get; set; }
        [JsonProperty("e")]
        public ulong End { get; set; }

        public new static Filter Create(JToken x)
        {
            var idf = new IDFilter();
            idf.Start = x[1][0][1].ToObject<ulong?>() ?? ulong.MinValue;
            idf.End = x[1][1][1].ToObject<ulong?>() ?? ulong.MaxValue;
            return idf;
        }


        public override string ToString()
        {
            if(Start == ulong.MinValue)
            {
                return $"Server has ID before {End} " +
                    $"({TimestampTag.FromDateTime(SnowflakeUtils.FromSnowflake(End).UtcDateTime)})";
            } else if (End == ulong.MaxValue)
            {
                return $"Server has ID after {Start} " +
                    $"({TimestampTag.FromDateTime(SnowflakeUtils.FromSnowflake(Start).UtcDateTime)})";
            } else
            {
                return $"Server has ID in range {Start} " +
                    $"({TimestampTag.FromDateTime(SnowflakeUtils.FromSnowflake(Start).UtcDateTime)}) " +
                    $"to {End} ({TimestampTag.FromDateTime(SnowflakeUtils.FromSnowflake(Start).UtcDateTime)})";
            }
        }
    }

    public class MemberCountFilter : Filter
    {
        public override FilterType Type => FilterType.MemberCount;
        [JsonProperty("s")]
        public int Start { get; set; }
        [JsonProperty("e")]
        public int End { get; set; }


        public new static Filter Create(JToken x)
        {
            var mcf = new MemberCountFilter();
            mcf.Start = x[1][0][1].ToObject<int?>() ?? 0;
            mcf.End = x[1][1][1].ToObject<int?>() ?? int.MaxValue;
            return mcf;
        }

        public override string ToString()
        {
            if(Start == 0)
            {
                return $"Server has member count below {End}";
            } else if(End == int.MaxValue)
            {
                return $"Server has member count over {Start}";
            } else
            {
                return $"Server has member count in range [{Start}, {End}]";
            }
        }
    }

    public class BucketOverride
    {
        [JsonProperty("b")]
        public int Bucket { get; set; }
        [JsonProperty("ids")]
        public string[] ServerIds { get; set; }

        public static BucketOverride Create(JToken x)
        {
            var bo = new BucketOverride();
            bo.Bucket = x["b"].ToObject<int>();
            bo.ServerIds = x["k"].ToObject<string[]>();
            return bo;
        }

        public override string ToString() => $"{Bucket} [{string.Join(", ", ServerIds)}]";
    }
}
