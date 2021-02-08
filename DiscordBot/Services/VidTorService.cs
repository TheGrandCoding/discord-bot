using Discord;
using DiscordBot.Classes;
using qBitApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class VidTorService : SavedService
    {
        public const string NamePattern = @"(\d{4})-(\d{2})-(\d{2})_([PM])([1-6])";
        public const string LessonPattern = @"1[23][ABC][A-Z][a-z][123]";
        public static string BasePath = Path.Combine(Program.BASE_PATH, "lessons");
        public List<TorrentInfo> Tracking { get; set; }
        public qBittorrentClient Client { get; private set; }
        public SyncClient Sync { get; private set; }
        public SemaphoreSlim _lock = new SemaphoreSlim(1, 1);


        public override string GenerateSave()
        {
            _lock.Wait();
            try
            {
                return InternalGenerateSave();
            }
            finally
            {
                _lock.Release();
            }
        }
        
        private string InternalGenerateSave()
        {
            return Program.Serialise(Tracking, Newtonsoft.Json.TypeNameHandling.None, new BotUserConverter());
        }

        public override void OnReady()
        {
            Client = new qBittorrentClient(new Uri("http://localhost:8080"));
            Client.Log += (message) =>
            {
                Program.LogMsg((message.Exception?.ToString() ?? "") + message.Message, (Discord.LogSeverity)message.Severity, "VidTor" + message.Source);
                return Task.CompletedTask;
            };
            Task.Run(async () =>
            {
                try
                {
                    await Client.LoginAsync("admin", Program.Configuration["tokens:qbit"] ?? "adminadmin");
                    var qVersion = await Client.GetApplicationVersionAsync();
                    var apiVersion = await Client.GetWebApiVersion();
                    Program.LogMsg($"Running {qVersion} with API {apiVersion}", Discord.LogSeverity.Info, "VidTorqBit");
                }
                catch (Exception ex)
                {
                    Program.LogMsg(ex, "VidTorqBit");
                    this.HasFailed = true;
                }
            }).Wait();
        }
        public override void OnLoaded()
        {
            var sv = ReadSave("[]");
            Tracking = Program.Deserialise<List<TorrentInfo>>(sv, new BotUserConverter());
            Sync = Client.GetSyncClient().Result;
            Sync.TorrentUpdated += Sync_TorrentUpdated;
            Sync.TorrentRemoved += Sync_TorrentRemoved;
            Sync.StartSync();
        }

        private async Task Sync_TorrentRemoved(string arg)
        {
            await _lock.WaitAsync();
            try
            {
                var exists = Tracking.FirstOrDefault(x => x.Hash == arg);
                if(exists != null)
                {
                    Tracking.Remove(exists);
                    try
                    {
                        await exists.UploadedBy.FirstValidUser.SendMessageAsync($"Uploading of lesson {exists.Lesson} failed or was cancelled.");
                    } catch (Exception ex)
                    {
                        Program.LogMsg(ex, "VidTorService");
                    }
                }
                DirectSave(InternalGenerateSave());
            } finally { _lock.Release(); }
        }

        private async Task Sync_TorrentUpdated(qBitApi.REST.Entities.Torrent arg)
        {
            if (arg.Category != "lessons")
                return;
            bool change = false;
            await _lock.WaitAsync();
            try
            {
                var exists = Tracking.FirstOrDefault(x => x.Hash == arg.Hash || x.Name == arg.Name);
                if (exists == null)
                    return;
                if (exists.Hash == null)
                {
                    Program.LogMsg($"Setting {exists.Name} hash to {arg.Hash} | {Tracking.Count}");
                    change = true;
                    exists.Hash = arg.Hash;
                    try
                    {
                        await exists.UploadedBy.FirstValidUser.SendMessageAsync($"Torrent for {exists.Lesson} has been picked up and is being downloaded.");
                    } catch { }
                } else if(arg.Progress == 1)
                {
                    change = true;
                    // Finished downloading, so we need to move it.
                    var from = arg.ContentPath; // single-file, so path to file itself
                    from = from.Replace("/data/", "/mnt/drive/");
                    var folder = Path.Combine(BasePath, exists.Lesson);
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    var to = Path.Combine(folder, Path.GetFileName(from));
                    File.Move(from, to, true);
                    Tracking.Remove(exists);
                    try
                    {
                        await Client.DeleteTorrents(true, arg.Hash);
                    }
                    catch (Exception ex)
                    {
                        Program.LogMsg(ex, "VidTor");
                        try
                        {
                            await exists.UploadedBy.FirstValidUser.SendMessageAsync($"Failed to remove torrent from download client; must be manually removed.\r\n {ex.Message}");
                        } catch { }
                    }
                    try
                    {
                        await exists.UploadedBy.FirstValidUser.SendMessageAsync($"Torrent for {exists.Lesson} has been downloaded and should now be available.");
                    }
                    catch { }
                } else
                {
                    try
                    {
                        await exists.UploadedBy.FirstValidUser.SendMessageAsync($"Torrent for {exists.Lesson} is {(arg.Progress * 100):00.0}% downloaded");
                    }
                    catch { }
                }
                if(change)
                    DirectSave(InternalGenerateSave());
            } finally { _lock.Release(); }
        }

        public override void OnClose()
        {
            Client.Dispose();
        }

        public async Task AddNew(string temp, Action<TorrentInfo> action)
        {
            var info = new TorrentInfo();
            action(info);
            var randomName = AuthToken.Generate(32);
            _lock.Wait();
            try
            {
                Tracking.Add(info);
            }
            finally
            {
                _lock.Release();
            }
            await Client.AddNewTorrent(x =>
            {
                x.TorrentFilePath = temp;
                x.Rename = randomName;
                x.Tags = new string[] { info.Lesson, $"{info.UploadedBy.Id}" };
                x.Category = "lessons";
            });
        }
    
    }

    public class TorrentInfo
    {
        public string Hash { get; set; }
        public string Name { get; set; }
        public BotUser UploadedBy { get; set; }
        public string Lesson { get; set; }
    }
}
