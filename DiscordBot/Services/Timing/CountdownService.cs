using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DiscordBot.Services.Timing
{
    public class CountdownService : SavedService, ISARProvider
    {
        public List<Countdown> Countdowns { get; set; }
        public override string GenerateSave() => Program.Serialise(Countdowns);
        private Semaphore _lock = new Semaphore(1, 1);

        public void Lock(Action action)
        {
            if (Program.GetToken().IsCancellationRequested)
                throw new InvalidOperationException("Closing");
            _lock.WaitOne();
            try
            {
                if (Program.GetToken().IsCancellationRequested)
                    throw new InvalidOperationException("Closing");
                action();
            } finally
            {
                _lock.Release();
            }
        }

        public override void OnLoaded(IServiceProvider services)
        {
            Countdowns = Program.Deserialise<List<Countdown>>(ReadSave("[]"));
            var th = new Thread(thread);
            th.Start(Program.GetToken());
        }

        void thread(object obj)
        {
            if (!(obj is CancellationToken token))
                return;
            try
            {
                do
                {
                    loop(token);
                } while (!token.IsCancellationRequested);
            } catch(Exception ex)
            {
                Program.LogError(ex, "Countdown");
            }
        }
        void loop(CancellationToken token)
        {
            int waitTime = Time.Ms.Second * 5;
            Lock(() =>
            {
                var done = new List<Countdown>();
                foreach (var x in Countdowns)
                {
                    Console.WriteLine($"{x.SecondsRemaining:00}");
                    if(x.SecondsRemaining >= 0 && x.SecondsRemaining <= 10)
                    {
                        var chnl = x.GetChannel();
                        bool sentFinal = false;
                        do
                        {
                            Console.WriteLine($":{x.SecondsRemaining:00}");
                            if(x.SecondsRemaining <= 0)
                            {
                                chnl.SendMessageAsync(x.Text);
                                sentFinal = true;
                                break;
                            } else if (x.SecondsRemaining <= 5)
                                chnl.SendMessageAsync(x.SecondsRemaining.ToString());
                            Thread.Sleep(Time.Ms.Second);
                        } while (!sentFinal);
                        done.Add(x);
                        continue;
                    }

                    var time = (x.SecondsRemaining - 5) * 1000;
                    if (time <= 0)
                        time = 1;
                    if (time < waitTime)
                        waitTime = time;
                }
                foreach (var y in done)
                    Countdowns.Remove(y);
            });
            Thread.Sleep(waitTime);
        }

        public JToken GetSARDataFor(ulong userId)
        {
            _lock.WaitOne();
            try
            {
                var ctn = Countdowns.Where(x => x.User?.Id == userId).ToList();
                var jar = new JArray();
                foreach(var x in ctn)
                {
                    var jobj = JObject.FromObject(x);
                    jar.Add(jobj);
                }
                return jar;
            } finally
            {
                _lock.Release();
            }
        }
    }

    public class Countdown
    {
        public DateTime Started { get; set; }
        public DateTime End { get; set; }
        public IUser User { get; set; }
        public ITextChannel Channel { get; set; }
        public string Text { get; set; }

        public IMessageChannel GetChannel()
        {
            if (Channel != null)
                return Channel;
            return User.CreateDMChannelAsync().Result;
        }

        public int SecondsRemaining {  get
            {
                var ms = Remaining.TotalMilliseconds;
                ms += Program.Client.Latency;
                return (int)Math.Floor(ms / 1000d);
            } }
        
        [JsonIgnore]
        public TimeSpan Remaining => End - DateTime.Now;
        [JsonIgnore]
        public bool Finished => DateTime.Now > End;
    }
}
