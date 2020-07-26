using DiscordBot.Classes.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Chess.COA
{
    public class CoAttachment
    {
        [JsonConstructor]
        private CoAttachment()
        {
        }

        public CoAttachment(string filename, ChessPlayer upload) 
        {
            FileName = filename;
            UploadedBy = upload;
        }

        [JsonProperty("f")]
        public string FileName { get; set; }
        [JsonProperty("u")]
        [JsonConverter(typeof(ChessPlayerConverter))]
        public ChessPlayer UploadedBy { get; set; }

        [JsonIgnore]
        public string DataPath { get; private set; }

        public void SetIds(string path, int index)
        {
            DataPath = System.IO.Path.Combine(path, index.ToString("00") + "_" + FileName);
        }
    }
}
