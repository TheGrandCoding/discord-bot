using Discord;
using Discord.WebSocket;
using DiscordBot.Classes;
using Markdig.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class FunService : SavedService
    {
        public Dictionary<string, List<string>> ImageTriggers { get; set; } = new Dictionary<string, List<string>>();

        public static bool IsChangeEnabled = false;

        Cached<bool> BlockChange = new Cached<bool>(false, 1);
        static List<int[]> RAINBOW = new List<int[]>()
            {
                new int[] {170, 0, 0},
                new int[] { 255, 85, 85 },
                new int[] {255,170,0},
                new int[] {255,255,85},
                new int[] {0,170,0},
                new int[] {85,255,85},
                new int[] {85,255,255},
                new int[] {0,170,170},
                new int[] {0,0,170},
                new int[] {85,85,255},
                new int[] {255,85,255},
                new int[] {170,0,170},
                //new int[] {255,255,255},
                new int[] {170,170,170},
                new int[] {85,85,85}
            };

        public override string GenerateSave()
        {
            return Program.Serialise(ImageTriggers);
        }

        public override void OnReady()
        {
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        bool TryGetValue(string text, out List<string> s)
        {
            s = new List<string>();
            foreach(var key in ImageTriggers.Keys)
            {

                if(key.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    s = ImageTriggers[key];
                    return true;
                }
            }
            return false;
        }

        int indexOf(int r, int g, int b)
        {
            for(int i = 0; i < RAINBOW.Count; i++)
            {
                var a = RAINBOW[i];
                if (a[0] == r && a[1] == g && a[2] == b)
                    return i;
            }
            return 0;
        }

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (arg.Author.IsBot)
                return;
            if(TryGetValue(arg.Content, out var possibles))
            {
                await arg.Channel.SendMessageAsync(possibles[Program.RND.Next(0, possibles.Count)],
                    messageReference: new MessageReference(arg.Id, arg.Channel.Id));
            }
            if(IsChangeEnabled && arg.Author is SocketGuildUser gUser && arg.Channel is SocketGuildChannel channel)
            {
                var guild = channel.Guild;
                var role = gUser.Roles.FirstOrDefault(x => x.Name == "Rainbow");
                if(role != null)
                {
                    if(BlockChange.GetValueOrDefault() == false)
                    {
                        BlockChange.Value = true;
                        var color = role.Color;
                        var i = indexOf(color.R, color.G, color.B);
                        if (++i >= RAINBOW.Count)
                            i = 0;
                        var clr = RAINBOW[i];
                        await role.ModifyAsync(x => x.Color = new Color(clr[0], clr[1], clr[2]));
                    }
                }
            }
        }
    }
}
