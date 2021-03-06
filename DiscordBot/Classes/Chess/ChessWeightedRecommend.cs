﻿#if INCLUDE_CHESS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiscordBot.Classes.Chess
{
    public class ChessWeightedRecommend
    {
        public ChessPlayer Self { get; set; }
        public ChessPlayer Other { get; set; }
        public ChessDbContext DB { get; set; }
        public double Weight { get; set; }

        public ChessWeightedRecommend(ChessDbContext db, ChessPlayer self, ChessPlayer other)
        {
            DB = db;
            Self = self;
            Other = other;
            Calculate();
        }

        DateTime lastPlayedAgainst(ChessPlayer p1, ChessPlayer p2)
        {
            DateTime last = DateTime.Now.AddDays(-1000);
            var games = DB.GetGamesWith(p1);
            var withP2 = games.Where(x => x.LoserId == p2.Id || x.WinnerId == p2.Id);
            foreach(var x in withP2)
            {
                if (x.Timestamp > last)
                {
                    last = x.Timestamp;
                }
            }
            return last;
        }

        // Via: https://www.desmos.com/calculator/rkonchwjnu
        public double calc_NotPlayedRecently()
        {
            var last1 = lastPlayedAgainst(Self, Other);
            var last2 = lastPlayedAgainst(Other, Self);
            var last = last2 > last1 ? last2 : last1;
            var diff = DateTime.Now - last;
            var days = (int)diff.TotalDays;
            if(days >= 100.49)
                return 100;
            if(days >= 10)
                return ((1 / 100d) * (days * days)) - 1;
            return ((1 / 2d) * (days * days)) - 50;
        }

        int getArbVote(ChessPlayer player, ChessPlayer other)
        {
            return player.ArbVotes.FirstOrDefault(x => x.VoteeId == other.Id)?.Score ?? 0;
        }

        // not actually random, but name reserved.
        public double calc_Random()
        {
            var vote = Math.Min(0, getArbVote(Self, Other));
            vote += Math.Min(0, getArbVote(Other, Self));
            // ignore positive values, since we dont want to *prefer* they play.
            // also include their vote of us.
            return vote * 50;
            // scores are arranged:
            // Strong dislike = -2; = -100
            // Dislike = -1; = -50
            // Neutral / no vote = 0; = 0
        }

        public double calc_ScoreDifference()
        {
            var diff = Other.Rating - Self.Rating;
            return ((-1 / 100d) * (diff * diff)) + 10;
        }


        public static List<MethodInfo> functions = null;
        public static List<MethodInfo> findCalculators()
        {
            if (functions != null)
                return functions;
            var methods = typeof(ChessWeightedRecommend).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            functions = new List<MethodInfo>();
            foreach(var x in methods)
            {
                if(x.Name.StartsWith("calc_") && x.ReturnType == typeof(double))
                {
                    functions.Add(x);
                }
            }
            return functions;
        }

        public void Calculate()
        {
            Weight = 0d;
            foreach(var func in findCalculators())
            {
                Weight += (double)func.Invoke(this, null);
            }
        }
    }
}
#endif