using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using DiscordBot.MLAPI;
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
    public class GroupGameWS : BotWSBase
    {
        public static GroupMuteService Service { get; set; }
        public SocketVoiceChannel VC { get; set; }
        public SocketGuildUser GuildUser { get; set; }
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
            Program.LogError(e.Exception, "WSGroup");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if(Game == null || VC == null || VC.Users.Any(x => x.Id == GuildUser.Id) == false)
            {
                Context.WebSocket.Close(CloseStatusCode.Abnormal, "Inconsistent state.");
                return;
            }
            if(e.Data == "dead")
            {
                // don't actually update any VC as this may give a hint to other players.
                Game.Do(() =>
                {
                    if (!Game.Dead.Contains(User.Id))
                        Game.Dead.Add(User.Id);
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
            if (User == null)
            {
                Context.WebSocket.Close(CloseStatusCode.Normal, "Authentication failed.");
                return;
            }
            Service ??= Services.GetRequiredService<GroupMuteService>();
            foreach(var guild in Program.Client.Guilds)
            {
                foreach(var voice in guild.VoiceChannels)
                {
                    foreach(var usr in voice.Users)
                    {
                        if(usr.Id == User.Id)
                        {
                            GuildUser = usr;
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
