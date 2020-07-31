using DiscordBot.Classes.Converters;
using DiscordBot.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.COA
{
    public class CoAMotion
    {
        public CoAMotion() { }

        [JsonConverter(typeof(ChessPlayerConverter))]
        public ChessPlayer Movant { get; set; }
        public DateTime Filed { get; set; }
        public DateTime? HoldingDate { get; set; }

        public List<CoAttachment> Attachments { get; set; } = new List<CoAttachment>();

        public string MotionType { get; set; }

        public string Holding { get; set; }

        [JsonIgnore]
        public bool Denied {  get
            {
                return Holding != null && (
                    Holding.Contains("deny", StringComparison.OrdinalIgnoreCase)
                    || Holding.Contains("denied", StringComparison.OrdinalIgnoreCase));
            } }
        [JsonIgnore]
        public bool Granted
        {
            get
            {
                return Holding != null && (
                    Holding.Contains("granted", StringComparison.OrdinalIgnoreCase));
            }
        }

        [JsonIgnore]
        public CoAHearing Hearing { get; set; }

        [JsonIgnore]
        public string DataPath { get
            {
                int index = Hearing.Motions.IndexOf(this);
                return System.IO.Path.Combine(Hearing.DataPath, index.ToString("00"));
            } }

        public void SetIds(CoAHearing hearing)
        {
            Hearing = hearing;
            int i = 0;
            foreach (var a in Attachments)
                a.SetIds(DataPath, i++);
        }
    }

    public static class Motions
    {
        public const string WritOfCertiorari = "Motion for writ of certiorari";
        public const string Dismiss = "Motion to dismiss";
        public const string SummaryJudgement = "Motion for summary judgement";
        public const string Join = "Motion to join";
        public const string Seal = "Motion to seal";
    }

}
