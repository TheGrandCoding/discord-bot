using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Server;
using static DiscordBot.Services.GroupMuteService;

namespace DiscordBot.Websockets
{
    public class GroupGameWS : WebSocketBehavior
    {
        public static GroupMuteService Service { get; set; }
        public BotUser BotUser { get; set; }
        public SocketVoiceChannel VC { get; set; }
        public SocketGuildUser User { get; set; }
        public GroupGame Game { get; set; }

        protected override void OnClose(CloseEventArgs e)
        {
            Game?.Do(() =>
            {
                Game.Listeners.Remove(this);
            });
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Program.LogMsg(e.Exception, "WSGroup");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if(Game == null || VC == null || VC.Users.Any(x => x.Id == User.Id) == false)
            {
                Context.WebSocket.Close(CloseStatusCode.Abnormal, "Inconsistent state.");
                return;
            }
            if(e.Data == "dead")
            {
                // don't actually update any VC as this may give a hint to other players.
                Game.Do(() =>
                {
                    if (!Game.Dead.Contains(BotUser.Id))
                        Game.Dead.Add(BotUser.Id);
                    Game.Broadcast(false);
                });
            } else if (e.Data == "toggle")
            {
                Game.Do(() =>
                {
                    Game.InDiscussion = !Game.InDiscussion;
                    Game.SetStates(false);
                    Game.Broadcast(false);
                });
            } else if (e.Data == "end")
            {
                Game.Do(() =>
                {
                    Game.Dead = new List<ulong>();
                    Game.InDiscussion = true;
                    Game.SetStates(false);
                    Game.Broadcast(false);
                });
            } else if (e.Data.StartsWith("kill"))
            {
                var id = ulong.Parse(e.Data.Split(':')[1]);
                Game.Do(() =>
                {
                    if (Game.Dead.Contains(id))
                        Game.Dead.Remove(id);
                    else
                        Game.Dead.Add(id);
                    if (Game.InDiscussion)
                    {
                        Game.SetStates(false);
                        Game.Broadcast(false);
                    }
                });
            }
        }

        protected override void OnOpen()
        {
            var auth = Context.CookieCollection[AuthToken.SessionToken]?.Value;
            auth ??= Context.QueryString[AuthToken.SessionToken];
            if(string.IsNullOrWhiteSpace(auth))
            {
                Context.WebSocket.Close(CloseStatusCode.UnsupportedData, "You must login first.");
                return;
            }
            BotUser = Program.Users.FirstOrDefault(x => x.Tokens.Any(x => x.Name == AuthToken.SessionToken && x.Value == auth));
            if(BotUser == null)
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "You must login first.");
                return;
            }
            Service ??= Program.Services.GetRequiredService<GroupMuteService>();
            foreach(var guild in Program.Client.Guilds)
            {
                foreach(var voice in guild.VoiceChannels)
                {
                    foreach(var usr in voice.Users)
                    {
                        if(usr.Id == BotUser.Id)
                        {
                            User = usr;
                            VC = voice;
                            break;
                        }
                    }
                    if (VC != null)
                        break;
                }
                if (VC != null)
                    break;
            }
            if(VC == null)
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "You must join a voice channel first.");
                return;
            }
            if (Service.Games.TryGetValue(VC, out var game))
            {
                game.Do(() =>
                {
                    game.Listeners.Add(this);
                    game.Broadcast(false);
                });
                Game = game;
            } else
            {
                var g = new GroupGame(VC);
                g.Listeners.Add(this);
                Service.Games[VC] = g;
                g.Broadcast(true);
                Game = g;
            }
        }
    }
}
