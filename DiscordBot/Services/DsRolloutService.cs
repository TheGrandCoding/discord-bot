using Discord;
using Discord.Rest;
using Discord.Interactions;
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
using System.Threading;
using System.Threading.Tasks;
using static DiscordBot.Utils.JsonUtils;

namespace DiscordBot.Services
{
    [Classes.Attributes.AlwaysSync]
    public class DsRolloutService : SavedService
    {
        public ConcurrentDictionary<string, Experiment> Experiments { get; set; } = new ConcurrentDictionary<string, Experiment>();

        public Cached<bool> HasRecentlySynced = new Cached<bool>(false, 60);

        public ConcurrentDictionary<ulong, GuildSave> Guilds { get; set; } = new ConcurrentDictionary<ulong, GuildSave>();

        public ConcurrentDictionary<ulong, IThreadChannel> CachedThreads { get; set; } = new ConcurrentDictionary<ulong, IThreadChannel>();

        public CommandHandlingService CommandHandlingService { get; set; }

        InteractionService SlashService => CommandHandlingService.InteractionService;

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
            http.DefaultRequestHeaders.Add("referer", "https://rollouts.advaith.io");
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.84 Safari/537.36");
            var response = await http.GetAsync("https://api.rollouts.advaith.io/");

            response.EnsureSuccessStatusCode();

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
                IReadOnlyCollection<IThreadChannel> archivedThreads;
                DateTimeOffset? before = DateTimeOffset.UtcNow;
                do
                {
                    archivedThreads = await guild.Channel.GetPublicArchivedThreadsAsync(before);
                    foreach (var th in archivedThreads)
                    {
                        guild.CachedThreads[th.Id] = th;
                        if (before == null || before > th.ArchiveTimestamp)
                            before = th.ArchiveTimestamp;
                    }
                } while (archivedThreads.Count > 0);
            }
            if (guild.CachedThreads.TryGetValue(message.Id, out thread))
                return thread;

