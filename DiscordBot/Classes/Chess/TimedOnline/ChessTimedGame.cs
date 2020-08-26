using DiscordBot.Classes.Chess.Online;
using DiscordBot.Services;
using DiscordBot.Websockets;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace DiscordBot.Classes.Chess
{
    public class ChessTimedGame
    {
        Semaphore _lock { get; } = new Semaphore(1, 1);
        public void getLock()
        {
            //var stack = new StackTrace(0, true);
            //var frame = stack.GetFrame(0);
            //var location = $"{frame.GetMethod()?.Name}#{frame.GetFileLineNumber()}";
            //Console.WriteLine($"Fetching lock {location}");
            _lock.WaitOne();
            //Console.WriteLine($"Achieved lock {location}");
        }
        public void releaseLock()
        {
            //var stack = new StackTrace(0, true);
            //var frame = stack.GetFrame(0);
            //var location = $"{frame.GetMethod()?.Name}#{frame.GetFileLineNumber()}";
            int i = _lock.Release();
            //Console.WriteLine($"Released {i} lock {location}");
        }

        public ChessPlayer White { get; set; }
        public ChessPlayer Black { get; set; }

        public List<ChessTimeWS> ListeningWS { get; set; } = new List<ChessTimeWS>();

        public TimeSpan WhiteTime { get; set; }
        public TimeSpan BlackTime { get; set; }

        public Guid Id { get; set; }

        public PlayerSide TickingFor { get; set; } = PlayerSide.White;
        public bool Paused { get; set; } = true;
        public bool Ended {  get
            {
                var e = WhiteTime.TotalSeconds <= 0 || BlackTime.TotalSeconds <= 0;
                if (e)
                    Paused = true;
                return e;
            } }
        public int HalfMoves { get; set; } = 0;

        TimeSpan deduct(TimeSpan ts)
        {
            var elapsed = DateTime.Now - last;
            if (ts.TotalMilliseconds <= elapsed.TotalMilliseconds)
                return new TimeSpan();
            return TimeSpan.FromMilliseconds(ts.TotalMilliseconds - elapsed.TotalMilliseconds);
        }

        const int msInterval = 100;
        DateTime last = DateTime.Now;
        void timeThread()
        {
            last = DateTime.Now;
            while(!Ended)
            {
                if (Paused)
                    continue;
                getLock();
                try
                {
                    if (TickingFor == PlayerSide.White)
                        WhiteTime = deduct(WhiteTime);
                    else if (TickingFor == PlayerSide.Black)
                        BlackTime = deduct(BlackTime);
                    last = DateTime.Now;
                    Thread.Sleep(msInterval);
                }
                finally
                {
                    releaseLock();
                }
            }
            getLock();
            try
            {
                Program.LogMsg($"Game {Id} ended; loser: {TickingFor}");
                var p = Program.Services.GetRequiredService<ChessService>();
                p.EndTimedGame(this);
            }
            finally
            {
                releaseLock();
            }
            BroadcastStatus();
        }
        Thread thread;

        public void Start()
        {
            if (Ended)
                return;
            last = DateTime.Now;
            Paused = false;
            if(thread == null)
            {
                thread = new Thread(timeThread);
                thread.Start();
            }
            BroadcastStatus();
        }
        public void Stop()
        {
            if (Ended)
                return;
            Paused = true;
            BroadcastStatus();
        }
        public void Switch()
        {
            if (Paused || Ended)
                return;
            TickingFor ^= (PlayerSide.White | PlayerSide.Black); // xor, flips.
            HalfMoves++;
            BroadcastStatus();
        }
        public void BroadcastStatus()
        {
            Program.LogMsg($"Entering Lock to Broadcast");
            getLock();
            try
            {
                Program.LogMsg($"{Paused} {Ended} {TickingFor} {WhiteTime} {BlackTime}");
                var rm = new List<ChessTimeWS>();
                foreach (var x in ListeningWS)
                {
                    try
                    {
                        x.SendStatus();
                    } catch
                    {
                        rm.Add(x);
                    }
                }
                foreach (var x in rm)
                    ListeningWS.Remove(x);
            } finally
            {
                releaseLock();
            }
        }

        public JObject ToJson()
        {
            var jobj = new JObject();
            jobj["wt"] = WhiteTime.TotalSeconds;
            jobj["bt"] = BlackTime.TotalSeconds;
            jobj["side"] = TickingFor == PlayerSide.White ? "white" : "black";
            jobj["paused"] = Paused;
            jobj["hmvs"] = HalfMoves;
            jobj["wn"] = White.Name;
            jobj["bn"] = Black.Name;
            if (Ended)
                jobj["end"] = true;
            return jobj;
        }
    }
}
