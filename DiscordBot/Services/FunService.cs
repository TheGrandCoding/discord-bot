using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class FunService : Service
    {
        public override void OnReady()
        {
            Program.Client.MessageReceived += Client_MessageReceived;
        }

        private async Task Client_MessageReceived(Discord.WebSocket.SocketMessage arg)
        {
            if (arg.Author.IsBot)
                return;
            if(arg.Content == "well")
            {
                var possibles = new List<string>()
                {
                    "https://pruittwater.com/wp-content/uploads/2018/04/Deep-Well-Ocala-Fl-1024x768.jpg",
                    "https://upload.wikimedia.org/wikipedia/commons/thumb/9/95/Fleetwood_round_table_wishing_well_-_DSC06564.JPG/1200px-Fleetwood_round_table_wishing_well_-_DSC06564.JPG",
                    "https://www.keeleyhire.co.uk/images/multi/full/100305.jpg"
                };
                await arg.Channel.SendMessageAsync(possibles[Program.RND.next(0, possibles.Count)],
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
                new int[] {255,255,255},
                new int[] {170,170,170},
                new int[] {85,85,85}
            };
            int i = 0;
            do
            {
                try
                {
                    var t = Program.GetToken();
                    var rgb = rainbow[i];
                    var c = new Color(rgb[0], rgb[1], rgb[2]);
                    role.ModifyAsync(x => x.Color = c).Wait(t);

                    if (++i >= rainbow.Count)
                        i = 0;
                    Task.Delay(30000, t).Wait();
                } catch
                {
                    break;
                }
            } while (true);
        }
    }
}