            try
            {
                thread = await guild.Channel.CreateThreadAsync(Program.Clamp(experiment.Title, 100), autoArchiveDuration: ThreadArchiveDuration.OneWeek, message: message);
                guild.CachedThreads[message.Id] = thread;
            } catch(Discord.Net.HttpException ex)
            {
                Error(ex, "getThreadFor");
                Error($"Errored when trying to create a thread for {experiment.Id} {experiment.Title}");
            }
            return thread;
        }

        public async Task sendMessageFor(Experiment experiment, EmbedBuilder builder, bool updateMain = true)
        {
            var token = Program.GetToken();
            var retry = new RequestOptions()
            {
                CancelToken = token,
                RetryMode = RetryMode.AlwaysRetry,
                Timeout = 10_000
            };
            foreach((var guildId, var guildSave) in Guilds)
            {
                IUserMessage message;
                if(!guildSave.Messages.TryGetValue(experiment.Id, out message))
                {
                    message = await guildSave.Channel.SendMessageAsync(embed: experiment.ToEmbed().Build(), options: retry);
                    guildSave.Messages[experiment.Id] = message;
                    updateMain = false;
                }

                if (updateMain) 
                { 
                    await message.ModifyAsync(x => { x.Embed = experiment.ToEmbed().Build(); }, options: retry);
                }

                IThreadChannel thread = await getThreadFor(guildSave, experiment, message);
                if(builder != null)
                    await thread.SendMessageAsync(embed: builder.Build(), options: retry);
                await Task.Delay(5000);
            }
        }

        public async Task updateTask()
        {
            HasRecentlySynced.Value = true;
            var fromAPIExperiments = await GetCurrentExperiments();
            if(fromAPIExperiments.Count <= 1)
            {
                Warning($"Only {fromAPIExperiments.Count} experiments from API, refusing to evaluate.");
                return;
            }

            var updatedExperiments = new List<Experiment>();

            var existingExperiments = Experiments.Values.ToList();

            bool changes = false;
            foreach(var updatedExp in fromAPIExperiments)
            {
                var existing = existingExperiments.FirstOrDefault(x => x.Id == updatedExp.Id);
                EmbedBuilder builder = null;
                if(existing == null)
                { // this is a brand new experiment
                    // send messages, etc

                    updatedExperiments.Add(updatedExp);
                    changes = true;

                    await sendMessageFor(updatedExp, builder);
                    continue;
                }
                existingExperiments.RemoveAll(x => x.Id == existing.Id);
                // compare them, see if equal

                if(existing.Removed)
                { // maybe re-added?
                    existing.Removed = false;
                    builder = new EmbedBuilder();
                    builder.Color = Color.Green;
                    builder.Title = "Experiment Re-added";
                    builder.Description = "This experiment was previously removed";
                }

                Func<int, string> percF = (int i) => $"{((i / 10000d) * 100):00.0}%";

                var ec = existing.GetChanges(updatedExp);
                if(ec.Count > 0) {
                    builder = updatedExp.ToEmbed();
                    builder.Title = "Updated";
                    foreach(var cng in ec.Take(EmbedBuilder.MaxFieldCount - builder.Fields.Count))
                    {
                        builder.AddField(cng.Type, $"{cng.Before}\r\n{cng.After}", true);
                    }
                }


                if(builder != null)
                {
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

        public async Task<IEnumerable<AutocompleteResult>> GetAutocomplete(IAutocompleteInteraction interaction)
        {
            if (!HasRecentlySynced.GetValueOrDefault(false))
            {
                await updateTask();
            }
            var ls = new List<AutocompleteResult>();
            string text = interaction.Data.Current.Value.ToString();

            foreach (var experiment in Experiments.Values)
            {
                if (string.IsNullOrWhiteSpace(text)
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
            experiment.Id = data.GetOrDefault<string>("id");
            experiment.Type = data.GetOrDefault<string>("type");
            experiment.Title = data.GetOrDefault<string>("title");
            var description = data.GetOrDefault<string[]>("description", new string[0]);
            experiment.Treatments = new Dictionary<int, string>();
            foreach(var d in description)
            {
                if (DsRolloutService.getInt(d, out var id))
                    experiment.Treatments[id] = d;
                else
                    experiment.Treatments[-1] = d;
            }
            experiment.Buckets = data.GetOrDefault<List<int>>("buckets", new List<int>());
            experiment.Hash = data.GetOrDefault<ulong>("hash");
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

        public string GetTreatment(SocketGuild guild, StringBuilder log)
            => GetTreatment(guild.Id, guild.MemberCount, guild.Features, log);

        public string GetTreatment(ulong serverId, StringBuilder log)
            => GetTreatment(serverId, null, null, log);
    
        public string GetTreatment(ulong serverId, int? memberCount, GuildFeatures features, StringBuilder log)
        {
            var id = Rollout.GetTreatmentId(Id, serverId, memberCount, features, log);
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


        public int? GetTreatmentId(string expId, ulong serverId, int? memberCount, GuildFeatures features, StringBuilder log)
        {
            log.AppendLine($"Hashing `{expId}:{serverId}`:");
            var bytes = Encoding.ASCII.GetBytes($"{expId}:{serverId}");
            using var algo = Murmur.MurmurHash.Create32();
            var hash = algo.ComputeHash(bytes);


            var intHash = BitConverter.ToUInt32(hash);
            log.AppendLine($"= Raw int: {intHash}");
            var value = intHash % 10_000;
            log.AppendLine($"= Actual: **{value}**");
            var highestFilters = int.MinValue;
            Population highest = null;
            log.AppendLine($"Looking into {Populations.Count} groups");
            int i = 0;
            foreach(var group in Populations)
            {
                log.AppendLine($"Group {i++}:");
                var pop = group.GetPopulation(value, serverId, memberCount, features, log);
                
                if(pop != null)
                {
                    log.AppendLine($"-- In pop {pop.Bucket}");
                    if(group.Filters.Count > highestFilters)
                    {
                        highestFilters = group.Filters.Count;
                        highest = pop;
                    }
                } else
                {
                    log.AppendLine($"-- Not in pop.");
                }
            }
            return highest?.Bucket ?? -1;
        }

        public static Rollout Create(JArray array)
        {
            var rl = new Rollout();
            rl.Hash = array.AtOrDefault<ulong>(0);
            // [1] is null
            rl.Unknown = array.AtOrDefault<int>(2);

            var populations = array[3];
            if(populations != null)
            {
                rl.Populations = populations
                    .Select(x => PopulationGroups.Create(x))
                    .ToList();
            }

            var overrides = array[4];
            if(overrides != null)
            {
                rl.Overrides = overrides.Select(x => BucketOverride.Create(x))
                    .ToList();
            }

            return rl;
        }


        public List<Change> GetChanges(Rollout r)
        {
            if (!this.Hash.Equals(r.Hash))
                return new Change("Hash", $"{this.Hash}", $"{r.Hash}");

            var thisPops = Populations;
            var otherPops = r.Populations;
            if(thisPops.Count < otherPops.Count)
            {
                return new Change("New population group", $"{thisPops.Count}", $"{otherPops.Count}");
            } else if (thisPops.Count > otherPops.Count)
            {
                return new Change("Removed population group", $"{thisPops.Count}", $"{otherPops.Count}");
            } else
            {
                var c = new List<Change>();
                for(int i = 0; i < thisPops.Count; i++)
                {
                    c.AddRange(thisPops[i].GetChanges(otherPops[i])
                        .Select(x => new Change($"Groups[{i}].{x.Type}", x.Before, x.After))
                    );
                }
                return c;
            }
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

        public List<Change> GetChanges(Population otherPop)
        {
            if (Count != otherPop.Count)
                return new Change("Size changed", Count, otherPop.Count);
            return new List<Change>();
        }
    }

    public class PopulationGroups
    {
        [JsonConstructor]
        private PopulationGroups() { }
        [JsonProperty("p")]
        public List<Population> Populations { get; set; } = new List<Population>();

        [JsonProperty("f", ItemTypeNameHandling = TypeNameHandling.All)]
        public List<Filter> Filters { get; set; } = new List<Filter>();

        public Population GetPopulation(uint value, ulong serverId, int? memberCount, GuildFeatures features, StringBuilder log)
        {
            var failsFilters = new List<string>();
            if (Filters != null)
            {
                foreach (var f in Filters)
                {
                    if (f is IDRangeFilter id)
                    {
                        if (serverId < id.Start || serverId > id.End)
                        {
                            failsFilters.Add($"Does not meet ID range filter: {id.Start} -> {id.End}");
                        }
                    }
                    else if (f is MemberCountFilter mf)
                    {
                        if (!memberCount.HasValue)
                        {
                            failsFilters.Add("Could not fetch member count, there is a filter on that.");
                            continue;
                        }
                        if (memberCount.Value < mf.Start || memberCount.Value > mf.End)
                        {
                            failsFilters.Add($"Member count out of range: {mf.Start} -> {mf.End}");
                        }
                    }
                    else if (f is FeatureFilter ff)
                    {
                        if (features == null)
                        {
                            failsFilters.Add("Could not fetch features, there is a filter on that.");
                            continue;
                        }
                        var any = false;
                        foreach (var k in ff.Features)
                        {
                            if (features.HasFeature(k))
                            {
                                any = true;
                                break;
                            }
                        }
                        failsFilters.Add($"Server has none of the following features: [{string.Join(", ", ff.Features)}]");
                    } else if(f is RangeByHashFilter rbh)
                    {
                        failsFilters.Add($"Could not computer range by hash filter.");
                    }
                }
            }
            if(failsFilters.Count > 0)
            {
                foreach (var x in failsFilters)
                    log.AppendLine($"- {x}");
                return null;
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

        public List<Change> GetChanges(PopulationGroups other)
        {
            var changes = new List<Change>();
            if(this.Populations.Count < other.Populations.Count)
            {
                return new Change("New population", $"{this.Populations.Count}", $"{other.Populations.Count}");
            } else if(this.Populations.Count > other.Populations.Count)
            {
                return new Change("Removed population", $"{this.Populations.Count}", $"{other.Populations.Count}");
            } else
            {
                for(int i = 0; i < Populations.Count; i++)
                {
                    var pop = Populations[i];
                    var otherPop = other.Populations.FirstOrDefault(x => x.Bucket == pop.Bucket);
                    if (otherPop == null)
                        return new Change("Population bucket changed", $"{pop.Bucket}", null);
                    changes.AddRange(pop.GetChanges(otherPop)
                        .Select(x => new Change($"Populations[{i}].{x.Type}", x.Before, x.After))
                    );
                }
            }

            if(this.Filters.Count < other.Filters.Count)
            {
                return new Change("New filters", $"{this.Filters.Count}", $"{other.Filters.Count}");
            }
            else if (this.Filters.Count > other.Filters.Count)
            {
                return new Change("Removed filters", $"{this.Filters.Count}", $"{other.Filters.Count}");
            } else
            {
                for (int i = 0; i < this.Filters.Count; i++)
                {
                    changes.AddRange(this.Filters[i].GetChanges(this.Filters[i])
                        .Select(x => new Change($"Filters[{i}].{x.Type}", x.Before, x.After))
                    );
                }
            }
            return changes;
        }
    }

    public enum FilterType : ulong
    {
        Feature         = 1604612045,
        IDRange         = 2404720969,
        MemberCount     = 2918402255,
        HubType         = 4148745523,
        ID              = 3013771838,
        VanityURL       = 188952590,
        RangeByHash     = 2294888943
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
            else if (type == FilterType.IDRange)
                return IDRangeFilter.Create(x);
            else if (type == FilterType.MemberCount)
                return MemberCountFilter.Create(x);
            else if (type == FilterType.HubType)
                return HubTypeFilter.Create(x);
            else if (type == FilterType.ID)
                return IDListFilter.Create(x);
            else if (type == FilterType.VanityURL)
                throw new NotImplementedException($"VanityURL filter is not implemented");
            else if (type == FilterType.RangeByHash)
                return RangeByHashFilter.Create(x);
            throw new ArgumentException($"Unknown filter type: {type}");
        }

        public override string ToString()
            => $"{Type}";

        public override int GetHashCode()
        {
            return (int)Type;
        }

        public abstract List<Change> GetChanges(Filter filter);
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
        {
            if(Features.Length > 1)
            {
                return "Server has any feature of " + string.Join(", ", Features);
            } else
            {
                return "Server has feature " + Features[0];
            }
        }

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

        public override List<Change> GetChanges(Filter filter)
        {
            if(!(filter is FeatureFilter other))
                return new Change("Type", $".", filter.GetType().Name);
            if (Features.Length != other.Features.Length)
                return new Change($"Features length", $"{Features.Length}", $"{other.Features.Length}");
            if (Features.Any(x => other.Features.Contains(x) == false))
                return new Change($"Feature removed", ".", ".");
            if(other.Features.Any(y => Features.Contains(y) == false))
                return new Change($"Feature added", ".", ".");
            return new List<Change>();
        }
    }

    public class IDRangeFilter : Filter
    {
        public override FilterType Type => FilterType.IDRange;
        [JsonProperty("s")]
        public ulong Start { get; set; }
        [JsonProperty("e")]
        public ulong End { get; set; }

        public new static Filter Create(JToken x)
        {
            var idf = new IDRangeFilter();
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
            if (!(obj is IDRangeFilter idf))
                return false;
            return this.Start == idf.Start && this.End == idf.End;
        }

        public override List<Change> GetChanges(Filter filter)
        {
            if (!(filter is IDRangeFilter other))
                return new Change("Type", $".", filter.GetType().Name);
            if (Start != other.Start)
                return new Change("Start", Start, other.Start);
            if (End != other.End)
                return new Change("End", End, other.End);
            return new List<Change>();
        }
    }

    public class IDListFilter : Filter
    {
        public override FilterType Type => FilterType.ID;

        [JsonProperty("ls")]
        public List<ulong> IDs { get; set; } = new List<ulong>();

        public new static Filter Create(JToken x)
        {
            var idlf = new IDListFilter();
            idlf.IDs = x[1][0][1].ToObject<List<ulong>>();
            return idlf;
        }

        public override string ToString()
        {
            return $"Server has ID of [{string.Join(", ", IDs)}]";
        }

        public override int GetHashCode()
        {
            return $"{Type}:{string.Join("", IDs)}".GetHashCode();
        }
        public override bool Equals(object obj)
        {
            if (!(obj is IDListFilter other))
                return false;
            return IDs.All(x => other.IDs.Any(y => y.Equals(x)));
        }

        public override List<Change> GetChanges(Filter filter)
        {
            if (!(filter is IDListFilter other))
                return new Change("Type", $".", filter.GetType().Name);
            if (IDs.Count != other.IDs.Count)
                return new Change($"IDs length", $"{IDs.Count}", $"{other.IDs.Count}");
            if (IDs.Any(x => other.IDs.Contains(x) == false))
                return new Change($"IDs removed", ".", ".");
            if (other.IDs.Any(y => IDs.Contains(y) == false))
                return new Change($"IDs added", ".", ".");
            return new List<Change>();
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

        public override List<Change> GetChanges(Filter filter)
        {
            if (!(filter is MemberCountFilter other))
                return new Change("Type", $".", filter.GetType().Name);
            if (Start != other.Start)
                return new Change("Start", Start, other.Start);
            if (End != other.End)
                return new Change("End", End, other.End);
            return new List<Change>();
        }
    }

    public class HubTypeFilter : Filter
    {
        public override FilterType Type => FilterType.HubType;
        [JsonProperty("ht")]
        public int[] HubType { get; set; }

        public new static Filter Create(JToken x)
        {
            var htf = new HubTypeFilter();
            htf.HubType = x[1][0][1].ToObject<int[]>();
            return htf;
        }

        public override string ToString()
        {
            return "Server has hub type " + string.Join(", ", HubType);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is HubTypeFilter htf))
                return false;
            return this.HubType.Equals(htf.HubType);
        }
        public override int GetHashCode()
        {
            return $"{Type}-{HubType.GetHashCode()}".GetHashCode();
        }

        public override List<Change> GetChanges(Filter filter)
        {
            if (!(filter is HubTypeFilter other))
                return new Change("Type", $".", filter.GetType().Name);
            if (HubType.Length != other.HubType.Length)
                return new Change($"HubType length", $"{HubType.Length}", $"{other.HubType.Length}");
            if (HubType.Any(x => other.HubType.Contains(x) == false))
                return new Change($"HubType removed", ".", ".");
            if (other.HubType.Any(y => HubType.Contains(y) == false))
                return new Change($"HubType added", ".", ".");
            return new List<Change>();
        }
    }

    public class RangeByHashFilter : Filter
    {
        public override FilterType Type => FilterType.RangeByHash;

        public ulong[] FirstPair { get; set; }
        public ulong[] SecondPair { get; set; }

        public new static Filter Create(JToken x)
        {
            var rbh = new RangeByHashFilter();
            rbh.FirstPair = x[1][0].ToObject<ulong[]>();
            rbh.SecondPair = x[1][1].ToObject<ulong[]>();
            return rbh;
        }

        public override List<Change> GetChanges(Filter filter)
        {
            if (!(filter is RangeByHashFilter other)) return new List<Change>();
            var ls = new List<Change>();
            if (FirstPair[0] != other.FirstPair[0] || FirstPair[1] != other.FirstPair[1])
                ls.Add(new Change("FirstPair", String.Join(",", FirstPair), String.Join(",", other.FirstPair)));
            if (SecondPair[0] != other.SecondPair[0] || SecondPair[1] != other.SecondPair[1])
                ls.Add(new Change("SecondPair", String.Join(",", SecondPair), String.Join(",", other.SecondPair)));
            return ls;
        }

        public override string ToString()
        {
            return $"Server ID is in range by hash key {SecondPair[0]} with target {SecondPair[1]} ([{string.Join(",", FirstPair)}]; [{string.Join(",", SecondPair)}])";
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
