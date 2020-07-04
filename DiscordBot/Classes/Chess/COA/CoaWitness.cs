using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordBot.Classes.Chess.CoA
{
    public enum CalledBy
    {
        NONE = -1,
        Plaintiff,
        Defendant,
        Justices
    }
    public class CoAWitness
    {
        public CoAWitness(ChessPlayer player, CalledBy who, ITextChannel chnl)
        {
            Witness = player;
            CalledByWho = who;
            Channel = chnl;
        }
        [JsonIgnore]
        public ChessPlayer Witness;
        [JsonProperty]
        int witnessId => Witness?.Id ?? _tempWitnessId;
        int _tempWitnessId;

        [JsonProperty("cbP")]
        public CalledBy CalledByWho;

        public bool IsFinishedTestimony => Stage > 4;

        [JsonProperty]
        public int Stage = -1;

        [JsonIgnore]
        public ITextChannel Channel;
        [JsonProperty]
        ulong channelId => Channel?.Id ?? _tempChannelId;
        ulong _tempChannelId;

        [JsonIgnore]
        public CoAHearing Hearing;

        [JsonProperty("vote")]
        public List<int> JusticesVotedAdvance;

        public string CurrentlyQuestioning {  get
            {
                switch (Stage)
                {
                    case 0:
                    case 2:
                        return CalledByWho == CalledBy.Defendant ? "Defendant" : "Plaintiff";
                    case 1:
                    case 3:
                        return CalledByWho == CalledBy.Defendant ? "Plaintiff" : "Defendant";
                    case 4: return "Justices";
                    default: return "Error";
                }
            } }

        [JsonConstructor]
        private CoAWitness(int witnessId, ulong channelId)
        {
            _tempWitnessId = witnessId;
            _tempChannelId = channelId;
        }

        public void SetIds(CoAHearing hearing)
        {
            Hearing = hearing;
            Witness = Services.ChessService.Players.FirstOrDefault(x => x.Id == witnessId);
            Channel = Program.ChessGuild.GetTextChannel(channelId);
            SetPermissions();
        }

        void setPerm(ChessPlayer player, OverwritePermissions perm)
        {
            var bUser = Program.GetUserOrDefault(player.ConnectedAccount);
            if (bUser == null || bUser.ServiceUser || bUser.GeneratedUser)
                return;
            Channel.AddPermissionOverwriteAsync(bUser, perm);
        }

        public void SetPermissions()
        {
            Channel.AddPermissionOverwriteAsync(Program.ChessGuild.EveryoneRole, Program.NoPerms);
            setPerm(Hearing.Plaintiff, IsFinishedTestimony ? Program.ReadPerms : Program.WritePerms);
            setPerm(Hearing.Defendant, IsFinishedTestimony ? Program.ReadPerms : Program.WritePerms);
            setPerm(Witness, IsFinishedTestimony ? Program.ReadPerms : Program.WritePerms);
            foreach (var j in Hearing.Justices)
                setPerm(j, IsFinishedTestimony ? Program.ReadPerms : Program.WritePerms);
        }

        public void SendEmbed()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithTitle("CoA Witness Testimony");
            builder.WithFooter(Witness.Name, Program.Client.GetUser(Witness.ConnectedAccount).GetAvatarUrl());
            builder.AddField("Called by", Enum.GetName(typeof(CalledBy), CalledByWho));
            builder.AddField("Non-questioning", "Those that are not questioning must **only** raise objections");
            builder.AddField("To rest questioning", $"To stop questioning, go to [the witness page at the MLAPI website]({MLAPI.Handler.LocalAPIUrl}/chess/coa/testimony?num={Hearing.CaseNumber}&witness={Witness.Id})");
            if(Witness.ConnectedAccount == 0)
            {
                builder.AddField("WITNESS NOT PRESENT",
                    "Witness has no Discord account, so this channel can be used to document any testimony they provide otherwise");
            }
            Channel.SendMessageAsync(text:$"Currently Questioned by: {CurrentlyQuestioning}", embed: builder.Build());
        }

        public bool CanMoveNextStage(ChessPlayer player, out bool usingPowers)
        {
            usingPowers = false;
            if (player.Id == Hearing.Defendant.Id)
                return CurrentlyQuestioning.StartsWith("D");
            if (player.Id == Hearing.Plaintiff.Id)
                return CurrentlyQuestioning.StartsWith("P");
            var just = Hearing.Justices.Select(x => x.Id);
            if(just.Contains(player.Id))
            {
                if (Stage == 4)
                    return true; // their turn - they can move on
                usingPowers = true; 
                return true; // not their turn - they can override and move on
            }
            return false;
        }

        public void AdvanceNextStage()
        {
            JusticesVotedAdvance = new List<int>();
            Stage++;
            SetPermissions();
        }

    }
}
