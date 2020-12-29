using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DiscordBot.Services.Timing
{
    public class CountdownService : SavedService
    {
        public List<Countdown> Countdowns { get; set; }
        public override string GenerateSave() => Program.Serialise(Countdowns);
        private Semaphore _lock = new Semaphore(1, 1);

        public void Lock(Action action)
        {
            _lock.WaitOne();
            if (source.IsCancellationRequested)
                throw new InvalidOperationException("Closing");
            try
            {
                action();
            } finally
            {
                _lock.Release();
            }
        }

        CancellationTokenSource source;
        public override void OnLoaded()
        {
            Countdowns = Program.Deserialise<List<Countdown>>(ReadSave("[]"));
            var th = new Thread(thread);
            source = new CancellationTokenSource();
            th.Start(source.Token);
        }
        public override void OnClose()
        {
            source.Cancel();
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
                Program.LogMsg("Countdown", ex);
            }
        }
        void loop(CancellationToken token)
        {
            int waitTime = Time.Second * 5;
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
                            Thread.Sleep(Time.Second);
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
            return User.GetOrCreateDMChannelAsync().Result;
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
