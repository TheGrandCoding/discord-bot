using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordBot.Classes.Legislation.Amending
{
    public abstract class BaseAmendment
    {
        [JsonIgnore]
        public Act AmendsAct { get; set; }
        [JsonIgnore]
        public AmendmentGroup Group => AmendsAct.AmendmentReferences[GroupId];

        [JsonProperty("id")]
        public int GroupId { get; set; }
        public AmendType Type { get; set; }
        public abstract string GetDescription();

    }

    public enum AmendType
    {
        Replace,
        Repeal,
        Insert
    }
}
