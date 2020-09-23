using Discord.WebSocket;
using DiscordBot.Websockets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Services
{
    public class GroupMuteService : Service
    {
        public Dictionary<SocketVoiceChannel, GroupGame> Games { get; set; } = new Dictionary<SocketVoiceChannel, GroupGame>();


        public class GroupGame
        {
            public bool InDiscussion { get; set; }
            public List<ulong> Dead { get; set; }
            public SocketVoiceChannel VC { get; set; }
            public List<GroupGameWS> Listeners { get; set; }

            private object _lock = new object();
            public void Do(Action thing)
            {
                Console.WriteLine("Entering lock...");
                lock(_lock)
                {
                    Console.WriteLine("Achieved lock...");
                    thing();
                }
                Console.WriteLine("Exited lock");
            }

            public GroupGame(SocketVoiceChannel vc)
            {
                VC = vc;
                InDiscussion = false;
                Dead = new List<ulong>();
                Listeners = new List<GroupGameWS>();
            }

            public void SetStates(bool needsLock = true)
            {
                if(needsLock)
                {
                    Do(() => SetStates(false));
                    return;
                }
                foreach(var usr in VC.Users)
                {
                    usr.ModifyAsync(x =>
                    {
                        x.Mute = InDiscussion == false || Dead.Contains(usr.Id);
                        x.Deaf = InDiscussion == false; // ghosts can still listen.
                    });
                }
            }

            public void Broadcast(bool needsLock = true)
            {
                if(needsLock)
                {
                    Do(() => Broadcast(false));
                    return;
                }
                List<GroupGameWS> rm = new List<GroupGameWS>();
                foreach(var ws in Listeners)
                {
                    var jobj = new JObject();
                    jobj["discuss"] = InDiscussion;
                    var jarr = new JArray();
                    foreach(var usr in VC.Users)
                    {
                        var jUsr = new JObject();
                        jUsr["id"] = usr.Id.ToString();
                        jUsr["name"] = usr.Nickname ?? usr.Username;
                        jUsr["dead"] = Dead.Contains(usr.Id);
                        jarr.Add(jUsr);
                    }
                    jobj["dead"] = Dead.Contains(ws.BotUser.Id);
                    jobj["users"] = jarr;
                    try
                    {
                        ws.Context.WebSocket.Send(jobj.ToString());
                    }
                    catch (Exception ex)
                    {
                        Program.LogMsg($"GM:{ws.BotUser?.Name}", ex);
                        rm.Add(ws);
                    }
                }
                foreach (var x in rm) Listeners.Remove(x);
            }
        }
    }

}
