using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class VCAnnounceService : Service
    {
        public static string BaseFolder => Path.Combine(Program.BASE_PATH, "data", "sounds", "vcann");

        public string getUserFolder(IUser user) => Path.Combine(BaseFolder, user.Id.ToString());

        public string getMediaType(IUser user, string type) => Path.Combine(getUserFolder(user), type + ".mp3");

        public override void OnReady()
        {
            Program.Client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
        }

        Process createStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
#if WINDOWS
                FileName = @"D:\inpath\ffmpeg.exe",
#else
                FileName = "/usr/bin/ffmpeg",
#endif
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }
        private async Task SendAsync(IAudioClient client, string path)
        {
            // Create FFmpeg using the previous example
            using (var ffmpeg = createStream(path))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed, 96 * 1024))
            {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }

        struct passing
        {
            public SocketUser arg1;
            public SocketVoiceState arg2;
            public SocketVoiceState arg3;
        }

        Dictionary<SocketGuild, Dictionary<SocketVoiceChannel, IAudioClient>> clients
            = new Dictionary<SocketGuild, Dictionary<SocketVoiceChannel, IAudioClient>>();

        async Task<IAudioClient> getAudioClient(SocketVoiceChannel vc)
        {
            IAudioClient ac;
            if(clients.TryGetValue(vc.Guild, out var channelLists))
            {
                if (channelLists.TryGetValue(vc, out ac))
                    return ac;
                foreach(var keypair in channelLists)
                {
                    if(keypair.Value.ConnectionState == ConnectionState.Connected)
                    {
                        Console.WriteLine($"Disconnecting from {keypair.Key.Name}");
                        await keypair.Key.DisconnectAsync();
                    }
                }
                channelLists = new Dictionary<SocketVoiceChannel, IAudioClient>();
                Console.WriteLine($"Connecting to {vc.Name}");
                ac = await vc.ConnectAsync();
                channelLists[vc] = ac;
                return ac;
            } else
            {
                ac = await vc.ConnectAsync();
                clients[vc.Guild] = new Dictionary<SocketVoiceChannel, IAudioClient>()
                {
                    {vc, ac }
                };
                return ac;
            }
        }

        async Task Client_UserVoiceStateUpdated(Discord.WebSocket.SocketUser arg1, Discord.WebSocket.SocketVoiceState arg2, Discord.WebSocket.SocketVoiceState arg3)
        {
            if (arg1.IsBot)
                return;
            var thread = new Thread(handle);
            thread.Start(new passing()
            {
                arg1 = arg1,
                arg2 = arg2,
                arg3 = arg3
            });
        }
        Semaphore lck = new Semaphore(1, 1);
        int waiting = 0;

        async Task doStuff(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            string folder = getUserFolder(arg1);
            if (!Directory.Exists(folder))
                return;
            if (arg3.VoiceChannel != null && arg2.VoiceChannel?.Id != arg3.VoiceChannel?.Id)
            {
                string file = getMediaType(arg1, "join");
                if (!File.Exists(file))
                    return;
                var vc = await getAudioClient(arg3.VoiceChannel);
                await SendAsync(vc, getMediaType(arg1, "join"));
                if(waiting <= 1)
                {
                    await arg3.VoiceChannel.DisconnectAsync();
                    clients.Remove(arg3.VoiceChannel.Guild);
                }
                Thread.Sleep(250);
            }
        }

        void handle(object o)
        {
            var thing = (passing)o;
            try
            {
                waiting++;
                Console.WriteLine($"{thing.arg1.Username} Entering lock {waiting}");
                lck.WaitOne();
                Console.WriteLine($"{thing.arg1.Username} Achieved lock {waiting}");
                doStuff(thing.arg1, thing.arg2, thing.arg3).Wait();
                Console.WriteLine($"{thing.arg1.Username} Performed action {waiting}");
            } catch (Exception ex)
            {
                Program.LogMsg(ex, "vcAnnounce");
                try
                {
                    thing.arg1.SendMessageAsync($"Could not announce your entry, error ocurred: {ex.Message}");
                } catch { }
            } finally
            {
                waiting--;
                lck.Release();
                Console.WriteLine($"{thing.arg1.Username} Released lock {waiting}");
            }
        }
    }
}
