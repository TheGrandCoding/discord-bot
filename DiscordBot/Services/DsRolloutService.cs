using Discord;
using Discord.Rest;
using Discord.SlashCommands;
using Discord.WebSocket;
using DiscordBot.Classes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public Cached<bool> HasRecentlySynced = new Cached<bool>(false, 60);

        public ConcurrentDictionary<ulong, GuildSave> Guilds { get; set; } = new ConcurrentDictionary<ulong, GuildSave>();

        public ConcurrentDictionary<ulong, IThreadChannel> CachedThreads { get; set; } = new ConcurrentDictionary<ulong, IThreadChannel>();

        public SlashCommandService SlashService { get; set; }

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
            var http = Program.Services.GetRequiredService<Classes.BotHttpClient>()
                .Child(nameof(DsRolloutService), debug: true);
            var response = await http.GetAsync("https://rollouts.advaith.workers.dev");

            if (!response.IsSuccessStatusCode)
                return null;

            var text = await response.Content.ReadAsStringAsync();
            var arr = JArray.Parse(text);

            return arr.Select(x => Experiment.Create(x as JObject)).ToList();
        }

        public override void OnDailyTick()
        {
            if(Guilds.Count > 0)
            {
                updateTask().Wait();
            }
        }

        async Task<IThreadChannel> getThreadFor(GuildSave guild, Experiment experiment, IMessage message)
        {
            IThreadChannel thread = await guild.Channel.Guild.GetThreadChannelAsync(message.Id);
            if (thread != null)
                return thread;

            if(guild.CachedThreads.Count == 0)
            {
                IThreadChannel[] archivedThreads = await guild.Channel.GetPublicArchivedThreadsAsync();
                foreach (var th in archivedThreads)
                    guild.CachedThreads[th.Id] = th;
            }
            if (guild.CachedThreads.TryGetValue(message.Id, out thread))
                return thread;

            thread = await guild.Channel.CreateThreadAsync(Program.Clamp(experiment.Title, 100), message: message);
            return thread;
        }

        public async Task sendMessageFor(Experiment experiment, EmbedBuilder builder, bool updateMain = true)
        {
            foreach((var guildId, var guildSave) in Guilds)
            {
                IUserMessage message;
                if(!guildSave.Messages.TryGetValue(experiment.Id, out message))
                {
                    message = await guildSave.Channel.SendMessageAsync(embed: experiment.ToEmbed().Build());
                    guildSave.Messages[experiment.Id] = message;
                    updateMain = false;
                }

                if (updateMain) 
                { 
                    await message.ModifyAsync(x => { x.Embed = experiment.ToEmbed().Build(); });
                }

                IThreadChannel thread = await getThreadFor(guildSave, experiment, message);
                if(builder != null)
                    await thread.SendMessageAsync(embed: builder.Build());
            }
        }

        public async Task updateTask()
        {
            var fromAPIExperiments = await GetCurrentExperiments();

            var updatedExperiments = new List<Experiment>();

            var existingExperiments = Experiments.Values.ToList();

            bool changes = false;
            foreach(var updatedExp in fromAPIExperiments)
            {
                var existing = existingExperiments.FirstOrDefault(x => x.Id == updatedExp.Id);
                EmbedBuilder builder;
                if(existing == null)
                { // this is a brand new experiment
                    // send messages, etc

                    updatedExperiments.Add(updatedExp);
                    changes = true;

                    await sendMessageFor(updatedExp, null);
                    continue;
                }
                existingExperiments.RemoveAll(x => x.Id == existing.Id);
                // compare them, see if equal
                builder = new EmbedBuilder();

                if(existing.Removed)
                { // maybe re-added?
                    existing.Removed = false;
                    builder.Color = Color.Green;
                    builder.AddField("Experiment Re-added", "This experiment was previously removed");
                }

                Func<int, string> percF = (int i) => $"{((i / 10000d) * 100):00.0}%";

                var ec = existing.GetChanges(updatedExp);
                foreach(var change in ec)
                {
                    builder.AddField($"{change.Type}", (change.Before ?? "null") + "\n" + (change.After ?? "null"), true);
                }


                if(builder.Fields.Count > 0)
                {
                    builder.Title = $"Changes";
                    existing.Update(updatedExp);
                    changes = true;

                    await sendMessageFor(updatedExp, builder);
                }
                updatedExperiments.Add(existing);
            }

            if(existingExperiments.Count > 0)
            { // these have been removed
                changes = true;
                foreach (var exp in existingExperiments)
                {
                    if (exp.Removed)
                        continue; // we've already sent a message about this
                    exp.Removed = true;
                    exp.LastChanged = DateTime.Now;
                    updatedExperiments.Add(exp);
                    await sendMessageFor(exp, new EmbedBuilder()
                        .WithDescription("Experiment removed").WithColor(Color.Red));
                }
            }

            HasRecentlySynced.Value = true;
            if(changes)
            {
                Experiments.Clear();
                foreach (var exp in updatedExperiments)
                    Experiments[exp.Id] = exp;

                OnSave();
            }
            foreach (var guild in Guilds)
                guild.Value.CachedThreads = new Dictionary<ulong, IThreadChannel>();
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

            [JsonIgnore]
            public Dictionary<ulong, IThreadChannel> CachedThreads { get; set; } = new Dictionary<ulong, IThreadChannel>();
        }

        public async Task<IEnumerable<AutocompleteResult>> GetAutocomplete(SocketAutocompleteInteraction interaction)
        {
            if(!HasRecentlySynced.GetValueOrDefault(false))
            {
                await updateTask();
            }
            var ls = new List<AutocompleteResult>();
            string text = interaction.Data.Current.Value.ToString();

            foreach(var experiment in Experiments.Values)
            {
                if(string.IsNullOrWhiteSpace(text) 
                    || experiment.Id.Contains(text, StringComparison.OrdinalIgnoreCase) 
                    || experiment.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    var result = new AutocompleteResult(experiment.Title, experiment.Id);
                    ls.Add(result);
                }
            }
            return ls;
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

        [JsonProperty("latest_change")]
        public DateTime LastChanged { get; set; }

        [JsonProperty("removed")]
        public bool Removed { get; set; }

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
            experiment.LastChanged = DateTime.Now;

            var rollouts = obj["rollout"] as JArray;
            experiment.Rollout = Rollout.Create(rollouts);

            return experiment;
        }
    
        public void Update(Experiment experiment)
        {
            this.Id = experiment.Id;
            this.Type = experiment.Type;
            this.Title = experiment.Title;
            this.Treatments = experiment.Treatments;
            this.Buckets = experiment.Buckets;
            this.Hash = experiment.Hash;
            this.Rollout = experiment.Rollout;
            this.LastChanged = DateTime.Now;
        }

        string getDescription(int id)
        {
            var s = $"Treatment {id}";
            foreach (var x in Treatments.Values)
                if (x.StartsWith(s))
                    return x;
            return "None";
        }

        public EmbedBuilder ToEmbed()
        {
            var builder = new EmbedBuilder();
            builder.WithTitle(Title);

            if(Removed)
            {
                builder.Color = Color.Red;  
            } else
            {
                var diff = DateTime.Now - LastChanged;
                if(diff.TotalDays < 7)
                {
                    builder.Color = Color.Green;
                } else
                {
                    builder.Color = Color.Orange;
                }
            }

            var treatmentCounts = new Dictionary<int, int>();

            var rl = this.Rollout;
            var dict = rl.GetFilteredGroupedPopulations();

            var sb = new StringBuilder();
            foreach((var filters, var grouping) in dict)
            {
                foreach (var f in filters)
                    sb.Append($"- {f}\n");
                sb.Append("```\n");

                bool doneControl = false;
                int sum = 0;
                foreach ((var treatId, var pops) in grouping)
                {
                    var count = pops.Sum(x => x.Count);
                    sum += count;
                    var perc = count / (double)Rollout.TotalCount;
                    string pstr = $"{(perc * 100):00.0}% ";
                    if (pstr[0] == '0')
                        pstr = " " + pstr[1..];
                    sb.Append(pstr);
                    sb.Append(getDescription(treatId));
                    sb.Append("\n");
                }
                if(sum < Rollout.TotalCount)
                {
                    var controlSum = Rollout.TotalCount - sum;
                    var controlPerc = controlSum / (double)Rollout.TotalCount;
                    string cpstr = $"{(controlPerc * 100):00.0}% ";
                    if (cpstr[0] == '0')
                        cpstr = " " + cpstr[1..];
                    sb.Append($"{cpstr} " + Treatments.GetValueOrDefault(0, "Control"));
                }
                sb.Append("```\n");
            }

            builder.WithDescription(sb.ToString());

            builder.WithFooter(Id);
            builder.WithTimestamp(new DateTimeOffset(LastChanged));

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

        public override bool Equals(object obj)
        {
            if (!(obj is Experiment e))
                return false;
            return GetChanges(e).Count == 0;
        }

        public List<Change> GetChanges(Experiment e) 
        { 
            if (!this.Id.Equals(e.Id))
                return new Change("Id", this.Id, e.Id);
            if (!this.Title.Equals(e.Title))
                return new Change("Title", this.Title, e.Title);
            if (!this.Hash.Equals(e.Hash))
                return new Change("Hash", $"{this.Hash}", $"{e.Hash}");
            return this.Rollout.GetChanges(e.Rollout);
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

        [JsonProperty("groups")]
        public List<PopulationGroups> Populations { get; set; } = new List<PopulationGroups>();

        public Dictionary<Filter[], Dictionary<int, List<Population>>> GetFilteredGroupedPopulations()
        {
            var dict = new Dictionary<Filter[], List<Population>>();
            foreach (var group in Populations)
            {
                var filterArray = group.Filters.ToArray();
                dict[filterArray] = group.Populations;
            }

            var fullDict = new Dictionary<Filter[], Dictionary<int, List<Population>>>();
            foreach((var filter, var pops) in dict)
            {
                var inner = new Dictionary<int, List<Population>>();
                foreach (var x in pops)
                    DiscordBot.Utils.EnumerableUtils.AddInner(inner, x.Bucket, x);
                fullDict[filter] = inner;
            }
            return fullDict;
        }


        [JsonIgnore] // don't really need to know this
        public List<BucketOverride> Overrides { get; set; }

        [JsonIgnore]
        public const int TotalCount = 10_000; // constant it seems
            //=> (Populations ?? new List<Population>()).Sum(x => x.Count);


        public int? GetTreatmentId(string expId, ulong serverId, int? memberCount, string[] features)
        {
            var bytes = Encoding.ASCII.GetBytes($"{expId}:{serverId}");
            using var algo = Murmur.MurmurHash.Create32();
            var hash = algo.ComputeHash(bytes);


            var intHash = BitConverter.ToUInt32(hash);
            var value = intHash % 10_000;
            var highestFilters = int.MinValue;
            Population highest = null;
            foreach(var group in Populations)
            {
                var pop = group.GetPopulation(value, serverId, memberCount, features);
                if(pop != null)
                {
                    if(group.Filters.Count > highestFilters)
                    {
                        highestFilters = group.Filters.Count;
                        highest = pop;
                    }
                }
            }
            return highest?.Bucket ?? -1;
        }

        public static Rollout Create(JArray array)
        {
            var rl = new Rollout();
            rl.Hash = array[0].ToObject<ulong>();
            // [1] is null
            rl.Unknown = array[2].ToObject<int>();

            var populations = array[3];
            rl.Populations = populations
                .Select(x => PopulationGroups.Create(x))
                .ToList();

            var overrides = array[4];

            rl.Overrides = overrides.Select(x => BucketOverride.Create(x))
                .ToList();

            return rl;
        }


        public List<Change> GetChanges(Rollout r)
        {
            if (!this.Hash.Equals(r.Hash))
                return new Change("Hash", $"{this.Hash}", $"{r.Hash}");
            var thisFilterGrouped = this.GetFilteredGroupedPopulations();
            var otherFilterGrouped = r.GetFilteredGroupedPopulations();
            if (thisFilterGrouped.Count != otherFilterGrouped.Count)
                return new Change("FilterGroupCount", $"{thisFilterGrouped.Count}", $"{otherFilterGrouped.Count}");
            var keys = new List<Filter[]>();
            keys.AddRange(thisFilterGrouped.Keys);
            keys.AddRange(otherFilterGrouped.Keys);
            keys = keys.Distinct().ToList();

            foreach(var filterKey in keys)
            {
                if (!thisFilterGrouped.TryGetValue(filterKey, out var thisGroup))
                    return new Change($"+FilterGroup", null, $"{filterKey}");
                if (!otherFilterGrouped.TryGetValue(filterKey, out var otherGroup))
                    return new Change($"-FilterGroup", $"{filterKey}", null);

                var treatments = new List<int>();
                treatments.AddRange(thisGroup.Keys);
                treatments.AddRange(otherGroup.Keys);
                treatments = treatments.Distinct().ToList();

                foreach(var treatment in treatments)
                {
                    if (!thisGroup.TryGetValue(treatment, out var thisPop))
                        return new Change($"+TreatPop", null, $"{treatment}");
                    if (!otherGroup.TryGetValue(treatment, out var otherPop))
                        return new Change($"-TreatPop", $"{treatment}", null);

                    var thisSum = thisPop.Sum(x => x.Count);
                    var otherSum = otherPop.Sum(x => x.Count);

                    if (thisSum != otherSum)
                        return new Change($"Treat{treatment}Sum", $"{thisSum}", $"{otherSum}");
                }
            }
            return new List<Change>();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Rollout r))
                return false;
            return this.GetChanges(r).Count == 0;
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
            foreach (var rng in Ranges)
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

    public class PopulationGroups
    {
        [JsonConstructor]
        private PopulationGroups() { }
        [JsonProperty("p")]
        public List<Population> Populations { get; set; } = new List<Population>();

        [JsonProperty("f", ItemTypeNameHandling = TypeNameHandling.All)]
        public List<Filter> Filters { get; set; } = new List<Filter>();

        public Population GetPopulation(uint value, ulong serverId, int? memberCount, string[] features)
        {
            if (Filters != null)
            {
                foreach (var f in Filters)
                {
                    if (f is IDFilter id)
                    {
                        if (serverId < id.Start || serverId > id.End)
                            return null; // doesn't meet filter
                    }
                    else if (f is MemberCountFilter mf)
                    {
                        if (!memberCount.HasValue)
                            return null;
                        if (memberCount.Value < mf.Start || memberCount.Value > mf.End)
                            return null;
                    }
                    else if (f is FeatureFilter ff)
                    {
                        if (features == null)
                            return null;
                        foreach (var k in ff.Features)
                            if (!features.Contains(k))
                                return null;
                    }
                }
            }
            foreach (var x in Populations)
                if (x.InPopulation(value))
                    return x;
            return null;
        }
    
        public static PopulationGroups Create(JToken token)
        {
            var grp = new PopulationGroups();
            grp.Populations = token[0].Select(x => Population.Create(x)).ToList();
            grp.Filters = token[1].Select(x => Filter.Create(x)).ToList();
            return grp;
        }
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

        public override int GetHashCode()
        {
            return (int)Type;
        }
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

        public override int GetHashCode()
        {
            return $"{Type}:{Features.GetHashCode()}".GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (!(obj is FeatureFilter f))
                return false;
            return Features.All(x => f.Features.Contains(x));
        }
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

        public override int GetHashCode()
        {
            return $"{Type}:{Start}-{End}".GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (!(obj is IDFilter idf))
                return false;
            return this.Start == idf.Start && this.End == idf.End;
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

        public override bool Equals(object obj)
        {
            if (!(obj is MemberCountFilter mcf))
                return false;
            return this.Start == mcf.Start && this.End == mcf.End;
        }
        public override int GetHashCode()
        {
            return $"{Type}-{Start}-{End}".GetHashCode();
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
