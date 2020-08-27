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

        public long WhiteTime { get; set; }
        public long BlackTime { get; set; }

        public Guid Id { get; set; }

        public PlayerSide TickingFor { get; set; } = PlayerSide.White;
        public bool Paused { get; set; } = true;
        public bool Ended {  get
            {
                var e = WhiteTime <= 0 || BlackTime <= 0;
                if (e)
                    Paused = true;
                return e;
            } }
        public int HalfMoves { get; set; } = 0;

        long deduct(long ts, long ms)
        {
            if (ts <= ms)
                return 0;
            return ts - ms;
        }
        long deduct(long ts)
        {
            var elapsed = DateTime.Now - last;
            return deduct(ts, (long)elapsed.TotalMilliseconds);
        }

        const int msInterval = 100;
        const int broadcastInterval = (10 * 1000) / msInterval;
        int intervalCounter = 0;
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
                intervalCounter++;
                if (intervalCounter >= broadcastInterval)
                {
                    intervalCounter = 0;
                    BroadcastStatus(((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds());
                }
            }
            getLock();
            try
            {
                Program.LogMsg($"Game {Id} ended; loser: {TickingFor}", Discord.LogSeverity.Verbose);
                var p = Program.Services.GetRequiredService<ChessService>();
                p.EndTimedGame(this);
            }
            finally
            {
                releaseLock();
            }
            BroadcastStatus(((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds());
        }
        Thread thread;

        long getMsElapsed(long time, out long current)
        {
            var now = ((DateTimeOffset)DateTime.UtcNow);
            current = now.ToUnixTimeMilliseconds();
            return current - time;
        }

        public void Start(long time)
        {
            if (Ended)
                return;
            var elapsed = getMsElapsed(time, out var current);
            // Whichever side has been ticking for 'elapsed' ms, so we should deduct it.
            if (TickingFor == PlayerSide.White)
                WhiteTime = deduct(WhiteTime, elapsed);
            else
                BlackTime = deduct(BlackTime, elapsed);
            last = DateTime.Now;
            Paused = false;
            if (thread == null)
            {
                thread = new Thread(timeThread);
                thread.Start();
            }
            BroadcastStatus(time);
        }
        public void Stop(long time)
        {
            if (Ended)
                return;
            Paused = true;
            var elapsed = getMsElapsed(time, out var current);
            // Whichever ticking side has not ticked for 'elapsed' ms, so we should add
            if (TickingFor == PlayerSide.White)
                WhiteTime = WhiteTime + elapsed;
            else
                BlackTime = BlackTime + elapsed;
            BroadcastStatus(time);
        }
        public void Switch(long time)
        {
            if (Paused || Ended)
                return;
            getLock();
            try
            {
                TickingFor ^= (PlayerSide.White | PlayerSide.Black); // xor, flips.
                HalfMoves++;

                var elapsed = getMsElapsed(time, out var current);
                // tickingfor is now the player switched TO.
                if (TickingFor == PlayerSide.White)
                {
                    // White has been ticking for elapsed, and Black has stopped for that time.
                    WhiteTime = WhiteTime - elapsed;
                    BlackTime = BlackTime + elapsed;
                } else
                {
                    // Black has been ticking for elapsed, and White has stopped for that time.
                    BlackTime = BlackTime - elapsed;
                    WhiteTime = WhiteTime + elapsed;
                }
            } finally
            {
                releaseLock();
            }

            BroadcastStatus(time);
        }
        public void BroadcastStatus(long time)
        {
            getLock();
            try
            {
                Program.LogMsg($"{Paused} {Ended} {TickingFor} {WhiteTime} {BlackTime}", Discord.LogSeverity.Debug);
                var rm = new List<ChessTimeWS>();
                foreach (var x in ListeningWS)
                {
                    try
                    {
                        x.SendStatus(time);
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
            jobj["wt"] = WhiteTime;
            jobj["bt"] = BlackTime;
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
