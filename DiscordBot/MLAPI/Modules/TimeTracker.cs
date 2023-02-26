using DiscordBot.Classes;
using DiscordBot.Services;
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
            DB = Program.Services.GetRequiredService<TimeTrackDb>();
        }

        public TimeTrackDb DB { get; }

        [Method("GET"), Path("/tracker")]
        public async Task Base()
        {
            var existing = Context.User.Tokens.FirstOrDefault(x => x.Name == AuthToken.TimeToken);
            if(existing == null)
            {
                existing = new AuthToken(AuthToken.TimeToken, 12, "html.api.tracker", "html.api.tracker.*");
                Context.User.Tokens.Add(existing);
                Program.Save();
            }
            RespondRaw(existing.Value);
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
            RespondRaw(obj.ToString(), HttpStatusCode.OK);
        }

        [Method("GET"), Path("/api/tracker/latestVersion")]
        public async Task LatestVersion()
        {
            RespondRaw(TimeTrackDb.GetExtensionVersion(), HttpStatusCode.OK);
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
            RespondJson(jobj, 200);
        }

        [Method("POST"), Path("/api/tracker/times")]
        public void SetTimes()
        {
            var jobj = JObject.Parse(Context.Body);
            foreach(JProperty token in jobj.Children())
            {
                var val = token.Value.ToObject<double>();
                DB.AddVideo(Context.User.Id, token.Name, val);
            }
            DB.SaveChanges();
            RespondRaw("OK", HttpStatusCode.Created);
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
                var thing = threads.LastOrDefault();
                if (thing == null)
                    continue;
                if(v == 2)
                {
                    var jar = new JArray();
                    foreach(var x in threads)
                    {
                        jar.Add(new DateTimeOffset(x.LastUpdated).ToUnixTimeMilliseconds());
                    }
                    threadObj["time"] = jar;
                } else
                {
                    threadObj["time"] = new DateTimeOffset(thing.LastUpdated).ToUnixTimeMilliseconds();
                }
                threadObj["count"] = thing.Comments;
                jobj[id] = threadObj;
            }
            RespondJson(jobj, 200);
        }

        [Method("POST"), Path("/tracker/webhook")]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        [RequireScope("*")]
        [RequireGithubSignatureValid("tracker:webhook")]
        public async Task VersionUpdate()
        {
            RespondRaw("Thanks");
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
