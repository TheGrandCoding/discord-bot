using Discord;
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

        public override string GenerateSave()
        {
            return Program.Serialise(ImageTriggers);
        }

        public override void OnReady()
        {
            Program.Client.MessageReceived += Client_MessageReceived;
            var th = new Thread(loop);
            th.Start();
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

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (arg.Author.IsBot)
                return;
            if(TryGetValue(arg.Content, out var possibles))
            {
                await arg.Channel.SendMessageAsync(possibles[Program.RND.Next(0, possibles.Count)],
                    messageReference: new MessageReference(arg.Id, arg.Channel.Id));
            }
        }
    
        private void loop()
        {
            var guild = Program.Client.Guilds.FirstOrDefault(x => x.Name == "Block Land");
            if (guild == null)
                return;
            var role = guild.Roles.FirstOrDefault(x => x.Id == 683065223535788066);
            if (role == null)
                return;
            var rainbow = new List<int[]>()
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
            int i = 0;
            do
            {
                try
                {
                    var t = Program.GetToken();
                    var usr = guild.GetUser(Program.AppInfo.Owner.Id);
                    if (usr.Status == UserStatus.Online)
                    {
                        var rgb = rainbow[i];
                        var c = new Color(rgb[0], rgb[1], rgb[2]);
                        role.ModifyAsync(x => x.Color = c).Wait(t);

                        if (++i >= rainbow.Count)
                            i = 0;
                    }
                    Task.Delay(30000, t).Wait();
                } catch
                {
                    break;
                }
            } while (true);
        }
    }
}
