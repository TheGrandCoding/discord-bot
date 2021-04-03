using Discord;
using DiscordBot.Classes;
using DiscordBot.Services.Sonarr;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;


namespace DiscordBot.Services.Radarr
{
    public class RadarrWebhookService : SavedService
    {
        public List<SaveChannel> Channels { get; set; }


#if DEBUG
        const string apiUrl = "http://192.168.1.3:7878/api";
#else
        const string apiUrl = "http://localhost:7878/api/v3";
#endif
        public Semaphore Lock = new Semaphore(1, 1);
        public HttpClient HTTP { get; private set; }
        public CacheDictionary<int, string> TagsCache { get; } = new CacheDictionary<int, string>(60 * 24);
        public CacheDictionary<int, string[]> SeriesTagsCache { get; } = new CacheDictionary<int, string[]>(60 * 24); // day

        public override string GenerateSave()
        {
            var save = new Save()
            {
                Channels = Channels
            };
            return Program.Serialise(save);
        }
        public override void OnReady()
        {
            var save = Program.Deserialise<Save>(ReadSave("{}"));
            Channels = save.Channels ?? new List<SaveChannel>();
        }
        public override void OnLoaded()
        {
            OnGrab += RadarrWebhookService_OnGrab;
        }

        private void RadarrWebhookService_OnGrab(object sender, OnGrabRadarrEvent e)
        {
            foreach(var chnl in Channels)
            {
                chnl.Channel.SendMessageAsync($"New movie grabbed.");
            }
        }

        async Task<string> GetTagLabel(int id)
        {
            if (TagsCache.TryGetValue(id, out var s))
                return s;
            var response = await HTTP.GetAsync(apiUrl + $"/tag/{id}");
            var jobj = JObject.Parse(await response.Content.ReadAsStringAsync());
            var l = jobj["label"].ToObject<string>();
            TagsCache[id] = l;
            return l;
        }

        public async Task<string[]> GetSeriesTags(int seriesId)
        {
            if (SeriesTagsCache.TryGetValue(seriesId, out var v))
                return v;
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl + $"/series/{seriesId}");
            var response = await HTTP.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            Program.LogMsg($"{seriesId} :: {response.StatusCode}", LogSeverity.Info, "GetSeriesTags");
            var jobj = JObject.Parse(content);
            var array = jobj["tags"].ToObject<string[]>();
            var parsed = new List<string>();
            foreach (var tag in array)
            {
                if (!int.TryParse(tag, out var tagId))
                {
                    parsed.Add(tag);
                    continue;
                }
                var label = await GetTagLabel(tagId);
                parsed.Add(label);
            }
            SeriesTagsCache.Add(seriesId, parsed.ToArray());
            Program.LogMsg($"For {seriesId}: [{string.Join(", ", parsed)}]", LogSeverity.Info, "GetSeriesTags");
            return parsed.ToArray();
        }

        public async Task<bool> SeriesHasTag(int seriesId, Func<string, bool> tagPredicate)
        {
            foreach (var tag in await GetSeriesTags(seriesId))
            {
                if (tagPredicate(tag))
                    return true;
            }
            return false;
        }

        public async Task<bool> ShouldSendInChannel(int seriesId, SaveChannel channel)
        {
            var tags = await GetSeriesTags(seriesId);
            if (tags.Contains("private") && channel.ShowsPrivate == false)
                return false;
            foreach (var required in channel.TagRequired)
                if (tags.Contains(required))
                    return true;
            return channel.TagRequired.Count == 0;
        }
        public event EventHandler<OnTestRadarrEvent> OnTest;
        public event EventHandler<OnGrabRadarrEvent> OnGrab;
        public event EventHandler<OnDownloadRadarrEvent> OnDownload;

        public void Handle(RadarrEvent type)
        {
            Program.LogMsg($"Waiting lock for {type.EventType} | {typeof(RadarrEvent)}", Discord.LogSeverity.Info, "OnGrab");
            Lock.WaitOne();
            Program.LogMsg($"Received lock for {type.EventType}", Discord.LogSeverity.Info, "OnGrab");
            try
            {
                if (type is OnTestRadarrEvent t)
                    OnTest?.Invoke(this, t);
                else if (type is OnGrabRadarrEvent g)
                {
                    Program.LogMsg($"Invoking event for OnGrab", Discord.LogSeverity.Info, "OnGrab");
                    Program.LogMsg($"{OnGrab?.GetInvocationList().Length} listeners #4", LogSeverity.Info, "OnGrab");
                    OnGrab?.Invoke(this, g);
                }
                else if (type is OnDownloadRadarrEvent d)
                    OnDownload?.Invoke(this, d);
            }
            finally
            {
                Lock.Release();
                Program.LogMsg($"Released lock for {type.EventType}", Discord.LogSeverity.Info, "OnGrab");
            }
        }
    }

    #region Events
    [JsonConverter(typeof(JsonSubtypes), "EventType")]
    [JsonSubtypes.KnownSubType(typeof(OnGrabRadarrEvent), "Grab")]
    [JsonSubtypes.KnownSubType(typeof(OnDownloadRadarrEvent), "Download")]
    [JsonSubtypes.KnownSubType(typeof(OnTestRadarrEvent), "Test")]
    public abstract class RadarrEvent
    {
        public string EventType { get; set; }
    }

    public class OnGrabRadarrEvent : RadarrEvent
    {
    }

    public class OnDownloadRadarrEvent : RadarrEvent
    {
    }

    public class OnTestRadarrEvent : RadarrEvent
    {
    }
    #endregion
}
