using DiscordBot.Classes;
using DiscordBot.Classes.DbContexts;
using DiscordBot.Services;
using DiscordBot.Utils;
using DiscordBot.Websockets;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules.TimeTracking
{
    public class TimeTracker : AuthedAPIBase
    {
        public TimeTracker(APIContext context) : base(context, "tracker")
        {
            DB = Context.Services.GetTimeDb("TimeTrackerAPI");
        }

        public TimeTrackDb DB { get; }

        [Method("GET"), Path("/tracker")]
        public async Task ViewTracker()
        {
            var existing = Context.User.AuthTokens.FirstOrDefault(x => x.Name == BotDbAuthToken.TimeToken);
            if(existing == null)
            {
                existing = BotDbAuthToken.Create(Context.User, BotDbAuthToken.TimeToken, 12, "html.api.tracker", "html.api.tracker.*");
                Context.User.AuthTokens.Add(existing);
                Context.BotDB.SaveChanges();
            }
            await RespondRaw(existing.Token);
        }

        [Method("GET"), Path("/api/tracker/user")]
        [RequireAuthentication(false, false)]
        [RequireApproval(false)]
        [RequireScope("html.?")]
        public async Task GetUser()
        {
            JToken obj;
            if (Context.User == null)
            {
                obj = JValue.CreateNull();
            }
            else
            {
                obj = new JObject();
                obj["id"] = Context.User.Id.ToString();
                obj["name"] = Context.User.Name;
                var intervalThings = new JObject();
                intervalThings["get"] = 10_000;
                intervalThings["set"] = 15_000;
                obj["interval"] = intervalThings;
            }
            await RespondRaw(obj.ToString(), HttpStatusCode.OK);
        }

        [Method("GET"), Path("/api/tracker/latestVersion")]
        public async Task LatestVersion()
        {
            await RespondRaw(TimeTrackDb.GetExtensionVersion(), HttpStatusCode.OK);
        }

        [Method("GET"), Path("/api/tracker/times")]
        public async Task GetTimes(string ids)
        {
            var jobj = new JObject();
            foreach (var id in ids.Split(';', ',')) 
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var thing = DB.GetVideo(Context.User.Id, id);
                jobj[id] = thing?.WatchedTime ?? 0d;
            }
            await RespondJson(jobj, 200);
        }

        [Method("POST"), Path("/api/tracker/times")]
        public async Task SetTimes()
        {
            var jobj = JObject.Parse(Context.Body);
            foreach(JProperty token in jobj.Children())
            {
                var val = token.Value.ToObject<double>();
                DB.AddVideo(Context.User.Id, token.Name, val);
            }
            DB.SaveChanges();
            await RespondRaw("OK", HttpStatusCode.Created);
        }

        [Method("POST"), Path("/api/tracker/threads")]
        public async Task VisitThread(string id, int count)
        {
            DB.AddThread(Context.User.Id, id, count);
            DB.SaveChanges();
            await RespondRaw("ok");
        }

        [Method("GET"), Path("/api/tracker/threads")]
        public async Task GetThreads(string ids, int v = 2)
        {
            var jobj = new JObject();
            foreach (var id in ids.Split(';', ','))
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var threads = DB.GetThread(Context.User.Id, id);
                
                var threadObj = new JObject();
                var arr = new JArray();

                foreach(var x in threads)
                {
                    var obj = new JObject();
                    obj["t"] = new DateTimeOffset(x.LastUpdated).ToUnixTimeMilliseconds();
                    obj["c"] = x.Comments;
                    arr.Add(obj);
                }

                threadObj["visits"] = arr;

                jobj[id] = threadObj;
            }
            await RespondJson(jobj, 200);
        }

        [Method("POST"), Path("/tracker/webhook")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireScope("*")]
        [RequireGithubSignatureValid("tracker:webhook")]
        public async Task VersionUpdate()
        {
            await RespondRaw("Thanks");
            var jobj = JObject.Parse(Context.Body);
            var release = jobj["release"]["tag_name"].ToObject<string>().Substring(1);
            TimeTrackDb.SetExtVersion(release);
            if(WSService.Server.WebSocketServices.TryGetServiceHost("/time-tracker", out var host))
            {
                TimeTrackerWS.BroadcastUpdate(release, host.Sessions);
            }
        }
    }
}
