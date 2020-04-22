using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Emgu.CV.OCR;
using System.Text.RegularExpressions;
using Emgu.CV;

namespace DiscordBot.Services
{
    public class ScamDetector : SavedService
    {
        public string Folder => Path.Combine(Program.BASE_PATH, "tessdata");
        private Tesseract _ocr;

        public List<Scam> Scams = new List<Scam>();

        public List<ScamResult> getPossibleScams(string testImagePath, out string[] words)
        {
            words = null;
            StringBuilder strBuilder = new StringBuilder();
            try
            {
                var pix = new Pix(new Mat(testImagePath));
                _ocr.SetImage(pix);
                _ocr.Recognize();
                var chrs = _ocr.GetCharacters();
                for (int i = 0; i < chrs.Length; i++)
                    strBuilder.Append(chrs[i].Text);
            }
            catch (Exception e)
            {
                Program.LogMsg("Unexpected Error: " + e.ToString(), Discord.LogSeverity.Error, "Scam");
                return null;
            }
            var text = strBuilder.ToString().ToLower();
            words = Regex.Split(text, @"\s");

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
            _ocr = new Tesseract(Folder, "eng", OcrEngineMode.TesseractLstmCombined);
            _ocr.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890");
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
                Program.LogMsg($"{Name} {i} contains: {contains}");
                var inOrder = numPhrasesInOrder(words.ToList(), split);
                Program.LogMsg($"{Name} {i} order: {contains}");
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
