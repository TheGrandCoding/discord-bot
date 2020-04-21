using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Tesseract;

namespace DiscordBot.Services
{
    public class ScamDetector : SavedService
    {
        public string Folder => Path.Combine(Program.BASE_PATH, "tessdata");

        public List<Scam> Scams = new List<Scam>();

        public List<ScamResult> getPossibleScams(string testImagePath, out string[] words, out float confidence)
        {
            words = null;
            confidence = 0;
            Page page;
            try
            {
                var engine = new TesseractEngine(Folder, "eng", EngineMode.Default);
                var img = Pix.LoadFromFile(testImagePath);
                page = engine.Process(img);
            }
            catch (Exception e)
            {
                Program.LogMsg("Unexpected Error: " + e.ToString(), Discord.LogSeverity.Error, "Scam");
                return null;
            }
            var text = page.GetText().ToLower();
            words = Regex.Split(text, @"\s");
            confidence = page.GetMeanConfidence();

            List<ScamResult> possible = new List<ScamResult>();
            foreach(var scam in Scams)
            {
                var thisScam = scam.PercentageMatch(words);
                Program.LogMsg($"{scam.Name}: {(thisScam * 100):00.0}%");
                possible.Add(new ScamResult()
                {
                    Scam = scam,
                    Confidence = thisScam,
                });
            }
            return possible;
        }

        public override void OnReady()
        {
            Scams = Program.Deserialise<List<Scam>>(ReadSave("[]"));
        }

        public override string GenerateSave()
        {
            return Program.Serialise(Scams);
        }
    }

    public class ScamResult
    {
        public double Confidence { get; set; }
        public Scam Scam { get; set; }
    }

    public class Scam
    {
        [JsonProperty("text")]
        public string[] Text { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonConstructor]
        private Scam(string[] text)
        {
            Text = text.Select(x => x.ToLower()).ToArray();
        }

        int numWordsContained(string[] words, string[] TestWords)
        {
            int numWords = 0;
            foreach (var test in TestWords)
            {
                if (words.Contains(test))
                    numWords++;
            }
            return numWords;
        }

        int numPhrasesInOrder(List<string> words, string[] TestWords)
        {
            int current = 0;
            for(int testing = 0; testing < TestWords.Length; testing++)
            {
                for(int y = 0; y < words.Count && testing < TestWords.Length; y++)
                {
                    var word = TestWords[testing];
                    var against = words[y];
                    if (against == "")
                        continue;
                    if (word == against)
                    {
                        current++;
                        testing++;
                    }
                }
            }
            return current;
        }

        public double PercentageMatch(string[] words)
        {
            double highest = 0;
            int i = 0;
            foreach(var possible in Text)
            {
                var split = possible.Split(' ');
                var contains = numWordsContained(words, split);
                var inOrder = numPhrasesInOrder(words.ToList(), split);
                var total = contains + inOrder;
                var perc = (double)total / (split.Length * 2);
                Program.LogMsg($"{Name} {i++} {(perc * 100):00}%");
                if (perc > highest)
                    highest = perc;
            }
            return highest;
        }
    }
}
